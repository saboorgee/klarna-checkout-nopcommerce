using System;
using Motillo.Nop.Plugin.KlarnaCheckout.Models;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using System.Collections.Generic;

namespace Motillo.Nop.Plugin.KlarnaCheckout.Services
{
    public interface IKlarnaCheckoutHelper
    {
        string GetOrderIdFromUri(Uri klarnaOrderUri);
        int ConvertToCents(decimal value);
        Cart GetCart();
        Merchant GetMerchant();
        IEnumerable<CartItem> GetCartItems(IEnumerable<ShoppingCartItem> items);
        Address GetShippingAddress();
        Motillo.Nop.Plugin.KlarnaCheckout.Services.KlarnaCheckoutHelper.SupportedLocale GetSupportedLocale();

        Gui GetGui();
        Options GetOptions();
    }
}
