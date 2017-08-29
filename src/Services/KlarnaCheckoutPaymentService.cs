using Klarna.Api;
using Klarna.Checkout;
using Motillo.Nop.Plugin.KlarnaCheckout.Data;
using Motillo.Nop.Plugin.KlarnaCheckout.Models;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Data;
using Nop.Services.Customers;
using Nop.Services.Logging;
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Nop.Core.Domain.Orders;
using Nop.Services.Orders;
using Order = Klarna.Checkout.Order;
using System.Collections.Generic;
using Nop.Services.Payments;

namespace Motillo.Nop.Plugin.KlarnaCheckout.Services
{
    public class KlarnaCheckoutPaymentService : IKlarnaCheckoutPaymentService
    {
        private readonly IStoreContext _storeContext;
        private readonly IWorkContext _workContext;
        private readonly IWebHelper _webHelper;
        private readonly IRepository<KlarnaCheckoutEntity> _klarnaRepository;
        private readonly IKlarnaCheckoutHelper _klarnaCheckoutUtils;
        private readonly ICustomerService _customerService;
        private readonly ILogger _logger;
        private readonly KlarnaCheckoutSettings _klarnaSettings;
        private readonly IOrderService _orderService;
        private string token;

        public KlarnaCheckoutPaymentService(
            IStoreContext storeContext,
            IWorkContext workContext,
            IWebHelper webHelper,
            IRepository<KlarnaCheckoutEntity> klarnaRepository,
            IKlarnaCheckoutHelper klarnaCheckoutUtils,
            ICustomerService customerService,
            ILogger logger,
            KlarnaCheckoutSettings klarnaSettings,
            IOrderService orderService)
        {
            _storeContext = storeContext;
            _workContext = workContext;
            _webHelper = webHelper;
            _klarnaRepository = klarnaRepository;
            _klarnaCheckoutUtils = klarnaCheckoutUtils;
            _customerService = customerService;
            _logger = logger;
            _klarnaSettings = klarnaSettings;
            _orderService = orderService;
        }

        private Uri BaseUri
        {
            get
            {
                return _klarnaSettings.TestMode ? Connector.TestBaseUri : Connector.BaseUri;
            }
        }

        public void Acknowledge(global::Nop.Core.Domain.Orders.Order order)
        {
            try
            {
                var entity = _klarnaRepository.Table.First(x => x.OrderGuid == order.OrderGuid);
                var resourceUri = new Uri(entity.KlarnaResourceUri);
                var apiOrder = Fetch(resourceUri);
                var klarnaOrder = KlarnaCheckoutOrder.FromApiOrder(apiOrder);

                if (klarnaOrder.Status == KlarnaCheckoutOrder.StatusCheckoutComplete)
                {
                    var updateData = new KlarnaCheckoutOrder
                    {
                        Status = KlarnaCheckoutOrder.StatusCreated,
                        MerchantReference = new MerchantReference
                        {
                            OrderId1 = order.Id.ToString(CultureInfo.InvariantCulture),
                            OrderId2 = order.OrderGuid.ToString()
                        }
                    };

                    var dictData = updateData.ToDictionary();

                    apiOrder.Update(dictData);

                    order.AuthorizationTransactionId = klarnaOrder.Reservation;
                    order.SubscriptionTransactionId = klarnaOrder.RecurringToken;

                    _orderService.UpdateOrder(order);
                }
            }
            catch (Exception ex)
            {
                throw new KlarnaCheckoutException("Error acknowledging Klarna order. Order Id: " + order.Id, ex);
            }
        }

        public Uri Create()
        {
            var cart = _klarnaCheckoutUtils.GetCart();
            var merchant = _klarnaCheckoutUtils.GetMerchant();
            var supportedLocale = _klarnaCheckoutUtils.GetSupportedLocale();
            var gui = _klarnaCheckoutUtils.GetGui();
            var options = _klarnaCheckoutUtils.GetOptions();
            var shippingAddress = _klarnaCheckoutUtils.ConvertAddress();

            var klarnaOrder = new KlarnaCheckoutOrder
            {
                Cart = cart,
                Merchant = merchant,
                Gui = gui,
                Options = options,
                Recurring = _klarnaCheckoutUtils.IsRecurringShoppingCart(),
                ShippingAddress = shippingAddress,
                Locale = supportedLocale.Locale,
                PurchaseCountry = supportedLocale.PurchaseCountry,
                PurchaseCurrency = supportedLocale.PurchaseCurrency
            };

            var dictData = klarnaOrder.ToDictionary();
            var connector = Connector.Create(_klarnaSettings.SharedSecret, BaseUri);
            var order = new Klarna.Checkout.Order(connector);

            order.Create(dictData);

            var location = order.Location;

            var kcoOrderRequest = GetKcoOrderRequest(_workContext.CurrentCustomer, location);
            _klarnaRepository.Insert(kcoOrderRequest);

            return location;
        }

