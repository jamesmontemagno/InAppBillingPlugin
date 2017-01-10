using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Plugin.InAppBilling.Abstractions
{
    /// <summary>
    /// Type of purchase error
    /// </summary>
    public enum PurchaseError
    {
        /// <summary>
        /// Billing system unavailable
        /// </summary>
        BillingUnavailable,
        /// <summary>
        /// Developer issue
        /// </summary>
        DeveloperError,
        /// <summary>
        /// Product sku not available
        /// </summary>
        ItemUnavailable,
        /// <summary>
        /// Other error
        /// </summary>
        GeneralError
    }

    /// <summary>
    /// Purchase exception
    /// </summary>
    public class InAppBillingPurchaseException : Exception
    {
        /// <summary>
        /// Type of error
        /// </summary>
        public PurchaseError PurchaseError { get; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="error"></param>
        /// <param name="ex"></param>
        public InAppBillingPurchaseException(PurchaseError error, Exception ex) : base("Unable to process purchase.", ex)
        {
            PurchaseError = error;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="error"></param>
        public InAppBillingPurchaseException(PurchaseError error) : base("Unable to process purchase.")
        {
            PurchaseError = error;
        }
    }
}
