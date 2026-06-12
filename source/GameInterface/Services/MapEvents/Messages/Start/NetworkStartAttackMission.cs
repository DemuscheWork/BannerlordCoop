using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkStartAttackMission : ICommand
{
    /// <summary>
    /// Server-rolled seed for battle terrain generation, so every machine
    /// generates the same terrain for the same map event.
    /// </summary>
    [ProtoMember(1)]
    public readonly int TerrainSeed;

    public NetworkStartAttackMission(int terrainSeed)
    {
        TerrainSeed = terrainSeed;
    }
}
