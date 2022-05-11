using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// Base implementation for In App Billing, handling disposables
    /// </summary>

    public abstract class BaseInAppBilling : IInAppBilling, IDisposable
    {

        /// <summary>
        /// Gets a representation of the storefront
        /// </summary>
        public virtual Storefront Storefront { get; } = null;

        /// <summary>
        /// Gets if the user can make payments
        /// </summary>
        public virtual bool CanMakePayments { get; } = true;
        /// <summary>
        /// Gets receitpt data on iOS
        /// </summary>
        public virtual string ReceiptData { get; } = string.Empty;

        /// <summary>
        /// If connected to the store
        /// </summary>
        public virtual bool IsConnected { get; set; } = true;

        /// <summary>
        /// Gets or sets if in testing mode
        /// </summary>
        public abstract bool InTestingMode { get; set; }

        /// <summary>
        /// Connect to billing service
        /// </summary>
        /// <returns>If Success</returns>
        public virtual Task<bool> ConnectAsync(bool enablePendingPurchases = true) => Task.FromResult(true);

        /// <summary>
        /// Disconnect from the billing service
        /// </summary>
        /// <returns>Task to disconnect</returns>
        public virtual Task DisconnectAsync() => Task.CompletedTask;

        /// <summary>
        /// Get product information of a specific product
        /// </summary>
        /// <param name="itemType">Type of product offering</param>
        /// <param name="productIds">Sku or Id of the product(s)</param>
        /// <returns>List of products</returns>
        public abstract Task<IEnumerable<InAppBillingProduct>> GetProductInfoAsync(ItemType itemType, params string[] productIds);



        /// <summary>
        /// Get all current purchases for a specific product type. If verification fails for some purchase, it's not contained in the result.
        /// </summary>
        /// <param name="itemType">Type of product</param>
        /// <returns>The current purchases</returns>
        public abstract Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType);



        /// <summary>
        /// Android only: Returns the most recent purchase made by the user for each SKU, even if that purchase is expired, canceled, or consumed.
        /// </summary>
        /// <param name="itemType">Type of product</param>
        /// <returns>The current purchases</returns>
        public virtual Task<IEnumerable<InAppBillingPurchase>> GetPurchasesHistoryAsync(ItemType itemType) =>
            Task.FromResult<IEnumerable<InAppBillingPurchase>>(new List<InAppBillingPurchase>());

        /// <summary>
        /// Purchase a specific product or subscription
        /// </summary>
        /// <param name="productId">Sku or ID of product</param>
        /// <param name="itemType">Type of product being requested</param>
        /// <param name="obfuscatedAccountId">Specifies an optional obfuscated string that is uniquely associated with the user's account in your app.</param>
        /// <param name="obfuscatedProfileId">Specifies an optional obfuscated string that is uniquely associated with the user's profile in your app.</param>
        /// <returns>Purchase details</returns>
        /// <exception cref="InAppBillingPurchaseException">If an error occurs during processing</exception>
        public abstract Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, string obfuscatedAccountId = null, string obfuscatedProfileId = null);

        /// <summary>
        /// (Android specific) Upgrade/Downgrade a previously purchased subscription
        /// </summary>
        /// <param name="newProductId">Sku or ID of product that will replace the old one</param>
        /// <param name="purchaseTokenOfOriginalSubscription">Purchase token of original subscription (can not be null)</param>
        /// <param name="prorationMode">Proration mode</param>
        /// <returns>Purchase details</returns>
        public abstract Task<InAppBillingPurchase> UpgradePurchasedSubscriptionAsync(string newProductId, string purchaseTokenOfOriginalSubscription, SubscriptionProrationMode prorationMode = SubscriptionProrationMode.ImmediateWithTimeProration);

        /// <summary>
        /// Consume a purchase with a purchase token.
        /// </summary>
        /// <param name="productId">Product Id</param>
        /// <param name="transactionIdentifier">Original Purchase Token</param>
        /// <returns>If consumed successful</returns>
        /// <exception cref="InAppBillingPurchaseException">If an error occurs during processing</exception>
        public abstract Task<bool> ConsumePurchaseAsync(string productId, string transactionIdentifier);

        /// <summary>
        /// Dispose of class and parent classes
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose up
        /// </summary>
        ~BaseInAppBilling()
        {
            Dispose(false);
        }

        bool disposed = false;
        /// <summary>
        /// Dispose method
        /// </summary>
        /// <param name="disposing"></param>
        public virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    //dispose only
                }

                disposed = true;
            }
        }
        /// <summary>
        /// acknowledge a purchase
        /// </summary>
        /// <param name="transactionIdentifier"></param>
        /// <returns></returns>
        public virtual Task<bool> FinalizePurchaseAsync(string transactionIdentifier) => Task.FromResult(true);

        /// <summary>
        /// iOS: Displays a sheet that enables users to redeem subscription offer codes that you configure in App Store Connect.
        /// </summary>
        public virtual void PresentCodeRedemption() { }
    }
}
