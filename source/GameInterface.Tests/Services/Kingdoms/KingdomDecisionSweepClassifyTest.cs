using GameInterface.Services.Kingdoms.Patches;
using System;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms;

/// <summary>
/// Tests for the unresolved-decision selection rules of <see cref="KingdomDecisionSweepPatches"/>,
/// mirroring vanilla <c>KingdomDecisionProposalBehavior.UpdateKingdomDecisions</c>.
/// </summary>
public class KingdomDecisionSweepClassifyTest
{
    private record Decision(bool Cancelled, bool PlayerParticipates, bool TriggerTimePast, bool NeedsPlayerResolution);

    private static (List<Decision> toCancel, List<Decision> toConclude) Classify(params Decision[] decisions)
    {
        var toCancel = new List<Decision>();
        var toConclude = new List<Decision>();

        KingdomDecisionSweepPatches.Classify(
            decisions,
            d => d.Cancelled,
            d => d.PlayerParticipates,
            d => d.TriggerTimePast,
            d => d.NeedsPlayerResolution,
            toCancel,
            toConclude);

        return (toCancel, toConclude);
    }

    [Fact]
    public void CancelledDecision_IsCancelled_EvenWhenConcludable()
    {
        var decision = new Decision(Cancelled: true, PlayerParticipates: false, TriggerTimePast: true, NeedsPlayerResolution: false);

        var (toCancel, toConclude) = Classify(decision);

        Assert.Single(toCancel, decision);
        Assert.Empty(toConclude);
    }

    [Fact]
    public void DecisionWithoutPlayerParticipant_Concludes_BeforeTriggerTime()
    {
        // A decision that queued even though no connected player participates (the misfiring
        // queue predicate observed in #1344) must drain without waiting for its voting period.
        var decision = new Decision(Cancelled: false, PlayerParticipates: false, TriggerTimePast: false, NeedsPlayerResolution: false);

        var (toCancel, toConclude) = Classify(decision);

        Assert.Empty(toCancel);
        Assert.Single(toConclude, decision);
    }

    [Fact]
    public void PlayerDecision_Concludes_OnceVotingPeriodEnds()
    {
        var decision = new Decision(Cancelled: false, PlayerParticipates: true, TriggerTimePast: true, NeedsPlayerResolution: false);

        var (toCancel, toConclude) = Classify(decision);

        Assert.Empty(toCancel);
        Assert.Single(toConclude, decision);
    }

    [Fact]
    public void PlayerDecision_StaysQueued_WhileVotingPeriodRuns()
    {
        var decision = new Decision(Cancelled: false, PlayerParticipates: true, TriggerTimePast: false, NeedsPlayerResolution: false);

        var (toCancel, toConclude) = Classify(decision);

        Assert.Empty(toCancel);
        Assert.Empty(toConclude);
    }

    [Fact]
    public void PlayerDecision_StaysQueued_WhenPlayerMustResolveIt()
    {
        // The ruling player's manual vote (kingdom screen) must stay available; the sweep must not
        // resolve over their head.
        var decision = new Decision(Cancelled: false, PlayerParticipates: true, TriggerTimePast: true, NeedsPlayerResolution: true);

        var (toCancel, toConclude) = Classify(decision);

        Assert.Empty(toCancel);
        Assert.Empty(toConclude);
    }

    [Fact]
    public void MixedQueue_SplitsPerDecision()
    {
        var cancelled = new Decision(Cancelled: true, PlayerParticipates: true, TriggerTimePast: false, NeedsPlayerResolution: false);
        var concludable = new Decision(Cancelled: false, PlayerParticipates: true, TriggerTimePast: true, NeedsPlayerResolution: false);
        var pending = new Decision(Cancelled: false, PlayerParticipates: true, TriggerTimePast: false, NeedsPlayerResolution: false);

        var (toCancel, toConclude) = Classify(cancelled, concludable, pending);

        Assert.Single(toCancel, cancelled);
        Assert.Single(toConclude, concludable);
    }
}
