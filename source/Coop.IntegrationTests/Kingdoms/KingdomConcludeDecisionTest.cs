using Coop.Core.Server.Services.Kingdoms.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Kingdoms.Messages;

using TaleWorlds.CampaignSystem;
namespace Coop.IntegrationTests.Kingdoms
{
    /// <summary>
    /// Test class for NetworkConcludeDecision message handling.
    /// </summary>
    public class KingdomConcludeDecisionTest
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        /// <summary>
        /// Used to Test that clients receive NetworkConcludeDecision messages.
        /// </summary>
        [Fact]
        public void ServerKingdom_ConcludeDecision_Publishes_AllClients()
        {
            // Arrange
            var kingdom = TestEnvironment.Server.CreateRegisteredObject<Kingdom>("kingdom1");

            // The kingdom is intentionally NOT registered on the clients: this test only verifies
            // the ConcludeDecision command reaches them; resolving the kingdom there would run the
            // election replay, which needs game state this environment does not provide.
            var triggerMessage = new DecisionConcluded(kingdom, 1, 0.5f);

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message over the network
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkConcludeDecision>());

            // Verify all clients hand a single command to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ConcludeDecision>());
            }
        }
    }
}
