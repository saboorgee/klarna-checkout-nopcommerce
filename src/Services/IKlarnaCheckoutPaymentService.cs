﻿using Klarna.Api;
using Motillo.Nop.Plugin.KlarnaCheckout.Models;
using Nop.Core.Domain.Orders;
using System;

namespace Motillo.Nop.Plugin.KlarnaCheckout.Services
{
    public interface IKlarnaCheckoutPaymentService
    {
        void Acknowledge(Order order);
        ActivateReservationResponse Activate(Order order);
        Uri Create();
        Klarna.Checkout.RecurringOrder CreateRecurring(Order newOrder);
        Klarna.Checkout.Order Fetch(Uri resourceUri);
        bool Update(Uri resourceUri);
        void SyncBillingAndShippingAddress(global::Nop.Core.Domain.Customers.Customer customer, KlarnaCheckoutOrder klarnaCheckoutOrder);
        void CancelPayment(string reservation, global::Nop.Core.Domain.Customers.Customer customer);
        void FullRefund(Order order);
    }
}
