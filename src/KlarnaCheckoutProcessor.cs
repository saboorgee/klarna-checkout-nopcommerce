using Motillo.Nop.Plugin.KlarnaCheckout.Controllers;
using Motillo.Nop.Plugin.KlarnaCheckout.Data;
using Motillo.Nop.Plugin.KlarnaCheckout.Services;
using Nop.Core.Domain.Orders;
using Nop.Core.Infrastructure;
using Nop.Core.Plugins;
using Nop.Services.Localization;
using Nop.Services.Payments;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Routing;
using Klarna.Api;
using Nop.Core.Data;
using Nop.Core.Domain.Payments;
using Nop.Services.Logging;
using Nop.Services.Orders;

namespace Motillo.Nop.Plugin.KlarnaCheckout
{
    public class KlarnaCheckoutProcessor : BasePlugin, IPaymentMethod
    {
        public const string PaymentMethodSystemName = "Motillo.KlarnaCheckout";

        private readonly ILogger _logger;
        private readonly IKlarnaCheckoutHelper _klarnaCheckout;
        private readonly IKlarnaCheckoutPaymentService _klarnaCheckoutPaymentService;
        private readonly IOrderService _orderService;
        private readonly IRepository<KlarnaCheckoutEntity> _repository;

        public KlarnaCheckoutProcessor(
            ILogger logger,
            IKlarnaCheckoutHelper klarnaCheckout,
            IKlarnaCheckoutPaymentService klarnaCheckoutPaymentService,
            IOrderService orderService,
            IRepository<KlarnaCheckoutEntity> repository)
        {
            _logger = logger;
            _klarnaCheckout = klarnaCheckout;
            _klarnaCheckoutPaymentService = klarnaCheckoutPaymentService;
            _orderService = orderService;
            _repository = repository;
        }

        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult();
        }

        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            _klarnaCheckoutPaymentService.Acknowledge(postProcessPaymentRequest.Order);
        }

        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            var match = _klarnaCheckout.GetSupportedLocale();

            return match == null;
        }

        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return 0m;
        }

        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            var order = capturePaymentRequest.Order;

            if (!string.IsNullOrEmpty(order.CaptureTransactionId))
            {
                result.AddError("The payment has already been captured.");
                return result;
            }

            try
            {
                var activationResult = _klarnaCheckoutPaymentService.Activate(order);

                result.NewPaymentStatus = PaymentStatus.Paid;
                result.CaptureTransactionId = activationResult.InvoiceNumber;

                order.OrderNotes.Add(new OrderNote
                {
                    Note = string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: Klarna order has been captured. Reservation: {0}, RiskStatus: {1}, InvoiceNumber: {2}",
                    order.AuthorizationTransactionId, activationResult.RiskStatus, activationResult.InvoiceNumber),
                    CreatedOnUtc = DateTime.UtcNow,
                    DisplayToCustomer = false
                });
                _orderService.UpdateOrder(order);

                var klarnaRequest = _repository.Table.FirstOrDefault(x => x.OrderGuid == order.OrderGuid);
                if (klarnaRequest != null)
                {
                    klarnaRequest.Status = KlarnaCheckoutStatus.Activated;
                    _repository.Update(klarnaRequest);
                }
            }
            catch (KlarnaCheckoutException ex)
            {
                order.OrderNotes.Add(new OrderNote
                {
                    Note = "KlarnaCheckout: An error occurred when the payment was being captured. See the error log for more information.",
                    CreatedOnUtc = DateTime.UtcNow,
                    DisplayToCustomer = false
                });
                _orderService.UpdateOrder(order);

                _logger.Error(string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: Error capturing payment. Order Number: {0}",
                    order.Id),
                    exception: ex,
                    customer: order.Customer);
                result.AddError("An error occurred while capturing the payment. See the error log for more information.");
            }

            return result;
        }

        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();

            if (refundPaymentRequest.IsPartialRefund)
            {
                result.AddError("Partial refund is not supported.");
                return result;
            }

            var order = refundPaymentRequest.Order;

            if (string.IsNullOrWhiteSpace(order.CaptureTransactionId))
            {
                result.AddError("Cannot refund. CaptureTransactionId is missing from the order. Has the order been shipped?");
                return result;
            }

            try
            {
                _klarnaCheckoutPaymentService.FullRefund(refundPaymentRequest.Order);
            }
            catch (KlarnaCheckoutException kce)
            {
                _logger.Error(string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: Error refunding klarna order. Reservation Number: {0}; Invoice Number: {1}",
                    order.AuthorizationTransactionId, order.CaptureTransactionId),
                    exception: kce,
                    customer: order.Customer);
                result.AddError("An error occurred while refundinging the order. See the error log for more information.");
            }

            return result;
        }

        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var order = voidPaymentRequest.Order;
            var result = new VoidPaymentResult();

            try
            {
                _klarnaCheckoutPaymentService.CancelPayment(order.AuthorizationTransactionId, order.Customer);

                result.NewPaymentStatus = PaymentStatus.Voided;

                order.OrderNotes.Add(new OrderNote
                {
                    Note = string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: The payment has been voided. Reservation: {0}",
                        order.AuthorizationTransactionId),
                    CreatedOnUtc = DateTime.UtcNow,
                    DisplayToCustomer = false
                });
                _orderService.UpdateOrder(order);
            }
            catch (KlarnaCheckoutException kce)
            {
                order.OrderNotes.Add(new OrderNote
                {
                    Note = "KlarnaCheckout: An error occurred when voiding the payment. See the error log for more information.",
                    CreatedOnUtc = DateTime.UtcNow,
                    DisplayToCustomer = false
                });
                _orderService.UpdateOrder(order);

                _logger.Error(string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: Error voiding payment. Order Id: {0}; Reservation: {1}",
                    order.Id, order.AuthorizationTransactionId),
                    exception: kce,
                    customer: order.Customer);

                result.AddError("An error occurred while voiding the order. See the error log for more information.");
            }

            return result;
        }

        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
            {
                throw new ArgumentNullException("order");
            }

            return false;
        }

        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "KlarnaCheckout";
            routeValues = new RouteValueDictionary
            {
                { "Namespaces", "Motillo.Nop.Plugin.KlarnaCheckout.Controllers" }, { "area", null }
            };
        }

        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "KlarnaCheckout";
            routeValues = new RouteValueDictionary
            {
                { "Namespaces", "Motillo.Nop.Plugin.KlarnaCheckout.Controllers" }, { "area", null }
            };
        }

        public Type GetControllerType()
        {
            return typeof(KlarnaCheckoutController);
        }

        public bool SupportCapture { get { return true; } }
        public bool SupportPartiallyRefund { get { return false; } }
        public bool SupportRefund { get { return true; } }
        public bool SupportVoid { get { return true; } }
        public RecurringPaymentType RecurringPaymentType { get { return RecurringPaymentType.NotSupported; } }
        public PaymentMethodType PaymentMethodType { get { return PaymentMethodType.Standard; } }
        public bool SkipPaymentInfo { get { return false; } }

        public override void Install()
        {
            var context = EngineContext.Current.Resolve<KlarnaCheckoutContext>();
            context.Install();

            this.AddOrUpdatePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.EId", "Butiks-ID (EID)");
            this.AddOrUpdatePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.SharedSecret", "Shared secret");
            this.AddOrUpdatePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.EnabledCountries", "Enabled countries");
            this.AddOrUpdatePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.EnabledCountries.Hint", "Comma separated list with the supported countries two letter ISO code. E.g. SE,NO");
            this.AddOrUpdatePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.TermsUrl", "Terms URL");
            this.AddOrUpdatePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.CheckoutUrl", "Checkout URL");
            this.AddOrUpdatePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.DisableAutofocus", "Disable autofocus");
            this.AddOrUpdatePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.AllowSeparateShippingAddress", "Allow separate shipping address");
            this.AddOrUpdatePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.AllowSeparateShippingAddress.Hint", "Make sure you're allowed to use this before activating!");
            this.AddOrUpdatePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.TestMode", "Test mode");
            this.AddOrUpdatePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.ColorButton", "Button color");
            this.AddOrUpdatePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.ColorButtonText", "Button text color");
            this.AddOrUpdatePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.ColorCheckbox", "Checkbox color");
            this.AddOrUpdatePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.ColorCheckboxCheckmark", "Checkbox checkmark color");
            this.AddOrUpdatePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.ColorHeader", "Header color");
            this.AddOrUpdatePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.ColorLink", "Link color");

            this.AddOrUpdatePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Text.RenderingError", "Error showing Klarna Checkout");
            this.AddOrUpdatePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Text.Unauthorized", "Unauthorized.");
            this.AddOrUpdatePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Text.ThankYouError", "An error occured while creating the order.");

            base.Install();
        }

        public override void Uninstall()
        {
            var context = EngineContext.Current.Resolve<KlarnaCheckoutContext>();
            context.Uninstall();

            this.DeletePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.EId");
            this.DeletePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.SharedSecret");
            this.DeletePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.EnabledCountries");
            this.DeletePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.EnabledCountries.Hint");
            this.DeletePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.TermsUrl");
            this.DeletePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.CheckoutUrl");
            this.DeletePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.DisableAutofocus");
            this.DeletePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.AllowSeparateShippingAddress");
            this.DeletePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.AllowSeparateShippingAddress.Hint");
            this.DeletePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.TestMode");
            this.DeletePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.ColorButton");
            this.DeletePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.ColorButtonText");
            this.DeletePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.ColorCheckbox");
            this.DeletePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.ColorCheckboxCheckmark");
            this.DeletePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.ColorHeader");
            this.DeletePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Settings.ColorLink");

            this.DeletePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Text.RenderingError");
            this.DeletePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Text.Unauthorized");
            this.DeletePluginLocaleResource("Motillo.Plugin.KlarnaCheckout.Text.ThankYouError");

            base.Uninstall();
        }
    }
}