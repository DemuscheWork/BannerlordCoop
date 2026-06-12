using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Kingdoms.Extentions;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Reflection;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Library;
using Common.Extensions;

namespace GameInterface.Services.Kingdoms.Handlers;

/// <summary>
/// Handler for <see cref="Kingdom"/> messages
/// </summary>
public class KingdomHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<KingdomHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public KingdomHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        messageBroker.Subscribe<AddDecision>(HandleAddDecision);
        messageBroker.Subscribe<RemoveDecision>(HandleRemoveDecision);
        messageBroker.Subscribe<ConcludeDecision>(HandleConcludeDecision);
    }

    private void HandleConcludeDecision(MessagePayload<ConcludeDecision> obj)
    {
        var payload = obj.What;

        if (!objectManager.TryGetObject(payload.KingdomId, out Kingdom kingdom))
        {
            Logger.Verbose("Kingdom not found in KingdomDecisionHandler with KingdomId: {id}", payload.KingdomId);
            return;
        }

        // Kingdoms created on clients skip the constructor, so the list can be null.
        var decisions = kingdom._unresolvedDecisions;
        if (decisions == null)
        {
            Logger.Verbose("Kingdom {id} has no unresolved decision list.", payload.KingdomId);
            return;
        }

        if (payload.Index < 0 || decisions.Count <= payload.Index)
        {
            Logger.Verbose("Index is out of bounds of the list.");
            return;
        }

        var decision = decisions[payload.Index];

        // Replay the server's election with its random roll; the election's own removal of the
        // decision is suppressed on clients, the queue entry goes away through the server's
        // RemoveDecision broadcast that follows.
        GameLoopRunner.RunOnMainThread(() =>
        {
            var election = new CoopKingdomElection(decision, payload.RandomNumber);
            election.StartElectionWithoutPlayerCoop();
        }, true);
    }

    private void HandleRemoveDecision(MessagePayload<RemoveDecision> obj)
    {
        var payload = obj.What;

        if (!objectManager.TryGetObject(payload.KingdomId, out Kingdom kingdom))
        {
            Logger.Verbose("Kingdom not found in KingdomDecisionHandler with KingdomId: {id}", payload.KingdomId);
            return;
        }

        // Kingdoms created on clients skip the constructor, so the list can be null.
        var decisions = kingdom._unresolvedDecisions;
        if (decisions == null)
        {
            Logger.Verbose("Kingdom {id} has no unresolved decision list.", payload.KingdomId);
            return;
        }

        if (payload.Index >= 0 && decisions.Count > payload.Index)
        {
            KingdomPatches.RunOriginalRemoveDecision(kingdom, decisions[payload.Index]);
        }
        else
        {
            Logger.Verbose("Index is out of bounds of the list.");
            return;
        }
    }

    private void HandleAddDecision(MessagePayload<AddDecision> obj)
    {
        var payload = obj.What;

        if (!objectManager.TryGetObject(payload.KingdomId, out Kingdom kingdom))
        {
            Logger.Verbose("Kingdom not found in KingdomDecisionHandler with KingdomId: {id}", payload.KingdomId);
            return;
        }

        if (!payload.Data.TryGetKingdomDecision(objectManager, out KingdomDecision kingdomDecision))
        {
            Logger.Verbose("KingdomDecision could not be deserialized in KingdomDecisionHandler.");
            return;
        }

        KingdomPatches.RunCoopAddDecision(kingdom, kingdomDecision, payload.IgnoreInfluenceCost, payload.RandomNumber);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AddDecision>(HandleAddDecision);
        messageBroker.Unsubscribe<RemoveDecision>(HandleRemoveDecision);
        messageBroker.Unsubscribe<ConcludeDecision>(HandleConcludeDecision);
    }
}
