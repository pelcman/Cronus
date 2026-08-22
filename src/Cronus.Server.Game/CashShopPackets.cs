using Cronus.Common;
using Cronus.Domain;
using Cronus.Network.Packets;
using Cronus.Server.Login;

namespace Cronus.Server.Game;

/// <summary>
/// Cash-shop server packet builders (ports <c>ResCStage.SetCashShop</c> and
/// <c>ResCCashShop</c>, JMS v186 paths). The client renders the catalog from its own Etc.wz;
/// the server sends only the character, balances, the locker, and per-action results.
/// </summary>
public sealed class CashShopPackets
{
    // OpsCashItem result types (the JMS v186 declared values).
    public const byte ResLoadLockerDone = 0x4E;
    public const byte ResSetWishFailed = 0x57;
    public const byte ResBuyDone = 0x58;
    public const byte ResBuyFailed = 0x59;
    public const byte ResIncSlotCountFailed = 0x62;
    public const byte ResIncTrunkCountFailed = 0x64;
    public const byte ResMoveLtoSDone = 0x6B;
    public const byte ResMoveLtoSFailed = 0x6C;
    public const byte ResMoveStoLDone = 0x6D;
    public const byte ResMoveStoLFailed = 0x6E;
    public const byte ResDestroyDone = 0x6F;
    public const byte ResDestroyFailed = 0x70;

    // OpsCashItemFailReason bytes carried by failure results (the JMS v186 declared values).
    public const byte FailNoRemainCash = 0xB4;
    public const byte FailNoEmptyPos = 0xC9;
    public const byte FailNoStock = 0xCE;

    /// <summary>A bare result that carries only its type byte (slot/trunk increase failures).</summary>
    public byte[] BareResult(byte resultType)
    {
        PacketWriter w = NewPacket(ServerOpcode.CashShopCashItemResult);
        w.WriteByte(resultType);
        return w.ToArray();
    }

    private readonly OpcodeTable _serverOpcodes;
    private readonly ServerConfig _config;

    public CashShopPackets(OpcodeTable serverOpcodes, ServerConfig config)
    {
        _serverOpcodes = serverOpcodes;
        _config = config;
    }

    private PacketWriter NewPacket(string opcode)
        => new(_serverOpcodes.Get(opcode), _config.PacketHeaderSize, _config.CodePage);

    /// <summary>
    /// Builds <c>LP_SetCashShop</c> — the cash-shop stage: the full CharacterData blob, the
    /// account name, and empty sale/discount/best-item tables (the client's own Etc.wz carries
    /// the catalog; ports <c>ResCStage.SetCashShop</c>, JMS v186 branch).
    /// </summary>
    public byte[] SetCashShop(Character c, string accountName)
    {
        PacketWriter w = NewPacket(ServerOpcode.SetCashShop);
        CharacterDataEncoder.WriteAllData(w, c);
        w.WriteString(accountName);          // maple id
        w.WriteShort(0);                     // SetSaleInfo: no overridden commodities
        w.WriteShort(0);                     // modified-commodity count (JMS >= 180)
        w.WriteByte(0);                      // discount-rate count
        w.WriteBytes(new byte[1080]);        // best items (9 categories x 5 x 2, 12 bytes each)
        w.WriteShort(0);                     // DecodeStock
        w.WriteShort(0);                     // DecodeLimitGoods
        w.WriteByte(0);                      // m_bEventOn
        return w.ToArray();
    }

    /// <summary>Builds <c>LP_CashShopQueryCashResult</c> — the point balances.</summary>
    public byte[] QueryCashResult(int nexonPoint, int maplePoint)
    {
        PacketWriter w = NewPacket(ServerOpcode.CashShopQueryCashResult);
        w.WriteInt(nexonPoint);
        w.WriteInt(maplePoint);
        return w.ToArray();
    }

