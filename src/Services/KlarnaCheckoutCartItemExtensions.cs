using System;
using Motillo.Nop.Plugin.KlarnaCheckout.Models;

namespace Motillo.Nop.Plugin.KlarnaCheckout.Services
{
    public static class KlarnaCheckoutCartItemExtensions
    {
        private const string PHYSICAL_CARTITEM_SEPARATOR = "__###__";
        private const string DISCOUNT_GIFTCARD_CARTITEM_SEPARATOR = "GiftCard_";
        private const string DISCOUNT_COUPON_TOTAL_CARTITEM_SEPARATOR = "CouponTotal_";
        private const string DISCOUNT_COUPON_SUB_CARTITEM_SEPARATOR = "CouponSub_";
        private const string DISCOUNT_REWARD_POINTS_CARTITEM_VALUE = "RewardPoints";
        private const string CHECKOUT_ATTRIBUTE_PREFIX = "CheckoutAttributes_";
        private const string CHECKOUT_ATTRIBUTE_SEPARATOR = "__WithValue_";
        private const string SHIPPING_COUPON_CODE_SEPARATOR = "__###__";

        public static CartItem WithShippingCouponCodes(this CartItem item, string[] couponCodes)
        {
            item.MerchantItemData = string.Join(SHIPPING_COUPON_CODE_SEPARATOR, couponCodes);
            return item;
        }

        public static string[] GetCouponCodesFromShippingItem(this CartItem item)
        {
            return item.MerchantItemData.Split(new[] {SHIPPING_COUPON_CODE_SEPARATOR},
                StringSplitOptions.RemoveEmptyEntries);
        }

        public static CartItem WithPhysicalCartItemMerchantInfo(this CartItem item, int productId, string attributesXml,
            params string[] couponCodes)
        {
            var couponCode = string.Join(PHYSICAL_CARTITEM_SEPARATOR, couponCodes);
            attributesXml = attributesXml ?? "";
            item.MerchantItemData =
                $"{productId}{PHYSICAL_CARTITEM_SEPARATOR}{attributesXml}{PHYSICAL_CARTITEM_SEPARATOR}{couponCode}";
            return item;
        }

        public static int GetProductIdFromPhysicalCartItem(this CartItem item)
        {
            var id = GetValueBeforeSeparator(item.MerchantItemData, PHYSICAL_CARTITEM_SEPARATOR);
            return int.Parse(id);
        }

        public static string GetAttributesXmlFromPhysicalCartItem(this CartItem item)
        {
            var attributesAndCoupon = GetValueAfterSeparator(item.MerchantItemData, PHYSICAL_CARTITEM_SEPARATOR);
            return GetValueBeforeSeparator(attributesAndCoupon, PHYSICAL_CARTITEM_SEPARATOR);
        }

        public static string[] GetCouponCodesFromPhysicalCartItem(this CartItem item)
        {
            var attributesAndCoupon = GetValueAfterSeparator(item.MerchantItemData, PHYSICAL_CARTITEM_SEPARATOR);
            var coupons = GetValueAfterSeparator(attributesAndCoupon, PHYSICAL_CARTITEM_SEPARATOR);
            return coupons.Split(new[] {PHYSICAL_CARTITEM_SEPARATOR}, StringSplitOptions.RemoveEmptyEntries);
        }

        public static CartItem WithGiftCardMerchantInfo(this CartItem item, int giftcardId)
        {
            item.MerchantItemData = $"{DISCOUNT_GIFTCARD_CARTITEM_SEPARATOR}{giftcardId}";
            return item;
        }

        public static bool IsDiscountGiftCardCartItem(this CartItem item)
        {
            return !string.IsNullOrWhiteSpace(item.MerchantItemData) &&
                   item.MerchantItemData.StartsWith(DISCOUNT_GIFTCARD_CARTITEM_SEPARATOR);
        }

        public static CartItem WithDiscountCouponTotalCartItem(this CartItem item, string couponCode)
        {
            item.MerchantItemData = $"{DISCOUNT_COUPON_TOTAL_CARTITEM_SEPARATOR}{couponCode}";
            return item;
        }

        public static string GetCouponCodeFromDiscountCouponCartItem(this CartItem item)
        {
            var coupon = GetValueAfterSeparator(item.MerchantItemData, DISCOUNT_COUPON_TOTAL_CARTITEM_SEPARATOR);

            if (string.IsNullOrWhiteSpace(coupon))
                coupon = GetValueAfterSeparator(item.MerchantItemData, DISCOUNT_COUPON_SUB_CARTITEM_SEPARATOR);

            return coupon;
        }

        public static CartItem WithDiscountCouponSubCartItem(this CartItem item, string couponCode)
        {
            item.MerchantItemData = $"{DISCOUNT_COUPON_SUB_CARTITEM_SEPARATOR}{couponCode}";
            return item;
        }

        public static int GetGiftCardIdFromDiscountGiftCardCartItem(this CartItem item)
        {
            var id = GetValueAfterSeparator(item.MerchantItemData, DISCOUNT_GIFTCARD_CARTITEM_SEPARATOR);
            return int.Parse(id);
        }

        public static bool HasOrderLevelDiscountCouponCode(this CartItem item)
        {
            return !string.IsNullOrWhiteSpace(item.GetCouponCodeFromDiscountCouponCartItem());
        }

        public static CartItem WithRewardPointsMerchantInfo(this CartItem item)
        {
            item.MerchantItemData = DISCOUNT_REWARD_POINTS_CARTITEM_VALUE;
            return item;
        }

        public static bool IsRewardPointsCartItem(this CartItem item)
        {
            return item.MerchantItemData == DISCOUNT_REWARD_POINTS_CARTITEM_VALUE;
        }

        public static CartItem WithCheckoutAttributeMerchantInfo(this CartItem item, int attributeId,
            string attributeValue)
        {
            item.MerchantItemData =
                $"{CHECKOUT_ATTRIBUTE_PREFIX}{attributeId}{CHECKOUT_ATTRIBUTE_SEPARATOR}{attributeValue}";
            return item;
        }

        public static bool IsCheckoutAttribtue(this CartItem item)
        {
            return (item.MerchantItemData != null) && item.MerchantItemData.StartsWith(CHECKOUT_ATTRIBUTE_PREFIX);
        }

        public static int GetCheckoutAttributeId(this CartItem item)
        {
            var s = GetValueAfterSeparator(item.MerchantItemData, CHECKOUT_ATTRIBUTE_PREFIX);
            var id = GetValueBeforeSeparator(s, CHECKOUT_ATTRIBUTE_SEPARATOR);
            return int.Parse(id);
        }

        public static string GetCheckoutAttributeValue(this CartItem item)
        {
            return GetValueAfterSeparator(item.MerchantItemData, CHECKOUT_ATTRIBUTE_SEPARATOR);
        }

        private static string GetValueBeforeSeparator(string s, string separator)
        {
            var separatorIndex = s.IndexOf(separator, StringComparison.InvariantCulture);
            if (separatorIndex != -1)
                return s.Substring(0, separatorIndex);

            return s;
        }

        private static string GetValueAfterSeparator(string s, string separator)
        {
            var separatorIndex = s.IndexOf(separator, StringComparison.InvariantCulture);
            if (separatorIndex != -1)
                return s.Substring(separatorIndex + separator.Length);

            return null;
        }
    }
}