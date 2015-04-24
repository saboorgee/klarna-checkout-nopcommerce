﻿using Motillo.Nop.Plugin.KlarnaCheckout.Data;
using Motillo.Nop.Plugin.KlarnaCheckout.Models;
using Motillo.Nop.Plugin.KlarnaCheckout.Services;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Data;
using Nop.Core.Domain.Orders;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Web.Framework.Controllers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using Customer = Nop.Core.Domain.Customers.Customer;
using Order = Nop.Core.Domain.Orders.Order;

namespace Motillo.Nop.Plugin.KlarnaCheckout.Controllers
{
    public class KlarnaCheckoutController : BasePaymentController
    {
        private readonly IWorkContext _workContext;
        private readonly ISettingService _settingService;
        private readonly KlarnaCheckoutSettings _settings;
        private readonly IRepository<KlarnaCheckoutEntity> _repository;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IStoreContext _storeContext;
        private readonly ILogger _logger;
        private readonly ICustomerService _customerService;
        private readonly IKlarnaCheckoutHelper _klarnaCheckoutHelper;
        private readonly IKlarnaCheckoutPaymentService _klarnaCheckoutPaymentService;

        public KlarnaCheckoutController(
            IWorkContext workContext,
            ISettingService settingService,
            KlarnaCheckoutSettings settings,
            IRepository<KlarnaCheckoutEntity> repository,
            IOrderService orderService,
            IOrderProcessingService orderProcessingService,
            IStoreContext storeContext,
            ILogger logger,
            ICustomerService customerService,
            IKlarnaCheckoutHelper klarnaCheckoutHelper,
            IKlarnaCheckoutPaymentService klarnaHelper)
        {
            _workContext = workContext;
            _settingService = settingService;
            _settings = settings;
            _repository = repository;
            _orderService = orderService;
            _orderProcessingService = orderProcessingService;
            _storeContext = storeContext;
            _logger = logger;
            _customerService = customerService;
            _klarnaCheckoutHelper = klarnaCheckoutHelper;
            _klarnaCheckoutPaymentService = klarnaHelper;
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
            var model = new ConfigurationModel
            {
                EId = _settings.EId,
                SharedSecret = _settings.SharedSecret,
                EnabledCountries = _settings.EnabledCountries,
                CheckoutUrl = _settings.CheckoutUrl,
                TermsUrl = _settings.TermsUrl,
                TestMode = _settings.TestMode,
                ColorButton = _settings.ColorButton,
                ColorButtonText = _settings.ColorButtonText,
                ColorCheckbox = _settings.ColorCheckbox,
                ColorCheckboxCheckmark = _settings.ColorCheckboxCheckmark,
                ColorHeader = _settings.ColorHeader,
                ColorLink = _settings.ColorLink
            };

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

            _settings.EId = model.EId;
            _settings.SharedSecret = model.SharedSecret;
            _settings.EnabledCountries = (model.EnabledCountries ?? string.Empty).ToUpperInvariant();
            _settings.TermsUrl = model.TermsUrl;
            _settings.CheckoutUrl = model.CheckoutUrl;
            _settings.TestMode = model.TestMode;
            _settings.ColorButton = model.ColorButton;
            _settings.ColorButtonText = model.ColorButtonText;
            _settings.ColorCheckbox = model.ColorCheckbox;
            _settings.ColorCheckboxCheckmark = model.ColorCheckboxCheckmark;
            _settings.ColorHeader = model.ColorHeader;
            _settings.ColorLink = model.ColorLink;

            _settingService.SaveSetting(_settings);
            _settingService.ClearCache();

            return Configure();
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            Uri resourceUri;
            var customer = _workContext.CurrentCustomer;
            var payment = _repository.Table
                .OrderByDescending(x => x.CreatedOnUtc)
                .FirstOrDefault(x => x.CustomerId == customer.Id && x.Status == KlarnaCheckoutStatus.Pending);

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

            var order = _klarnaCheckoutPaymentService.Fetch(resourceUri);
            var data = order.Marshal();
            var jsonData = JsonConvert.SerializeObject(data);
            var klarnaOrder = JsonConvert.DeserializeObject<KlarnaOrder>(jsonData);

            var model = new PaymentInfoModel
            {
                SnippetHtml = klarnaOrder.Gui.Snippet
            };

            return View("~/Plugins/Motillo.KlarnaCheckout/Views/KlarnaCheckout/PaymentInfo.cshtml", model);
        }

