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

namespace Motillo.Nop.Plugin.KlarnaCheckout.Services
{
    public class KlarnaCheckoutPaymentService : IKlarnaCheckoutPaymentService
    {
        private readonly Regex _klarnaIdRegex = new Regex(@"/([^/]+)$");

        private readonly IStoreContext _storeContext;
        private readonly IWorkContext _workContext;
        private readonly IWebHelper _webHelper;
        private readonly IRepository<KlarnaCheckoutEntity> _klarnaRepository;
        private readonly IKlarnaCheckoutHelper _klarnaCheckoutUtils;
        private readonly ICustomerService _customerService;
        private readonly ILogger _logger;
        private readonly KlarnaCheckoutSettings _klarnaSettings;
        private readonly IOrderService _orderService;

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

        private string GetKlarnaOrderId(Uri resourceUri)
        {
            if (resourceUri == null) throw new ArgumentNullException(nameof(resourceUri));

            var match = _klarnaIdRegex.Match(resourceUri.ToString());
            if (!match.Success)
            {
                throw new InvalidOperationException("Could not extract id from url: " + resourceUri);
            }

            return match.Groups[1].Value;
        }

        private Uri BaseUri
        {
            get
            {
                return _klarnaSettings.TestMode ? Connector.TestBaseUri : Connector.BaseUri;
            }
        }

        public void Acknowledge(Uri resourceUri, global::Nop.Core.Domain.Orders.Order order)
        {
            try
            {
                var klarnaOrderId = GetKlarnaOrderId(resourceUri);
                var connector = Connector.Create(_klarnaSettings.SharedSecret, BaseUri);
                var klarnaOrder = new Klarna.Checkout.Order(connector, klarnaOrderId);

                klarnaOrder.Fetch();
                var fetchedData = klarnaOrder.Marshal();
                var typedData = KlarnaOrder.FromDictionary(fetchedData);

                if (typedData.Status == KlarnaOrder.StatusCheckoutComplete)
                {
                    var updateData = new KlarnaOrder
                    {
                        Status = KlarnaOrder.StatusCreated,
                        MerchantReference = new MerchantReference
                        {
                            OrderId1 = order.Id.ToString(CultureInfo.InvariantCulture),
                            OrderId2 = order.OrderGuid.ToString()
                        }
                    };

                    var dictData = updateData.ToDictionary();

                    klarnaOrder.Update(dictData);
                }
            }
            catch (Exception ex)
            {
                throw new NopException("Error Acknowledging Klarna Order", ex);
            }
        }

        public Uri Create()
        {
            var cart = _klarnaCheckoutUtils.GetCart();
            var merchant = _klarnaCheckoutUtils.GetMerchant();
            var supportedLocale = _klarnaCheckoutUtils.GetSupportedLocale();
            var gui = _klarnaCheckoutUtils.GetGui();
            var options = _klarnaCheckoutUtils.GetOptions();
            var shippingAddress = _klarnaCheckoutUtils.GetShippingAddress();

            var klarnaOrder = new KlarnaOrder
            {
                Cart = cart,
                Merchant = merchant,
                Gui = gui,
                Options = options,
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
            var klarnaOrderId = GetKlarnaOrderId(resourceUri);
            var connector = Connector.Create(_klarnaSettings.SharedSecret, BaseUri);
            var order = new Klarna.Checkout.Order(connector, klarnaOrderId);

            order.Fetch();

            return order;
        }

        public bool Update(Uri resourceUri)
        {
            try
            {
                var klarnaOrderId = GetKlarnaOrderId(resourceUri);
                var cart = _klarnaCheckoutUtils.GetCart();
                var options = _klarnaCheckoutUtils.GetOptions();
                var connector = Connector.Create(_klarnaSettings.SharedSecret, BaseUri);
                var supportedLocale = _klarnaCheckoutUtils.GetSupportedLocale();

                var klarnaOrder = new KlarnaOrder
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

        public void SyncBillingAndShippingAddress(global::Nop.Core.Domain.Customers.Customer customer, KlarnaOrder klarnaOrder)
        {
            try
            {
                var billingAddress = klarnaOrder.BillingAddress;
                var shippingAddress = klarnaOrder.ShippingAddress;

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
                var billing = JsonConvert.SerializeObject(klarnaOrder.BillingAddress);
                var shipping = JsonConvert.SerializeObject(klarnaOrder.ShippingAddress);
                throw new KlarnaCheckoutException(string.Format(CultureInfo.CurrentCulture, "Error syncing addresses. Billing: {0}, Shipping: {1}", billing, shipping), ex);
            }
        }

        public bool CancelPayment(string reservation, global::Nop.Core.Domain.Customers.Customer customer)
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
                var cancelled = api.CancelReservation(reservation);

                if (cancelled)
                {
                    _logger.Warning("KlarnaCheckout: Reservation cancelled: " + reservation, customer: customer);
                }

                return cancelled;
            }
            catch (Exception ex)
            {
                _logger.Error("KlarnaCheckout: Error cancelling reservation: " + reservation, exception: ex, customer: customer);
            }

            return false;
        }

        public string FullRefund(global::Nop.Core.Domain.Orders.Order order)
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

            return result;
        }

        public bool Capture(global::Nop.Core.Domain.Orders.Order order)
        {
            var reservation = order.AuthorizationTransactionId;

            try
            {
                var configuration = new Configuration(Country.Code.SE, Language.Code.SV, Currency.Code.SEK, Encoding.Sweden)
                {
                    Eid = _klarnaSettings.EId,
                    Secret = _klarnaSettings.SharedSecret,
                    IsLiveMode = !_klarnaSettings.TestMode
                };

                var api = new Api(configuration);
                var activationResult = api.Activate(reservation);

                order.OrderNotes.Add(new OrderNote
                {
                    Note = string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: Klarna order has been captured. Reservation: {0}, RiskStatus: {1}, InvoiceNumber: {2}",
                    reservation, activationResult.RiskStatus, activationResult.InvoiceNumber),
                    CreatedOnUtc = DateTime.UtcNow,
                    DisplayToCustomer = false
                });
                order.CaptureTransactionId = activationResult.InvoiceNumber;
                _orderService.UpdateOrder(order);

                var klarnaRequest = _klarnaRepository.Table.FirstOrDefault(x => x.OrderGuid == order.OrderGuid);
                if (klarnaRequest != null)
                {
                    klarnaRequest.Status = KlarnaCheckoutStatus.Activated;
                    _klarnaRepository.Update(klarnaRequest);
                }

                return true;
            }
            catch (Exception ex)
            {
                order.OrderNotes.Add(new OrderNote
                {
                    Note = "KlarnaCheckout: An error occurred when the payment was being captured. See the error log for more information.",
                    CreatedOnUtc = DateTime.UtcNow,
                    DisplayToCustomer = false
                });
                _orderService.UpdateOrder(order);

                _logger.Error("KlarnaCheckout: Error activating reservation: " + reservation, exception: ex, customer: order.Customer);
            }

            return false;
        }
    }
}