        public RecurringOrder CreateRecurring(global::Nop.Core.Domain.Orders.Order newOrder)
        {
            var customValues = newOrder.DeserializeCustomValues();
            var cart = _klarnaCheckoutUtils.GetCartFromOrder(newOrder);
            var merchant = _klarnaCheckoutUtils.GetMerchant();
            var supportedLocale = _klarnaCheckoutUtils.GetSupportedLocale(newOrder.ShippingAddress, newOrder.CustomerCurrencyCode);
            var shippingAddress = _klarnaCheckoutUtils.ConvertAddress(newOrder.ShippingAddress);
            var billingAddress = _klarnaCheckoutUtils.ConvertAddress(newOrder.BillingAddress);
            var connector = Connector.Create(_klarnaSettings.SharedSecret, BaseUri);
            var order = new RecurringOrder(connector, newOrder.SubscriptionTransactionId); // (string)customValues["recurring_token"]);

            var klarnaOrder = new KlarnaCheckoutOrder
            {
                Cart = cart,
                Merchant = merchant,
                MerchantReference = new MerchantReference
                {
                    OrderId1 = newOrder.Id.ToString(CultureInfo.InvariantCulture),
                    OrderId2 = newOrder.OrderGuid.ToString()
                },
                ShippingAddress = shippingAddress,
                BillingAddress = billingAddress,
                Locale = supportedLocale.Locale,
                PurchaseCountry = supportedLocale.PurchaseCountry,
                PurchaseCurrency = supportedLocale.PurchaseCurrency
            };

            var dictData = klarnaOrder.ToDictionary();

            order.Create(dictData);

            return order;
        }

        private KlarnaCheckoutEntity GetKcoOrderRequest(global::Nop.Core.Domain.Customers.Customer currentCustomer, Uri resourceUri)
        {
            return new KlarnaCheckoutEntity
            {
                CustomerId = currentCustomer.Id,
                OrderGuid = Guid.NewGuid(),
                KlarnaResourceUri = resourceUri.ToString(),
                Status = KlarnaCheckoutStatus.Pending,
                IpAddress = _webHelper.GetCurrentIpAddress(),
                AffiliateId = currentCustomer.AffiliateId,
                StoreId = _storeContext.CurrentStore.Id,
                CreatedOnUtc = DateTime.UtcNow
            };
        }

        public Order Fetch(Uri resourceUri)
        {
            try
            {
                var klarnaOrderId = _klarnaCheckoutUtils.GetOrderIdFromUri(resourceUri);
                var connector = Connector.Create(_klarnaSettings.SharedSecret, BaseUri);
                var order = new Klarna.Checkout.Order(connector, klarnaOrderId);

                order.Fetch();

                return order;
            }
            catch (Exception ex)
            {
                throw new KlarnaCheckoutException("Error fetching klarna order: " + resourceUri, ex);
            }
        }

        public bool Update(Uri resourceUri)
        {
            try
            {
                var klarnaOrderId = _klarnaCheckoutUtils.GetOrderIdFromUri(resourceUri);
                var cart = _klarnaCheckoutUtils.GetCart();
                var options = _klarnaCheckoutUtils.GetOptions();
                var connector = Connector.Create(_klarnaSettings.SharedSecret, BaseUri);
                var supportedLocale = _klarnaCheckoutUtils.GetSupportedLocale();

                var klarnaOrder = new KlarnaCheckoutOrder
                {
                    Cart = cart,
                    Options = options,
                    Locale = supportedLocale.Locale,
                    PurchaseCountry = supportedLocale.PurchaseCountry,
                    PurchaseCurrency = supportedLocale.PurchaseCurrency
                };

                var order = new Order(connector, klarnaOrderId);
                var dictData = klarnaOrder.ToDictionary();

                order.Update(dictData);

                return true;
            }
            catch (Exception ex)
            {
                var exceptionJson = JsonConvert.SerializeObject(ex.Data);

                _logger.Warning(string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: Error updating Klarna order. Will try to create a new one. ResourceURI: {0}, Data: {1}",
                    resourceUri, exceptionJson), exception: ex);
            }

            return false;
        }

