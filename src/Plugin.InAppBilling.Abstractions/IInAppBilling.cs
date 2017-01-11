
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Plugin.InAppBilling.Abstractions
{
    /// <summary>
    /// Interface for InAppBilling
    /// </summary>
    public interface IInAppBilling
    {
        /// <summary>
        /// Connect to billing service
        /// </summary>
        /// <returns>If Success</returns>
        Task<bool> ConnectAsync();

        /// <summary>
        /// Disconnect from the billing service
        /// </summary>
        /// <returns>Task to disconnect</returns>
        Task DisconnectAsync();

        /// <summary>
        /// Get product information of a specific product
        /// </summary>
        /// <param name="itemType">Type of product offering</param>
        /// <param name="productId">Sku or Id of the product(s)</param>
        /// <returns></returns>
        Task<IEnumerable<InAppBillingProduct>> GetProductInfoAsync(ItemType itemType, params string[] productIds);


        /// <summary>
        /// Get all current purhcase for a specifiy product type.
        /// </summary>
        /// <param name="itemType">Type of product</param>
        /// <returns>The current purchases</returns>
        Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType, IInAppBillingVerifyPurchase verifyPurchase = null);

        /// <summary>
        /// Purchase a specific product or subscription
        /// </summary>
        /// <param name="productId">Sku or ID of product</param>
        /// <param name="itemType">Type of product being requested</param>
        /// <param name="payload">Developer specific payload</param>
        /// <returns></returns>
        /// <exception cref="InAppBillingPurchaseException">If an error occures during processing</exception>
        Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, string payload, IInAppBillingVerifyPurchase verifyPurchase = null);
    }
}
