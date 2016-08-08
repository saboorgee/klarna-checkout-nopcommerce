using Motillo.Nop.Plugin.KlarnaCheckout.Data;
using Motillo.Nop.Plugin.KlarnaCheckout.Models;
using Motillo.Nop.Plugin.KlarnaCheckout.Services;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Configuration;
using Nop.Core.Data;
using Nop.Core.Domain.Orders;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Stores;
using Nop.Web.Framework.Controllers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Web.Mvc;
using Nop.Core.Domain.Shipping;
using Customer = Nop.Core.Domain.Customers.Customer;
using Order = Nop.Core.Domain.Orders.Order;
using Nop.Core.Domain.Customers;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Shipping;
using Nop.Services.Orders;

namespace Motillo.Nop.Plugin.KlarnaCheckout.Controllers
{
    public class KlarnaCheckoutController : BasePaymentController
    {
        private readonly IWorkContext _workContext;
        private readonly ISettingService _settingService;
        private readonly OrderSettings _orderSettings;
        private readonly IRepository<KlarnaCheckoutEntity> _repository;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IStoreContext _storeContext;
        private readonly ILogger _logger;
        private readonly ICustomerService _customerService;
        private readonly ICurrencyService _currencyService;
        private readonly IStoreService _storeService;
        private readonly ILocalizationService _localizationService;
        private readonly IKlarnaCheckoutHelper _klarnaCheckoutHelper;
        private readonly IKlarnaCheckoutPaymentService _klarnaCheckoutPaymentService;
        private readonly IPaymentService _paymentService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IProductService _productService;
        private readonly IShippingService _shippingService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IGiftCardService _giftCardService;
        private readonly ICheckoutAttributeParser _checkoutAttributeParser;
        private readonly ICheckoutAttributeService _checkoutAttributeService;

        // Used so that the push and thank you actions don't race. For the Google Analytics widget to work, the ThankYou action
        // needs to redirect to checkout/completed.
        // This dictionary can grow indefinately. The stored objects don't take up much space, but better solution is welcome.
        private static readonly ConcurrentDictionary<string, object> Locker = new ConcurrentDictionary<string, object>();

        public KlarnaCheckoutController(
            IWorkContext workContext,
            ISettingService settingService,
            OrderSettings orderSettings,
            IRepository<KlarnaCheckoutEntity> repository,
            IOrderService orderService,
            IOrderProcessingService orderProcessingService,
            IStoreContext storeContext,
            ILogger logger,
            ICustomerService customerService,
            ICurrencyService currencyService,
            IStoreService storeService,
            ILocalizationService localizationService,
            IKlarnaCheckoutHelper klarnaCheckoutHelper,
            IKlarnaCheckoutPaymentService klarnaHelper,
            IPaymentService paymentService,
            IShoppingCartService shoppingCartService,
            IProductService productService,
            IShippingService shippingService,
            IGenericAttributeService genericAttributeService,
            IGiftCardService giftCardService, ICheckoutAttributeParser checkoutAttributeParser, ICheckoutAttributeService checkoutAttributeService)
        {
            _workContext = workContext;
            _settingService = settingService;
            _orderSettings = orderSettings;
            _repository = repository;
            _orderService = orderService;
            _orderProcessingService = orderProcessingService;
            _storeContext = storeContext;
            _logger = logger;
            _customerService = customerService;
            _currencyService = currencyService;
            _storeService = storeService;
            _localizationService = localizationService;
            _klarnaCheckoutHelper = klarnaCheckoutHelper;
            _klarnaCheckoutPaymentService = klarnaHelper;
            _paymentService = paymentService;
            _shoppingCartService = shoppingCartService;
            _productService = productService;
            _shippingService = shippingService;
            _genericAttributeService = genericAttributeService;
            _giftCardService = giftCardService;
            _checkoutAttributeParser = checkoutAttributeParser;
            _checkoutAttributeService = checkoutAttributeService;
        }

