
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Plugin.InAppBilling
{
	/// <summary>
	/// Interface for InAppBilling
	/// </summary>
	[Preserve(AllMembers = true)]
	public interface IInAppBilling : IDisposable
    {
        /// <summary>
        /// Gets or sets if in testing mode
        /// </summary>
        bool InTestingMode { get; set; }


        Task<bool> AcknowledgePurchaseAsync(string purchaseToken);

        /// <summary>
        /// Connect to billing service
        /// </summary>
        /// <returns>If Success</returns>
        Task<bool> ConnectAsync(bool enablePendingPurchases = true);

        /// <summary>
        /// Disconnect from the billing service
        /// </summary>
        /// <returns>Task to disconnect</returns>
        Task DisconnectAsync();

        /// <summary>
        /// Get product information of a specific product
        /// </summary>
        /// <param name="itemType">Type of product offering</param>
        /// <param name="productIds">Sku or Id of the product(s)</param>
        /// <returns>List of products</returns>
        Task<IEnumerable<InAppBillingProduct>> GetProductInfoAsync(ItemType itemType, params string[] productIds);

		/// <summary>
		/// Get all current purchases for a specific product type. If you use verification and it fails for some purchase, it's not contained in the result.
		/// </summary>
		/// <param name="itemType">Type of product</param>
        /// <param name="verifyPurchase">Verify purchase implementation</param>
		/// <returns>The current purchases</returns>
		Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType);

        /// <summary>
        /// (Android specific) Upgrade/Downgrade/Change a previously purchased subscription
        /// </summary>
        /// <param name="oldProductId">Sku or ID of product that needs to be upgraded</param>
        /// <param name="newProductId">Sku or ID of product that will replace the old one</param>
        /// <param name="purchaseTokenOfOriginalSubscription">Purchase token of original subscription (can not be null)</param>
        /// <param name="prorationMode">Proration mode (1 - ImmediateWithTimeProration, 2 - ImmediateAndChargeProratedPrice, 3 - ImmediateWithoutProration, 4 - Deferred)</param>
        /// <param name="verifyPurchase">Verify Purchase implementation</param>
        /// <returns>Purchase details</returns>
        /// <exception cref="InAppBillingPurchaseException">If an error occures during processing</exception>
        Task<InAppBillingPurchase> UpgradePurchasedSubscriptionAsync(string newProductId, string oldProductId, string purchaseTokenOfOriginalSubscription, int prorationMode = 1, IInAppBillingVerifyPurchase verifyPurchase = null);

        /// <summary>
        /// Purchase a specific product or subscription
        /// </summary>
        /// <param name="productId">Sku or ID of product</param>
        /// <param name="itemType">Type of product being requested</param>
        /// <param name="payload">Developer specific payload (can not be null)</param>
        /// <param name="verifyPurchase">Verify Purchase implementation</param>
        /// <returns>Purchase details</returns>
        /// <exception cref="InAppBillingPurchaseException">If an error occures during processing</exception>
        Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, IInAppBillingVerifyPurchase verifyPurchase = null);

        /// <summary>
        /// Consume a purchase with a purchase token.
        /// </summary>
        /// <param name="productId">Id or Sku of product</param>
        /// <param name="purchaseToken">Original Purchase Token</param>
        /// <returns>If consumed successful</returns>
        /// <exception cref="InAppBillingPurchaseException">If an error occures during processing</exception>
        Task<bool> ConsumePurchaseAsync(string productId, string purchaseToken);

		Task<bool> FinishTransaction(InAppBillingPurchase purchase);

		Task<bool> FinishTransaction(string purchaseId);

	}
}
