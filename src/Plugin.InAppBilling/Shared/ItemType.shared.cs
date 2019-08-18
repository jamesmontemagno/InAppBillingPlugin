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
        /// On going subscription
        /// </summary>
        Subscription
    }
}