        public override IList<string> ValidatePaymentForm(System.Web.Mvc.FormCollection form)
        {
            var warnings = new List<string>();
            return warnings;
        }

        public override global::Nop.Services.Payments.ProcessPaymentRequest GetPaymentInfo(System.Web.Mvc.FormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();
            return paymentInfo;
        }

        [HttpGet]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var klarnaCheckoutSettings = _settingService.LoadSetting<KlarnaCheckoutSettings>(storeScope);
            var model = new ConfigurationModel
            {
                ActiveStoreScopeConfiguration = storeScope,
                EId = klarnaCheckoutSettings.EId,
                SharedSecret = klarnaCheckoutSettings.SharedSecret,
                EnabledCountries = klarnaCheckoutSettings.EnabledCountries,
                CheckoutUrl = klarnaCheckoutSettings.CheckoutUrl,
                TermsUrl = klarnaCheckoutSettings.TermsUrl,
                DisableAutofocus = klarnaCheckoutSettings.DisableAutofocus,
                AllowSeparateShippingAddress = klarnaCheckoutSettings.AllowSeparateShippingAddress,
                TestMode = klarnaCheckoutSettings.TestMode,
                ColorButton = klarnaCheckoutSettings.ColorButton,
                ColorButtonText = klarnaCheckoutSettings.ColorButtonText,
                ColorCheckbox = klarnaCheckoutSettings.ColorCheckbox,
                ColorCheckboxCheckmark = klarnaCheckoutSettings.ColorCheckboxCheckmark,
                ColorHeader = klarnaCheckoutSettings.ColorHeader,
                ColorLink = klarnaCheckoutSettings.ColorLink
            };

            if (storeScope > 0)
            {
                model.EId_OverrideForStore = _settingService.SettingExists(klarnaCheckoutSettings, x => x.EId, storeScope);
                model.SharedSecret_OverrideForStore = _settingService.SettingExists(klarnaCheckoutSettings, x => x.SharedSecret, storeScope);
                model.EnabledCountries_OverrideForStore = _settingService.SettingExists(klarnaCheckoutSettings, x => x.EnabledCountries, storeScope);
                model.CheckoutUrl_OverrideForStore = _settingService.SettingExists(klarnaCheckoutSettings, x => x.CheckoutUrl, storeScope);
                model.TermsUrl_OverrideForStore = _settingService.SettingExists(klarnaCheckoutSettings, x => x.TermsUrl, storeScope);
                model.DisableAutofocus_OverrideForStore = _settingService.SettingExists(klarnaCheckoutSettings, x => x.DisableAutofocus, storeScope);
                model.AllowSeparateShippingAddress_OverrideForStore = _settingService.SettingExists(klarnaCheckoutSettings, x => x.AllowSeparateShippingAddress, storeScope);
                model.TestMode_OverrideForStore = _settingService.SettingExists(klarnaCheckoutSettings, x => x.TestMode, storeScope);
                model.ColorButton_OverrideForStore = _settingService.SettingExists(klarnaCheckoutSettings, x => x.ColorButton, storeScope);
                model.ColorButtonText_OverrideForStore = _settingService.SettingExists(klarnaCheckoutSettings, x => x.ColorButtonText, storeScope);
                model.ColorCheckbox_OverrideForStore = _settingService.SettingExists(klarnaCheckoutSettings, x => x.ColorCheckbox, storeScope);
                model.ColorCheckboxCheckmark_OverrideForStore = _settingService.SettingExists(klarnaCheckoutSettings, x => x.ColorCheckboxCheckmark, storeScope);
                model.ColorHeader_OverrideForStore = _settingService.SettingExists(klarnaCheckoutSettings, x => x.ColorHeader, storeScope);
                model.ColorLink_OverrideForStore = _settingService.SettingExists(klarnaCheckoutSettings, x => x.ColorLink, storeScope);
            }

