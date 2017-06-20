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
        /// Purchase/Order Id
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

        /// <summary>
        /// Indicates whether the subscritpion renewes automatically. If true, the sub is active, else false the user has canceled.
        /// </summary>
        public bool AutoRenewing { get; set; }

        /// <summary>
        /// Unique token identifying the purchase for a given item
        /// </summary>
        public string PurchaseToken { get; set; }

        /// <summary>
        /// Gets the current purchase/subscription state
        /// </summary>
        public PurchaseState State { get; set; }

        /// <summary>
        /// Gets the current consumption state
        /// </summary>
        public ConsumptionState ConsumptionState { get; set; }

        /// <summary>
        /// Developer payload
        /// </summary>
        public string Payload { get; set; }

        /// <summary>
        /// Prints out product
        /// </summary>
        /// <returns></returns>
        public override string ToString() => 
			$"ProductId:{ProductId} | AutoRenewing:{AutoRenewing} | State:{State} | Id:{Id}";
        
    }

}
