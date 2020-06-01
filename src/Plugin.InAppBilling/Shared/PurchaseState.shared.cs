namespace Plugin.InAppBilling
{
    /// <summary>
    /// Gets the current status of the purchase
    /// </summary>
    public enum PurchaseState
    {
        /// <summary>
        /// Purchased and in good standing
        /// </summary>
        Purchased = 0,
        /// <summary>
        /// Purchase was canceled
        /// </summary>
        Canceled = 1,
        /// <summary>
        /// Purchase was refunded
        /// </summary>
        Refunded = 2,
        /// <summary>
        /// In the process of being processed
        /// </summary>
        Purchasing,
        /// <summary>
        /// Transaction has failed
        /// </summary>
        Failed,
        /// <summary>
        /// Was restored.
        /// </summary>
        Restored,
        /// <summary>
        /// In queue, pending external action
        /// </summary>
        Deferred,
        /// <summary>
        /// In free trial
        /// </summary>
        FreeTrial,
        /// <summary>
        /// Pending Purchase
        /// </summary>
        PaymentPending,
        /// <summary>
        /// Purchase state unknown
        /// </summary>
        Unknown
    }
}
