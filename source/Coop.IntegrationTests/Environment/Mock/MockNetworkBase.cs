using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Tests.Utils;
using Coop.Core.Client.Network;
using Coop.IntegrationTests.Environment.Extensions;
using LiteNetLib;

namespace Coop.IntegrationTests.Environment.Mock;

public abstract class MockNetworkBase : INetwork
{
    private readonly TestNetworkRouter networkOrchestrator;
    private readonly IPacketManager packetManager;
    private readonly ILoadingPacketBuffer loadingPacketBuffer;
    public static int InstanceCount = 0;

    public MockNetworkBase(TestNetworkRouter networkOrchestrator, IPacketManager packetManager, ILoadingPacketBuffer loadingPacketBuffer = null)
    {
        this.networkOrchestrator = networkOrchestrator;
        this.packetManager = packetManager;
        this.loadingPacketBuffer = loadingPacketBuffer;
        InstanceCount = Interlocked.Increment(ref InstanceCount);

        NetPeer = NetPeerExtensions.CreatePeer(InstanceCount);
    }

    public INetworkConfiguration Configuration => throw new NotImplementedException();

    public int Priority => throw new NotImplementedException();

    public NetPeer NetPeer { get; } = NetPeerExtensions.CreatePeer();

    public MessageCollection NetworkSentMessages { get; } = new MessageCollection();
    public PacketCollection NetworkSentPackets { get; } = new PacketCollection();

    public void ReceiveFromNetwork(NetPeer peer, IPacket packet)
    {
        // Mirror CoopClient.OnNetworkReceive: the loading buffer sees every incoming packet first,
        // so integration tests exercise the same gate as production (#1329). Server instances have
        // no buffer and handle directly.
        if (loadingPacketBuffer?.Intercept(peer, packet) == true) return;

        packetManager.HandleReceive(peer, packet);
    }

    /// <summary>
    /// Mirror of the drain step of <c>CoopClient.Update</c>: replays buffered packets batch by
    /// batch until the backlog is empty. No-op for instances without a loading buffer.
    /// </summary>
    public void PumpBufferedPackets()
    {
        if (loadingPacketBuffer == null) return;

        while (true)
        {
            var batch = loadingPacketBuffer.DrainIfRequested();
            if (batch.Count == 0) break;

            foreach (var (peer, packet) in batch)
            {
                packetManager.HandleReceive(peer, packet);
            }
        }
    }

    public void Send(NetPeer netPeer, IPacket packet)
    {
        NetworkSentPackets.Add(packet);

        networkOrchestrator.Send(NetPeer, netPeer, packet);
    }

    public void Send(NetPeer netPeer, IMessage message)
    {
        NetworkSentMessages.Add(message);

        networkOrchestrator.Send(NetPeer, netPeer, message);
    }

    public void SendAll(IPacket packet)
    {
        NetworkSentPackets.Add(packet);

        networkOrchestrator.SendAll(NetPeer, packet);
    }

    public void SendAll(IMessage message)
    {
        NetworkSentMessages.Add(message);

        networkOrchestrator.SendAll(NetPeer, message);
    }

    public void SendAllBut(NetPeer excludedPeer, IPacket packet)
    {
        NetworkSentPackets.Add(packet);

        networkOrchestrator.SendAllBut(NetPeer, excludedPeer, packet);
    }

    public void SendAllBut(NetPeer excludedPeer, IMessage message)
    {
        NetworkSentMessages.Add(message);

        networkOrchestrator.SendAllBut(NetPeer, excludedPeer, message);
    }

    public void Start()
    {
        throw new NotImplementedException();
    }

    public void Stop()
    {
        throw new NotImplementedException();
    }

    public void Update(TimeSpan frameTime)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
    }
}
