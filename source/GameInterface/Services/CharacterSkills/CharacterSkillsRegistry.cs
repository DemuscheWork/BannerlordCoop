using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CharacterSkills
{
    /// <summary>
    /// Registry for <see cref="MBCharacterSkills"/> type
    /// </summary>
    /// <remarks>
    /// Character skill sets are a static catalog loaded from XML on every instance before the co-op session
    /// starts, so they are registered by their existing StringId and no creation/destruction is patched.
    /// </remarks>
    internal class CharacterSkillsRegistry : AutoRegistryBase<MBCharacterSkills>
    {
        public CharacterSkillsRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
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

            foreach (var skill in mbObjectManager.GetObjectTypeList<MBCharacterSkills>())
            {
                objectManager.AddExisting(skill.StringId, skill);
            }
        }

        public override void OnClientCreated(MBCharacterSkills obj, string id)
        {
        }

        public override void OnClientDestroyed(MBCharacterSkills obj, string id)
        {
        }

        public override void OnServerCreated(MBCharacterSkills obj, string id)
        {
        }

        public override void OnServerDestroyed(MBCharacterSkills obj, string id)
        {
        }
    }
}
