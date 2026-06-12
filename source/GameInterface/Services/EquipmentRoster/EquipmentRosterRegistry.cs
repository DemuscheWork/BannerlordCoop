using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.EquipmentRoster
{
    /// <summary>
    /// Registry for <see cref="MBEquipmentRoster"/> type
    /// </summary>
    /// <remarks>
    /// Equipment rosters are a static catalog loaded from XML on every instance before the co-op session
    /// starts, so they are registered by their existing StringId and no creation/destruction is patched.
    /// </remarks>
    internal class EquipmentRosterRegistry : AutoRegistryBase<MBEquipmentRoster>
    {
        public EquipmentRosterRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
            : base(logger, autoRegistryFactory, objectManager)
        {
        }

        public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

        public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

        public override void RegisterAllObjects()
        {
            var mbObjectManager = MBObjectManager.Instance;
            if (mbObjectManager == null)
            {
                Logger.Error("Unable to register objects when MBObjectManager is null");
                return;
            }

            foreach (var equipRoster in mbObjectManager.GetObjectTypeList<MBEquipmentRoster>())
            {
                objectManager.AddExisting(equipRoster.StringId, equipRoster);
            }
        }

        public override void OnClientCreated(MBEquipmentRoster obj, string id)
        {
        }

        public override void OnClientDestroyed(MBEquipmentRoster obj, string id)
        {
        }

        public override void OnServerCreated(MBEquipmentRoster obj, string id)
        {
        }

        public override void OnServerDestroyed(MBEquipmentRoster obj, string id)
        {
        }
    }
}
