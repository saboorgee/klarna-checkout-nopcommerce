using Motillo.Nop.Plugin.KlarnaCheckout.Services;
using Newtonsoft.Json;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Plugins;
using Nop.Services.Events;
using Nop.Services.Logging;
using Nop.Services.Orders;
using System;
using System.Globalization;

namespace Motillo.Nop.Plugin.KlarnaCheckout.Plugins
{
    public class ConnectRecurringOrderPlugin : BasePlugin, IConsumer<OrderPlacedEvent>
    {
        private readonly IOrderService _orderService;
        private readonly ILogger _logger;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IKlarnaCheckoutPaymentService _klarnaPaymentService;

        public ConnectRecurringOrderPlugin(
            IOrderService orderService,
            ILogger logger,
            IOrderProcessingService orderProcessingService,
            IKlarnaCheckoutPaymentService klarnaPaymentService)
        {
            _orderService = orderService;
            _logger = logger;
            _orderProcessingService = orderProcessingService;
            _klarnaPaymentService = klarnaPaymentService;
        }

        public void HandleEvent(OrderPlacedEvent eventMessage)
        {
            var order = eventMessage.Order;

            if (order.PaymentMethodSystemName == KlarnaCheckoutProcessor.PaymentMethodSystemName && order.PaymentStatus == PaymentStatus.Pending)
            {
                if (!string.IsNullOrWhiteSpace(order.SubscriptionTransactionId))
                {
                    try
                    {
                        var recurringOrder = _klarnaPaymentService.CreateRecurring(order);
                        var result = recurringOrder.Marshal();

                        order.OrderNotes.Add(new OrderNote
                        {
                            CreatedOnUtc = DateTime.UtcNow,
                            DisplayToCustomer = false,
                            Note = $"KlarnaCheckout: Recurring order created: {recurringOrder.Location}"
                        });

                        if (result.ContainsKey("reservation"))
                        {
                            var reservation = result["reservation"] as string;
                            order.AuthorizationTransactionId = reservation;

                            if (_orderProcessingService.CanMarkOrderAsAuthorized(order))
                            {
                                _orderProcessingService.MarkAsAuthorized(order);
                                order.OrderNotes.Add(new OrderNote
                                {
                                    CreatedOnUtc = DateTime.UtcNow,
                                    DisplayToCustomer = false,
                                    Note = $"KlarnaCheckout: Order authorized, reservation: {reservation}"
                                });
                            }
                        }

                        if (result.ContainsKey("invoice"))
                        {
                            var invoice = result["invoice"] as string;
                            order.CaptureTransactionId = invoice;

                            if (_orderProcessingService.CanMarkOrderAsPaid(order))
                            {
                                _orderProcessingService.CanMarkOrderAsPaid(order);
                                order.OrderNotes.Add(new OrderNote
                                {
                                    CreatedOnUtc = DateTime.UtcNow,
                                    DisplayToCustomer = false,
                                    Note = $"KlarnaCheckout: Order captured, invoice: {invoice}"
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        var exceptionJson = JsonConvert.SerializeObject(ex.Data);

                        _logger.Error(string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: Error creating recurring Klarna order. Token: {0}, Data: {1}", order.SubscriptionTransactionId, exceptionJson),
                            exception: ex, customer: order.Customer);

                        order.OrderNotes.Add(new OrderNote
                        {
                            DisplayToCustomer = false,
                            Note = $"KlarnaCheckout: Error creating recurring order. Data: ${exceptionJson}, Exception: {ex.Message}"
                        });
                    }

                    _orderService.UpdateOrder(order);
                }
            }
        }
    }
}
