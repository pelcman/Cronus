// ChannelHandler partial: mini rooms: trade, mini games, player shops, hired merchants.
using System.Security.Cryptography;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Scripting;

namespace Cronus.Server.Channel;

public sealed partial class ChannelHandler
{
    /// <summary>
    /// Handles <c>CP_MiniRoom</c> (ports <c>ReqCMiniRoomBaseDlg</c>): the trade room (type 3,
    /// <c>MapleTrade</c>) and the Omok / match-card game rooms (types 1/2, <c>MapleMiniGame</c>).
    /// Personal/hired shops (types 4/5) aren't modelled.
    /// </summary>
    private async ValueTask HandleMiniRoomAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        Character c = _player.Character;
        byte protocol = packet.ReadByte();
        switch (protocol)
        {
            case ChannelPackets.MiniRoomCreate:
            {
                byte roomType = packet.ReadByte();
                if (roomType is MiniGame.TypeOmok or MiniGame.TypeMatchCard)
                {
                    await CreateMiniGameAsync(session, c, roomType, packet).ConfigureAwait(false);
                    return;
                }

                if (roomType == 4)
                {
                    await CreatePlayerShopAsync(session, c, packet).ConfigureAwait(false);
                    return;
                }

                if (roomType == 5)
                {
                    await CreateHiredMerchantAsync(session, c, packet).ConfigureAwait(false);
                    return;
                }

                if (roomType != 3 || _trades.Get(c.Id) is not null)
                {
                    return; // trades, game rooms, shops, and merchants; one room at a time
                }

                var trade = new Trade(_player);
                _trades.TryAdd(c.Id, trade);
                await session.SendAsync(_packets.TradeStart(c, 0, null)).ConfigureAwait(false);
                break;
            }

            case ChannelPackets.MiniRoomInvite:
            {
                int targetId = packet.ReadInt();
                Trade? trade = _trades.Get(c.Id);
                if (trade is null || trade.Starter.Player.Character.Id != c.Id || trade.Visitor is not null)
                {
                    return;
                }

                FieldPlayer? target = _field?.Players.FirstOrDefault(p => p.Character.Id == targetId);
                if (target is null || _trades.Get(targetId) is not null)
                {
                    await ReplyAsync(session, "the other player can't trade right now").ConfigureAwait(false);
                    await CancelTradeAsync(trade).ConfigureAwait(false);
                    return;
                }

                trade.InvitedCharacterId = targetId;
                _trades.TryAdd(targetId, trade);
                await TrySendAsync(target, _packets.TradeInvite(c.Name)).ConfigureAwait(false);
                break;
            }

            case ChannelPackets.MiniRoomInviteResult: // the invitee declined
            {
                if (_trades.Get(c.Id) is { } trade)
                {
                    await CancelTradeAsync(trade).ConfigureAwait(false);
                }

                break;
            }

            case ChannelPackets.MiniRoomEnter:
            {
                Trade? trade = _trades.Get(c.Id);
                if (trade is not null)
                {
                    if (trade.VisitorEntered || trade.InvitedCharacterId != c.Id)
                    {
                        return;
                    }

                    trade.Join(_player);
                    trade.VisitorEntered = true;
                    await session.SendAsync(_packets.TradeStart(c, 1, trade.Starter.Player.Character)).ConfigureAwait(false);
                    await TrySendAsync(trade.Starter.Player, _packets.TradePartnerAdd(c)).ConfigureAwait(false);
                    return;
                }

                await EnterMiniRoomAsync(session, c, packet).ConfigureAwait(false);
                break;
            }

            case ChannelPackets.MiniRoomChat:
            {
                packet.ReadInt(); // update time
                string message = packet.ReadString();
                if (_trades.Get(c.Id) is { } trade && trade.SideOf(c.Id) is { } side)
                {
                    byte[] chat = _packets.TradeChat(side.Slot, $"{c.Name} : {message}");
                    await session.SendAsync(chat).ConfigureAwait(false);
                    if (trade.PartnerOf(side) is { } partner)
                    {
                        await TrySendAsync(partner.Player, chat).ConfigureAwait(false);
                    }
                }
                else if (_miniGames.GetForCharacter(c.Id) is { } game && game.SeatOf(c.Id) is int seat and >= 0)
                {
                    await BroadcastToMiniGameAsync(game, _packets.TradeChat((byte)seat, $"{c.Name} : {message}")).ConfigureAwait(false);
                }
                else if (_playerShops.GetForCharacter(c.Id) is { } shop && shop.SeatOf(c.Id) is int shopSeat and >= 0)
                {
                    await BroadcastToPlayerShopAsync(shop, _packets.TradeChat((byte)shopSeat, $"{c.Name} : {message}")).ConfigureAwait(false);
                }
                else if (_merchants.GetForParticipant(c.Id) is { } merchant && merchant.SeatOf(c.Id) is int merchSeat and >= 0)
                {
                    await BroadcastToMerchantAsync(merchant, _packets.TradeChat((byte)merchSeat, $"{c.Name} : {message}")).ConfigureAwait(false);
                }

                break;
            }

            case ChannelPackets.MiniRoomLeave:
            {
                if (_trades.Get(c.Id) is { } trade)
                {
                    await CancelTradeAsync(trade).ConfigureAwait(false);
                }
                else if (_miniGames.GetForCharacter(c.Id) is { } game)
                {
                    await ExitMiniGameAsync(game, c.Id).ConfigureAwait(false);
                }
                else if (_playerShops.GetForCharacter(c.Id) is { } shop)
                {
                    await ExitPlayerShopAsync(shop, c.Id).ConfigureAwait(false);
                }
                else if (_merchants.GetForParticipant(c.Id) is { } merchant)
                {
                    await ExitHiredMerchantAsync(merchant, c.Id).ConfigureAwait(false);
                }

                break;
            }

            case ChannelPackets.MiniRoomBalloonReq:
                if (_merchants.GetForParticipant(c.Id) is { } stocked && stocked.SeatOf(c.Id) == 0)
                {
                    await OpenHiredMerchantForBusinessAsync(stocked).ConfigureAwait(false);
                }
                else
                {
                    await OpenPlayerShopForBusinessAsync(c).ConfigureAwait(false);
                }

                break;

            case ChannelPackets.PsPutItem:
            case ChannelPackets.EsPutItem:
                await HandleShopPutItemAsync(session, c, packet).ConfigureAwait(false);
                break;

            case ChannelPackets.PsBuyItem:
            case ChannelPackets.EsBuyItem:
            case ChannelPackets.EsBuyResult:
                if (_merchants.GetForParticipant(c.Id) is { } sellingMerchant)
                {
                    await HandleMerchantBuyItemAsync(session, c, sellingMerchant, packet).ConfigureAwait(false);
                }
                else
                {
                    await HandleShopBuyItemAsync(session, c, packet).ConfigureAwait(false);
                }

                break;

            case ChannelPackets.PsMoveItemToInventory:
            case ChannelPackets.EsMoveItemToInventory:
                if (_merchants.GetForParticipant(c.Id) is { } managedMerchant)
                {
                    await HandleMerchantReclaimItemAsync(session, c, managedMerchant, packet).ConfigureAwait(false);
                }
                else
                {
                    await HandleShopReclaimItemAsync(session, c, packet).ConfigureAwait(false);
                }

                break;

            case ChannelPackets.PsBan:
                await HandleShopBanAsync(c, packet).ConfigureAwait(false);
                break;

            case ChannelPackets.TradePutItem:
                await HandleTradePutItemAsync(session, c, packet).ConfigureAwait(false);
                break;

            case ChannelPackets.TradePutMoney:
                await HandleTradePutMoneyAsync(session, c, packet).ConfigureAwait(false);
                break;

            case ChannelPackets.TradeConfirm:
                await HandleTradeConfirmAsync(session, c).ConfigureAwait(false);
                break;

            default:
                await HandleMiniGameOpAsync(session, c, protocol, packet).ConfigureAwait(false);
                break;
        }
    }

    private async ValueTask HandleTradePutItemAsync(MapleSession session, Character c, PacketReader packet)
    {
        // TRP_PutItem: [invType:1][slot:2][qty:2][targetSlot:1]
        int tab = packet.ReadByte();
        short slot = packet.ReadShort();
        int qty = packet.ReadShort();
        byte targetSlot = packet.ReadByte();

        Trade? trade = _trades.Get(c.Id);
        TradeSide? side = trade?.SideOf(c.Id);
        if (trade is null || side is null || side.Locked || !trade.VisitorEntered)
        {
            return;
        }

        InventoryItem? item = Inventory.ItemAt(c, tab, slot);
        if (item is null || qty < 0)
        {
            return;
        }

        InventoryItem staged;
        InventoryChange invChange;
        if (tab == 1 || qty == 0 || qty >= item.Quantity)
        {
            // Move the whole item object (equips keep their stats through the trade).
            c.EquippedItems.Remove(item);
            staged = item;
            invChange = new InventoryChange(InvMode.Remove, tab, slot, null, 0);
        }
        else
        {
            item.Quantity -= (short)qty;
            staged = new InventoryItem { ItemId = item.ItemId, Quantity = (short)qty };
            invChange = new InventoryChange(InvMode.Update, tab, slot, item, item.Quantity);
        }

        staged.Position = targetSlot; // the trade-window slot
        side.Items.Add(staged);
        _characters.Save(c);

        await session.SendAsync(_packets.InventoryOperation(new[] { invChange })).ConfigureAwait(false);
        await session.SendAsync(_packets.TradeItemAdd(0, staged)).ConfigureAwait(false);
        if (trade.PartnerOf(side) is { } partner)
        {
            await TrySendAsync(partner.Player, _packets.TradeItemAdd(1, staged)).ConfigureAwait(false);
        }
    }

    private async ValueTask HandleTradePutMoneyAsync(MapleSession session, Character c, PacketReader packet)
    {
        int meso = packet.ReadInt();
        Trade? trade = _trades.Get(c.Id);
        TradeSide? side = trade?.SideOf(c.Id);
        if (trade is null || side is null || side.Locked || !trade.VisitorEntered || meso <= 0 || c.Meso < meso)
        {
            return;
        }

        c.Meso -= meso;
        side.Meso += meso;
        _characters.Save(c);

        await session.SendAsync(_packets.StatChanged(c, StatFlag.Meso)).ConfigureAwait(false);
        await session.SendAsync(_packets.TradeMesoSet(0, side.Meso)).ConfigureAwait(false);
        if (trade.PartnerOf(side) is { } partner)
        {
            await TrySendAsync(partner.Player, _packets.TradeMesoSet(1, side.Meso)).ConfigureAwait(false);
        }
    }

    private async ValueTask HandleTradeConfirmAsync(MapleSession session, Character c)
    {
        Trade? trade = _trades.Get(c.Id);
        TradeSide? side = trade?.SideOf(c.Id);
        if (trade is null || side is null || side.Locked || !trade.VisitorEntered)
        {
            return;
        }

        side.Locked = true;
        if (trade.PartnerOf(side) is { } partner)
        {
            await TrySendAsync(partner.Player, _packets.TradeConfirmation()).ConfigureAwait(false);
        }

        if (trade.BothLocked)
        {
            await CompleteTradeAsync(trade).ConfigureAwait(false);
        }
    }

    /// <summary>Executes a locked trade: each side receives the other's staged items and meso.</summary>
    private async ValueTask CompleteTradeAsync(Trade trade)
    {
        if (!trade.TryClose())
        {
            return; // the other session's confirm/cancel got here first
        }

        _trades.Remove(trade);
        TradeSide[] sides = { trade.Starter, trade.Visitor! };
        foreach (TradeSide side in sides)
        {
            TradeSide giver = side == trade.Starter ? trade.Visitor! : trade.Starter;
            Character receiver = side.Player.Character;

            var changes = new List<InventoryChange>();
            foreach (InventoryItem item in giver.Items)
            {
                changes.Add(Inventory.Place(receiver, item));
            }

            if (giver.Meso > 0)
            {
                receiver.Meso = (int)Math.Clamp((long)receiver.Meso + giver.Meso, 0, int.MaxValue);
            }

            _characters.Save(receiver);
            if (changes.Count > 0)
            {
                await TrySendAsync(side.Player, _packets.InventoryOperation(changes)).ConfigureAwait(false);
            }

            if (giver.Meso > 0)
            {
                await TrySendAsync(side.Player, _packets.StatChanged(receiver, StatFlag.Meso)).ConfigureAwait(false);
            }

            await TrySendAsync(side.Player, _packets.TradeLeave(side.Slot, ChannelPackets.TradeMsgSuccess)).ConfigureAwait(false);
        }
    }

    /// <summary>Cancels a trade: staged items and meso return to their owners; both sides close.</summary>
    private async ValueTask CancelTradeAsync(Trade trade)
    {
        if (!trade.TryClose())
        {
            return; // already completed/cancelled by the other session
        }

        _trades.Remove(trade);
        foreach (TradeSide? side in new[] { trade.Starter, trade.Visitor })
        {
            if (side is null)
            {
                continue;
            }

            Character owner = side.Player.Character;
            var changes = new List<InventoryChange>();
            foreach (InventoryItem item in side.Items)
            {
                changes.Add(Inventory.Place(owner, item));
            }

            if (side.Meso > 0)
            {
                owner.Meso = (int)Math.Clamp((long)owner.Meso + side.Meso, 0, int.MaxValue);
            }

            _characters.Save(owner);
            if (changes.Count > 0)
            {
                await TrySendAsync(side.Player, _packets.InventoryOperation(changes)).ConfigureAwait(false);
            }

            if (side.Meso > 0)
            {
                await TrySendAsync(side.Player, _packets.StatChanged(owner, StatFlag.Meso)).ConfigureAwait(false);
            }

            await TrySendAsync(side.Player, _packets.TradeLeave(side.Slot, ChannelPackets.TradeMsgCancelled)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Creates an Omok / match-card room (ports the MRP_Create game branch): needs the board item
    /// (4080000+n stone set / 4080100 card deck), one room per player. The room opens for the owner
    /// and its balloon appears over their head for the whole map.
    /// </summary>
    private async ValueTask CreateMiniGameAsync(MapleSession session, Character c, byte gameType, PacketReader packet)
    {
        string description = packet.ReadString();
        byte hasPassword = packet.ReadByte();
        string password = hasPassword > 0 ? packet.ReadString() : string.Empty;
        int piece = packet.ReadByte();

        int itemId = gameType == MiniGame.TypeOmok ? 4080000 + piece : 4080100;
        if (_miniGames.GetForCharacter(c.Id) is not null
            || _trades.Get(c.Id) is not null
            || CountInventoryItem(c, itemId) < 1)
        {
            return;
        }

        MiniGame game = _miniGames.Create(gameType, _player!, description, password, piece);
        await session.SendAsync(_packets.MiniGameRoom(game, viewerSeat: 0)).ConfigureAwait(false);
        if (_field is not null)
        {
            await _field.BroadcastAsync(_packets.MiniRoomBalloon(c.Id, game)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Joins a game room or a personal shop from its balloon (ports the MRP_Enter branch): a free
    /// seat if the room is open (password-gated for games); everyone sees the updated room.
    /// </summary>
    private async ValueTask EnterMiniRoomAsync(MapleSession session, Character c, PacketReader packet)
    {
        int objectId = packet.ReadInt();
        if (_miniGames.GetForCharacter(c.Id) is not null || _playerShops.GetForCharacter(c.Id) is not null)
        {
            await session.SendAsync(_packets.MiniGameFull()).ConfigureAwait(false);
            return;
        }

        if (_miniGames.Get(objectId) is { } game)
        {
            if (game.Visitor is not null || !game.Open || game.SeatOf(c.Id) >= 0)
            {
                await session.SendAsync(_packets.MiniGameFull()).ConfigureAwait(false);
                return;
            }

            if (game.Password.Length > 0)
            {
                byte hasPassword = packet.Remaining > 0 ? packet.ReadByte() : (byte)0;
                string password = hasPassword > 0 ? packet.ReadString() : string.Empty;
                if (!string.Equals(password, game.Password, StringComparison.Ordinal))
                {
                    await session.SendAsync(_packets.MiniGameFull()).ConfigureAwait(false);
                    return;
                }
            }

            _miniGames.SetVisitor(game, _player!);
            await TrySendAsync(game.Owner, _packets.MiniGameNewVisitor(game, c, seat: 1)).ConfigureAwait(false);
            await session.SendAsync(_packets.MiniGameRoom(game, viewerSeat: 1)).ConfigureAwait(false);
            if (_field is not null)
            {
                await _field.BroadcastAsync(_packets.MiniRoomBalloon(game.Owner.Character.Id, game)).ConfigureAwait(false);
            }

            return;
        }

        if (_playerShops.Get(objectId) is { } shop)
        {
            int seat = shop.FreeSeat();
            if (!shop.Open || seat < 0 || shop.SeatOf(c.Id) >= 0)
            {
                await session.SendAsync(_packets.MiniGameFull()).ConfigureAwait(false);
                return;
            }

            byte[] visitorAdd = _packets.PlayerShopVisitorAdd(c, seat);
            await BroadcastToPlayerShopAsync(shop, visitorAdd).ConfigureAwait(false);
            _playerShops.SetVisitor(shop, seat, _player!);
            await session.SendAsync(_packets.PlayerShopRoom(shop, seat)).ConfigureAwait(false);
            await UpdatePlayerShopBalloonAsync(shop).ConfigureAwait(false);
            return;
        }

        if (_merchants.Get(objectId) is { } merchant)
        {
            if (merchant.OwnerId == c.Id)
            {
                // The owner opens management: browsing visitors are shown the door first.
                for (int s = 1; s <= HiredMerchant.MaxVisitors; s++)
                {
                    if (merchant.Visitors[s - 1] is { } visitor)
                    {
                        await TrySendAsync(visitor, _packets.HiredMerchantMaintenance((byte)s)).ConfigureAwait(false);
                        _merchants.RemoveVisitor(merchant, s);
                    }
                }

                merchant.Open = false;
                _merchants.SetManager(merchant, _player!);
                await session.SendAsync(_packets.HiredMerchantRoom(merchant, viewerSeat: 0, firstTime: false)).ConfigureAwait(false);
                return;
            }

            int seat = merchant.FreeSeat();
            if (!merchant.Open || seat < 0 || merchant.SeatOf(c.Id) >= 0)
            {
                await session.SendAsync(_packets.MiniGameFull()).ConfigureAwait(false);
                return;
            }

            byte[] visitorAdd = _packets.PlayerShopVisitorAdd(c, seat);
            await BroadcastToMerchantAsync(merchant, visitorAdd).ConfigureAwait(false);
            _merchants.SetVisitor(merchant, seat, _player!);
            await session.SendAsync(_packets.HiredMerchantRoom(merchant, seat, firstTime: false)).ConfigureAwait(false);
            Field merchantField = _fields.Get(merchant.MapId);
            await merchantField.BroadcastAsync(_packets.EmployeeMiniRoomBalloon(merchant)).ConfigureAwait(false);
            return;
        }

        await session.SendAsync(_packets.MiniGameFull()).ConfigureAwait(false);
    }

    /// <summary>Sends a packet to both seats of a game room.</summary>
    private async ValueTask BroadcastToMiniGameAsync(MiniGame game, byte[] packet)
    {
        await TrySendAsync(game.Owner, packet).ConfigureAwait(false);
        if (game.Visitor is { } visitor)
        {
            await TrySendAsync(visitor, packet).ConfigureAwait(false);
        }
    }

    /// <summary>Refreshes the room's balloon for the owner's map.</summary>
    private async ValueTask UpdateMiniGameBalloonAsync(MiniGame game, bool closed = false)
    {
        Field field = _fields.Get(game.Owner.Character.MapId);
        await field.BroadcastAsync(_packets.MiniRoomBalloon(game.Owner.Character.Id, closed ? null : game)).ConfigureAwait(false);
    }

    /// <summary>
    /// A participant leaves the room (ports <c>MapleMiniGame.exit</c>): the owner leaving closes
    /// the room for everyone; a visitor leaving frees their seat. An abandoned round ends.
    /// </summary>
    private async ValueTask ExitMiniGameAsync(MiniGame game, int leavingCharacterId)
    {
        if (game.SeatOf(leavingCharacterId) == 0)
        {
            // Owner closes the room: the visitor is told the room is closing.
            if (game.Visitor is { } visitor)
            {
                await TrySendAsync(visitor, _packets.MiniRoomClosed(1, reason: 3)).ConfigureAwait(false);
            }

            _miniGames.Remove(game);
            await UpdateMiniGameBalloonAsync(game, closed: true).ConfigureAwait(false);
        }
        else
        {
            _miniGames.RemoveVisitor(game);
            game.Ready[1] = false;
            game.Open = true; // a running round is abandoned
            await TrySendAsync(game.Owner, _packets.MiniRoomVisitorLeave(1)).ConfigureAwait(false);
            await UpdateMiniGameBalloonAsync(game).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Ends a round (ports <c>getMiniGameResult</c>'s stat updates + <c>checkExitAfterGame</c>):
    /// records the result, shows it to both seats, reopens the lobby, and honors "leave after game".
    /// </summary>
    private async ValueTask EndMiniGameRoundAsync(MiniGame game, int result, int seat)
    {
        // Stat updates exactly as the reference: a give-up records only the loser's loss.
        game.AddResult(seat, result);
        if (result != MiniGame.ResultLose)
        {
            game.AddResult(seat == 1 ? 0 : 1, result == MiniGame.ResultWin ? MiniGame.ResultLose : MiniGame.ResultTie);
        }

        _characters.Save(game.Owner.Character);
        if (game.Visitor is { } visitor)
        {
            _characters.Save(visitor.Character);
        }

        await BroadcastToMiniGameAsync(game, _packets.MiniGameResult(game, result, seat)).ConfigureAwait(false);
        game.Open = true;
        game.RequestedTie = -1;
        await UpdateMiniGameBalloonAsync(game).ConfigureAwait(false);

        for (int s = MiniGame.MaxSize - 1; s >= 0; s--) // visitor first so the owner-close is last
        {
            if (game.ExitAfter[s] && game.PlayerAt(s) is { } player)
            {
                game.ExitAfter[s] = false;
                await ExitMiniGameAsync(game, player.Character.Id).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Handles the in-game mini-room ops (ports the MGRP_/ORP_/MGP_ cases of
    /// <c>ReqCMiniRoomBaseDlg.OnMiniRoom</c>): ready/start, Omok stones, match-card flips,
    /// tie/give-up/leave-after, turn timeouts, and kicks.
    /// </summary>
    private async ValueTask HandleMiniGameOpAsync(MapleSession session, Character c, byte protocol, PacketReader packet)
    {
        MiniGame? game = _miniGames.GetForCharacter(c.Id);
        if (game is null)
        {
            return;
        }

        int seat = game.SeatOf(c.Id);
        switch (protocol)
        {
            case ChannelPackets.MgReady:
            case ChannelPackets.MgCancelReady:
                if (seat == 1 && game.Open)
                {
                    game.Ready[1] = !game.Ready[1];
                    await BroadcastToMiniGameAsync(game, _packets.MiniGameReady(game.Ready[1])).ConfigureAwait(false);
                }

                break;

            case ChannelPackets.MgStart:
                if (seat == 0 && game.Open && game.Visitor is not null && game.Ready[1])
                {
                    game.StartRound();
                    byte[] start = game.GameType == MiniGame.TypeOmok
                        ? _packets.MiniGameStart(game.Loser)
                        : _packets.MatchCardStart(game, game.Loser);
                    await BroadcastToMiniGameAsync(game, start).ConfigureAwait(false);
                    await UpdateMiniGameBalloonAsync(game).ConfigureAwait(false);
                }

                break;

            case ChannelPackets.MgTieRequest:
                if (!game.Open)
                {
                    FieldPlayer? other = game.PlayerAt(seat == 0 ? 1 : 0);
                    if (other is not null)
                    {
                        await TrySendAsync(other, _packets.MiniGameTieRequest()).ConfigureAwait(false);
                    }

                    game.RequestedTie = seat;
                }

                break;

            case ChannelPackets.MgTieResult:
                if (!game.Open && game.RequestedTie > -1 && game.RequestedTie != seat)
                {
                    byte answer = packet.ReadByte();
                    if (answer > 0)
                    {
                        await EndMiniGameRoundAsync(game, MiniGame.ResultTie, game.RequestedTie).ConfigureAwait(false);
                        game.NextLoser();
                    }
                    else
                    {
                        await BroadcastToMiniGameAsync(game, _packets.MiniGameTieDenied()).ConfigureAwait(false);
                    }

                    game.RequestedTie = -1;
                }

                break;

            case ChannelPackets.MgGiveUpRequest:
                if (!game.Open)
                {
                    await EndMiniGameRoundAsync(game, MiniGame.ResultLose, seat).ConfigureAwait(false);
                    game.NextLoser();
                }

                break;

            case ChannelPackets.MgLeaveEngage:
            case ChannelPackets.MgLeaveEngageCancel:
                if (!game.Open && seat >= 0)
                {
                    game.ExitAfter[seat] = !game.ExitAfter[seat];
                    await BroadcastToMiniGameAsync(game, _packets.MiniGameExitAfter(game.ExitAfter[seat])).ConfigureAwait(false);
                }

                break;

            case ChannelPackets.MgTimeOver:
                if (!game.Open)
                {
                    await BroadcastToMiniGameAsync(game, _packets.MiniGameSkip(seat)).ConfigureAwait(false);
                    game.NextLoser();
                }

                break;

            case ChannelPackets.MgBan:
                if (seat == 0 && game.Open && game.Visitor is { } banned)
                {
                    await TrySendAsync(banned, _packets.MiniRoomClosed(1, reason: 5)).ConfigureAwait(false);
                    _miniGames.RemoveVisitor(game);
                    game.Ready[1] = false;
                    await TrySendAsync(game.Owner, _packets.MiniRoomVisitorLeave(1)).ConfigureAwait(false);
                    await UpdateMiniGameBalloonAsync(game).ConfigureAwait(false);
                }

                break;

            case ChannelPackets.MgPutStone:
            {
                if (game.Open || game.GameType != MiniGame.TypeOmok)
                {
                    return;
                }

                int x = packet.ReadInt();
                int y = packet.ReadInt();
                byte type = packet.ReadByte();
                if (!game.TryPlacePiece(x, y, type))
                {
                    return; // occupied square — the reference silently ignores it
                }

                await BroadcastToMiniGameAsync(game, _packets.MiniGameOmokMove(x, y, type)).ConfigureAwait(false);
                if (game.HasFiveInARow(type))
                {
                    await EndMiniGameRoundAsync(game, MiniGame.ResultWin, seat).ConfigureAwait(false);
                }

                game.NextLoser(); // the reference advances the turn after every placement
                break;
            }

            case ChannelPackets.MgTurnUpCard:
            {
                if (game.Open || game.GameType != MiniGame.TypeMatchCard)
                {
                    return;
                }

                int slot = packet.ReadByte();
                int turn = game.Turn;
                int firstSlot = game.FirstSlot;
                FieldPlayer? other = game.PlayerAt(seat == 0 ? 1 : 0);

                if (turn == 1)
                {
                    // First card of the pair: echo it to the other seat only.
                    game.FirstSlot = slot;
                    if (other is not null)
                    {
                        await TrySendAsync(other, _packets.MatchCardSelect(turn, slot, firstSlot, turn)).ConfigureAwait(false);
                    }

                    game.Turn = 0;
                    return;
                }

                if (firstSlot > 0 && game.CardId(firstSlot + 1) == game.CardId(slot + 1))
                {
                    // Match: the flipper scores and keeps the turn.
                    await BroadcastToMiniGameAsync(game, _packets.MatchCardSelect(turn, slot, firstSlot, seat == 0 ? 2 : 3)).ConfigureAwait(false);
                    game.Points[seat]++;
                    if (game.Points[0] + game.Points[1] >= game.MatchesToWin)
                    {
                        bool tie = game.Points[0] == game.Points[1];
                        int winner = game.Points[1] > game.Points[0] ? 1 : 0;
                        await EndMiniGameRoundAsync(game, tie ? MiniGame.ResultTie : MiniGame.ResultWin, winner).ConfigureAwait(false);
                    }
                }
                else
                {
                    // Miss: the turn passes.
                    await BroadcastToMiniGameAsync(game, _packets.MatchCardSelect(turn, slot, firstSlot, seat == 0 ? 0 : 1)).ConfigureAwait(false);
                    game.NextLoser();
                }

                game.Turn = 1;
                game.FirstSlot = 0;
                break;
            }
        }
    }

    /// <summary>The Free Market rooms where personal shops may open (the reference's map gate).</summary>
    private static bool IsFreeMarketMap(int mapId) => mapId is >= 910000001 and <= 910000022;

    /// <summary>
    /// Sets up a personal shop (ports the MRP_Create shop branch): needs a store-permit cash item
    /// and a Free Market room. The shop opens in stocking mode; MRP_Balloon opens it for business.
    /// </summary>
    private async ValueTask CreatePlayerShopAsync(MapleSession session, Character c, PacketReader packet)
    {
        string description = packet.ReadString();
        packet.ReadByte();
        short slot = packet.ReadShort();
        int itemId = packet.ReadInt();

        if (!IsFreeMarketMap(c.MapId)
            || _playerShops.GetForCharacter(c.Id) is not null
            || _miniGames.GetForCharacter(c.Id) is not null
            || _trades.Get(c.Id) is not null)
        {
            return;
        }

        InventoryItem? permit = Inventory.ItemAt(c, Inventory.Tab(itemId), slot);
        if (permit is null || permit.ItemId != itemId)
        {
            return;
        }

        PlayerShop shop = _playerShops.Create(_player!, description, itemId);
        await session.SendAsync(_packets.PlayerShopRoom(shop, viewerSeat: 0)).ConfigureAwait(false);
    }

    /// <summary>MRP_Balloon — the owner finishes stocking and opens for business.</summary>
    private async ValueTask OpenPlayerShopForBusinessAsync(Character c)
    {
        if (_playerShops.GetForCharacter(c.Id) is { } shop && shop.SeatOf(c.Id) == 0 && !shop.Open)
        {
            shop.Open = true;
            await UpdatePlayerShopBalloonAsync(shop).ConfigureAwait(false);
        }
    }

    /// <summary>Sends a packet to everyone in the shop.</summary>
    private async ValueTask BroadcastToPlayerShopAsync(PlayerShop shop, byte[] packet, int exceptCharacterId = -1)
    {
        if (shop.Owner.Character.Id != exceptCharacterId)
        {
            await TrySendAsync(shop.Owner, packet).ConfigureAwait(false);
        }

        foreach (FieldPlayer? visitor in shop.Visitors)
        {
            if (visitor is not null && visitor.Character.Id != exceptCharacterId)
            {
                await TrySendAsync(visitor, packet).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask UpdatePlayerShopBalloonAsync(PlayerShop shop, bool closed = false)
    {
        Field field = _fields.Get(shop.Owner.Character.MapId);
        await field.BroadcastAsync(_packets.PlayerShopBalloon(shop.Owner.Character.Id, closed ? null : shop)).ConfigureAwait(false);
    }

    /// <summary>
    /// PSP_PutItem — the owner lists items for sale (ports the reference's checks): the stock
    /// leaves the inventory immediately; rechargeables list as one bundle of the whole stack.
    /// </summary>
    private async ValueTask HandleShopPutItemAsync(MapleSession session, Character c, PacketReader packet)
    {
        int tab = packet.ReadByte();
        short slot = packet.ReadShort();
        short bundles = packet.ReadShort();
        short perBundle = packet.ReadShort();
        int price = packet.ReadInt();

        // The stocking surface is shared: a personal shop or a managed hired merchant.
        List<PlayerShopItem>? listings = null;
        byte[]? refresh = null;
        if (_playerShops.GetForCharacter(c.Id) is { } shop && shop.SeatOf(c.Id) == 0)
        {
            listings = shop.Items;
        }
        else if (_merchants.GetForParticipant(c.Id) is { } merchant && merchant.SeatOf(c.Id) == 0)
        {
            listings = merchant.Items;
        }

        if (listings is null || price <= 0 || bundles <= 0 || perBundle <= 0)
        {
            return;
        }

        InventoryItem? item = Inventory.ItemAt(c, tab, slot);
        long total = (long)bundles * perBundle;
        if (item is null || total <= 0 || total > 32767)
        {
            return;
        }

        bool rechargeable = item.ItemId / 10000 is 207 or 233; // stars / bullets
        if (!rechargeable && item.Quantity < total)
        {
            return;
        }

        InventoryChange? change;
        if (rechargeable)
        {
            // The whole stack lists as a single bundle (ports the star/bullet special case).
            change = Inventory.RemoveFromSlot(c, tab, slot, item.Quantity);
            listings.Add(new PlayerShopItem(
                new InventoryItem { ItemId = item.ItemId, Quantity = item.Quantity }, bundles: 1, price));
        }
        else if (tab == 1)
        {
            // The equip instance itself goes on the shelf so its stats survive the sale.
            change = Inventory.RemoveFromSlot(c, tab, slot, 1);
            item.Quantity = 1;
            listings.Add(new PlayerShopItem(item, bundles: 1, price));
        }
        else
        {
            change = Inventory.RemoveFromSlot(c, tab, slot, (int)total);
            listings.Add(new PlayerShopItem(
                new InventoryItem { ItemId = item.ItemId, Quantity = perBundle }, bundles, price));
        }

        if (_playerShops.GetForCharacter(c.Id) is { } s2)
        {
            refresh = _packets.PlayerShopItemUpdate(s2);
        }
        else
        {
            HiredMerchant stockedMerchant = _merchants.GetForParticipant(c.Id)!;
            _merchants.Persist(stockedMerchant);
            refresh = _packets.HiredMerchantItemUpdate(stockedMerchant);
        }

        _characters.Save(c);
        if (change is { } ch)
        {
            await session.SendAsync(_packets.InventoryOperation(new[] { ch })).ConfigureAwait(false);
        }

        await session.SendAsync(refresh).ConfigureAwait(false);
    }

    /// <summary>PSP_BuyItem — a visitor buys bundles (ports <c>MaplePlayerShop.buy</c>).</summary>
    private async ValueTask HandleShopBuyItemAsync(MapleSession session, Character c, PacketReader packet)
    {
        int index = packet.ReadByte();
        short quantity = packet.ReadShort();

        PlayerShop? shop = _playerShops.GetForCharacter(c.Id);
        if (shop is null || shop.SeatOf(c.Id) <= 0 || index < 0 || index >= shop.Items.Count || quantity <= 0)
        {
            return;
        }

        PlayerShopItem listing = shop.Items[index];
        long units = (long)quantity * listing.Item.Quantity;
        long cost = (long)listing.Price * quantity;
        if (units <= 0 || units > 32767 || cost <= 0 || cost > int.MaxValue || c.Meso < cost)
        {
            return;
        }

        // Claim the bundles under the shop lock so two visitors can't oversell a listing.
        lock (shop.Items)
        {
            if (listing.Bundles < quantity)
            {
                return;
            }

            listing.Bundles -= quantity;
        }

        Character owner = shop.Owner.Character;

        // Hand the goods over: an equip carries its instance; bundles stack normally.
        List<InventoryChange> changes;
        if (Inventory.Tab(listing.Item.ItemId) == 1)
        {
            changes = new List<InventoryChange> { Inventory.Place(c, listing.Item) };
        }
        else
        {
            int slotMax = _items.GetConsume(listing.Item.ItemId)?.SlotMax ?? Inventory.DefaultSlotMax;
            changes = Inventory.Add(c, listing.Item.ItemId, (int)units, slotMax);
        }

        c.Meso -= (int)cost;
        owner.Meso = (int)Math.Clamp((long)owner.Meso + cost, 0, int.MaxValue);
        _characters.Save(c);
        _characters.Save(owner);

        if (changes.Count > 0)
        {
            await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
        }

        await session.SendAsync(_packets.StatChanged(c, StatFlag.Meso)).ConfigureAwait(false);
        await TrySendAsync(shop.Owner, _packets.StatChanged(owner, StatFlag.Meso)).ConfigureAwait(false);
        await BroadcastToPlayerShopAsync(shop, _packets.PlayerShopItemUpdate(shop)).ConfigureAwait(false);

        if (shop.IsSoldOut)
        {
            await ClosePlayerShopAsync(shop, PlayerShop.CloseReasonSoldOut).ConfigureAwait(false);
        }
    }

    /// <summary>PSP_MoveItemToInventory — the owner reclaims a listing.</summary>
    private async ValueTask HandleShopReclaimItemAsync(MapleSession session, Character c, PacketReader packet)
    {
        int index = packet.ReadShort();
        PlayerShop? shop = _playerShops.GetForCharacter(c.Id);
        if (shop is null || shop.SeatOf(c.Id) != 0 || index < 0 || index >= shop.Items.Count)
        {
            return;
        }

        PlayerShopItem listing = shop.Items[index];
        if (listing.Bundles > 0)
        {
            List<InventoryChange> changes = ReturnListingTo(c, listing);
            _characters.Save(c);
            if (changes.Count > 0)
            {
                await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
            }
        }

        shop.Items.RemoveAt(index);
        await session.SendAsync(_packets.PlayerShopItemUpdate(shop)).ConfigureAwait(false);
    }

    /// <summary>PSP_Ban — the owner throws a visitor out (ports <c>banPlayer</c>).</summary>
    private async ValueTask HandleShopBanAsync(Character c, PacketReader packet)
    {
        packet.ReadByte(); // claimed slot
        string name = packet.ReadString();
        PlayerShop? shop = _playerShops.GetForCharacter(c.Id);
        if (shop is null || shop.SeatOf(c.Id) != 0)
        {
            return;
        }

        for (int i = 0; i < shop.Visitors.Length; i++)
        {
            if (shop.Visitors[i] is { } visitor
                && string.Equals(visitor.Character.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                await TrySendAsync(visitor, _packets.MiniRoomClosed(1, PlayerShop.CloseReasonKicked)).ConfigureAwait(false);
                _playerShops.RemoveVisitor(shop, i + 1);
                await BroadcastToPlayerShopAsync(shop, _packets.MiniRoomVisitorLeave((byte)(i + 1))).ConfigureAwait(false);
                await UpdatePlayerShopBalloonAsync(shop).ConfigureAwait(false);
                return;
            }
        }
    }

    /// <summary>Puts a listing's remaining stock back into a character's inventory.</summary>
    private List<InventoryChange> ReturnListingTo(Character c, PlayerShopItem listing)
    {
        if (Inventory.Tab(listing.Item.ItemId) == 1)
        {
            listing.Bundles = 0;
            return new List<InventoryChange> { Inventory.Place(c, listing.Item) };
        }

        int units = listing.Bundles * listing.Item.Quantity;
        listing.Bundles = 0;
        int slotMax = _items.GetConsume(listing.Item.ItemId)?.SlotMax ?? Inventory.DefaultSlotMax;
        return Inventory.Add(c, listing.Item.ItemId, units, slotMax);
    }

    /// <summary>
    /// Sets up a hired merchant (ports the MRP_Create entrusted-shop branch): needs the employee
    /// permit cash item and a Free Market room, one merchant per owner. The owner enters the
    /// stocking view; MRP_Balloon then puts the employee NPC on the map.
    /// </summary>
    private async ValueTask CreateHiredMerchantAsync(MapleSession session, Character c, PacketReader packet)
    {
        string description = packet.ReadString();
        packet.ReadByte();
        short slot = packet.ReadShort();
        int itemId = packet.ReadInt();

        if (!IsFreeMarketMap(c.MapId)
            || _merchants.GetByOwner(c.Id) is not null
            || _merchants.GetForParticipant(c.Id) is not null
            || _playerShops.GetForCharacter(c.Id) is not null
            || _trades.Get(c.Id) is not null)
        {
            return;
        }

        InventoryItem? permit = Inventory.ItemAt(c, Inventory.Tab(itemId), slot);
        if (permit is null || permit.ItemId != itemId || itemId / 10000 != 503)
        {
            return;
        }

        HiredMerchant merchant = _merchants.Create(c, description, itemId, c.MapId, _player!.X, _player.Y, 0);
        _merchants.SetManager(merchant, _player);
        await session.SendAsync(_packets.HiredMerchantRoom(merchant, viewerSeat: 0, firstTime: true)).ConfigureAwait(false);
    }

    /// <summary>MRP_Balloon for a stocked merchant: the employee NPC goes live on the map and
    /// keeps selling with the owner gone (ports the MRP_Balloon merchant branch).</summary>
    private async ValueTask OpenHiredMerchantForBusinessAsync(HiredMerchant merchant)
    {
        _merchants.RemoveManager(merchant);
        merchant.Open = true;
        _merchants.Persist(merchant);
        Field field = _fields.Get(merchant.MapId);
        await field.BroadcastAsync(_packets.EmployeeEnterField(merchant)).ConfigureAwait(false);
    }

    /// <summary>Sends a packet to everyone inside the merchant room.</summary>
    private async ValueTask BroadcastToMerchantAsync(HiredMerchant merchant, byte[] packet, int exceptCharacterId = -1)
    {
        if (merchant.Manager is { } manager && manager.Character.Id != exceptCharacterId)
        {
            await TrySendAsync(manager, packet).ConfigureAwait(false);
        }

        foreach (FieldPlayer? visitor in merchant.Visitors)
        {
            if (visitor is not null && visitor.Character.Id != exceptCharacterId)
            {
                await TrySendAsync(visitor, packet).ConfigureAwait(false);
            }
        }
    }

    /// <summary>ESP_BuyItem — a visitor buys from the merchant (ports <c>HiredMerchant.buy</c>):
    /// the taxed price banks on the merchant, the sale lands in the owner's sold list.</summary>
    private async ValueTask HandleMerchantBuyItemAsync(MapleSession session, Character c, HiredMerchant merchant, PacketReader packet)
    {
        int index = packet.ReadByte();
        short quantity = packet.ReadShort();
        if (merchant.SeatOf(c.Id) <= 0 || index < 0 || index >= merchant.Items.Count || quantity <= 0)
        {
            return;
        }

        PlayerShopItem listing = merchant.Items[index];
        long units = (long)quantity * listing.Item.Quantity;
        long cost = (long)listing.Price * quantity;
        if (units <= 0 || units > 32767 || cost <= 0 || cost > int.MaxValue || c.Meso < cost)
        {
            return;
        }

        // Claim the bundles under the merchant lock so two shoppers can't oversell a listing.
        lock (merchant.Items)
        {
            if (listing.Bundles < quantity)
            {
                return;
            }

            listing.Bundles -= quantity;
        }

        List<InventoryChange> changes;
        if (Inventory.Tab(listing.Item.ItemId) == 1)
        {
            changes = new List<InventoryChange> { Inventory.Place(c, listing.Item) };
        }
        else
        {
            int slotMax = _items.GetConsume(listing.Item.ItemId)?.SlotMax ?? Inventory.DefaultSlotMax;
            changes = Inventory.Add(c, listing.Item.ItemId, (int)units, slotMax);
        }

        c.Meso -= (int)cost;
        merchant.Sold.Add(new SoldRecord(listing.Item.ItemId, quantity, (int)cost, c.Name));
        long banked = merchant.Meso + cost;
        merchant.Meso = (int)Math.Clamp(banked - HiredMerchant.Tax((int)Math.Min(banked, int.MaxValue)), 0, int.MaxValue);
        _characters.Save(c);

        if (changes.Count > 0)
        {
            await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
        }

        await session.SendAsync(_packets.StatChanged(c, StatFlag.Meso)).ConfigureAwait(false);
        await BroadcastToMerchantAsync(merchant, _packets.HiredMerchantItemUpdate(merchant)).ConfigureAwait(false);
        _merchants.Persist(merchant);
    }

    /// <summary>ESP_MoveItemToInventory — the managing owner reclaims a listing.</summary>
    private async ValueTask HandleMerchantReclaimItemAsync(MapleSession session, Character c, HiredMerchant merchant, PacketReader packet)
    {
        int index = packet.ReadShort();
        if (merchant.SeatOf(c.Id) != 0 || index < 0 || index >= merchant.Items.Count)
        {
            return;
        }

        PlayerShopItem listing = merchant.Items[index];
        if (listing.Bundles > 0)
        {
            List<InventoryChange> changes = ReturnListingTo(c, listing);
            _characters.Save(c);
            if (changes.Count > 0)
            {
                await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
            }
        }

        merchant.Items.RemoveAt(index);
        _merchants.Persist(merchant);
        await session.SendAsync(_packets.HiredMerchantItemUpdate(merchant)).ConfigureAwait(false);
    }

    /// <summary>
    /// A participant leaves the merchant room. A visitor frees their seat; the managing owner
    /// leaving either reopens the store (stock remains) or packs it up — remaining stock and the
    /// banked (taxed) meso go back to the owner and the employee NPC leaves the map.
    /// </summary>
    private async ValueTask ExitHiredMerchantAsync(HiredMerchant merchant, int leavingCharacterId)
    {
        int seat = merchant.SeatOf(leavingCharacterId);
        if (seat > 0)
        {
            _merchants.RemoveVisitor(merchant, seat);
            await BroadcastToMerchantAsync(merchant, _packets.MiniRoomVisitorLeave((byte)seat)).ConfigureAwait(false);
            return;
        }

        if (seat != 0)
        {
            return;
        }

        _merchants.RemoveManager(merchant);
        if (merchant.Items.Any(i => i.Bundles > 0))
        {
            // Stock remains: back to business.
            merchant.Open = true;
            _merchants.Persist(merchant);
            Field field = _fields.Get(merchant.MapId);
            await field.BroadcastAsync(_packets.EmployeeMiniRoomBalloon(merchant)).ConfigureAwait(false);
            return;
        }

        await CloseHiredMerchantAsync(merchant).ConfigureAwait(false);
    }

    /// <summary>Packs the merchant up: stock + banked meso return to the owner, the NPC leaves.</summary>
    private async ValueTask CloseHiredMerchantAsync(HiredMerchant merchant)
    {
        foreach (FieldPlayer? visitor in merchant.Visitors)
        {
            if (visitor is not null)
            {
                await TrySendAsync(visitor, _packets.MiniRoomClosed(1, PlayerShop.CloseReasonClosed)).ConfigureAwait(false);
            }
        }

        Character? owner = _characters.Find(merchant.OwnerId);
        if (owner is not null)
        {
            var returned = new List<InventoryChange>();
            foreach (PlayerShopItem listing in merchant.Items)
            {
                if (listing.Bundles > 0)
                {
                    returned.AddRange(ReturnListingTo(owner, listing));
                }
            }

            owner.Meso = (int)Math.Clamp((long)owner.Meso + merchant.Meso, 0, int.MaxValue);
            _characters.Save(owner);

            if (FindOnlinePlayer(owner.Id) is { } online)
            {
                if (returned.Count > 0)
                {
                    await TrySendAsync(online, _packets.InventoryOperation(returned)).ConfigureAwait(false);
                }

                await TrySendAsync(online, _packets.StatChanged(owner, StatFlag.Meso)).ConfigureAwait(false);
            }
        }

        _merchants.Remove(merchant);
        Field field = _fields.Get(merchant.MapId);
        await field.BroadcastAsync(_packets.EmployeeLeaveField(merchant)).ConfigureAwait(false);
    }

    /// <summary>A participant leaves the shop; the owner leaving (or a sell-out) closes it,
    /// returning unsold stock (ports <c>MaplePlayerShop.closeShop</c> / <c>removeVisitor</c>).</summary>
    private async ValueTask ExitPlayerShopAsync(PlayerShop shop, int leavingCharacterId)
    {
        int seat = shop.SeatOf(leavingCharacterId);
        if (seat == 0)
        {
            await ClosePlayerShopAsync(shop, PlayerShop.CloseReasonClosed).ConfigureAwait(false);
        }
        else if (seat > 0)
        {
            _playerShops.RemoveVisitor(shop, seat);
            await BroadcastToPlayerShopAsync(shop, _packets.MiniRoomVisitorLeave((byte)seat)).ConfigureAwait(false);
            await UpdatePlayerShopBalloonAsync(shop).ConfigureAwait(false);
        }
    }

    private async ValueTask ClosePlayerShopAsync(PlayerShop shop, byte reason)
    {
        // Visitors are shown the door first, then unsold stock returns to the owner.
        foreach (FieldPlayer? visitor in shop.Visitors)
        {
            if (visitor is not null)
            {
                await TrySendAsync(visitor, _packets.MiniRoomClosed(1, reason)).ConfigureAwait(false);
            }
        }

        Character owner = shop.Owner.Character;
        var returned = new List<InventoryChange>();
        foreach (PlayerShopItem listing in shop.Items)
        {
            if (listing.Bundles > 0)
            {
                returned.AddRange(ReturnListingTo(owner, listing));
            }
        }

        _characters.Save(owner);
        if (returned.Count > 0)
        {
            await TrySendAsync(shop.Owner, _packets.InventoryOperation(returned)).ConfigureAwait(false);
        }

        await TrySendAsync(shop.Owner, _packets.MiniRoomClosed(0, reason)).ConfigureAwait(false);
        _playerShops.Remove(shop);
        await UpdatePlayerShopBalloonAsync(shop, closed: true).ConfigureAwait(false);
    }

    // CP_FriendRequest flags (OpsFriend).
    private const byte FriendReqLoad = 0;
    private const byte FriendReqSet = 1;
    private const byte FriendReqAccept = 2;
    private const byte FriendReqDelete = 3;
}
