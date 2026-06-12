using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Workshops
{
    /// <summary>
    /// Registry for <see cref="WorkshopType"/> type
    /// </summary>
    /// <remarks>
    /// Workshop types are a static catalog loaded from XML on every instance before the co-op session starts,
    /// so no creation/destruction is patched.
    /// </remarks>
    internal class WorkshopTypeRegistry : AutoRegistryBase<WorkshopType>
    {
        public WorkshopTypeRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
            : base(logger, autoRegistryFactory, objectManager)
        {
        }

        public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

        public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

        public override void RegisterAllObjects()
        {
            // THIS BREAKS SAVING AND LOADING FOR SOME REASON, DO NOT UNCOMMENT UNTIL WE FIGURE OUT WHY AND HOW TO FIX
            //foreach(WorkshopType workshopType in WorkshopType.All)
            //{
            //    objectManager.AddExisting(workshopType.StringId, workshopType);
            //}
        }

        public override void OnClientCreated(WorkshopType obj, string id)
        {
        }

        public override void OnClientDestroyed(WorkshopType obj, string id)
        {
        }

        public override void OnServerCreated(WorkshopType obj, string id)
        {
        }

        public override void OnServerDestroyed(WorkshopType obj, string id)
        {
        }
    }
}