        [HttpPost]
        public void KlarnaCheckoutPush(string sid, string klarna_order)
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
                SyncKlarnaAndNopOrder(klarnaRequest, customer);
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: Error syncing klarna and nop order in Push. CustomerId: {0}, KlarnaId: {1}", customer.Id, klarnaRequest.Id), exception: ex);
            }
        }

        public ActionResult ThankYou(string sid, string klarna_order)
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

            var resourceUri = new Uri(klarnaRequest.KlarnaResourceUri);
            var order = _klarnaCheckoutPaymentService.Fetch(resourceUri);
            var data = order.Marshal();
            var jsonData = JsonConvert.SerializeObject(data);
            var klarnaOrder = JsonConvert.DeserializeObject<Models.KlarnaOrder>(jsonData);

            if (klarnaOrder.Status == KlarnaOrder.StatusCheckoutComplete)
            {
                TempData["KlarnaSnippet"] = klarnaOrder.Gui.Snippet;
                ViewBag.KlarnaSnippet = klarnaOrder.Gui.Snippet;

                return View("~/Plugins/Motillo.KlarnaCheckout/Views/KlarnaCheckout/ThankYou.cshtml");
            }

            return RedirectToAction("Index", "Home"); 
        }

        private Order SyncKlarnaAndNopOrder(KlarnaCheckoutEntity klarnaRequest, Customer customer)
        {
            var nopOrder = _orderService.GetOrderByGuid(klarnaRequest.OrderGuid);
            var resourceUri = new Uri(klarnaRequest.KlarnaResourceUri);
            var order = _klarnaCheckoutPaymentService.Fetch(resourceUri);
            var data = order.Marshal();
            var jsonData = JsonConvert.SerializeObject(data);
            var klarnaOrder = JsonConvert.DeserializeObject<Models.KlarnaOrder>(jsonData);

            if (klarnaOrder.Status == KlarnaOrder.StatusCheckoutComplete)
            {
                klarnaRequest.Status = KlarnaCheckoutStatus.Complete;
                _repository.Update(klarnaRequest);
            }

            // Create the order if it doesn't exist in nop. According to the Klarna Checkout
            // developer guidelines, one should only create the order if the status is
            // 'checkout_complete'.
            // https://developers.klarna.com/en/klarna-checkout/acknowledge-an-order
            if (nopOrder == null && klarnaOrder.Status == KlarnaOrder.StatusCheckoutComplete)
            {
                _klarnaCheckoutPaymentService.SyncBillingAndShippingAddress(customer, klarnaOrder);

                nopOrder = CreateOrderAndSyncWithKlarna(klarnaRequest, customer, klarnaOrder, resourceUri);
            }

            return nopOrder;
        }

        private Order CreateOrderAndSyncWithKlarna(KlarnaCheckoutEntity klarnaRequest, Customer customer, KlarnaOrder klarnaOrder, Uri resourceUri)
        {
            var processPaymentRequest = new ProcessPaymentRequest
            {
                OrderGuid = klarnaRequest.OrderGuid,
                CustomerId = customer.Id,
                StoreId = _storeContext.CurrentStore.Id,
                PaymentMethodSystemName = "Motillo.KlarnaCheckout"
            };

            var placeOrderResult = _orderProcessingService.PlaceOrder(processPaymentRequest);

            // If you tamper with the cart after the klarna widget is rendered nop fails to create the order.
            if (!placeOrderResult.Success)
            {
                var errors = string.Join("; ", placeOrderResult.Errors);
                _logger.Error(string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: Klarna has been processed but order could not be created in Nop! Klarna ID={0}, ResourceURI={1}. Errors: {2}",
                    klarnaOrder.Id, klarnaRequest.KlarnaResourceUri, errors), customer: customer);

                if (!_klarnaCheckoutPaymentService.CancelPayment(klarnaOrder.Reservation, customer))
                {
                    _logger.Error("KlarnaCheckout: Error canceling reservation: " + klarnaOrder.Reservation, customer: customer);
                }

                throw new KlarnaCheckoutException("Error creating order: " + errors);
            }

            // Order was successfully created.
            var orderId = placeOrderResult.PlacedOrder.Id;
            var nopOrder = _orderService.GetOrderById(orderId);

            _klarnaCheckoutPaymentService.Acknowledge(resourceUri, nopOrder);

            nopOrder.AuthorizationTransactionId = klarnaOrder.Reservation;
            _orderService.UpdateOrder(nopOrder);

            var orderTotalInCents = _klarnaCheckoutHelper.ConvertToCents(nopOrder.OrderTotal);

            // Ensure the cart wasn't tampered with during checkout.
            if (orderTotalInCents == klarnaOrder.Cart.TotalPriceIncludingTax)
            {
                nopOrder.OrderNotes.Add(new OrderNote
                {
                    Note = "KlarnaCheckout: Order acknowledged. Uri: " + resourceUri,
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });
                _orderService.UpdateOrder(nopOrder);

                if (_orderProcessingService.CanMarkOrderAsPaid(nopOrder))
                {
                    _orderProcessingService.MarkOrderAsPaid(nopOrder);

                    // Sometimes shipping isn't required, e.g. if only ordering virtual gift cards.
                    // In those cases, make sure the Klarna order is activated.
                    if (nopOrder.OrderStatus == OrderStatus.Complete)
                    {
                        var activationResult = _klarnaCheckoutPaymentService.Activate(klarnaOrder.Reservation, customer);

                        if (activationResult != null)
                        {
                            nopOrder.OrderNotes.Add(new OrderNote
                            {
                                Note = string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: Order complete after payment, activating Klarna order. Reservation: {0}, RiskStatus: {1}, InvoiceNumber: {2}",
                                    klarnaOrder.Reservation, activationResult.RiskStatus, activationResult.InvoiceNumber),
                                CreatedOnUtc = DateTime.UtcNow,
                                DisplayToCustomer = false
                            });
                            _orderService.UpdateOrder(nopOrder);

                            klarnaRequest.Status = KlarnaCheckoutStatus.Activated;
                            _repository.Update(klarnaRequest);

                            _logger.Information(string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: Order complete after payment, activating Klarna order. OrderId: {0}, OrderGuid: {1}",
                                nopOrder.Id, nopOrder.OrderGuid));
                        }
                    }
                }
            }
            // Paid amount and order total differs. The cart might've been tampered with.
            else
            {
                nopOrder.OrderNotes.Add(new OrderNote
                {
                    Note = string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: Order total differs from Klarna order. OrderTotal: {0}, KlarnaTotal: {1}, Uri: {2}",
                        orderTotalInCents, klarnaOrder.Cart.TotalPriceIncludingTax, resourceUri),
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });
                nopOrder.OrderNotes.Add(new OrderNote
                {
                    Note = "KlarnaCheckout: Order total differs from paid amount to Klarna. Order has not been marked as paid. Please contact an administrator.",
                    DisplayToCustomer = true,
                    CreatedOnUtc = DateTime.UtcNow
                });
                _orderService.UpdateOrder(nopOrder);

                _logger.Warning(string.Format(CultureInfo.CurrentCulture, "KlarnaCheckout: Order total differs. Please verify the sum and mark the order as paid in the administration. See order notes for more details. OrderId: {0}, OrderGuid: {1}",
                    nopOrder.Id, nopOrder.OrderGuid), customer: customer);
            }

            return nopOrder;
        }
    }
}