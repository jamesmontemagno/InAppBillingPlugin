using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Globalization;
using Windows.Services.Store;
using Windows.System;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// Microsoft Store implementation for WinUI, Windows App SDK, and other packaged desktop apps, using Windows.Services.Store.
    /// </summary>
    [Preserve(AllMembers = true)]
    public class InAppBillingImplementation : BaseInAppBilling
    {
        static readonly Uri RedeemUri = new("https://account.microsoft.com/billing/redeem");
        static readonly Lazy<bool> Supported = new(() => HasPackageIdentity() && !IsElevated());

        WindowsStoreClient client;
        WindowsStoreBilling storeBilling;
        SynchronizationContext uiContext = SynchronizationContext.Current;

        /// <summary>
        /// Gets or sets a provider for the window that owns Store dialogs. Set this for multi-window apps or if purchases report a missing window.
        /// By default the active window of the calling thread is used, and then the main window of the process.
        /// </summary>
        public static Func<IntPtr> WindowHandleProvider { get; set; }

        /// <summary>
        /// Gets or sets a callback for license changes that happen outside of the app, such as a subscription expiring. Call GetPurchasesAsync to refresh.
        /// It is only raised while connected and can arrive on any thread.
        /// </summary>
        public static Action OnWindowsLicensesChanged { get; set; }

        /// <summary>
        /// Gets the in-memory Store that is used when <see cref="InTestingMode"/> is true.
        /// </summary>
        public static WindowsStoreSimulator Simulator { get; } = new();

        static readonly WindowsStoreBilling SimulatorBilling = new(Simulator);

        /// <summary>
        /// Default constructor
        /// </summary>
        public InAppBillingImplementation()
        {
        }

        /// <summary>
        /// Gets or sets if the <see cref="Simulator"/> is used instead of the Microsoft Store.
        /// </summary>
        public override bool InTestingMode { get; set; }

        /// <summary>
        /// Gets if connected to the Store or the simulator.
        /// </summary>
        public override bool IsConnected { get; set; }

        /// <summary>
        /// Gets if the app can make purchases. The Store requires a packaged app that is not running elevated.
        /// </summary>
        public override bool CanMakePayments => InTestingMode || Supported.Value;

        /// <summary>
        /// Gets the Windows home region of the user, which is what the Store bases its market on. Id is the two-letter code.
        /// </summary>
        public override Storefront Storefront
        {
            get
            {
                try
                {
                    var region = new GeographicRegion();
                    return new Storefront { Id = region.CodeTwoLetter, CountryCode = region.CodeThreeLetter };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Unable to read the home region: {ex.Message}");
                    return null;
                }
            }
        }

        WindowsStoreBilling Billing
        {
            get
            {
                if (InTestingMode)
                {
                    IsConnected = true;
                    return SimulatorBilling;
                }

                EnsureClient();
                return storeBilling;
            }
        }

        void EnsureClient()
        {
            if (client != null)
                return;

            if (!Supported.Value)
                throw new InAppBillingPurchaseException(PurchaseError.BillingUnavailable, "The Microsoft Store requires a packaged app that is not running elevated. Set InTestingMode to use the simulator.");

            client = new WindowsStoreClient(ResolveOwnerWindow, () => OnWindowsLicensesChanged?.Invoke());
            storeBilling = new WindowsStoreBilling(client);
            IsConnected = true;
        }

        /// <summary>
        /// Prepares the Store connection. Returns false if the app can't use the Store.
        /// </summary>
        /// <returns>If Success</returns>
        public override Task<bool> ConnectAsync(bool enablePendingPurchases = true, CancellationToken cancellationToken = default)
        {
            uiContext = SynchronizationContext.Current ?? uiContext;

            if (!CanMakePayments)
            {
                IsConnected = false;
                return Task.FromResult(false);
            }

            if (!InTestingMode)
                EnsureClient();

            IsConnected = true;
            return Task.FromResult(true);
        }

        /// <summary>
        /// Releases the Store connection. The next billing call reconnects.
        /// </summary>
        /// <returns>Task to disconnect</returns>
        public override Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            ReleaseClient();
            return Task.CompletedTask;
        }

        void ReleaseClient()
        {
            client?.Dispose();
            client = null;
            storeBilling = null;
            IsConnected = false;
        }

        /// <summary>
        /// Dispose of the Store connection
        /// </summary>
        /// <param name="disposing"></param>
        public override void Dispose(bool disposing)
        {
            if (disposing)
                ReleaseClient();

            base.Dispose(disposing);
        }

        /// <summary>
        /// Gets product information. Product IDs can be Partner Center product IDs or Store IDs.
        /// </summary>
        /// <param name="itemType">Type of item</param>
        /// <param name="productIds">Product Ids</param>
        /// <returns>The products</returns>
        /// <exception cref="InAppBillingPurchaseException">If an ID is invalid and IgnoreInvalidProducts is false, or an error occurs during processing</exception>
        public override Task<IEnumerable<InAppBillingProduct>> GetProductInfoAsync(ItemType itemType, string[] productIds, CancellationToken cancellationToken = default) =>
            Billing.GetProductInfoAsync(itemType, productIds, IgnoreInvalidProducts, cancellationToken);

        /// <summary>
        /// Get the current purchases. InAppPurchase and InAppPurchaseConsumable both return all one-time purchases, as on Android.
        /// </summary>
        /// <param name="itemType">Type of product</param>
        /// <returns>The current purchases</returns>
        public override Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType, CancellationToken cancellationToken = default) =>
            Billing.GetPurchasesAsync(itemType, cancellationToken);

        /// <summary>
        /// Purchase a specific product or subscription. The Store requires this to start on the UI thread, so it moves there if it can.
        /// </summary>
        /// <param name="productId">Partner Center product ID or Store ID</param>
        /// <param name="itemType">Type of product being requested</param>
        /// <param name="obfuscatedAccountId">Stored with the purchase in the Store's developer offer ID.</param>
        /// <param name="obfuscatedProfileId">Stored with the purchase in the Store's developer offer ID.</param>
        /// <param name="subOfferToken">The Store ID of a specific SKU to buy, from WindowsExtras.Skus.</param>
        /// <returns>Purchase details</returns>
        /// <exception cref="InAppBillingPurchaseException">If the purchase does not complete</exception>
        public override Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, string obfuscatedAccountId = null, string obfuscatedProfileId = null, string subOfferToken = null, CancellationToken cancellationToken = default)
        {
            var billing = Billing;
            return RunOnUiContextAsync(() => billing.PurchaseAsync(productId, itemType, obfuscatedAccountId, obfuscatedProfileId, subOfferToken, cancellationToken));
        }

        /// <summary>
        /// (Not supported) The Microsoft Store has no client API to change a subscription. Customers manage them at https://account.microsoft.com/services.
        /// </summary>
        /// <exception cref="InAppBillingPurchaseException">Always, with PurchaseError.FeatureNotSupported</exception>
        public override Task<InAppBillingPurchase> UpgradePurchasedSubscriptionAsync(string newProductId, string purchaseTokenOfOriginalSubscription, SubscriptionProrationMode prorationMode = SubscriptionProrationMode.ImmediateWithTimeProration, CancellationToken cancellationToken = default) =>
            throw new InAppBillingPurchaseException(PurchaseError.FeatureNotSupported, "The Microsoft Store has no API to change a subscription. Customers manage subscriptions at https://account.microsoft.com/services.");

        /// <summary>
        /// Consume a purchase. A developer-managed consumable is fulfilled, and a Store-managed consumable has its whole remaining balance fulfilled.
        /// Grant the units in InAppBillingPurchase.Quantity when consuming a Store-managed consumable.
        /// </summary>
        /// <param name="productId">Partner Center product ID or Store ID</param>
        /// <param name="transactionIdentifier">The TransactionIdentifier of the purchase</param>
        /// <returns>If consumed successful</returns>
        /// <exception cref="InAppBillingPurchaseException">If an error occurs during processing</exception>
        public override Task<bool> ConsumePurchaseAsync(string productId, string transactionIdentifier, CancellationToken cancellationToken = default) =>
            Billing.ConsumePurchaseAsync(productId, transactionIdentifier, cancellationToken);

        /// <summary>
        /// Consume part of a Store-managed consumable.
        /// </summary>
        /// <param name="productId">Partner Center product ID or Store ID</param>
        /// <param name="quantity">Number of units to report as fulfilled</param>
        /// <param name="trackingId">Identifies this fulfillment. Retrying with the same value is handled exactly once by the Store, so use a new value for each fulfillment.</param>
        /// <returns>If consumed successful</returns>
        /// <exception cref="InAppBillingPurchaseException">If an error occurs during processing</exception>
        public Task<bool> ConsumePurchaseAsync(string productId, uint quantity, Guid trackingId, CancellationToken cancellationToken = default) =>
            Billing.ConsumePurchaseAsync(productId, quantity, trackingId, cancellationToken);

        /// <summary>
        /// Gets the remaining balance of a Store-managed consumable.
        /// </summary>
        /// <param name="productId">Partner Center product ID or Store ID</param>
        /// <returns>The remaining units</returns>
        /// <exception cref="InAppBillingPurchaseException">If an error occurs during processing</exception>
        public Task<uint> GetConsumableBalanceAsync(string productId, CancellationToken cancellationToken = default) =>
            Billing.GetConsumableBalanceAsync(productId, cancellationToken);

        /// <summary>
        /// The Microsoft Store completes purchases itself, so there is nothing to acknowledge and every ID reports success.
        /// </summary>
        /// <param name="transactionIdentifier">Transaction identifiers</param>
        /// <returns>Success for every ID</returns>
        public override Task<IEnumerable<(string Id, bool Success)>> FinalizePurchaseAsync(string[] transactionIdentifier, CancellationToken cancellationToken = default) =>
            Task.FromResult(transactionIdentifier.Select(id => (id, true)));

        /// <summary>
        /// The Microsoft Store completes purchases itself, so there is nothing to acknowledge and every ID reports success.
        /// </summary>
        /// <param name="productIds">Product IDs</param>
        /// <returns>Success for every ID</returns>
        public override Task<IEnumerable<(string Id, bool Success)>> FinalizePurchaseOfProductAsync(string[] productIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(productIds.Select(id => (id, true)));

        /// <summary>
        /// Opens the Microsoft account page for redeeming promotional codes.
        /// </summary>
        public override async void PresentCodeRedemption()
        {
            try
            {
                await Launcher.LaunchUriAsync(RedeemUri);
            }
            catch (Exception ex)
            {
                // This is async void, so an exception here would take down the app.
                Debug.WriteLine($"Unable to open the redeem page: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the StoreContext, associated with the owner window, for Store features outside of this API such as
        /// trials (GetAppLicenseAsync), server-side validation (GetCustomerCollectionsIdAsync), or reviews (RequestRateAndReviewAppAsync).
        /// </summary>
        /// <returns>The StoreContext</returns>
        /// <exception cref="InAppBillingPurchaseException">If the app can't use the Store</exception>
        public StoreContext GetStoreContext()
        {
            EnsureClient();
            return client.GetContext();
        }

        Task<T> RunOnUiContextAsync<T>(Func<Task<T>> action)
        {
            var context = uiContext;
            if (context == null || context == SynchronizationContext.Current)
                return action();

            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            context.Post(async _ =>
            {
                try
                {
                    completion.TrySetResult(await action());
                }
                catch (OperationCanceledException)
                {
                    completion.TrySetCanceled();
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            }, null);

            return completion.Task;
        }

        static IntPtr ResolveOwnerWindow()
        {
            var window = WindowHandleProvider?.Invoke() ?? IntPtr.Zero;
            if (window == IntPtr.Zero)
                window = GetActiveWindow();
            if (window == IntPtr.Zero)
            {
                using var process = Process.GetCurrentProcess();
                window = process.MainWindowHandle;
            }

            return window;
        }

        static bool HasPackageIdentity()
        {
            try
            {
                return Package.Current != null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"No package identity: {ex.Message}");
                return false;
            }
        }

        static bool IsElevated()
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();
    }
}
