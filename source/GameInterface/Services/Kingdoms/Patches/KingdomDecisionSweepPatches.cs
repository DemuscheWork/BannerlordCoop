using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Extentions;
using GameInterface.Services.Kingdoms.Extentions;
using GameInterface.Services.Kingdoms.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Kingdoms.Patches;

/// <summary>
/// Server-side replacement for the unresolved-decision sweep of the disabled
/// <see cref="TaleWorlds.CampaignSystem.CampaignBehaviors.KingdomDecisionProposalBehavior"/>.
/// </summary>
/// <remarks>
/// Decisions raised in a kingdom that contains a connected player's clan are queued into
/// <c>Kingdom._unresolvedDecisions</c> (see <see cref="KingdomPatches.AddDecisionPrefix"/>). Vanilla drains that
/// queue from the proposal behavior's ticks via <c>UpdateKingdomDecisions</c>, but coop disables the whole behavior
/// (it would re-propose and double-handle decisions), so without this sweep the queue only drains through the
/// kingdom-screen UI and decisions stall forever (#1344). Hourly on the server, expired decisions are cancelled
/// (the patched <see cref="Kingdom.RemoveDecision"/> broadcasts the removal) and decisions whose voting period is
/// over are concluded with <see cref="CoopKingdomElection"/>; the election roll is broadcast first via
/// <see cref="DecisionConcluded"/> so clients replay the same election deterministically, mirroring how
/// <see cref="DecisionAdded"/> already syncs instant elections. Vanilla sweeps the player's kingdom hourly and
/// other kingdoms daily; sweeping every kingdom with a non-empty queue hourly is a cheap superset of that.
/// </remarks>
[HarmonyPatch(typeof(CampaignPeriodicEventManager))]
internal static class KingdomDecisionSweepPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<CampaignPeriodicEventManager>();

    [HarmonyPatch("PeriodicHourlyTick")]
    [HarmonyPostfix]
    private static void PeriodicHourlyTickPostfix()
    {
        if (ModInformation.IsClient) return;
        if (Campaign.Current == null) return;

        try
        {
            SweepUnresolvedDecisions();
        }
        catch (Exception e)
        {
            // The postfix runs inside the campaign's tick; a throw here would crash the server.
            Logger.Error(e, "Failed to sweep unresolved kingdom decisions");
        }
    }

    private static void SweepUnresolvedDecisions()
    {
        foreach (var kingdom in Kingdom.All)
        {
            // Kingdoms created on clients skip the constructor, so the list can be null; the server
            // creates them normally, but stay defensive since this runs unattended every hour.
            var unresolvedDecisions = kingdom._unresolvedDecisions;
            if (unresolvedDecisions == null || unresolvedDecisions.Count == 0) continue;

            var toCancel = new List<KingdomDecision>();
            var toConclude = new List<KingdomDecision>();
            Classify(
                unresolvedDecisions,
                decision => decision.ShouldBeCancelled(),
                AnyConnectedPlayerClanParticipates,
                decision => decision.TriggerTime.IsPast,
                NeedsConnectedPlayerResolution,
                toCancel,
                toConclude);

            foreach (var decision in toCancel)
            {
                Logger.Information("Cancelling expired kingdom decision {decision} in {kingdom}",
                    decision.GetType().Name, kingdom.StringId);

                // The patched RemoveDecision broadcasts the removal to clients by index.
                kingdom.RemoveDecision(decision);

                // Vanilla UpdateKingdomDecisions dispatches the cancellation after removing.
                bool isPlayerInvolved = decision.DetermineChooser().Leader.IsHumanPlayerCharacter ||
                    decision.DetermineSupporters().Any(supporter => supporter.IsPlayer);
                CampaignEventDispatcher.Instance.OnKingdomDecisionCancelled(decision, isPlayerInvolved);
            }

            foreach (var decision in toConclude)
            {
                // The index addresses the decision on clients (queues mutate in lockstep through the
                // synced add/remove paths), and the roll pins the election outcome; both must be
                // captured and broadcast before the election mutates the queue.
                int index = unresolvedDecisions.IndexOf(decision);
                if (index < 0) continue; // removed by a cancellation above

                float randomNumber = MBRandom.RandomFloat;

                Logger.Information("Concluding kingdom decision {decision} in {kingdom} by election",
                    decision.GetType().Name, kingdom.StringId);

                MessageBroker.Instance.Publish(kingdom, new DecisionConcluded(kingdom, index, randomNumber));

                var election = new CoopKingdomElection(decision, randomNumber);
                election.StartElectionWithoutPlayerCoop();
            }
        }
    }

    /// <summary>
    /// Splits unresolved decisions into those to cancel and those to conclude, keeping the rest queued.
    /// Mirrors vanilla <c>KingdomDecisionProposalBehavior.UpdateKingdomDecisions</c>: cancellation wins, then a
    /// decision concludes when no player participates in it, or its voting period ended and no player has to
    /// resolve it manually. Predicates are injected so the selection rules are unit-testable without game state.
    /// </summary>
    internal static void Classify<TDecision>(
        IEnumerable<TDecision> decisions,
        Func<TDecision, bool> shouldBeCancelled,
        Func<TDecision, bool> playerParticipates,
        Func<TDecision, bool> triggerTimePast,
        Func<TDecision, bool> needsPlayerResolution,
        List<TDecision> toCancel,
        List<TDecision> toConclude)
    {
        foreach (var decision in decisions)
        {
            if (shouldBeCancelled(decision))
            {
                toCancel.Add(decision);
            }
            else if (!playerParticipates(decision) || (triggerTimePast(decision) && !needsPlayerResolution(decision)))
            {
                toConclude.Add(decision);
            }
        }
    }

    /// <summary>
    /// Coop-aware <c>KingdomDecision.IsPlayerParticipant</c>: vanilla compares against the local
    /// <see cref="Clan.PlayerClan"/>, which on a server is not the connected clients' clans.
    /// </summary>
    private static bool AnyConnectedPlayerClanParticipates(KingdomDecision decision)
    {
        foreach (var party in Campaign.Current.CampaignObjectManager.GetPlayerMobileParties())
        {
            var clan = party.ActualClan;
            if (clan != null && clan.Kingdom == decision.Kingdom && !clan.IsUnderMercenaryService)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Coop-aware <c>KingdomDecision.NeedsPlayerResolution</c>: an enforced decision, or one whose voting period
    /// ended while a connected player's clan rules the kingdom, must wait for that player's kingdom-screen vote.
    /// </summary>
    private static bool NeedsConnectedPlayerResolution(KingdomDecision decision)
    {
        if (!AnyConnectedPlayerClanParticipates(decision)) return false;
        if (decision.IsEnforced) return true;
        if (!decision.TriggerTime.IsPast) return false;

        var rulingClan = decision.Kingdom.RulingClan;
        if (rulingClan == null) return false;

        foreach (var party in Campaign.Current.CampaignObjectManager.GetPlayerMobileParties())
        {
            if (party.ActualClan == rulingClan)
                return true;
        }

        return false;
    }
}
