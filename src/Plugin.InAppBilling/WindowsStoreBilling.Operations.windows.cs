using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Plugin.InAppBilling
{
    sealed partial class WindowsStoreBilling
    {
        public async Task<IEnumerable<InAppBillingProduct>> GetProductInfoAsync(ItemType itemType, string[] productIds, bool ignoreInvalidProducts, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(productIds);

            var resolved = await ResolveAsync(productIds, KindsFor(itemType), cancellationToken).ConfigureAwait(true);
            var invalid = productIds.Where(id => id == null || !resolved.TryGetValue(id, out var item) || !Matches(item, itemType)).ToArray();

            if (invalid.Length > 0 && !ignoreInvalidProducts)
                throw new InAppBillingPurchaseException(PurchaseError.InvalidProduct, $"Invalid products: {string.Join(", ", invalid)}", invalid);

            return productIds.Except(invalid).Distinct().Select(id => ToProduct(resolved[id], id)).ToList();
        }

        public async Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType, CancellationToken cancellationToken)
        {
            var collection = EnsureSucceeded(await CallAsync(() => client.GetUserCollectionAsync(KindsFor(itemType), cancellationToken), PurchaseError.RestoreFailed).ConfigureAwait(true), PurchaseError.RestoreFailed);

            var purchases = new List<InAppBillingPurchase>();
            foreach (var item in collection.Items.Where(i => Matches(i, itemType)))
            {
                var purchase = await ToCurrentPurchaseAsync(item, PreferredId(item), cancellationToken).ConfigureAwait(true);
                if (purchase != null)
                    purchases.Add(purchase);
            }

            return purchases;
        }

        async Task<InAppBillingPurchase> ToCurrentPurchaseAsync(WindowsStoreItem item, string productId, CancellationToken cancellationToken)
        {
            var purchase = ToPurchase(item, productId, DateTimeOffset.UtcNow);
            if (purchase == null || item.ProductKind != StoreManagedConsumable)
                return purchase;

            var balance = await CallAsync(() => client.GetConsumableBalanceAsync(item.StoreId, cancellationToken), PurchaseError.RestoreFailed).ConfigureAwait(true);
            if (balance.Status != WindowsStoreConsumableStatus.Succeeded)
                return purchase;

            // A fully consumed Store-managed consumable is no longer a current purchase.
            if (balance.Balance == 0)
                return null;

            purchase.Quantity = (int)Math.Min(balance.Balance, int.MaxValue);
            return purchase;
        }

        public async Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, string obfuscatedAccountId, string obfuscatedProfileId, string subOfferToken, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(productId))
                throw new ArgumentNullException(nameof(productId));

            if (!string.IsNullOrWhiteSpace(obfuscatedProfileId) && string.IsNullOrWhiteSpace(obfuscatedAccountId))
                throw new ArgumentNullException(nameof(obfuscatedAccountId), "You must set an account id if you are setting a profile id");

            if (Interlocked.Exchange(ref purchaseInProgress, 1) == 1)
                throw new InvalidOperationException("Purchase task is already running. Please wait or cancel previous request");

            try
            {
                var resolved = await ResolveAsync(new[] { productId }, KindsFor(itemType), cancellationToken).ConfigureAwait(true);
                if (!resolved.TryGetValue(productId, out var item) || !Matches(item, itemType))
                    throw new InAppBillingPurchaseException(PurchaseError.InvalidProduct, $"Invalid product: {productId}", new[] { productId });

                WindowsStoreSkuItem sku = null;
                if (!string.IsNullOrWhiteSpace(subOfferToken))
                {
                    sku = item.Skus.FirstOrDefault(s => s.StoreId == subOfferToken)
                        ?? throw new InAppBillingPurchaseException(PurchaseError.InvalidProduct, $"{productId} has no SKU {subOfferToken}", new[] { subOfferToken });
                }

                var developerOfferId = JoinDeveloperOfferId(obfuscatedAccountId, obfuscatedProfileId);
                var result = await CallAsync(() => client.RequestPurchaseAsync(item, sku, developerOfferId, cancellationToken), PurchaseError.GeneralError).ConfigureAwait(true);
                ThrowIfNotPurchased(result);

                var purchase = await FindPurchaseAsync(item, productId, cancellationToken).ConfigureAwait(true)
                    ?? CreatePurchase(item, sku ?? item.Skus.FirstOrDefault(), productId, Guid.NewGuid().ToString(), DateTime.UtcNow);

                // Reflect what was just requested even if the collection has not caught up yet.
                purchase.ObfuscatedAccountId ??= string.IsNullOrWhiteSpace(obfuscatedAccountId) ? null : obfuscatedAccountId;
                purchase.ObfuscatedProfileId ??= string.IsNullOrWhiteSpace(obfuscatedProfileId) ? null : obfuscatedProfileId;
                return purchase;
            }
            finally
            {
                Volatile.Write(ref purchaseInProgress, 0);
            }
        }

        static void ThrowIfNotPurchased(WindowsStorePurchaseResult result)
        {
            switch (result.Status)
            {
                case WindowsStorePurchaseStatus.Succeeded:
                    return;
                case WindowsStorePurchaseStatus.AlreadyPurchased:
                    throw new InAppBillingPurchaseException(PurchaseError.AlreadyOwned);
                case WindowsStorePurchaseStatus.NetworkError:
                    throw result.ExtendedError == null ? new InAppBillingPurchaseException(PurchaseError.NetworkError) : ToException(result.ExtendedError, PurchaseError.NetworkError);
                case WindowsStorePurchaseStatus.ServerError:
                    throw result.ExtendedError == null ? new InAppBillingPurchaseException(PurchaseError.ServiceUnavailable) : ToException(result.ExtendedError, PurchaseError.ServiceUnavailable);
                default:
                    // The Store reports a cancelled dialog and a failed purchase with the same status.
                    throw result.ExtendedError == null ? new InAppBillingPurchaseException(PurchaseError.UserCancelled) : ToException(result.ExtendedError, PurchaseError.UserCancelled);
            }
        }

        // The purchase already succeeded, so a failed refresh must not be reported as a failed purchase.
        async Task<InAppBillingPurchase> FindPurchaseAsync(WindowsStoreItem item, string productId, CancellationToken cancellationToken)
        {
            try
            {
                var collection = await client.GetUserCollectionAsync(new[] { item.ProductKind }, cancellationToken).ConfigureAwait(true);
                var owned = collection.ExtendedError == null ? collection.Items.FirstOrDefault(i => i.StoreId == item.StoreId) : null;
                return owned == null ? null : await ToCurrentPurchaseAsync(owned, productId, cancellationToken).ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"Unable to refresh purchases: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> ConsumePurchaseAsync(string productId, string transactionIdentifier, CancellationToken cancellationToken)
        {
            var item = await ResolveConsumableAsync(productId, cancellationToken).ConfigureAwait(true);

            if (item.ProductKind != StoreManagedConsumable)
                return await ReportFulfillmentAsync(item.StoreId, 1, ToTrackingId(transactionIdentifier), cancellationToken).ConfigureAwait(true);

            // The balance is re-read on every call, so a retry can't report units twice and needs no stable tracking ID.
            var balance = await GetBalanceAsync(item.StoreId, cancellationToken).ConfigureAwait(true);
            if (balance == 0)
                throw new InAppBillingPurchaseException(PurchaseError.NotOwned);

            return await ReportFulfillmentAsync(item.StoreId, balance, Guid.NewGuid(), cancellationToken).ConfigureAwait(true);
        }

        public async Task<bool> ConsumePurchaseAsync(string productId, uint quantity, Guid trackingId, CancellationToken cancellationToken)
        {
            var item = await ResolveConsumableAsync(productId, cancellationToken).ConfigureAwait(true);
            return await ReportFulfillmentAsync(item.StoreId, quantity, trackingId, cancellationToken).ConfigureAwait(true);
        }

        public async Task<uint> GetConsumableBalanceAsync(string productId, CancellationToken cancellationToken)
        {
            var item = await ResolveConsumableAsync(productId, cancellationToken).ConfigureAwait(true);
            return await GetBalanceAsync(item.StoreId, cancellationToken).ConfigureAwait(true);
        }

        async Task<WindowsStoreItem> ResolveConsumableAsync(string productId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(productId))
                throw new ArgumentNullException(nameof(productId));

            var resolved = await ResolveAsync(new[] { productId }, ConsumableKinds, cancellationToken).ConfigureAwait(true);
            return resolved.TryGetValue(productId, out var item)
                ? item
                : throw new InAppBillingPurchaseException(PurchaseError.InvalidProduct, $"{productId} is not a consumable add-on", new[] { productId });
        }

        async Task<uint> GetBalanceAsync(string storeId, CancellationToken cancellationToken)
        {
            var result = await CallAsync(() => client.GetConsumableBalanceAsync(storeId, cancellationToken), PurchaseError.GeneralError).ConfigureAwait(true);
            EnsureConsumableSucceeded(result);
            return result.Balance;
        }

        async Task<bool> ReportFulfillmentAsync(string storeId, uint quantity, Guid trackingId, CancellationToken cancellationToken)
        {
            var result = await CallAsync(() => client.ReportConsumableFulfillmentAsync(storeId, quantity, trackingId, cancellationToken), PurchaseError.GeneralError).ConfigureAwait(true);
            EnsureConsumableSucceeded(result);
            return true;
        }

        static void EnsureConsumableSucceeded(WindowsStoreConsumableResult result)
        {
            var error = result.Status switch
            {
                WindowsStoreConsumableStatus.Succeeded => (PurchaseError?)null,
                WindowsStoreConsumableStatus.InsufficientQuantity => PurchaseError.NotOwned,
                WindowsStoreConsumableStatus.NetworkError => PurchaseError.NetworkError,
                WindowsStoreConsumableStatus.ServerError => PurchaseError.ServiceUnavailable,
                _ => PurchaseError.GeneralError,
            };

            if (error != null)
                throw result.ExtendedError == null ? new InAppBillingPurchaseException(error.Value) : ToException(result.ExtendedError, error.Value);
        }

        // The same transaction always maps to the same tracking ID, which makes the Store treat a retry as one fulfillment.
        internal static Guid ToTrackingId(string transactionIdentifier)
        {
            if (string.IsNullOrWhiteSpace(transactionIdentifier))
                return Guid.NewGuid();

            return Guid.TryParse(transactionIdentifier, out var guid)
                ? guid
                : new Guid(MD5.HashData(Encoding.UTF8.GetBytes(transactionIdentifier)));
        }
    }
}
