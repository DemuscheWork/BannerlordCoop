using Common.PacketHandlers;
using Common.Serialization;
using Coop.Core.Client.Messages;
using Coop.Core.Common.Network.Packets;
using Coop.Core.Server.Services.Kingdoms.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Mock;

namespace Coop.IntegrationTests.Network
{
    /// <summary>
    /// Exercises the client loading packet gate (<c>LoadingPacketBuffer</c>) through the same mock
    /// receive path the rest of the integration suite uses, so a regression in the gate is caught
    /// outside its unit tests (#1329): the mock network now routes every received packet through
    /// the buffer exactly like <c>CoopClient.OnNetworkReceive</c>.
    /// </summary>
    public class LoadingPacketBufferGateTest
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        private static MessagePacket MakeMessagePacket(Environment.Instance.EnvironmentInstance instance, object message)
        {
            var serializer = instance.Resolve<ICommonSerializer>();
            return new MessagePacket(serializer.Serialize(message));
        }

        [Fact]
        public void PacketsAfterSaveData_AreHeld_UntilCampaignEntered()
        {
            // Arrange
            var client1 = TestEnvironment.Clients.First();
            var mockClient = client1.Resolve<MockClient>();
            var serverPeer = TestEnvironment.Server.NetPeer;

            var gameplayPacket = MakeMessagePacket(client1, new NetworkRemoveDecision("kingdom1", 0));

            // Act: the save arms the gate; the gameplay packet that follows is a post-snapshot delta.
            mockClient.ReceiveFromNetwork(serverPeer, new GameSaveDataPacket(Array.Empty<byte>(), "campaign1", null));
            mockClient.ReceiveFromNetwork(serverPeer, gameplayPacket);

            // Assert: while the campaign loads, the delta must not reach the client's broker.
            Assert.Equal(0, client1.InternalMessages.GetMessageCount<NetworkRemoveDecision>());

            // Act: the campaign becomes ready; the poller pump replays the backlog.
            client1.SimulateMessage(this, new ClientCampaignEntered());
            mockClient.PumpBufferedPackets();

            // Assert: the delta arrived exactly once, after the load.
            Assert.Equal(1, client1.InternalMessages.GetMessageCount<NetworkRemoveDecision>());
        }

        [Fact]
        public void PacketsBeforeSaveData_PassStraightThrough()
        {
            // Arrange
            var client1 = TestEnvironment.Clients.First();
            var mockClient = client1.Resolve<MockClient>();
            var serverPeer = TestEnvironment.Server.NetPeer;

            var gameplayPacket = MakeMessagePacket(client1, new NetworkRemoveDecision("kingdom1", 0));

            // Act: no save has arrived — the gate is off (normal play / pre-save handshake).
            mockClient.ReceiveFromNetwork(serverPeer, gameplayPacket);

            // Assert
            Assert.Equal(1, client1.InternalMessages.GetMessageCount<NetworkRemoveDecision>());
        }
    }
}