        public void SyncBillingAndShippingAddress(global::Nop.Core.Domain.Customers.Customer customer, KlarnaCheckoutOrder klarnaCheckoutOrder)
        {
            try
            {
                var billingAddress = klarnaCheckoutOrder.BillingAddress;
                var shippingAddress = klarnaCheckoutOrder.ShippingAddress;

                var nopBillingAddress = customer.Addresses.FirstOrDefault(billingAddress.RepresentsAddress);
                if (nopBillingAddress == null)
                {
                    nopBillingAddress = new global::Nop.Core.Domain.Common.Address { CreatedOnUtc = DateTime.UtcNow };
                    customer.Addresses.Add(nopBillingAddress);
                }

                customer.BillingAddress = nopBillingAddress;
                billingAddress.CopyTo(nopBillingAddress);

                var nopShippingAddress = customer.Addresses.FirstOrDefault(shippingAddress.RepresentsAddress);
                if (nopShippingAddress == null)
                {
                    nopShippingAddress = new global::Nop.Core.Domain.Common.Address { CreatedOnUtc = DateTime.UtcNow };
                    customer.Addresses.Add(nopShippingAddress);
                }

                customer.ShippingAddress = nopShippingAddress;
                shippingAddress.CopyTo(nopShippingAddress);

                _customerService.UpdateCustomer(customer);
            }
            catch (Exception ex)
            {
                var billing = JsonConvert.SerializeObject(klarnaCheckoutOrder.BillingAddress);
                var shipping = JsonConvert.SerializeObject(klarnaCheckoutOrder.ShippingAddress);
                throw new KlarnaCheckoutException(string.Format(CultureInfo.CurrentCulture, "Error syncing addresses. Billing: {0}, Shipping: {1}", billing, shipping), ex);
            }
        }

        public void CancelPayment(string reservation, global::Nop.Core.Domain.Customers.Customer customer)
        {
            try
            {
                var configuration = new Configuration(Country.Code.SE, Language.Code.SV, Currency.Code.SEK, Encoding.Sweden)
                {
                    Eid = _klarnaSettings.EId,
                    Secret = _klarnaSettings.SharedSecret,
                    IsLiveMode = !_klarnaSettings.TestMode
                };

                var api = new Api(configuration);
                api.CancelReservation(reservation);

                _logger.Information("KlarnaCheckout: Reservation cancelled: " + reservation, customer: customer);
            }
            catch (Exception ex)
            {
                _logger.Error("KlarnaCheckout: Error cancelling reservation: " + reservation, exception: ex, customer: customer);
            }
        }

        public void FullRefund(global::Nop.Core.Domain.Orders.Order order)
        {
            try
            {
                var configuration = new Klarna.Api.Configuration(Country.Code.SE,
                Language.Code.SV, Currency.Code.SEK, Encoding.Sweden)
                {
                    Eid = _klarnaSettings.EId,
                    Secret = _klarnaSettings.SharedSecret,
                    IsLiveMode = !_klarnaSettings.TestMode
                };

                var api = new Api(configuration);
                var result = api.CreditInvoice(order.CaptureTransactionId);

                _logger.Information(string.Format(CultureInfo.InvariantCulture, "KlarnaCheckout: Order refunded. InvoiceNumber: {0}, AppliedTo: {1}",
                    result, result), customer: order.Customer);

                order.OrderNotes.Add(new OrderNote
                {
                    Note = string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: Order has been refunded. Invoice Number: {0}; Refund applied to: {1}",
                        order.CaptureTransactionId, result),
                    CreatedOnUtc = DateTime.UtcNow,
                    DisplayToCustomer = false
                });
                _orderService.UpdateOrder(order);
            }
            catch (Exception ex)
            {
                throw new KlarnaCheckoutException("An error occurred when refunding order " + order.Id, ex);
            }
        }

        public ActivateReservationResponse Activate(global::Nop.Core.Domain.Orders.Order order)
        {
            var reservation = order.AuthorizationTransactionId;

            if (!string.IsNullOrEmpty(order.CaptureTransactionId))
            {
                throw new KlarnaCheckoutException("The payment has already been captured. Order Id: " + order.Id);
            }

            try
            {
                var configuration = new Configuration(Country.Code.SE, Language.Code.SV, Currency.Code.SEK, Encoding.Sweden)
                {
                    Eid = _klarnaSettings.EId,
                    Secret = _klarnaSettings.SharedSecret,
                    IsLiveMode = !_klarnaSettings.TestMode
                };

                var api = new Api(configuration);
                return api.Activate(reservation);
            }
            catch (Exception ex)
            {
                throw new KlarnaCheckoutException("Error activating Klarna reservation for order " + order.Id, ex);
            }
        }
    }
}
