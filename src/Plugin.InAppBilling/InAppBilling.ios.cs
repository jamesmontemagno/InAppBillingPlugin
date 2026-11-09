using CoreFoundation;
using Foundation;
using StoreKit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// StoreKit 2 implementation for InAppBilling on iOS
    /// </summary>
    [Preserve(AllMembers = true)]
    public class InAppBillingImplementation : BaseInAppBilling
    {
        /// <summary>
        /// Backwards compat flag that may be removed in the future to auto finish all transactions like in v4
        /// </summary>
        public static bool FinishAllTransactions { get; set; } = true;

        /// <summary>
        /// Gets or sets a callback for completed purchases, including purchases made outside of the app, renewals, and approved Ask to Buy requests.
        /// </summary>
        public static Action<InAppBillingPurchase> OnPurchaseComplete { get; set; } = null;

        /// <summary>
        /// Gets or sets a callback for failed purchases, transactions that fail verification, and revoked transactions.
        /// </summary>
        public static Action<InAppBillingPurchase> OnPurchaseFailure { get; set; } = null;

        /// <summary>
        /// Gets or sets a callback for a purchase started from a promoted item on the App Store (iOS 16.4+). It receives the product id, return true to purchase it now.
        /// </summary>
        public static Func<string, bool> OnPurchaseIntent { get; set; } = null;

        /// <summary>
        /// No longer invoked because StoreKit 2 does not use the payment queue.
        /// </summary>
        [Obsolete("StoreKit 2 does not use SKPaymentQueue. Use OnPurchaseIntent instead.", error: true)]
        public static Func<SKPaymentQueue, SKPayment, SKProduct, bool> OnShouldAddStorePayment { get; set; } = null;

        bool listening;
        Storefront storefront;

        /// <summary>
        /// Default constructor for In App Billing on iOS
        /// </summary>
        public InAppBillingImplementation()
        {
            Init();
        }

        void Init()
        {
            if (listening)
                return;

            listening = true;
            StoreKit2Native.StartListeners(storeEvent => _ = HandleEventAsync(storeEvent));
            _ = RefreshStorefrontAsync();
        }

        async Task RefreshStorefrontAsync()
        {
            try
            {
                var current = (await StoreKit2Native.StorefrontAsync()).Storefront;
                if (current != null)
                    storefront = new Storefront { Id = current.Id, CountryCode = current.CountryCode };
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Unable to get storefront: " + ex);
            }
        }

        /// <summary>
        /// iOS: Displays a sheet that enables users to redeem subscription offer codes that you configure in App Store Connect.
        /// </summary>
        public override void PresentCodeRedemption() => StoreKit2Native.PresentOfferCodeSheet();

        /// <summary>
        /// Returns representation of storefront
        /// </summary>
        public override Storefront Storefront
        {
            get
            {
                if (storefront != null)
                    return storefront;

                // StoreKit 2 only exposes the storefront asynchronously, so this covers reads before the first refresh completes.
                var queueStorefront = SKPaymentQueue.DefaultQueue.Storefront;
                return queueStorefront == null ? null : new Storefront
                {
                    CountryCode = queueStorefront.CountryCode,
                    Id = queueStorefront.Identifier
                };
            }
        }

        /// <summary>
        /// Gets if user can make payments
        /// </summary>
        public override bool CanMakePayments => StoreKit2Native.CanMakePayments;

        /// <summary>
        /// Gets or sets if in testing mode. Only for UWP
        /// </summary>
        public override bool InTestingMode { get; set; }

        /// <summary>
        /// Get product information of a specific product
        /// </summary>
        /// <param name="productIds">Sku or Id of the product(s)</param>
        /// <param name="itemType">Type of product offering</param>
        /// <returns></returns>
        public override async Task<IEnumerable<InAppBillingProduct>> GetProductInfoAsync(ItemType itemType, string[] productIds, CancellationToken cancellationToken)
        {
            Init();
            var response = await StoreKit2Native.ProductsAsync(productIds).WaitAsync(cancellationToken);

            if (response.Error != null)
                throw StoreKitMapper.ToException(response.Error, PurchaseError.ProductRequestFailed);

            if (!IgnoreInvalidProducts && response.Missing?.Count > 0)
                throw new InAppBillingPurchaseException(PurchaseError.InvalidProduct, "Invalid products found when querying product list", response.Missing.ToArray());

            return response.Products?.Select(StoreKitMapper.ToProduct).ToList() ?? new List<InAppBillingProduct>();
        }

        /// <summary>
        /// Get everything the user currently owns and any purchase that has not been finished. All types are returned regardless of itemType.
        /// </summary>
        /// <param name="itemType"></param>
        /// <returns></returns>
        public override async Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType, CancellationToken cancellationToken)
        {
            Init();
            var response = await StoreKit2Native.CurrentPurchasesAsync().WaitAsync(cancellationToken);

            if (response.Error != null)
                throw StoreKitMapper.ToException(response.Error, PurchaseError.RestoreFailed);

            var transactions = response.Transactions ?? new List<StoreKitTransaction>();

            if (FinishAllTransactions && transactions.Count > 0)
                await StoreKit2Native.FinishTransactionsAsync(transactions.Select(t => t.Id).ToArray());

            return transactions.Select(StoreKitMapper.ToPurchase).ToList();
        }

        /// <summary>
        /// Asks the App Store to sync this device's transactions, for use behind a restore button. This may prompt the user to sign in.
        /// StoreKit 2 keeps purchases in sync automatically, so most apps only need GetPurchasesAsync.
        /// </summary>
        /// <exception cref="InAppBillingPurchaseException">If the sync fails or is cancelled</exception>
        public async Task SyncPurchasesAsync(CancellationToken cancellationToken = default)
        {
            Init();
            var response = await StoreKit2Native.SyncAsync().WaitAsync(cancellationToken);

            if (response.Error != null)
                throw StoreKitMapper.ToException(response.Error, PurchaseError.RestoreFailed);
        }

        /// <summary>
        /// Purchase a specific product or subscription
        /// </summary>
        /// <param name="productId">Sku or ID of product</param>
        /// <param name="itemType">Type of product being requested</param>
        /// <param name="obfuscatedAccountId">Sent to StoreKit as the appAccountToken. A value that is not a UUID is converted to a deterministic version 5 UUID.</param>
        /// <param name="obfuscatedProfileId">Not used on iOS</param>
        /// <param name="subOfferToken">Not used on iOS</param>
        /// <returns>The purchase. Its state is Deferred when it needs approval, the result then arrives through OnPurchaseComplete.</returns>
        public override async Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, string obfuscatedAccountId = null, string obfuscatedProfileId = null, string subOfferToken = null, CancellationToken cancellationToken = default)
        {
            Init();
            var pending = StoreKit2Native.PurchaseAsync(productId, StoreKitMapper.ToAppAccountToken(obfuscatedAccountId));

            StoreKitResponse response;
            try
            {
                response = await pending.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The payment sheet can't be dismissed from here, so a purchase may still go through after the caller stops waiting.
                _ = CompleteAbandonedPurchaseAsync(productId, pending);
                throw;
            }

            return await CompletePurchaseAsync(productId, response);
        }

        static async Task CompleteAbandonedPurchaseAsync(string productId, Task<StoreKitResponse> pending)
        {
            try
            {
                await CompletePurchaseAsync(productId, await pending);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Abandoned purchase did not complete: " + ex);
            }
        }

        static async Task<InAppBillingPurchase> CompletePurchaseAsync(string productId, StoreKitResponse response)
        {
            if (response.Error != null)
            {
                NotifyPurchaseFailed(productId);
                throw StoreKitMapper.ToException(response.Error);
            }

            switch (response.Status)
            {
                case "success":
                    if (FinishAllTransactions)
                        await StoreKit2Native.FinishTransactionsAsync(new[] { response.Transaction.Id });

                    var purchase = StoreKitMapper.ToPurchase(response.Transaction);
                    Notify(OnPurchaseComplete, purchase);
                    return purchase;
                case "pending":
                    return StoreKitMapper.ToDeferredPurchase(productId);
                case "userCancelled":
                    NotifyPurchaseFailed(productId);
                    throw new InAppBillingPurchaseException(PurchaseError.UserCancelled);
                default:
                    NotifyPurchaseFailed(productId);
                    throw new InAppBillingPurchaseException(PurchaseError.GeneralError, $"Unknown purchase status: {response.Status}");
            }
        }

        async Task HandleEventAsync(StoreKitEvent storeEvent)
        {
            try
            {
                switch (storeEvent?.Kind)
                {
                    case "transaction":
                        await HandleTransactionUpdateAsync(storeEvent.Transaction);
                        break;
                    case "purchaseIntent":
                        await HandlePurchaseIntentAsync(storeEvent.ProductId);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Unable to handle StoreKit event: " + ex);
            }
        }

        static async Task HandleTransactionUpdateAsync(StoreKitTransaction transaction)
        {
            // An unverified transaction stays unfinished so that it isn't acknowledged by accident.
            if (FinishAllTransactions && transaction.Verified)
                await StoreKit2Native.FinishTransactionsAsync(new[] { transaction.Id });

            var purchase = StoreKitMapper.ToPurchase(transaction);
            Notify(purchase.State == PurchaseState.Purchased ? OnPurchaseComplete : OnPurchaseFailure, purchase);
        }

        async Task HandlePurchaseIntentAsync(string productId)
        {
            var handler = OnPurchaseIntent;
            if (handler == null)
                return;

            var shouldPurchase = false;
            DispatchQueue.MainQueue.DispatchSync(() => shouldPurchase = handler(productId));

            // The product type is not used on iOS and the result is reported through the purchase callbacks.
            if (shouldPurchase)
                await PurchaseAsync(productId, ItemType.InAppPurchase);
        }

        static void NotifyPurchaseFailed(string productId) =>
            Notify(OnPurchaseFailure, new InAppBillingPurchase
            {
                ProductId = productId,
                ProductIds = new[] { productId },
                State = PurchaseState.Failed
            });

        static void Notify(Action<InAppBillingPurchase> callback, InAppBillingPurchase purchase)
        {
            if (callback != null)
                DispatchQueue.MainQueue.DispatchAsync(() => callback(purchase));
        }

        /// <summary>
        /// (iOS not supported) Apple store manages upgrades natively when subscriptions of the same group are purchased.
        /// </summary>
        /// <exception cref="NotImplementedException">iOS not supported</exception>
        public override Task<InAppBillingPurchase> UpgradePurchasedSubscriptionAsync(string newProductId, string purchaseTokenOfOriginalSubscription, SubscriptionProrationMode prorationMode = SubscriptionProrationMode.ImmediateWithTimeProration, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException("iOS not supported. Apple store manages upgrades natively when subscriptions of the same group are purchased.");

        /// <summary>
        /// Gets the app receipt from the bundle. Apple has deprecated receipts, new server code should verify InAppBillingPurchase.PurchaseToken, which is the signed JWS transaction.
        /// </summary>
        public override string ReceiptData
        {
            get
            {
                NSData receiptUrl = null;
                if (NSBundle.MainBundle.AppStoreReceiptUrl != null)
                    receiptUrl = NSData.FromUrl(NSBundle.MainBundle.AppStoreReceiptUrl);

                return receiptUrl?.GetBase64EncodedString(NSDataBase64EncodingOptions.None);
            }
        }

        /// <summary>
        /// Consume a purchase with a purchase token.
        /// </summary>
        /// <param name="productId">Id or Sku of product</param>
        /// <param name="transactionIdentifier">Original Purchase Token</param>
        /// <returns>If consumed successful</returns>
        /// <exception cref="InAppBillingPurchaseException">If an error occurs during processing</exception>
        public override async Task<bool> ConsumePurchaseAsync(string productId, string transactionIdentifier, CancellationToken cancellationToken)
        {
            var items = await FinalizePurchaseAsync(new[] { transactionIdentifier }, cancellationToken);
            return items.FirstOrDefault().Success;
        }

        /// <summary>
        /// Finish every unfinished transaction of the given products
        /// </summary>
        /// <param name="productIds"></param>
        /// <returns></returns>
        public override async Task<IEnumerable<(string Id, bool Success)>> FinalizePurchaseOfProductAsync(string[] productIds, CancellationToken cancellationToken)
        {
            Init();
            var response = await StoreKit2Native.FinishProductsAsync(productIds).WaitAsync(cancellationToken);
            return ToFinishResults(response);
        }

        /// <summary>
        /// Finish a transaction manually
        /// </summary>
        /// <param name="transactionIdentifier"></param>
        /// <returns></returns>
        public override async Task<IEnumerable<(string Id, bool Success)>> FinalizePurchaseAsync(string[] transactionIdentifier, CancellationToken cancellationToken)
        {
            Init();
            var response = await StoreKit2Native.FinishTransactionsAsync(transactionIdentifier).WaitAsync(cancellationToken);
            return ToFinishResults(response);
        }

        static List<(string Id, bool Success)> ToFinishResults(StoreKitResponse response) =>
            response.Results?.Select(r => (r.Id, r.Success)).ToList() ?? new List<(string Id, bool Success)>();

        bool disposed = false;

        /// <summary>
        /// Dispose
        /// </summary>
        /// <param name="disposing"></param>
        public override void Dispose(bool disposing)
        {
            if (!disposed && disposing)
            {
                disposed = true;
                if (listening)
                {
                    StoreKit2Native.StopListeners();
                    listening = false;
                }
            }

            base.Dispose(disposing);
        }
    }
}