    /// <summary>Builds <c>LP_CashShopChargeParamResult</c> (the charge-page redirect ack).</summary>
    public byte[] ChargeParamResult(string accountName)
    {
        PacketWriter w = NewPacket(ServerOpcode.CashShopChargeParamResult);
        w.WriteString(accountName);
        return w.ToArray();
    }

    /// <summary>JMS free-coupon dialog; we always report "no free coupon".</summary>
    public byte[] FreeCouponDialog()
    {
        PacketWriter w = NewPacket(ServerOpcode.JmsPointshopFreeCouponDialog);
        w.WriteByte(0);
        return w.ToArray();
    }

    /// <summary>The 55-byte cash item info (ports <c>DataGW_CashItemInfo.Encode</c>).</summary>
    private static void WriteCashItemInfo(PacketWriter w, CashLockerItem item, int accountId)
    {
        w.WriteLong(item.CashId);
        w.WriteLong(accountId);
        w.WriteInt(item.ItemId);
        w.WriteInt(0);
        w.WriteShort(item.Quantity);
        w.WriteFixedString(string.Empty, 13);            // owner
        w.WriteLong(ItemEncoder.MagicalExpiration);      // expiration sentinel
        w.WriteLong(item.CommoditySn);
    }

    /// <summary>
    /// Builds the locker listing sent at entry (<c>CashItemRes_LoadLocker_Done</c>): the items,
    /// then trunk/character slot counts.
    /// </summary>
    public byte[] LoadLockerDone(IReadOnlyCollection<CashLockerItem> items, int accountId, int trunkSlots, int charSlots, int charCount)
    {
        PacketWriter w = NewPacket(ServerOpcode.CashShopCashItemResult);
        w.WriteByte(ResLoadLockerDone);
        w.WriteShort((short)items.Count);
        foreach (CashLockerItem item in items)
        {
            WriteCashItemInfo(w, item, accountId);
        }

        w.WriteShort((short)trunkSlots);
        w.WriteShort((short)charSlots);
        w.WriteShort(0);                 // m_nBuyCharacterCount
        w.WriteShort((short)charCount);
        return w.ToArray();
    }

    /// <summary>A purchase landed in the locker (<c>CashItemRes_Buy_Done</c>).</summary>
    public byte[] BuyDone(CashLockerItem item, int accountId)
    {
        PacketWriter w = NewPacket(ServerOpcode.CashShopCashItemResult);
        w.WriteByte(ResBuyDone);
        WriteCashItemInfo(w, item, accountId);
        return w.ToArray();
    }

    /// <summary>A one-byte-reason failure result (buy failed, move failed, …).</summary>
    public byte[] FailResult(byte resultType, byte reason)
    {
        PacketWriter w = NewPacket(ServerOpcode.CashShopCashItemResult);
        w.WriteByte(resultType);
        w.WriteByte(reason);
        return w.ToArray();
    }

    /// <summary>Locker → inventory done: the landing slot then the full item body.</summary>
    public byte[] MoveLtoSDone(InventoryItem item)
    {
        PacketWriter w = NewPacket(ServerOpcode.CashShopCashItemResult);
        w.WriteByte(ResMoveLtoSDone);
        w.WriteShort(item.Position);
        ItemEncoder.WriteItem(w, item);
        return w.ToArray();
    }

    /// <summary>Inventory → locker done: the item as cash item info.</summary>
    public byte[] MoveStoLDone(CashLockerItem item, int accountId)
    {
        PacketWriter w = NewPacket(ServerOpcode.CashShopCashItemResult);
        w.WriteByte(ResMoveStoLDone);
        WriteCashItemInfo(w, item, accountId);
        return w.ToArray();
    }

    /// <summary>A locker item was destroyed (<c>CashItemRes_Destroy_Done</c>).</summary>
    public byte[] DestroyDone(long cashId)
    {
        PacketWriter w = NewPacket(ServerOpcode.CashShopCashItemResult);
        w.WriteByte(ResDestroyDone);
        w.WriteLong(cashId);
        return w.ToArray();
    }
}
