using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// An in-memory Microsoft Store used when InTestingMode is true, because Windows.Services.Store has no simulator of its own.
    /// Nothing is persisted and no money is involved.
    /// </summary>
    public sealed class WindowsStoreSimulator : IWindowsStoreClient
    {
        sealed class Product
        {
            public string ProductId, StoreId, Kind, Title, Description, Price;
            public decimal ListPrice;
            public ItemType ItemType;
            public uint ConsumableQuantity;
            public bool HasTrial;
        }

        sealed class Ownership
        {
            public string TransactionId = Guid.NewGuid().ToString();
            public string SkuStoreId, DeveloperOfferId;
            public DateTimeOffset AcquiredDate = DateTimeOffset.UtcNow;
            public uint Balance;
        }

        readonly object gate = new();
        readonly List<Product> products = new();
        readonly Dictionary<string, Ownership> owned = new();
        readonly HashSet<(string StoreId, Guid TrackingId)> fulfillments = new();

        /// <summary>
        /// Forces the result of the next purchase request. It is cleared after that request.
        /// </summary>
        public WindowsStorePurchaseStatus? NextPurchaseStatus { get; set; }

        /// <summary>
        /// Adds a product to the simulated catalog and returns its Store ID.
        /// </summary>
        /// <param name="productId">The product ID, as entered in Partner Center.</param>
        /// <param name="itemType">The type of product.</param>
        /// <param name="name">The display name.</param>
        /// <param name="listPrice">The price, in USD.</param>
        /// <param name="storeManagedQuantity">For a consumable, the units the Store tracks per purchase. 0 makes it developer-managed.</param>
        /// <param name="hasTrial">If a subscription offers a one month trial SKU.</param>
        public string AddProduct(string productId, ItemType itemType, string name = null, decimal listPrice = 0.99m, uint storeManagedQuantity = 0, bool hasTrial = false)
        {
            if (string.IsNullOrWhiteSpace(productId))
                throw new ArgumentNullException(nameof(productId));

            lock (gate)
            {
                var product = new Product
                {
                    ProductId = productId,
                    StoreId = $"9SIM{products.Count:D8}",
                    ItemType = itemType,
                    Kind = itemType != ItemType.InAppPurchaseConsumable ? "Durable" : storeManagedQuantity > 0 ? "Consumable" : "UnmanagedConsumable",
                    Title = name ?? productId,
                    Description = $"Simulated add-on {productId}",
                    ListPrice = listPrice,
                    Price = FormatPrice(listPrice),
                    ConsumableQuantity = storeManagedQuantity,
                    HasTrial = hasTrial,
                };
                products.Add(product);
                return product.StoreId;
            }
        }

        /// <summary>
        /// Removes the catalog and all simulated purchases.
        /// </summary>
        public void Reset()
        {
            lock (gate)
            {
                products.Clear();
                owned.Clear();
                fulfillments.Clear();
                NextPurchaseStatus = null;
            }
        }

        static string FormatPrice(decimal price) => price.ToString("$0.00", System.Globalization.CultureInfo.InvariantCulture);

        WindowsStoreQueryResult Query(IReadOnlyList<string> kinds, Func<Product, bool> filter)
        {
            lock (gate)
            {
                return new WindowsStoreQueryResult { Items = products.Where(p => kinds.Contains(p.Kind) && filter(p)).Select(ToItem).ToList() };
            }
        }

        WindowsStoreItem ToItem(Product product)
        {
            owned.TryGetValue(product.StoreId, out var ownership);
            var isSubscription = product.ItemType == ItemType.Subscription;
            var offersTrial = isSubscription && product.HasTrial && ownership == null;
            var month = new SubscriptionPeriod { NumberOfUnits = 1, Unit = SubscriptionPeriodUnit.Month };

            var fullSku = new WindowsStoreSkuItem
            {
                StoreId = $"{product.StoreId}/0010",
                Title = product.Title,
                Description = product.Description,
                FormattedPrice = product.Price,
                FormattedRecurrencePrice = isSubscription ? product.Price : null,
                AvailabilityJson = WindowsStoreJson.CreateListPriceJson(product.ListPrice, "USD"),
                IsSubscription = isSubscription,
                BillingPeriod = isSubscription ? month : null,
            };

            var skus = new List<WindowsStoreSkuItem>();
            if (offersTrial)
            {
                skus.Add(new WindowsStoreSkuItem
                {
                    StoreId = $"{product.StoreId}/0020",
                    Title = product.Title,
                    Description = product.Description,
                    FormattedPrice = FormatPrice(0),
                    FormattedRecurrencePrice = product.Price,
                    AvailabilityJson = WindowsStoreJson.CreateListPriceJson(0, "USD"),
                    IsSubscription = true,
                    IsTrial = true,
                    BillingPeriod = month,
                    TrialPeriod = month,
                });
            }
            skus.Add(fullSku);

            if (ownership != null)
            {
                var sku = skus.FirstOrDefault(s => s.StoreId == ownership.SkuStoreId) ?? fullSku;
                sku.IsInUserCollection = true;
                sku.Collection = new WindowsStoreCollectionInfo
                {
                    AcquiredDate = ownership.AcquiredDate,
                    EndDate = new DateTimeOffset(9999, 12, 31, 0, 0, 0, TimeSpan.Zero),
                    DeveloperOfferId = ownership.DeveloperOfferId,
                    Json = JsonSerializer.Serialize(new Dictionary<string, object>
                    {
                        ["transactionId"] = ownership.TransactionId,
                        ["orderId"] = ownership.TransactionId,
                        ["status"] = "Active",
                        ["autoRenew"] = isSubscription,
                        ["quantity"] = Math.Max(ownership.Balance, 1u),
                        ["purchasedCountry"] = "US",
                    }),
                };
            }

            return new WindowsStoreItem
            {
                StoreId = product.StoreId,
                InAppOfferToken = product.ProductId,
                ProductKind = product.Kind,
                Title = product.Title,
                Description = product.Description,
                FormattedPrice = skus[0].FormattedPrice,
                FormattedBasePrice = product.Price,
                FormattedRecurrencePrice = isSubscription ? product.Price : null,
                CurrencyCode = "USD",
                IsInUserCollection = ownership != null,
                Skus = skus,
            };
        }

        Task<WindowsStoreQueryResult> IWindowsStoreClient.GetProductsAsync(IReadOnlyList<string> productKinds, IReadOnlyList<string> storeIds, CancellationToken cancellationToken) =>
            Task.FromResult(Query(productKinds, p => storeIds.Contains(p.StoreId)));

        Task<WindowsStoreQueryResult> IWindowsStoreClient.GetAssociatedProductsAsync(IReadOnlyList<string> productKinds, CancellationToken cancellationToken) =>
            Task.FromResult(Query(productKinds, _ => true));

        Task<WindowsStoreQueryResult> IWindowsStoreClient.GetUserCollectionAsync(IReadOnlyList<string> productKinds, CancellationToken cancellationToken) =>
            Task.FromResult(Query(productKinds, p => owned.ContainsKey(p.StoreId)));

        Task<WindowsStorePurchaseResult> IWindowsStoreClient.RequestPurchaseAsync(WindowsStoreItem item, WindowsStoreSkuItem sku, string developerOfferId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (gate)
            {
                var forced = NextPurchaseStatus;
                NextPurchaseStatus = null;
                if (forced != null && forced != WindowsStorePurchaseStatus.Succeeded)
                    return Task.FromResult(new WindowsStorePurchaseResult { Status = forced.Value });

                var product = products.First(p => p.StoreId == item.StoreId);
                owned.TryGetValue(product.StoreId, out var ownership);

                if (ownership != null && product.Kind != "Consumable")
                    return Task.FromResult(new WindowsStorePurchaseResult { Status = WindowsStorePurchaseStatus.AlreadyPurchased });

                ownership ??= new Ownership();
                ownership.SkuStoreId = (sku ?? item.Skus[0]).StoreId;
                ownership.DeveloperOfferId = developerOfferId;
                ownership.Balance += product.ConsumableQuantity;
                owned[product.StoreId] = ownership;

                return Task.FromResult(new WindowsStorePurchaseResult { Status = WindowsStorePurchaseStatus.Succeeded });
            }
        }

        Task<WindowsStoreConsumableResult> IWindowsStoreClient.ReportConsumableFulfillmentAsync(string storeId, uint quantity, Guid trackingId, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                var product = products.First(p => p.StoreId == storeId);
                owned.TryGetValue(storeId, out var ownership);

                if (!fulfillments.Contains((storeId, trackingId)))
                {
                    var available = product.Kind == "Consumable" ? ownership?.Balance ?? 0 : ownership != null ? 1u : 0u;
                    if (quantity == 0 || quantity > available)
                        return Task.FromResult(new WindowsStoreConsumableResult { Status = WindowsStoreConsumableStatus.InsufficientQuantity, Balance = available });

                    fulfillments.Add((storeId, trackingId));
                    if (product.Kind == "Consumable")
                        ownership.Balance -= quantity;
                    if (product.Kind != "Consumable" || ownership.Balance == 0)
                        owned.Remove(storeId);
                }

                owned.TryGetValue(storeId, out ownership);
                return Task.FromResult(new WindowsStoreConsumableResult { Status = WindowsStoreConsumableStatus.Succeeded, Balance = ownership?.Balance ?? 0 });
            }
        }

        Task<WindowsStoreConsumableResult> IWindowsStoreClient.GetConsumableBalanceAsync(string storeId, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                owned.TryGetValue(storeId, out var ownership);
                return Task.FromResult(new WindowsStoreConsumableResult { Status = WindowsStoreConsumableStatus.Succeeded, Balance = ownership?.Balance ?? 0 });
            }
        }
    }
}
