namespace Cronus.Server.Game;

/// <summary>
/// <c>LP_ShopResult</c> result codes for JMS v186 (ports the <c>OpsShop</c> result enum, v186 block).
/// Recharge reuses the Sell success/error codes in the reference; the Recharge* codes are here for
/// completeness.
/// </summary>
public enum ShopResultCode : byte
{
    BuySuccess = 0,
    BuyNoStock = 1,
    BuyNoMoney = 2,
    BuyUnknown = 3,
    SellSuccess = 4,
    SellNoStock = 5,
    SellIncorrectRequest = 6,
    SellUnknown = 7,
    RechargeSuccess = 8,
    RechargeNoStock = 9,
    RechargeNoMoney = 10,
    RechargeIncorrectRequest = 11,
    RechargeUnknown = 12,
}

/// <summary>Shop-item classification shared by the packet builder and the buy/sell handler.</summary>
public static class ShopItems
{
    /// <summary>True for rechargeable stacks (throwing stars 207xxxx / bullets 233xxxx).</summary>
    public static bool IsRechargeable(int itemId)
    {
        int type = itemId / 10000;
        return type == 207 || type == 233;
    }
}
