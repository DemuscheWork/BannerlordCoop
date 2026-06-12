using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Clans;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Template.Handlers;
using GameInterface.Services.Template.Messages;
using GameInterface.Services.Template.Patches;
using GameInterface.Utils;
using SandBox.GauntletUI;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Heroes.Handlers;
internal class HeroDataHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroDataHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public HeroDataHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<ChangeHeroName>(Handle_HeroChangeName);
        
    }

    public void Dispose()
    {
        messageBroker?.Unsubscribe<ChangeHeroName>(Handle_HeroChangeName);
    }

    private void Handle_HeroChangeName(MessagePayload<ChangeHeroName> payload)
    {
        var data = payload.What.Data;

        // ChangeHeroName is published synchronously from network handlers, and the rename reads
        // ScreenManager.TopScreen, which only tolerates the main thread; the hero is re-resolved
        // inside the deferred action.
        GameThreadDispatcher.RunOnGameThread(nameof(ChangeHeroName), () =>
        {
            if (objectManager.TryGetObject<Hero>(data.HeroStringId, out var hero) == false)
            {
                Logger.Error("Unable to get {type} from id {stringId}", typeof(Hero), data.HeroStringId);
                return;
            }

            var fullName = new TextObject(data.FullName);
            var firstName = new TextObject(data.FirstName);

            HeroDataPatches.SetNameOverride(hero, fullName, firstName);

            InformationManager.DisplayMessage(new InformationMessage($"Changed hero name to {fullName}"));

            if (ScreenManager.TopScreen is GauntletClanScreen clanScreen)
            {
                clanScreen._dataSource?.RefreshValues();
            }
        });
    }
}
