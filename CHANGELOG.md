# 1.2.0-beta.1 (TBA)

## New Features
  - Support full refund.
  - Support capture.
  - Support void.

## Upgrade Notes

Edit the view `Checkout/Completed.cshtml` and replace the old snipped code with this:
```csharp
@Html.Action("ConfirmationSnippet", "KlarnaCheckout", new { orderId = Model.OrderId })
```

Previously all orders were marked as paid. Now a created order is marked as authorized
until the payment has been captured. The payment is still automatically captured when
the order is shipped, but it can also be manually captured by an admin.

# 1.1.0 (2015-06-24)

## New Features
  - Support for Austria.
  - Support for reward points.
  - Support for checkout attributes.
  - New configuration setting for alternative shipping address.
  - Autofill the checkout iframe with email and postal code.
  - Allow different configuration values per store.

## Bug Fixes
  - Works with the Google Analytics eCommerce tracking plugin.
  - Order total validation due to currency conversion.
  - The setting "Disable autofocus" wasn't saved properly.
  - Prevent billing and shipping addresses from being overwritten.
  - Use separate Klarna order per store.

## Upgrade Notes
The `ThankYou` view has been removed. Instead the plugin redirects to nop's Checkout/Completed action. The themes `Checkout/Completed.cshtml` view needs to be modified to show Klarna's confirmation snippet. E.g.
```csharp
@if (TempData["KlarnaSnippet"] is string)
{
    <div class="row">
        @Html.Raw((string)TempData["KlarnaSnippet"])
    </div>
}
```

# 1.0.2-beta.5 (2015-06-17)

## Bug Fixes
  - Only autofill email and postal code the first time the Klarna Checkout widget is loaded. Due to buggy behavior, do not try to change it on updates.

# 1.0.2-beta.4 (2015-06-16)

## New Features
  - Autofill email and postal code (based on shipping address) when the widget is first loaded.

# 1.0.2-beta.2 (2015-06-03)

## New Features
  - Support for alternative shipping address option.

## Bug Fixes
  - Fix so that billing and shipping address don't get overwritten.
  - Fix so the value of the Disable Autofocus option is saved properly.

# 1.0.2-beta.1 (2015-05-07)

## New Features
  - Support for reward points.
  - Support for checkout attributes.
  - Add Austria as valid country.

## Bug Fixes
  - The plugin now works properly with the "Google Analytics or Universal Analytics" widget (see the upgrade notes below).
  - Fix where orders on rare occasions wasn't marked as paid (could happen due to rounding if prices contained more than two decimals).
  - The plugin now works during test when no public push URI is available.

## Upgrade notes
The `ThankYou` view has been removed. Instead the plugin redirects to nop's Checkout/Completed action. You need to modify the `Checkout/Completed.cshtml` view to show Klarna's HTML confirmation snippet. E.g.
```csharp
@if (TempData["KlarnaSnippet"] is string)
{
    <div class="row">
        @Html.Raw((string)TempData["KlarnaSnippet"])
    </div>
}
```

# 1.0.1 (2015-04-27)

## Bug fixes
  - Correct price when using category discount with absolute value (not percentage).
  - Correct price for shipping, gift cards, and some discount types when not using primary store currency.
  - Fix where specified locale for Finnish wasn't used (always got fi-fi even if sv-fi was specified).

# 1.0.0 (2015-04-07)

First release!

## Features
  - Implemented as a nopCommerce payment method.
  - Test/live mode.
  - Configuration of snippet colors.
  - Automatic activation of the Klarna order when the nop order is marked as shipped.
