using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// State of consumable
    /// </summary>
    public enum ConsumptionState
    {
        /// <summary>
        /// Has not been consumed yet
        /// </summary>
        NoYetConsumed,
        /// <summary>
        /// Consumed
        /// </summary>
        Consumed
    }
}
