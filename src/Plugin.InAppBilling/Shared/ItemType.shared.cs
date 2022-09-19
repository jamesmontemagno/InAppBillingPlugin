using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// Product item type
    /// </summary>
    public enum ItemType
    {
        /// <summary>
        /// Single purchase (managed)
        /// </summary>
        InAppPurchase,
        /// <summary>
        /// Single purchase that needs to be consumed manually
        /// </summary>
        InAppPurchaseConsumable,
        /// <summary>
        /// On going subscription
        /// </summary>
        Subscription
    }

    /// <summary>
    /// Subscription proration mode
    /// </summary>
    public enum SubscriptionProrationMode
    {
        ImmediateWithTimeProration = 1,
        ImmediateAndChargeProratedPrice = 2,
        ImmediateWithoutProration = 3,
        Deferred = 4,
        ImmediateAndChargeFullPrice = 5
    }
}
