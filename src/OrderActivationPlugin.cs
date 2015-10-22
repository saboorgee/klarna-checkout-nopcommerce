using Motillo.Nop.Plugin.KlarnaCheckout.Data;
using Motillo.Nop.Plugin.KlarnaCheckout.Services;
using Newtonsoft.Json;
using Nop.Core.Data;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Core.Plugins;
using Nop.Services.Events;
using Nop.Services.Logging;
using Nop.Services.Orders;
using System;
using System.Globalization;
using System.Linq;
using Nop.Services.Payments;

namespace Motillo.Nop.Plugin.KlarnaCheckout
{
    public class OrderActivationPlugin : BasePlugin, IConsumer<ShipmentSentEvent>
    {
        private readonly IOrderService _orderService;
        private readonly ILogger _logger;
        private readonly IPaymentService _paymentService;

        public OrderActivationPlugin(
            IOrderService orderService,
            ILogger logger,
            IPaymentService paymentService)
        {
            _orderService = orderService;
            _logger = logger;
            _paymentService = paymentService;
        }

        public void HandleEvent(ShipmentSentEvent eventMessage)
        {
            var nopOrder = eventMessage.Shipment.Order;

            // Ignore other payment methods.
            if (nopOrder.PaymentMethodSystemName != KlarnaCheckoutProcessor.PaymentMethodSystemName)
            {
                return;
            }

            try
            {
                // Check if the order has been captured manually.
                if (!string.IsNullOrEmpty(nopOrder.CaptureTransactionId))
                {
                    nopOrder.OrderNotes.Add(new OrderNote
                    {
                        Note = "KlarnaCheckout: Order shipped but the payment has already been captured.",
                        CreatedOnUtc = DateTime.UtcNow,
                        DisplayToCustomer = false
                    });
                    _orderService.UpdateOrder(nopOrder);
                    return;
                }

                nopOrder.OrderNotes.Add(new OrderNote
                {
                    Note = "KlarnaCheckout: Order has been shipped, trying to capture payment from Klarna.",
                    CreatedOnUtc = DateTime.UtcNow,
                    DisplayToCustomer = false
                });
                _orderService.UpdateOrder(nopOrder);

                var klarnaPaymentService =
                    _paymentService.LoadPaymentMethodBySystemName(KlarnaCheckoutProcessor.PaymentMethodSystemName);
                var captureRequest = new CapturePaymentRequest
                {
                    Order = nopOrder
                };
                klarnaPaymentService.Capture(captureRequest);
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: Error capturing order when shipped. OrderId: {0}, OrderGuid: {1}",
                    nopOrder.Id, nopOrder.OrderGuid), exception: ex, customer: nopOrder.Customer);
            }
        }
    }
}