            return View("~/Plugins/Motillo.KlarnaCheckout/Views/KlarnaCheckout/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("~/Plugins/Motillo.KlarnaCheckout/Views/KlarnaCheckout/Configure.cshtml", model);
            }

            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var klarnaSettings = _settingService.LoadSetting<KlarnaCheckoutSettings>(storeScope);

            klarnaSettings.EId = model.EId;
            klarnaSettings.SharedSecret = model.SharedSecret;
            klarnaSettings.EnabledCountries = (model.EnabledCountries ?? string.Empty).ToUpperInvariant();
            klarnaSettings.TermsUrl = model.TermsUrl;
            klarnaSettings.CheckoutUrl = model.CheckoutUrl;
            klarnaSettings.DisableAutofocus = model.DisableAutofocus;
            klarnaSettings.AllowSeparateShippingAddress = model.AllowSeparateShippingAddress;
            klarnaSettings.TestMode = model.TestMode;
            klarnaSettings.ColorButton = model.ColorButton;
            klarnaSettings.ColorButtonText = model.ColorButtonText;
            klarnaSettings.ColorCheckbox = model.ColorCheckbox;
            klarnaSettings.ColorCheckboxCheckmark = model.ColorCheckboxCheckmark;
            klarnaSettings.ColorHeader = model.ColorHeader;
            klarnaSettings.ColorLink = model.ColorLink;

            // Update settings based on eventual store overrides.
            SaveSettingValue(klarnaSettings, storeScope, model.EId_OverrideForStore, x => x.EId);
            SaveSettingValue(klarnaSettings, storeScope, model.SharedSecret_OverrideForStore, x => x.SharedSecret);
            SaveSettingValue(klarnaSettings, storeScope, model.EnabledCountries_OverrideForStore, x => x.EnabledCountries);
            SaveSettingValue(klarnaSettings, storeScope, model.TermsUrl_OverrideForStore, x => x.TermsUrl);
            SaveSettingValue(klarnaSettings, storeScope, model.CheckoutUrl_OverrideForStore, x => x.CheckoutUrl);
            SaveSettingValue(klarnaSettings, storeScope, model.DisableAutofocus_OverrideForStore, x => x.DisableAutofocus);
            SaveSettingValue(klarnaSettings, storeScope, model.AllowSeparateShippingAddress_OverrideForStore, x => x.AllowSeparateShippingAddress);
            SaveSettingValue(klarnaSettings, storeScope, model.TestMode_OverrideForStore, x => x.TestMode);
            SaveSettingValue(klarnaSettings, storeScope, model.ColorButton_OverrideForStore, x => x.ColorButton);
            SaveSettingValue(klarnaSettings, storeScope, model.ColorButtonText_OverrideForStore, x => x.ColorButtonText);
            SaveSettingValue(klarnaSettings, storeScope, model.ColorCheckbox_OverrideForStore, x => x.ColorCheckbox);
            SaveSettingValue(klarnaSettings, storeScope, model.ColorCheckboxCheckmark_OverrideForStore, x => x.ColorCheckboxCheckmark);
            SaveSettingValue(klarnaSettings, storeScope, model.ColorHeader_OverrideForStore, x => x.ColorHeader);
            SaveSettingValue(klarnaSettings, storeScope, model.ColorLink_OverrideForStore, x => x.ColorLink);

            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        private void SaveSettingValue<T, TPropType>(T settings, int storeScope, bool overrideEnabled, Expression<Func<T, TPropType>> keySelector) where T : ISettings, new()
        {
            if (overrideEnabled || storeScope == 0)
            {
                _settingService.SaveSetting(settings, keySelector, storeId: storeScope, clearCache: false);
            }
            else if (storeScope > 0)
            {
                _settingService.DeleteSetting(settings, keySelector, storeId: storeScope);
            }
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            Uri resourceUri;
            var customer = _workContext.CurrentCustomer;
            var storeId = _storeContext.CurrentStore.Id;
            var payment = _repository.Table
                .OrderByDescending(x => x.CreatedOnUtc)
                .FirstOrDefault(x => x.CustomerId == customer.Id && x.StoreId == storeId && x.Status == KlarnaCheckoutStatus.Pending);

            if (payment == null)
            {
                try
                {
                    resourceUri = _klarnaCheckoutPaymentService.Create();
                }
                catch (Exception ex)
                {
                    var exceptionJson = JsonConvert.SerializeObject(ex.Data);

                    _logger.Error(string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: Error creating Klarna order. Data: {0}", exceptionJson),
                        exception: ex, customer: customer);
                    ViewBag.StatusCode = ex.Data["http_status_code"];

                    return View("~/Plugins/Motillo.KlarnaCheckout/Views/KlarnaCheckout/PaymentInfoError.cshtml");
                }
            }
            else
            {
                try
                {
                    resourceUri = new Uri(payment.KlarnaResourceUri);

                    // If update of old Klarna order failed, try creating a new one.
                    // Failure can occur for old orders or when toggling between live/test mode.
                    if (!_klarnaCheckoutPaymentService.Update(resourceUri))
                    {
                        payment.Status = KlarnaCheckoutStatus.Failed;
                        _repository.Update(payment);

                        resourceUri = _klarnaCheckoutPaymentService.Create();
                    }
                }
                catch (Exception ex)
                {
                    var exceptionJson = JsonConvert.SerializeObject(ex.Data);

                    _logger.Error(string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: Error updating Klarna order. ResourceURI: {0}, Data: {1}",
                        payment.KlarnaResourceUri, exceptionJson), exception: ex, customer: customer);

                    ViewBag.StatusCode = ex.Data["http_status_code"];

                    return View("~/Plugins/Motillo.KlarnaCheckout/Views/KlarnaCheckout/PaymentInfoError.cshtml");
                }
            }

