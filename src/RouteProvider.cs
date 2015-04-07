using System.Web.Mvc;
using System.Web.Routing;
using Nop.Web.Framework.Mvc.Routes;

namespace Motillo.Nop.Plugin.KlarnaCheckout
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(RouteCollection routes)
        {
            //System.Web.Mvc.ViewEngines.Engines.Add(new KlarnaCheckoutViewEngine());
            
            
            routes.MapRoute("Motillo.Nop.Plugin.KlarnaCheckout",
                 "Plugins/Motillo.KlarnaCheckout/{action}",
                 new { controller = "KlarnaCheckout" },
                 new[] { "Motillo.Nop.Plugin.KlarnaCheckout.Controllers" }
            );

            /*
            routes.MapRoute("Plugin.Payments.KlarnaCheckout.Fetch",
                 "Plugins/PaymentsKlarnaCheckout/Fetch",
                 new { controller = "PaymentsKlarnaCheckout", action = "Fetch" },
                 new[] { "Majako.Plugin.Payments.KlarnaCheckout.Controllers" }
            );

            routes.MapRoute("Plugin.Payments.KlarnaCheckout.ShippingMethods",
                 "Plugins/PaymentsKlarnaCheckout/ShippingMethods",
                 new { controller = "PaymentsKlarnaCheckout", action = "ShippingMethods" },
                 new[] { "Majako.Plugin.Payments.KlarnaCheckout.Controllers" }
            );

            routes.MapRoute("Plugin.Payments.KlarnaCheckout.ChangeShippingMethod",
                 "Plugins/PaymentsKlarnaCheckout/ChangeShippingMethod",
                 new { controller = "PaymentsKlarnaCheckout", action = "ChangeShippingMethod" },
                 new[] { "Majako.Plugin.Payments.KlarnaCheckout.Controllers" }
            );

            routes.MapRoute("Plugin.Payments.KlarnaCheckout.CheckoutSnippet",
                 "Plugins/PaymentsKlarnaCheckout/CheckoutSnippet",
                 new { controller = "PaymentsKlarnaCheckout", action = "CheckoutSnippet" },
                 new[] { "Majako.Plugin.Payments.KlarnaCheckout.Controllers" }
            );

            routes.MapRoute("Plugin.Payments.KlarnaCheckout.KcoPush",
                 "Plugins/PaymentsKlarnaCheckout/KcoPush",
                 new { controller = "PaymentsKlarnaCheckout", action = "KcoPush" },
                 new[] { "Majako.Plugin.Payments.KlarnaCheckout.Controllers" }
            );
            
            routes.MapRoute("Plugin.Payments.KlarnaCore.ThankYou",
                 "Plugins/PaymentsKlarnaCheckout/ThankYou",
                 new { controller = "PaymentsKlarnaCheckout", action = "ThankYou" },
                 new[] { "Majako.Plugin.Payments.KlarnaCore.Controllers" }
            );
            */
        }
        public int Priority
        {
            get
            {
                return 0;
            }
        }
    }
}
