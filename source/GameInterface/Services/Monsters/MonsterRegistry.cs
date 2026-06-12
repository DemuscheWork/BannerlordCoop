using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Monsters;

/// <summary>
/// Registry for <see cref="Monster"/> type
/// </summary>
/// <remarks>
/// Monsters are a static catalog loaded from XML on every instance before the co-op session starts, so they
/// are registered by their existing StringId and no creation/destruction is patched.
/// </remarks>
internal class MonsterRegistry : AutoRegistryBase<Monster>
{
    public MonsterRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
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

        foreach (Monster monster in mbObjectManager.GetObjectTypeList<Monster>())
        {
            objectManager.AddExisting(monster.StringId, monster);
        }
    }

    public override void OnClientCreated(Monster obj, string id)
    {
    }

    public override void OnClientDestroyed(Monster obj, string id)
    {
    }

    public override void OnServerCreated(Monster obj, string id)
    {
    }

    public override void OnServerDestroyed(Monster obj, string id)
    {
    }
}