            var apiOrder = _klarnaCheckoutPaymentService.Fetch(resourceUri);
            var klarnaOrder = KlarnaCheckoutOrder.FromApiOrder(apiOrder);

            var model = new PaymentInfoModel
            {
                SnippetHtml = klarnaOrder.Gui.Snippet
            };

            return View("~/Plugins/Motillo.KlarnaCheckout/Views/KlarnaCheckout/PaymentInfo.cshtml", model);
        }

        [HttpPost]
        public void KlarnaCheckoutPush(string sid, string klarna_order)
        {
            lock (Locker.GetOrAdd(klarna_order, new object()))
            {
                _logger.Information(string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: Push URI request. sid: {0}, uri: {1}",
                    sid, klarna_order));

                var klarnaRequest = _repository.Table
                    .OrderByDescending(x => x.CreatedOnUtc)
                    .FirstOrDefault(x => x.KlarnaResourceUri == klarna_order);

                if (klarnaRequest == null)
                {
                    _logger.Warning("KlarnaCheckout: Got push request for request not found. ResourceURI = " + klarna_order);
                    return;
                }

                var customer = _customerService.GetCustomerById(klarnaRequest.CustomerId);

                try
                {
                    Order nopOrder;
                    KlarnaCheckoutOrder klarnaCheckoutOrder;

                    SyncKlarnaAndNopOrder(klarnaRequest, customer, out nopOrder, out klarnaCheckoutOrder);
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: Error syncing klarna and nop order in Push. CustomerId: {0}, KlarnaId: {1}", customer.Id, klarnaRequest.Id), exception: ex);
                }
            }
        }

        public ActionResult ThankYou(string sid, string klarna_order)
        {
            lock (Locker.GetOrAdd(klarna_order, new object()))
            {
                _logger.Information(string.Format(CultureInfo.CurrentCulture,
                        "KlarnaCheckout: Thank you request. Sid: {0}, klarna_order: {1}",
                        sid, klarna_order));

                var klarnaRequest = _repository.Table
                    .OrderByDescending(x => x.CreatedOnUtc)
                    .FirstOrDefault(x => x.KlarnaResourceUri == klarna_order);

                if (klarnaRequest == null)
                {
                    _logger.Warning("KlarnaCheckout: Got thank you request for Klarna request not found. ResourceURI = " + klarna_order);
                    return RedirectToAction("Index", "Home");
                }

                if (_orderSettings.DisableOrderCompletedPage)
                {
                    return RedirectToAction("Index", "Home");
                }

                var customer = _customerService.GetCustomerById(klarnaRequest.CustomerId);
                KlarnaCheckoutOrder klarnaCheckoutOrder;
                Order nopOrder;

                try
                {
                    SyncKlarnaAndNopOrder(klarnaRequest, customer, out nopOrder, out klarnaCheckoutOrder);
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: Error syncing klarna and nop order in ThankYou. CustomerId: {0}, KlarnaId: {1}", customer.Id, klarnaRequest.Id), customer: customer, exception: ex);
                    return View("~/Plugins/Motillo.KlarnaCheckout/Views/KlarnaCheckout/ThankYouError.cshtml");
                }

                if (klarnaCheckoutOrder.Status == KlarnaCheckoutOrder.StatusCheckoutComplete || klarnaCheckoutOrder.Status == KlarnaCheckoutOrder.StatusCreated)
                {
                    TempData["KlarnaSnippet"] = klarnaCheckoutOrder.Gui.Snippet;
                    ViewBag.KlarnaSnippet = klarnaCheckoutOrder.Gui.Snippet;
                }

                return RedirectToRoute("CheckoutCompleted", new { orderId = nopOrder.Id });
            }
        }

        [HttpGet]
        [ChildActionOnly]
        public ActionResult ConfirmationSnippet(int orderId)
        {
            var order = _orderService.GetOrderById(orderId);

            if (order == null || order.PaymentMethodSystemName != KlarnaCheckoutProcessor.PaymentMethodSystemName)
            {
                return Content(string.Empty);
            }

            var klarnaRequest = _repository.Table.FirstOrDefault(x => x.OrderGuid == order.OrderGuid);
            if (klarnaRequest == null)
            {
                _logger.Warning(string.Format(CultureInfo.CurrentCulture,
                    "KlarnaCheckout: Didn't find entity for order payed with Klarna. Order Id: {0}", orderId));
                return Content(string.Empty);
            }

            try
            {
                var apiOrder = _klarnaCheckoutPaymentService.Fetch(new Uri(klarnaRequest.KlarnaResourceUri));
                var klarnaOrder = KlarnaCheckoutOrder.FromApiOrder(apiOrder);

                if (klarnaOrder.Status != KlarnaCheckoutOrder.StatusCheckoutComplete && klarnaOrder.Status != KlarnaCheckoutOrder.StatusCreated)
                {
                    _logger.Warning(string.Format(CultureInfo.CurrentCulture,
                        "KlarnaCheckout: Cannot show confirmation snippet for Klarna order that is not marked as complete or created. Order Id: {0}; Status: {1}; Resource URI: {2}",
                        orderId, klarnaOrder.Status, klarnaRequest.KlarnaResourceUri));
                    return Content(string.Empty);
                }

                return Content(klarnaOrder.Gui.Snippet);
            }
            catch (KlarnaCheckoutException kce)
            {
                _logger.Error(string.Format(CultureInfo.CurrentCulture,
                        "KlarnaCheckout: Error when fetching and getting Klarna confirmation snippet. Order Id: {0}; Resource URI: {1}",
                        orderId, klarnaRequest.KlarnaResourceUri),
                        exception: kce, customer: order.Customer);
            }

            return Content(string.Empty);
        }

        private void SyncKlarnaAndNopOrder(KlarnaCheckoutEntity klarnaRequest, Customer customer, out Order nopOrder, out KlarnaCheckoutOrder klarnaCheckoutOrder)
        {
            nopOrder = _orderService.GetOrderByGuid(klarnaRequest.OrderGuid);
            var resourceUri = new Uri(klarnaRequest.KlarnaResourceUri);
            var apiOrder = _klarnaCheckoutPaymentService.Fetch(resourceUri);

            klarnaCheckoutOrder = KlarnaCheckoutOrder.FromApiOrder(apiOrder);

            // Create the order if it doesn't exist in nop. According to the Klarna Checkout
            // developer guidelines, one should only create the order if the status is
            // 'checkout_complete'.
            // https://developers.klarna.com/en/klarna-checkout/acknowledge-an-order
            if (nopOrder == null && klarnaCheckoutOrder.Status == KlarnaCheckoutOrder.StatusCheckoutComplete)
            {
                if (klarnaCheckoutOrder.Status == KlarnaCheckoutOrder.StatusCheckoutComplete)
                {
                    klarnaRequest.Status = KlarnaCheckoutStatus.Complete;
                    _repository.Update(klarnaRequest);
                }

                _klarnaCheckoutPaymentService.SyncBillingAndShippingAddress(customer, klarnaCheckoutOrder);

                nopOrder = CreateOrderAndSyncWithKlarna(klarnaRequest, customer, klarnaCheckoutOrder, resourceUri);
            }
        }

        private Order CreateOrderAndSyncWithKlarna(KlarnaCheckoutEntity klarnaRequest, Customer customer, KlarnaCheckoutOrder klarnaCheckoutOrder, Uri resourceUri)
        {
            SyncCartWithKlarnaOrder(customer, klarnaCheckoutOrder);

            var processPaymentRequest = new ProcessPaymentRequest
            {
                OrderGuid = klarnaRequest.OrderGuid,
                CustomerId = customer.Id,
                StoreId = _storeContext.CurrentStore.Id,
                PaymentMethodSystemName = KlarnaCheckoutProcessor.PaymentMethodSystemName
            };

            var placeOrderResult = _orderProcessingService.PlaceOrder(processPaymentRequest);

            // If you tamper with the cart after the klarna widget is rendered nop fails to create the order.
            if (!placeOrderResult.Success)
            {
                var errors = string.Join("; ", placeOrderResult.Errors);
                _logger.Error(string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: Klarna has been processed but order could not be created in Nop! Klarna ID={0}, ResourceURI={1}. Errors: {2}",
                    klarnaCheckoutOrder.Id, klarnaRequest.KlarnaResourceUri, errors), customer: customer);

                _klarnaCheckoutPaymentService.CancelPayment(klarnaCheckoutOrder.Reservation, customer);

                throw new KlarnaCheckoutException("Error creating order: " + errors);
            }

            // Order was successfully created.
            var orderId = placeOrderResult.PlacedOrder.Id;
            var nopOrder = _orderService.GetOrderById(orderId);
            var klarnaPayment =
                _paymentService.LoadPaymentMethodBySystemName(KlarnaCheckoutProcessor.PaymentMethodSystemName);
            klarnaPayment.PostProcessPayment(new PostProcessPaymentRequest
            {
                Order = nopOrder
            });

            var orderTotalInCurrentCurrency = _currencyService.ConvertFromPrimaryStoreCurrency(nopOrder.OrderTotal, _workContext.WorkingCurrency);

            // Due to rounding when using prices contains more than 2 decimals (e.g. currency conversion), we allow
            // a slight diff in paid price and nop's reported price.
            // For example nop rounds the prices after _all_ cart item prices have been summed but when sending
            // items to Klarna, each price needs to be rounded separately (Klarna uses 2 decimals).

            // Assume a cart with two items.
            // 1.114 + 2.114 = 3.228 which nop rounds to 3.23.
            // 1.11 + 2.11 is sent to Klarna, which totals 3.22.

            var allowedPriceDiff = orderTotalInCurrentCurrency * 0.01m;
            var diff = Math.Abs(orderTotalInCurrentCurrency - (klarnaCheckoutOrder.Cart.TotalPriceIncludingTax.Value / 100m));

            if (diff >= allowedPriceDiff)
            {
                var orderTotalInCents = _klarnaCheckoutHelper.ConvertToCents(orderTotalInCurrentCurrency);

                nopOrder.OrderNotes.Add(new OrderNote
                {
                    Note = string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: Order total differs from Klarna order. OrderTotal: {0}, OrderTotalInCents: {1}, KlarnaTotal: {2}, AllowedDiff: {3}, Diff: {4}, Uri: {5}",
                        orderTotalInCurrentCurrency, orderTotalInCents, klarnaCheckoutOrder.Cart.TotalPriceIncludingTax, allowedPriceDiff, diff, resourceUri),
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });
            }

            nopOrder.OrderNotes.Add(new OrderNote
            {
                Note = "KlarnaCheckout: Order acknowledged. Uri: " + resourceUri,
                DisplayToCustomer = false,
                CreatedOnUtc = DateTime.UtcNow
            });
            _orderService.UpdateOrder(nopOrder);

            if (_orderProcessingService.CanMarkOrderAsAuthorized(nopOrder))
            {
                _orderProcessingService.MarkAsAuthorized(nopOrder);

                // Sometimes shipping isn't required, e.g. if only ordering virtual gift cards.
                // In those cases, make sure the Klarna order is activated.
                if (nopOrder.OrderStatus == OrderStatus.Complete || nopOrder.ShippingStatus == ShippingStatus.ShippingNotRequired)
                {
                    nopOrder.OrderNotes.Add(new OrderNote
                    {
                        Note = "KlarnaCheckout: Order complete after payment, will try to capture payment.",
                        CreatedOnUtc = DateTime.UtcNow,
                        DisplayToCustomer = false
                    });
                    _orderService.UpdateOrder(nopOrder);

                    if (_orderProcessingService.CanCapture(nopOrder))
                    {
                        _orderProcessingService.Capture(nopOrder);
                    }
                }
            }

            return nopOrder;
        }

        private void SyncCartWithKlarnaOrder(Customer customer, KlarnaCheckoutOrder klarnaCheckoutOrder)
        {
            var currentStoreId = _storeContext.CurrentStore.Id;
            var couponCodePrefix = string.Format(CultureInfo.CurrentUICulture, "{0}: ", _localizationService.GetResource("shoppingcart.discountcouponcode"));

            var orderCurrency = _currencyService.GetCurrencyByCode(klarnaCheckoutOrder.PurchaseCurrency);
            if (orderCurrency != null)
                _workContext.WorkingCurrency = orderCurrency;

            ClearItemsInCart(customer, currentStoreId);
            ClearDiscountsAndShippingSelection(customer, currentStoreId);

            var physicalItems = klarnaCheckoutOrder.Cart.Items.Where(x => x.Type == CartItem.TypePhysical);
            var appliedCoupons = klarnaCheckoutOrder.Cart.Items.Where(x => x.Type == CartItem.TypeDiscount && x.HasCouponCode());
            var appliedGiftCards = klarnaCheckoutOrder.Cart.Items.Where(x => x.Type == CartItem.TypeDiscount && x.IsDiscountGiftCardCartItem());
            var appliedRewardPoints = klarnaCheckoutOrder.Cart.Items.Where(x => x.Type == CartItem.TypeDiscount && x.IsRewardPointsCartItem());
            var checkoutAttributes = klarnaCheckoutOrder.Cart.Items.Where(x => x.Type == CartItem.TypeDiscount && x.IsCheckoutAttribtue());
            var shippingItems = klarnaCheckoutOrder.Cart.Items.Where(x => x.Type == CartItem.TypeShippingFee);

            foreach (var physicalItem in physicalItems)
            {
                AddPhysicalItemToCart(customer, physicalItem, currentStoreId);
            }

            foreach (var coupon in appliedCoupons)
            {
                var couponCode = coupon.Name.Substring(couponCodePrefix.Length);
                _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.DiscountCouponCode, couponCode);
            }

            foreach (var giftCardCartItem in appliedGiftCards)
            {
                var giftCardId = giftCardCartItem.GetGiftCardIdFromDiscountGiftCardCartItem();
                var giftCard = _giftCardService.GetGiftCardById(giftCardId);
                customer.ApplyGiftCardCouponCode(giftCard.GiftCardCouponCode);
            }

            if (appliedRewardPoints.Any())
            {
                _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.UseRewardPointsDuringCheckout, true, currentStoreId);
            }

            var checkoutAttributesXml = GetSelectedCheckoutAttributesThatHasNotBeenSentToKlarna(customer, currentStoreId);
            foreach (var item in checkoutAttributes)
            {
                var ca = _checkoutAttributeService.GetCheckoutAttributeById(item.GetCheckoutAttributeId());
                checkoutAttributesXml = _checkoutAttributeParser.AddCheckoutAttribute(checkoutAttributesXml, ca, item.GetCheckoutAttributeValue());
            }
            _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.CheckoutAttributes, checkoutAttributesXml, currentStoreId);

            foreach (var shippingItem in shippingItems)
            {
                AddShippingItemToCart(customer, shippingItem, currentStoreId);
            }
        }

        private string GetSelectedCheckoutAttributesThatHasNotBeenSentToKlarna(Customer customer, int currentStoreId)
        {
            string checkoutAttributesXml = null;

            var originalCheckoutAttributesXml = customer.GetAttribute<string>(SystemCustomerAttributeNames.CheckoutAttributes, currentStoreId);
            var allCheckoutAttributes = _checkoutAttributeService.GetAllCheckoutAttributes(currentStoreId);

            foreach (var checkoutAttribute in allCheckoutAttributes.Where(x => !x.ShouldHaveValues()))
            {
                var selectedValues = _checkoutAttributeParser.ParseValues(originalCheckoutAttributesXml, checkoutAttribute.Id);
                foreach (var selectedValue in selectedValues)
                {
                    checkoutAttributesXml = _checkoutAttributeParser.AddCheckoutAttribute(checkoutAttributesXml, checkoutAttribute, selectedValue);
                }
            }

            return checkoutAttributesXml;
        }

        private void ClearDiscountsAndShippingSelection(Customer customer, int storeId)
        {
            _genericAttributeService.SaveAttribute<string>(customer, SystemCustomerAttributeNames.GiftCardCouponCodes, null);
            _genericAttributeService.SaveAttribute<string>(customer, SystemCustomerAttributeNames.DiscountCouponCode, null);
            _genericAttributeService.SaveAttribute<ShippingOption>(customer, SystemCustomerAttributeNames.SelectedShippingOption, null, storeId);
        }

        private void AddShippingItemToCart(Customer customer, CartItem item, int currentStoreId)
        {
            var shippingOptions = _shippingService.GetShippingOptions(customer.ShoppingCartItems.ToList(), customer.ShippingAddress, storeId: currentStoreId);
            if (shippingOptions.Success)
            {
                var selectedShippingOption = shippingOptions.ShippingOptions.SingleOrDefault(x => x.Name == item.Reference);
                _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.SelectedShippingOption, selectedShippingOption, _storeContext.CurrentStore.Id);
            }
        }

        private void AddPhysicalItemToCart(Customer customer, CartItem item, int currentStoreId)
        {
            var productId = item.GetProductIdFromPhysicalCartItem();
            var attributesXml = item.GetAttributesXmlFromPhysicalCartItem();
            var product = _productService.GetProductById(productId);

            _shoppingCartService.AddToCart(
                customer,
                product,
                ShoppingCartType.ShoppingCart,
                currentStoreId,
                attributesXml,
                quantity: item.Quantity);
        }

        private void ClearItemsInCart(Customer customer, int storeId)
        {
            var itemsToRemove = customer.ShoppingCartItems.Where(x => x.StoreId == storeId && x.ShoppingCartType == ShoppingCartType.ShoppingCart).ToArray();

            foreach (var item in itemsToRemove)
            {
                _shoppingCartService.DeleteShoppingCartItem(item, ensureOnlyActiveCheckoutAttributes: true);
            }
        }
    }
}
