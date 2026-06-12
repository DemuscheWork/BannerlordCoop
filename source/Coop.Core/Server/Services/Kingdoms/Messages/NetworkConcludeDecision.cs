using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Kingdoms.Messages
{
    /// <summary>
    /// Conclude decision network message, sent when the server sweep resolves an unresolved kingdom decision.
    /// Carries the server's random roll so clients replay the same election deterministically.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkConcludeDecision : ICommand
    {
        [ProtoMember(1)]
        public string KingdomId { get; }
        [ProtoMember(2)]
        public int Index { get; }
        [ProtoMember(3)]
        public float RandomNumber { get; }

        public NetworkConcludeDecision(string kingdomId, int index, float randomNumber)
        {
            KingdomId = kingdomId;
            Index = index;
            RandomNumber = randomNumber;
        }
    }
}
