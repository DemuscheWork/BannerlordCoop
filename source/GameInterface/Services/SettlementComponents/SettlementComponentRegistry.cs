using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.SettlementComponents;

/// <summary>
/// Registry for <see cref="SettlementComponent"/> type
/// </summary>
/// <remarks>
/// Settlement components (fiefs, villages, hideouts) are created during campaign load on every instance,
/// so they are registered by their existing StringId and no creation/destruction is patched.
/// </remarks>
internal class SettlementComponentRegistry : AutoRegistryBase<SettlementComponent>
{
    public SettlementComponentRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        List<SettlementComponent> settlementComponents = new List<SettlementComponent>();

        settlementComponents.AddRange(Town.AllFiefs);
        settlementComponents.AddRange(Village.All);
        settlementComponents.AddRange(Hideout.All);

        foreach (var settlementComponent in settlementComponents.DistinctBy(comp => comp.StringId))
        {
            objectManager.AddExisting(settlementComponent.StringId, settlementComponent);
        }
    }

    public override void OnClientCreated(SettlementComponent obj, string id)
    {
    }

    public override void OnClientDestroyed(SettlementComponent obj, string id)
    {
    }

    public override void OnServerCreated(SettlementComponent obj, string id)
    {
    }

    public override void OnServerDestroyed(SettlementComponent obj, string id)
    {
    }
}
