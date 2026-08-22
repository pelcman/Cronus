using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Game;
using Cronus.Server.Login;

namespace Cronus.Server.Channel;

/// <summary>
/// The cash-shop server: a client that picks the cash-shop button migrates here, browses the
/// catalog its own Etc.wz renders, buys into the account locker, moves items between the locker
/// and the character's inventory, and migrates back to its channel (ports the
/// <c>PacketHandler_CashShop</c> role: <c>ReqCClientSocket.OnMigrateIn</c>'s CASHSHOP branch +
/// <c>ReqCCashShop</c>). One instance serves one session.
/// </summary>
public sealed class CashShopHandler : PacketHandlerBase
{
    private const int IncSlotPrice = 390; // the reference's slot/trunk expansion price

    // OpsCashItem request types (JMS v186 declared values).
    private const byte ReqBuy = 0x03;
    private const byte ReqSetWish = 0x05;
    private const byte ReqIncSlotCount = 0x06;
    private const byte ReqIncTrunkCount = 0x07;
    private const byte ReqMoveLtoS = 0x0E;
    private const byte ReqMoveStoL = 0x0F;
    private const byte ReqDestroy = 0x1B;
    private const byte ReqBuyPackage = 0x1F;
    private const byte ReqBuyNormal = 0x21;

    /// <summary>Process-wide cash-id source; time-seeded so ids stay unique across restarts.</summary>
    private static long _nextCashId = DateTime.UtcNow.Ticks & 0x0000_FFFF_FFFF_FFFF;

    private readonly CashShopPackets _cs;
    private readonly ChannelPackets _packets;
    private readonly ICharacterRepository _characters;
    private readonly IAccountRepository _accounts;
    private readonly ICommodityProvider _commodities;
    private readonly IReadOnlyList<System.Net.IPEndPoint> _channelEndpoints;
    private readonly int _nxFloor;
    private readonly int _characterSlots;

    private readonly int _opMigrateIn;
    private readonly int _opAliveAck;
    private readonly int _opChargeParam;
    private readonly int _opQueryCash;
    private readonly int _opCashItem;
    private readonly int _opCheckCoupon;
    private readonly int _opRecommendedAvatar;
    private readonly int _opTransferField;

    private Character? _character;
    private Account? _account;

    public CashShopHandler(
        OpcodeTable clientOpcodes,
        OpcodeTable serverOpcodes,
        ICharacterRepository characters,
        IAccountRepository accounts,
        ServerConfig config,
        ICommodityProvider? commodities = null,
        IReadOnlyList<System.Net.IPEndPoint>? channelEndpoints = null,
        int nxFloor = 0,
        int characterSlots = 3)
    {
        _cs = new CashShopPackets(serverOpcodes, config);
        _packets = new ChannelPackets(serverOpcodes, config);
        _characters = characters;
        _accounts = accounts;
        _commodities = commodities ?? new InMemoryCommodityProvider();
        _channelEndpoints = channelEndpoints ?? Array.Empty<System.Net.IPEndPoint>();
        _nxFloor = nxFloor;
        _characterSlots = characterSlots;

        _opMigrateIn = clientOpcodes.Get(ClientOpcode.MigrateIn);
        _opAliveAck = clientOpcodes.Get(ClientOpcode.AliveAck);
        _opChargeParam = clientOpcodes.Get(ClientOpcode.CashShopChargeParamRequest);
        _opQueryCash = clientOpcodes.Get(ClientOpcode.CashShopQueryCashRequest);
        _opCashItem = clientOpcodes.Get(ClientOpcode.CashShopCashItemRequest);
        _opCheckCoupon = clientOpcodes.Get(ClientOpcode.CashShopCheckCouponRequest);
        _opRecommendedAvatar = clientOpcodes.Get(ClientOpcode.JmsRecommendedAvatar);
        _opTransferField = clientOpcodes.Get(ClientOpcode.UserTransferFieldRequest);
    }

