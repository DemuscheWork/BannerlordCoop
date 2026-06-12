using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.VillageMarketDatas;

/// <summary>
/// Registry manager for VillageMarketData
/// </summary>
/// <remarks>
/// Each village creates its market data during campaign load on every instance. VillageMarketData has no
/// StringId of its own, so it is registered under an id derived from its village; no creation/destruction
/// is patched.
/// </remarks>
internal class VillageMarketRegistry : AutoRegistryBase<VillageMarketData>
{
    public VillageMarketRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var village in Campaign.Current._villages)
        {
            var networkId = $"{nameof(VillageMarketData)}_{village.StringId}";
            objectManager.AddExisting(networkId, village._marketData);
        }
    }

    public override void OnClientCreated(VillageMarketData obj, string id)
    {
    }

    public override void OnClientDestroyed(VillageMarketData obj, string id)
    {
    }

    public override void OnServerCreated(VillageMarketData obj, string id)
    {
    }

    public override void OnServerDestroyed(VillageMarketData obj, string id)
    {
    }
}
