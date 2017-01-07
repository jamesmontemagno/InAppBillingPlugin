
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
        /// <param name="productId">Sku or Id of the product</param>
        /// <param name="itemType">Type of product offering</param>
        /// <returns></returns>
        Task<InAppBillingProduct> GetProductInfoAsync(string productId, ItemType itemType);

        /// <summary>
        /// Get all current purhcase for a specifiy product type.
        /// </summary>
        /// <param name="itemType">Type of product</param>
        /// <returns>The current purchases</returns>
        Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType);

        /// <summary>
        /// Purchase a specific product or subscription
        /// </summary>
        /// <param name="productId">Sku or ID of product</param>
        /// <param name="itemType">Type of product being requested</param>
        /// <param name="payload">Developer specific payload</param>
        /// <returns></returns>
        Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, string payload);

        /// <summary>
        /// Validation public key from App Store
        /// </summary>
        string ValidationPublicKey { get; set; }
    }
}
