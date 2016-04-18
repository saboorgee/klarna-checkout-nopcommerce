using System;
using System.Globalization;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Shipping;
using Nop.Core.Plugins;
using Nop.Services.Events;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;

namespace Motillo.Nop.Plugin.KlarnaCheckout.Plugins
{
    public class OrderActivationPlugin : BasePlugin, IConsumer<ShipmentSentEvent>
    {
        private readonly IOrderService _orderService;
        private readonly ILogger _logger;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IPaymentService _paymentService;

        public OrderActivationPlugin(
            IOrderService orderService,
            ILogger logger,
            IOrderProcessingService orderProcessingService,
            IPaymentService paymentService)
        {
            _orderService = orderService;
            _logger = logger;
            _orderProcessingService = orderProcessingService;
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

                // Before capture was supported all orders were marked as Paid.
                // Existing paid orders need special treatment since the
                // OrderProcessingService can't capture paid orders but they
                // still need to activate the Klarna order.
                if (nopOrder.PaymentStatus == PaymentStatus.Paid)
                {
                    var klarnaPaymentProvider =
                        _paymentService.LoadPaymentMethodBySystemName(KlarnaCheckoutProcessor.PaymentMethodSystemName);
                    var captureResponse = klarnaPaymentProvider.Capture(new CapturePaymentRequest
                    {
                        Order = nopOrder
                    });

                    nopOrder.CaptureTransactionId = captureResponse.CaptureTransactionId;
                    _orderService.UpdateOrder(nopOrder);
                }
                else if (_orderProcessingService.CanCapture(nopOrder))
                {
                    _orderProcessingService.Capture(nopOrder);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: Error capturing order when shipped. OrderId: {0}, OrderGuid: {1}",
                    nopOrder.Id, nopOrder.OrderGuid),
                    exception: ex,
                    customer: nopOrder.Customer);
            }
        }
    }
}