using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Services.Store;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// Connects the billing logic to Windows.Services.Store.
    /// </summary>
    sealed class WindowsStoreClient : IWindowsStoreClient, IDisposable
    {
        const uint PageSize = 25;

        readonly Func<IntPtr> ownerWindow;
        readonly Action licensesChanged;
        readonly StoreContext context = StoreContext.GetDefault();
        IntPtr initializedWindow;

        public WindowsStoreClient(Func<IntPtr> ownerWindow, Action licensesChanged)
        {
            this.ownerWindow = ownerWindow;
            this.licensesChanged = licensesChanged;
            context.OfflineLicensesChanged += OnOfflineLicensesChanged;
        }

        void OnOfflineLicensesChanged(StoreContext sender, object args) => licensesChanged?.Invoke();

        public void Dispose() => context.OfflineLicensesChanged -= OnOfflineLicensesChanged;

        /// <summary>
        /// Gets the context, associated with the owner window that desktop apps need for Store dialogs.
        /// </summary>
        public StoreContext GetContext(bool requireWindow = false)
        {
            var window = ownerWindow();
            if (window == IntPtr.Zero)
            {
                return !requireWindow
                    ? context
                    : throw new InAppBillingPurchaseException(PurchaseError.DeveloperError, "No window was found to own the Microsoft Store dialog. Show a window first or set InAppBillingImplementation.WindowHandleProvider.");
            }

            if (window != initializedWindow)
            {
                try
                {
                    WinRT.Interop.InitializeWithWindow.Initialize(context, window);
                    initializedWindow = window;
                }
                catch (Exception ex)
                {
                    // CsWinRT surfaces HRESULTs as many exception types. The earlier owner stays in place, and the Store reports a problem if it is no longer valid.
                    System.Diagnostics.Debug.WriteLine($"Unable to set the Store owner window: {ex.Message}");
                }
            }

            return context;
        }

        public async Task<WindowsStoreQueryResult> GetProductsAsync(IReadOnlyList<string> productKinds, IReadOnlyList<string> storeIds, CancellationToken cancellationToken)
        {
            var result = await GetContext().GetStoreProductsAsync(productKinds, storeIds).AsTask(cancellationToken).ConfigureAwait(true);
            return new WindowsStoreQueryResult { Items = result.Products.Values.Select(ToItem).ToList(), ExtendedError = result.ExtendedError };
        }

        public async Task<WindowsStoreQueryResult> GetAssociatedProductsAsync(IReadOnlyList<string> productKinds, CancellationToken cancellationToken)
        {
            var page = await GetContext().GetAssociatedStoreProductsWithPagingAsync(productKinds, PageSize).AsTask(cancellationToken).ConfigureAwait(true);
            return await ReadAllPagesAsync(page, cancellationToken).ConfigureAwait(true);
        }

        public async Task<WindowsStoreQueryResult> GetUserCollectionAsync(IReadOnlyList<string> productKinds, CancellationToken cancellationToken)
        {
            var page = await GetContext().GetUserCollectionWithPagingAsync(productKinds, PageSize).AsTask(cancellationToken).ConfigureAwait(true);
            return await ReadAllPagesAsync(page, cancellationToken).ConfigureAwait(true);
        }

        static async Task<WindowsStoreQueryResult> ReadAllPagesAsync(StoreProductPagedQueryResult page, CancellationToken cancellationToken)
        {
            var items = new List<WindowsStoreItem>();
            while (true)
            {
                if (page.ExtendedError != null)
                    return new WindowsStoreQueryResult { Items = items, ExtendedError = page.ExtendedError };

                items.AddRange(page.Products.Values.Select(ToItem));

                if (!page.HasMoreResults)
                    return new WindowsStoreQueryResult { Items = items };

                page = await page.GetNextAsync().AsTask(cancellationToken).ConfigureAwait(true);
            }
        }

        public async Task<WindowsStorePurchaseResult> RequestPurchaseAsync(WindowsStoreItem item, WindowsStoreSkuItem sku, string developerOfferId, CancellationToken cancellationToken)
        {
            GetContext(requireWindow: true);

            var properties = new StorePurchaseProperties();
            if (!string.IsNullOrEmpty(developerOfferId))
                properties.ExtendedJsonData = WindowsStoreJson.CreatePurchaseJson(developerOfferId);

            var operation = sku == null
                ? ((StoreProduct)item.Native).RequestPurchaseAsync(properties)
                : ((StoreSku)sku.Native).RequestPurchaseAsync(properties);

            var result = await operation.AsTask(cancellationToken).ConfigureAwait(true);

            return new WindowsStorePurchaseResult
            {
                ExtendedError = result.ExtendedError,
                Status = result.Status switch
                {
                    StorePurchaseStatus.Succeeded => WindowsStorePurchaseStatus.Succeeded,
                    StorePurchaseStatus.AlreadyPurchased => WindowsStorePurchaseStatus.AlreadyPurchased,
                    StorePurchaseStatus.NetworkError => WindowsStorePurchaseStatus.NetworkError,
                    StorePurchaseStatus.ServerError => WindowsStorePurchaseStatus.ServerError,
                    _ => WindowsStorePurchaseStatus.NotPurchased,
                },
            };
        }

        public async Task<WindowsStoreConsumableResult> ReportConsumableFulfillmentAsync(string storeId, uint quantity, Guid trackingId, CancellationToken cancellationToken) =>
            ToResult(await GetContext().ReportConsumableFulfillmentAsync(storeId, quantity, trackingId).AsTask(cancellationToken).ConfigureAwait(true));

        public async Task<WindowsStoreConsumableResult> GetConsumableBalanceAsync(string storeId, CancellationToken cancellationToken) =>
            ToResult(await GetContext().GetConsumableBalanceRemainingAsync(storeId).AsTask(cancellationToken).ConfigureAwait(true));

        static WindowsStoreConsumableResult ToResult(StoreConsumableResult result) => new()
        {
            Balance = result.BalanceRemaining,
            ExtendedError = result.ExtendedError,
            Status = result.Status switch
            {
                StoreConsumableStatus.Succeeded => WindowsStoreConsumableStatus.Succeeded,
                StoreConsumableStatus.InsufficentQuantity => WindowsStoreConsumableStatus.InsufficientQuantity,
                StoreConsumableStatus.NetworkError => WindowsStoreConsumableStatus.NetworkError,
                StoreConsumableStatus.ServerError => WindowsStoreConsumableStatus.ServerError,
                _ => WindowsStoreConsumableStatus.Unknown,
            },
        };

        static WindowsStoreItem ToItem(StoreProduct product) => new()
        {
            StoreId = product.StoreId,
            InAppOfferToken = product.InAppOfferToken,
            ProductKind = product.ProductKind,
            Title = product.Title,
            Description = product.Description,
            FormattedPrice = product.Price?.FormattedPrice,
            FormattedBasePrice = product.Price?.FormattedBasePrice,
            FormattedRecurrencePrice = product.Price?.FormattedRecurrencePrice,
            CurrencyCode = product.Price?.CurrencyCode,
            IsOnSale = product.Price?.IsOnSale ?? false,
            SaleEndDate = product.Price?.SaleEndDate ?? default,
            ImageUri = product.Images?.FirstOrDefault()?.Uri,
            LinkUri = product.LinkUri,
            Keywords = product.Keywords?.ToList() ?? new List<string>(),
            IsInUserCollection = product.IsInUserCollection,
            Skus = product.Skus?.Select(ToSku).ToList() ?? new List<WindowsStoreSkuItem>(),
            Native = product,
        };

        static WindowsStoreSkuItem ToSku(StoreSku sku)
        {
            var subscription = sku.IsSubscription ? sku.SubscriptionInfo : null;
            var collection = sku.CollectionData;

            return new WindowsStoreSkuItem
            {
                StoreId = sku.StoreId,
                Title = sku.Title,
                Description = sku.Description,
                CustomDeveloperData = sku.CustomDeveloperData,
                FormattedPrice = sku.Price?.FormattedPrice,
                FormattedRecurrencePrice = sku.Price?.FormattedRecurrencePrice,
                AvailabilityJson = sku.Availabilities?.FirstOrDefault()?.ExtendedJsonData,
                IsSubscription = sku.IsSubscription,
                IsTrial = sku.IsTrial,
                IsInUserCollection = sku.IsInUserCollection,
                BillingPeriod = subscription == null ? null : ToPeriod(subscription.BillingPeriod, subscription.BillingPeriodUnit),
                TrialPeriod = subscription?.HasTrialPeriod == true ? ToPeriod(subscription.TrialPeriod, subscription.TrialPeriodUnit) : null,
                Collection = collection == null ? null : new WindowsStoreCollectionInfo
                {
                    AcquiredDate = collection.AcquiredDate,
                    EndDate = collection.EndDate,
                    IsTrial = collection.IsTrial,
                    DeveloperOfferId = collection.DeveloperOfferId,
                    CampaignId = collection.CampaignId,
                    Json = collection.ExtendedJsonData,
                },
                Native = sku,
            };
        }

        static SubscriptionPeriod ToPeriod(uint units, StoreDurationUnit unit) => new()
        {
            NumberOfUnits = (int)units,
            Unit = unit switch
            {
                StoreDurationUnit.Day => SubscriptionPeriodUnit.Day,
                StoreDurationUnit.Week => SubscriptionPeriodUnit.Week,
                StoreDurationUnit.Month => SubscriptionPeriodUnit.Month,
                StoreDurationUnit.Year => SubscriptionPeriodUnit.Year,
                _ => SubscriptionPeriodUnit.Unknown,
            },
        };
    }
}
