using System;

namespace Plugin.InAppBilling.Abstractions
{
    /// <summary>
    /// Purchase from in app billing
    /// </summary>
    public class InAppBillingPurchase
    {
        /// <summary>
        /// 
        /// </summary>
        public InAppBillingPurchase()
        {
        }

        /// <summary>
        /// Purchase Id
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Trasaction date in UTC
        /// </summary>
        public DateTime TransactionDateUtc { get; set; }

        /// <summary>
        /// Product Id/Sku
        /// </summary>
        public string ProductId { get; set; }
    }

}