    public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader packet)
    {
        if (opcode == _opMigrateIn)
        {
            await EnterAsync(session, packet).ConfigureAwait(false);
            return;
        }

        if (opcode == _opAliveAck || _character is null || _account is null)
        {
            return;
        }

        if (opcode == _opChargeParam)
        {
            await session.SendAsync(_cs.ChargeParamResult(_account.LoginId)).ConfigureAwait(false);
        }
        else if (opcode == _opQueryCash || opcode == _opRecommendedAvatar)
        {
            await SendBalanceAsync(session).ConfigureAwait(false);
        }
        else if (opcode == _opCashItem)
        {
            await HandleCashItemAsync(session, packet).ConfigureAwait(false);
            await SendBalanceAsync(session).ConfigureAwait(false); // the reference refreshes after every op
        }
        else if (opcode == _opCheckCoupon)
        {
            // No coupon system: every code is invalid.
            await session.SendAsync(_cs.FailResult(0x5D, 0xBF)).ConfigureAwait(false); // UseCoupon_Failed / InvalidCoupon
            await SendBalanceAsync(session).ConfigureAwait(false);
        }
        else if (opcode == _opTransferField)
        {
            await LeaveAsync(session).ConfigureAwait(false);
        }
    }

    private ValueTask SendBalanceAsync(MapleSession session)
        => session.SendAsync(_cs.QueryCashResult(_account!.NexonPoint, _account.MaplePoint));

    /// <summary>
    /// The client migrated in: bind the character/account, top the NX balance up to the floor,
    /// and send the entry sequence (stage + balances + locker + JMS coupon dialog).
    /// </summary>
    private async ValueTask EnterAsync(MapleSession session, PacketReader packet)
    {
        // JMS v186: [characterId:4][machineId:16][unk:2][unk:1][clientKey:8]
        int characterId = packet.ReadInt();
        Character? character = _characters.Find(characterId);
        if (character is null)
        {
            return;
        }

        Account? account = _accounts.FindById(character.AccountId);
        if (account is null)
        {
            return;
        }

        // The in-group allowance: keep everyone's balance at least at the configured floor.
        if (_nxFloor > 0 && account.NexonPoint < _nxFloor)
        {
            account.NexonPoint = _nxFloor;
            _accounts.Save(account);
        }

        _character = character;
        _account = account;

        int charCount = _characters.ListByAccount(account.Id, character.WorldId).Count;
        await session.SendAsync(_cs.SetCashShop(character, account.LoginId)).ConfigureAwait(false);
        await SendBalanceAsync(session).ConfigureAwait(false);
        await session.SendAsync(_cs.LoadLockerDone(
            account.CashLocker.Values, account.Id, trunkSlots: 4, _characterSlots, charCount)).ConfigureAwait(false);
        await session.SendAsync(_cs.FreeCouponDialog()).ConfigureAwait(false);
    }

    /// <summary>Leaving: persist and hand the client back to the channel it came from.</summary>
    private async ValueTask LeaveAsync(MapleSession session)
    {
        Character c = _character!;
        _characters.Save(c);
        _accounts.Save(_account!);

        if (_channelEndpoints.Count == 0)
        {
            return;
        }

        System.Net.IPEndPoint back = _channelEndpoints[Math.Clamp(c.LastChannel, 0, _channelEndpoints.Count - 1)];
        await session.SendAsync(_packets.MigrateCommand(back.Address, back.Port)).ConfigureAwait(false);
    }

    private async ValueTask HandleCashItemAsync(MapleSession session, PacketReader packet)
    {
        Character c = _character!;
        Account account = _account!;
        if (packet.Remaining < 1)
        {
            return;
        }

        byte type = packet.ReadByte();
        switch (type)
        {
            case ReqBuy:
            case ReqBuyNormal:
            {
                bool useMaplePoint = type == ReqBuy && packet.ReadByte() != 0;
                int sn = packet.ReadInt();
                Commodity? commodity = _commodities.GetBySn(sn);
                if (commodity is null)
                {
                    await session.SendAsync(_cs.FailResult(CashShopPackets.ResBuyFailed, CashShopPackets.FailNoStock)).ConfigureAwait(false);
                    return;
                }

                int balance = useMaplePoint ? account.MaplePoint : account.NexonPoint;
                if (balance < commodity.Price)
                {
                    await session.SendAsync(_cs.FailResult(CashShopPackets.ResBuyFailed, CashShopPackets.FailNoRemainCash)).ConfigureAwait(false);
                    return;
                }

                var bought = new CashLockerItem
                {
                    CashId = Interlocked.Increment(ref _nextCashId),
                    ItemId = commodity.ItemId,
                    Quantity = commodity.Count,
                    CommoditySn = commodity.Sn,
                };
                account.CashLocker[bought.CashId] = bought;
                if (useMaplePoint)
                {
                    account.MaplePoint -= commodity.Price;
                }
                else
                {
                    account.NexonPoint -= commodity.Price;
                }

                _accounts.Save(account);
                await session.SendAsync(_cs.BuyDone(bought, account.Id)).ConfigureAwait(false);
                break;
            }

            case ReqMoveLtoS: // locker -> the character's inventory
            {
                long cashId = packet.ReadLong();
                if (!account.CashLocker.TryGetValue(cashId, out CashLockerItem? locker))
                {
                    await session.SendAsync(_cs.FailResult(CashShopPackets.ResMoveLtoSFailed, CashShopPackets.FailNoEmptyPos)).ConfigureAwait(false);
                    return;
                }

                var item = new InventoryItem
                {
                    ItemId = locker.ItemId,
                    Quantity = locker.Quantity,
                    CashId = locker.CashId,
                    PetName = ItemEncoder.IsPet(locker.ItemId) ? "ペット" : string.Empty,
                };
                Inventory.Place(c, item);
                account.CashLocker.Remove(cashId);
                _characters.Save(c);
                _accounts.Save(account);
                await session.SendAsync(_cs.MoveLtoSDone(item)).ConfigureAwait(false);
                break;
            }

            case ReqMoveStoL: // the character's inventory -> locker
            {
                long cashId = packet.ReadLong();
                InventoryItem? item = c.EquippedItems.FirstOrDefault(i => i.CashId == cashId && i.Position > 0);
                if (item is null)
                {
                    await session.SendAsync(_cs.FailResult(CashShopPackets.ResMoveStoLFailed, CashShopPackets.FailNoEmptyPos)).ConfigureAwait(false);
                    return;
                }

                var locker = new CashLockerItem
                {
                    CashId = item.CashId,
                    ItemId = item.ItemId,
                    Quantity = item.Quantity,
                };
                c.EquippedItems.Remove(item);
                account.CashLocker[locker.CashId] = locker;
                _characters.Save(c);
                _accounts.Save(account);
                await session.SendAsync(_cs.MoveStoLDone(locker, account.Id)).ConfigureAwait(false);
                break;
            }

            case ReqDestroy:
            {
                packet.ReadString(); // nexon id
                long cashId = packet.ReadLong();
                if (account.CashLocker.Remove(cashId))
                {
                    _accounts.Save(account);
                    await session.SendAsync(_cs.DestroyDone(cashId)).ConfigureAwait(false);
                }

                break;
            }

            case ReqIncSlotCount:
                // Per-tab slot limits aren't modelled; decline so the client's dialog closes.
                await session.SendAsync(_cs.BareResult(CashShopPackets.ResIncSlotCountFailed)).ConfigureAwait(false);
                break;

            case ReqIncTrunkCount:
                await session.SendAsync(_cs.BareResult(CashShopPackets.ResIncTrunkCountFailed)).ConfigureAwait(false);
                break;

            case ReqSetWish:
            case ReqBuyPackage:
                await session.SendAsync(_cs.FailResult(CashShopPackets.ResSetWishFailed, CashShopPackets.FailNoStock)).ConfigureAwait(false);
                break;
        }
    }
}

/// <summary>An empty commodity catalog (a cash shop with nothing on sale).</summary>
public sealed class InMemoryCommodityProvider : ICommodityProvider
{
    private readonly Dictionary<int, Commodity> _bySn;

    public InMemoryCommodityProvider(IEnumerable<Commodity>? commodities = null)
        => _bySn = (commodities ?? Array.Empty<Commodity>()).ToDictionary(c => c.Sn);

    public Commodity? GetBySn(int sn) => _bySn.TryGetValue(sn, out Commodity? c) ? c : null;
}
