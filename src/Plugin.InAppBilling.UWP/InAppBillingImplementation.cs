using Plugin.InAppBilling.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// Implementation for Feature
    /// </summary>
    public class InAppBillingImplementation : IInAppBilling
    {
        /// <summary>
        /// Validation public key from App Store
        /// </summary>
        public string ValidationPublicKey
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Connect to billing service
        /// </summary>
        /// <returns>If Success</returns>
        public Task<bool> ConnectAsync()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Disconnect from the billing service
        /// </summary>
        /// <returns>Task to disconnect</returns>
        public Task DisconnectAsync()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get product information of a specific product
        /// </summary>
        /// <param name="productId">Sku or Id of the product</param>
        /// <param name="itemType">Type of product offering</param>
        /// <returns></returns>
        public Task<InAppBillingProduct> GetProductInfoAsync(string productId, ItemType itemType)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get all current purhcase for a specifiy product type.
        /// </summary>
        /// <param name="itemType">Type of product</param>
        /// <returns>The current purchases</returns>
        public Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Purchase a specific product or subscription
        /// </summary>
        /// <param name="productId">Sku or ID of product</param>
        /// <param name="itemType">Type of product being requested</param>
        /// <param name="payload">Developer specific payload</param>
        /// <returns></returns>
        public Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, string payload)
        {
            throw new NotImplementedException();
        }
    }
}