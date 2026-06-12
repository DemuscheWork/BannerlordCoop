using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using System;

namespace Coop.Core.Client.Services.MobileParties.Handlers;

/// <summary>
/// Handles changes to parties for settlement entry and exit on the client side. Encounter start requests are
/// rate-limited to at most one per <see cref="StartRequestCooldown"/>: the patched
/// <c>EncounterManager.StartSettlementEncounter</c> suppresses the real encounter start on the client, so vanilla
/// re-attempts it every tick until the server's reply lands, republishing
/// <see cref="StartSettlementEncounterAttempted"/> in a tight loop (100+/s observed) and flooding the server.
/// </summary>
public class ClientSettlementExitEnterHandler : IHandler
{
    private static readonly TimeSpan StartRequestCooldown = TimeSpan.FromMilliseconds(500);

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    private DateTime lastStartRequestSentUtc = DateTime.MinValue;

    public ClientSettlementExitEnterHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<StartSettlementEncounterAttempted>(Handle);
        messageBroker.Subscribe<EndSettlementEncounterAttempted>(Handle);
        messageBroker.Subscribe<NetworkEndSettlementEncounter>(Handle);
        messageBroker.Subscribe<NetworkStartSettlementEncounter>(Handle);

        messageBroker.Subscribe<NetworkPartyEnterSettlement>(Handle);
        messageBroker.Subscribe<NetworkPartyLeaveSettlement>(Handle);
    }



    public void Dispose()
    {
        messageBroker.Unsubscribe<StartSettlementEncounterAttempted>(Handle);
        messageBroker.Unsubscribe<EndSettlementEncounterAttempted>(Handle);
        messageBroker.Unsubscribe<NetworkEndSettlementEncounter>(Handle);
        messageBroker.Unsubscribe<NetworkStartSettlementEncounter>(Handle);

        messageBroker.Unsubscribe<NetworkPartyEnterSettlement>(Handle);
        messageBroker.Unsubscribe<NetworkPartyLeaveSettlement>(Handle);
    }


    private void Handle(MessagePayload<StartSettlementEncounterAttempted> obj)
    {
        var now = DateTime.UtcNow;
        if (now - lastStartRequestSentUtc < StartRequestCooldown)
            return; // drop: at most one request per cooldown window

        var payload = obj.What;

        if (!objectManager.TryGetIdWithLogging(payload.Party, out var partyId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.Settlement, out var settlementId)) return;

        lastStartRequestSentUtc = now;

        var message = new NetworkRequestStartSettlementEncounter(partyId, settlementId);

        network.SendAll(message);
    }

    private void Handle(MessagePayload<EndSettlementEncounterAttempted> obj)
    {
        var payload = obj.What;

        if (!objectManager.TryGetIdWithLogging(payload.Party, out var partyId)) return;

        var message = new NetworkRequestEndSettlementEncounter(partyId);

        network.SendAll(message);
    }

    private void Handle(MessagePayload<NetworkStartSettlementEncounter> obj)
    {
        var payload = obj.What;
        var message = new StartSettlementEncounter(payload.PartyId, payload.SettlementId);

        messageBroker.Publish(this, message);
    }

    private void Handle(MessagePayload<NetworkEndSettlementEncounter> obj)
    {
        var message = new EndSettlementEncounter();

        messageBroker.Publish(this, message);
    }

    private void Handle(MessagePayload<NetworkPartyEnterSettlement> obj)
    {
        var payload = obj.What;

        var message = new PartyEnterSettlement(payload.SettlementId, payload.PartyId);
        messageBroker.Publish(this, message);
    }

    private void Handle(MessagePayload<NetworkPartyLeaveSettlement> obj)
    {
        var payload = obj.What;
        var message = new PartyLeaveSettlement(payload.PartyId);

        messageBroker.Publish(this, message);
    }
}
