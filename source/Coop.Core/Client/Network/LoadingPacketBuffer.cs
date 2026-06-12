using Common.Logging;
using Common.Messaging;
using Common.PacketHandlers;
using Coop.Core.Client.Messages;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Coop.Core.Client.Network;

/// <summary>
/// Buffers gameplay packets that arrive at a joining client while it is loading the transfer save,
/// then replays them in order once the campaign is ready.
/// </summary>
/// <remarks>
/// Without this, world-change packets (chiefly DynamicSync messages) are handled against a campaign
/// that has not loaded yet and are lost or throw. This generalises the per-message deferral that
/// <c>RemotePlayerHeroHandler</c> does for new-player heroes.
///
/// Lifecycle, driven by the save's arrival and the client state machine:
/// <list type="bullet">
/// <item>Off by default — packets pass straight through (normal play and the pre-save handshake).</item>
/// <item>The <see cref="PacketType.SaveData"/> packet arms buffering and then passes through (it
/// starts the load). Everything after it on the ordered channel is a post-snapshot delta.</item>
/// <item>While armed, every packet except the save and <see cref="PacketType.PacketWrapper"/> is
/// queued. This is deadlock-safe: the client's load is driven by local game-state events and needs
/// no further incoming packet to finish.</item>
/// <item>On <see cref="ClientCampaignEntered"/> the queue is drained in FIFO order, at most
/// <see cref="MaxDrainBatchSize"/> packets per poller update so a long join's backlog is replayed
/// over a few frames instead of one long synchronous stall; packets arriving between batches keep
/// queueing behind the backlog so ordering is preserved. Buffering stops once the backlog is empty.
/// The backlog size is logged when replay starts, and a warning fires at doubling thresholds while
/// it grows, so a pathologically slow join is observable (#1329).</item>
/// </list>
/// Leaving coop (disconnect/abort) is not handled here: this buffer is scoped to the coop container,
/// so it is disposed when the container is torn down and a reconnect builds a fresh one. It must NOT
/// react to <c>MainMenuEntered</c> — that also fires as an intermediate step of a normal join (the
/// client clears the character-creation game before loading the host save), which would wrongly
/// disarm and clear the buffer mid-load.
///
/// Threading: <see cref="Intercept"/> and <see cref="DrainIfRequested"/> are both called on the
/// network poller thread (CoopClient receive + update), so the queue is effectively single-threaded;
/// only the drain request is raised from the broker thread and is therefore a volatile flag.
/// </remarks>
public interface ILoadingPacketBuffer
{
    /// <summary>
    /// Returns true if the packet was buffered and must NOT be handled now; false if the caller
    /// should handle it immediately.
    /// </summary>
    bool Intercept(NetPeer peer, IPacket packet);

    /// <summary>
    /// If the campaign just became ready, stops buffering and returns the buffered packets in FIFO
    /// order for replay; otherwise returns an empty list.
    /// </summary>
    IReadOnlyList<(NetPeer Peer, IPacket Packet)> DrainIfRequested();
}

internal sealed class LoadingPacketBuffer : ILoadingPacketBuffer, IDisposable
{
    /// <summary>
    /// Upper bound on packets replayed per <see cref="DrainIfRequested"/> call (one poller update),
    /// so a large join backlog is spread over a few frames instead of one long synchronous stall.
    /// </summary>
    internal const int MaxDrainBatchSize = 512;

    /// <summary>First backlog size that logs a warning; doubles after each warning to avoid spam.</summary>
    internal const int InitialBacklogWarnThreshold = 1024;

    private static readonly ILogger Logger = LogManager.GetLogger<LoadingPacketBuffer>();

    private readonly IMessageBroker messageBroker;
    private readonly ConcurrentQueue<(NetPeer, IPacket)> queue = new();

    private volatile bool buffering;
    private volatile bool drainRequested;

    // Poller-thread only (like the queue's producers/consumer).
    private bool draining;
    private int backlogWarnThreshold = InitialBacklogWarnThreshold;

    public LoadingPacketBuffer(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<ClientCampaignEntered>(Handle_ClientCampaignEntered);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ClientCampaignEntered>(Handle_ClientCampaignEntered);
    }

    public bool Intercept(NetPeer peer, IPacket packet)
    {
        // The save arms buffering, but is itself always handled — it drives the load that ends it.
        if (packet.PacketType == PacketType.SaveData)
        {
            buffering = true;
            return false;
        }

        if (!buffering) return false;

        // Infrastructure wrapper is never gameplay state — let it through.
        if (packet.PacketType == PacketType.PacketWrapper) return false;

        queue.Enqueue((peer, packet));

        // The backlog is unbounded by design (dropping deltas would desync the client), so make a
        // pathological one observable instead: warn at doubling thresholds while it grows.
        if (queue.Count >= backlogWarnThreshold)
        {
            Logger.Warning(
                "Loading packet backlog reached {Count} packets while the campaign loads",
                queue.Count);
            backlogWarnThreshold *= 2;
        }

        return true;
    }

    public IReadOnlyList<(NetPeer Peer, IPacket Packet)> DrainIfRequested()
    {
        if (drainRequested)
        {
            drainRequested = false;
            draining = true;
            Logger.Information(
                "Campaign ready — replaying {Count} buffered packets in batches of up to {BatchSize}",
                queue.Count, MaxDrainBatchSize);
        }

        if (!draining) return Array.Empty<(NetPeer, IPacket)>();

        var drained = new List<(NetPeer, IPacket)>();
        while (drained.Count < MaxDrainBatchSize && queue.TryDequeue(out var item))
        {
            drained.Add(item);
        }

        // Keep buffering while batches remain so live packets queue behind the undrained backlog
        // and ordering is preserved; only an empty queue ends the replay.
        if (queue.IsEmpty)
        {
            draining = false;
            buffering = false;
            backlogWarnThreshold = InitialBacklogWarnThreshold;
        }

        return drained;
    }

    private void Handle_ClientCampaignEntered(MessagePayload<ClientCampaignEntered> payload)
    {
        // Defer the drain to the poller thread (DrainIfRequested) so replayed packets stay ordered
        // relative to live ones and the queue stays single-threaded.
        drainRequested = true;
    }
}
