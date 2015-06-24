using System.ComponentModel.DataAnnotations;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;

namespace Motillo.Nop.Plugin.KlarnaCheckout.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        private const string HexPattern = @"^#(?:[0-9a-fA-F]{3}){1,2}$";

        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Motillo.Plugin.KlarnaCheckout.Settings.EId")]
        public int EId { get; set; }
        public bool EId_OverrideForStore { get; set; }

        [NopResourceDisplayName("Motillo.Plugin.KlarnaCheckout.Settings.SharedSecret")]
        public string SharedSecret { get; set; }
        public bool SharedSecret_OverrideForStore { get; set; }

        [NopResourceDisplayName("Motillo.Plugin.KlarnaCheckout.Settings.EnabledCountries")]
        public string EnabledCountries { get; set; }
        public bool EnabledCountries_OverrideForStore { get; set; }

        [NopResourceDisplayName("Motillo.Plugin.KlarnaCheckout.Settings.TermsUrl")]
        public string TermsUrl { get; set; }
        public bool TermsUrl_OverrideForStore { get; set; }

        [NopResourceDisplayName("Motillo.Plugin.KlarnaCheckout.Settings.CheckoutUrl")]
        public string CheckoutUrl { get; set; }
        public bool CheckoutUrl_OverrideForStore { get; set; }

        [NopResourceDisplayName("Motillo.Plugin.KlarnaCheckout.Settings.DisableAutofocus")]
        public bool DisableAutofocus { get; set; }
        public bool DisableAutofocus_OverrideForStore { get; set; }

        [NopResourceDisplayName("Motillo.Plugin.KlarnaCheckout.Settings.AllowSeparateShippingAddress")]
        public bool AllowSeparateShippingAddress { get; set; }
        public bool AllowSeparateShippingAddress_OverrideForStore { get; set; }

        [NopResourceDisplayName("Motillo.Plugin.KlarnaCheckout.Settings.TestMode")]
        public bool TestMode { get; set; }
        public bool TestMode_OverrideForStore { get; set; }

        [NopResourceDisplayName("Motillo.Plugin.KlarnaCheckout.Settings.ColorButton")]
        [RegularExpression(HexPattern)]
        public string ColorButton { get; set; }
        public bool ColorButton_OverrideForStore { get; set; }

        [NopResourceDisplayName("Motillo.Plugin.KlarnaCheckout.Settings.ColorButtonText")]
        [RegularExpression(HexPattern)]
        public string ColorButtonText { get; set; }
        public bool ColorButtonText_OverrideForStore { get; set; }

        [NopResourceDisplayName("Motillo.Plugin.KlarnaCheckout.Settings.ColorCheckbox")]
        [RegularExpression(HexPattern)]
        public string ColorCheckbox { get; set; }
        public bool ColorCheckbox_OverrideForStore { get; set; }

        [NopResourceDisplayName("Motillo.Plugin.KlarnaCheckout.Settings.ColorCheckboxCheckmark")]
        [RegularExpression(HexPattern)]
        public string ColorCheckboxCheckmark { get; set; }
        public bool ColorCheckboxCheckmark_OverrideForStore { get; set; }

        [NopResourceDisplayName("Motillo.Plugin.KlarnaCheckout.Settings.ColorHeader")]
        [RegularExpression(HexPattern)]
        public string ColorHeader { get; set; }
        public bool ColorHeader_OverrideForStore { get; set; }

        [NopResourceDisplayName("Motillo.Plugin.KlarnaCheckout.Settings.ColorLink")]
        [RegularExpression(HexPattern)]
        public string ColorLink { get; set; }
        public bool ColorLink_OverrideForStore { get; set; }
    }
}
