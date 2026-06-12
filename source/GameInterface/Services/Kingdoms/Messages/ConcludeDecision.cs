using Common.Messaging;

namespace GameInterface.Services.Kingdoms.Messages
{
    /// <summary>
    /// Command handled on the client side, when the server sends a NetworkConcludeDecision message to clients.
    /// </summary>
    public class ConcludeDecision : ICommand
    {
        public string KingdomId { get; }
        public int Index { get; }
        public float RandomNumber { get; }

        public ConcludeDecision(string kingdomId, int index, float randomNumber)
        {
            KingdomId = kingdomId;
            Index = index;
            RandomNumber = randomNumber;
        }
    }
}
