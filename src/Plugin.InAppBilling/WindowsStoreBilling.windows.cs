using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// Maps the cross-platform billing API onto Microsoft Store operations.
    /// </summary>
    sealed partial class WindowsStoreBilling
    {
        const string Durable = "Durable";
        const string StoreManagedConsumable = "Consumable";
        const string DeveloperManagedConsumable = "UnmanagedConsumable";

        const uint StoreUnknownApp = 0x803F6107;
        const uint InvalidWindowHandle = 0x80070578;

        static readonly string[] OneTimeKinds = { Durable, StoreManagedConsumable, DeveloperManagedConsumable };
        static readonly string[] SubscriptionKinds = { Durable };
        static readonly string[] ConsumableKinds = { StoreManagedConsumable, DeveloperManagedConsumable };

        static readonly Regex StoreIdPattern = new("^[0-9A-Z]{12}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        readonly IWindowsStoreClient client;
        int purchaseInProgress;

        public WindowsStoreBilling(IWindowsStoreClient client) => this.client = client;

        static IReadOnlyList<string> KindsFor(ItemType itemType) =>
            itemType == ItemType.Subscription ? SubscriptionKinds : OneTimeKinds;

        static bool IsSubscription(WindowsStoreItem item) => item.Skus.Any(s => s.IsSubscription);

        static bool Matches(WindowsStoreItem item, ItemType itemType) =>
            IsSubscription(item) == (itemType == ItemType.Subscription);

        internal static InAppBillingPurchaseException ToException(Exception error, PurchaseError fallback) =>
            unchecked((uint)error.HResult) switch
            {
                StoreUnknownApp => new InAppBillingPurchaseException(PurchaseError.DeveloperError, "The Microsoft Store does not know this app (0x803F6107). Associate the project with your app in Partner Center and install it from the Store once."),
                InvalidWindowHandle => new InAppBillingPurchaseException(PurchaseError.DeveloperError, "The Store dialog has no valid owner window (0x80070578). Set InAppBillingImplementation.WindowHandleProvider."),
                _ => new InAppBillingPurchaseException(fallback, error),
            };

        static async Task<T> CallAsync<T>(Func<Task<T>> call, PurchaseError fallback)
        {
            try
            {
                return await call().ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is not InAppBillingPurchaseException and not OperationCanceledException)
            {
                throw ToException(ex, fallback);
            }
        }

        static WindowsStoreQueryResult EnsureSucceeded(WindowsStoreQueryResult result, PurchaseError fallback) =>
            result.ExtendedError == null ? result : throw ToException(result.ExtendedError, fallback);

        async Task<Dictionary<string, WindowsStoreItem>> ResolveAsync(IReadOnlyCollection<string> productIds, IReadOnlyList<string> kinds, CancellationToken cancellationToken)
        {
            var resolved = new Dictionary<string, WindowsStoreItem>();

            var storeIds = productIds.Where(id => id != null && StoreIdPattern.IsMatch(id)).Distinct().ToList();
            if (storeIds.Count > 0)
            {
                var query = EnsureSucceeded(await CallAsync(() => client.GetProductsAsync(kinds, storeIds, cancellationToken), PurchaseError.ProductRequestFailed).ConfigureAwait(true), PurchaseError.ProductRequestFailed);
                foreach (var id in storeIds)
                {
                    var item = query.Items.FirstOrDefault(i => i.StoreId == id);
                    if (item != null)
                        resolved[id] = item;
                }
            }

            // Everything else is a Partner Center product ID, which only the associated product list can resolve on this SDK.
            var remaining = productIds.Where(id => id != null && !resolved.ContainsKey(id)).Distinct().ToList();
            if (remaining.Count > 0)
            {
                var query = EnsureSucceeded(await CallAsync(() => client.GetAssociatedProductsAsync(kinds, cancellationToken), PurchaseError.ProductRequestFailed).ConfigureAwait(true), PurchaseError.ProductRequestFailed);
                foreach (var id in remaining)
                {
                    var item = query.Items.FirstOrDefault(i => string.Equals(i.InAppOfferToken, id, StringComparison.OrdinalIgnoreCase) || i.StoreId == id);
                    if (item != null)
                        resolved[id] = item;
                }
            }

            return resolved;
        }
    }
}
