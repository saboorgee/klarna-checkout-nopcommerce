using Motillo.Nop.Plugin.KlarnaCheckout.Models;
using Nop.Core.Domain.Orders;
using System;
using System.Collections.Generic;

namespace Motillo.Nop.Plugin.KlarnaCheckout.Services
{
    public interface IKlarnaCheckoutHelper
    {
        string GetOrderIdFromUri(Uri klarnaOrderUri);
        int ConvertToCents(decimal value);
        Cart GetCartFromOrder(Order order);
        Cart GetCart();
        bool IsRecurringShoppingCart();
        Merchant GetMerchant();
        IEnumerable<CartItem> GetCartItems(IEnumerable<ShoppingCartItem> items);
        Address ConvertAddress();
        Address ConvertAddress(global::Nop.Core.Domain.Common.Address address);
        Motillo.Nop.Plugin.KlarnaCheckout.Services.KlarnaCheckoutHelper.SupportedLocale GetSupportedLocale();
        Motillo.Nop.Plugin.KlarnaCheckout.Services.KlarnaCheckoutHelper.SupportedLocale GetSupportedLocale(global::Nop.Core.Domain.Common.Address shippingAddress, string currency);

        Gui GetGui();
        Options GetOptions();
        Dictionary<string, object> GetEmdForRecurringOrder();
    }
}
