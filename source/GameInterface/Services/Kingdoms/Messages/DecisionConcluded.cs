using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages;

/// <summary>
/// Event published on the server when the hourly sweep concludes an unresolved kingdom decision by election,
/// carrying the random roll so clients replay the same election deterministically.
/// </summary>
public readonly struct DecisionConcluded : IEvent
{
    public readonly Kingdom Kingdom;
    public readonly int Index;
    public readonly float RandomNumber;

    public DecisionConcluded(Kingdom kingdom, int index, float randomNumber)
    {
        Kingdom = kingdom;
        Index = index;
        RandomNumber = randomNumber;
    }
}
