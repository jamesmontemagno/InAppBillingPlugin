using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// The result of a Microsoft Store purchase request.
    /// </summary>
    public enum WindowsStorePurchaseStatus
    {
        /// <summary>
        /// The purchase succeeded.
        /// </summary>
        Succeeded,
        /// <summary>
        /// The user already owns the product.
        /// </summary>
        AlreadyPurchased,
        /// <summary>
        /// The purchase did not complete, for example because the user cancelled it.
        /// </summary>
        NotPurchased,
        /// <summary>
        /// The purchase failed because of a network error.
        /// </summary>
        NetworkError,
        /// <summary>
        /// The purchase failed because of a Microsoft Store server error.
        /// </summary>
        ServerError
    }

    enum WindowsStoreConsumableStatus
    {
        Succeeded,
        InsufficientQuantity,
        NetworkError,
        ServerError,
        Unknown
    }

    sealed class WindowsStoreCollectionInfo
    {
        public DateTimeOffset AcquiredDate { get; set; }
        public DateTimeOffset EndDate { get; set; }
        public bool IsTrial { get; set; }
        public string DeveloperOfferId { get; set; }
        public string CampaignId { get; set; }
        public string Json { get; set; }
    }

    sealed class WindowsStoreSkuItem
    {
        public string StoreId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string CustomDeveloperData { get; set; }
        public string FormattedPrice { get; set; }
        public string FormattedRecurrencePrice { get; set; }
        public string AvailabilityJson { get; set; }
        public bool IsSubscription { get; set; }
        public bool IsTrial { get; set; }
        public bool IsInUserCollection { get; set; }
        public SubscriptionPeriod BillingPeriod { get; set; }
        public SubscriptionPeriod TrialPeriod { get; set; }
        public WindowsStoreCollectionInfo Collection { get; set; }
        public object Native { get; set; }
    }

    sealed class WindowsStoreItem
    {
        public string StoreId { get; set; }
        public string InAppOfferToken { get; set; }
        public string ProductKind { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string FormattedPrice { get; set; }
        public string FormattedBasePrice { get; set; }
        public string FormattedRecurrencePrice { get; set; }
        public string CurrencyCode { get; set; }
        public bool IsOnSale { get; set; }
        public DateTimeOffset SaleEndDate { get; set; }
        public Uri ImageUri { get; set; }
        public Uri LinkUri { get; set; }
        public IReadOnlyList<string> Keywords { get; set; } = Array.Empty<string>();
        public bool IsInUserCollection { get; set; }
        public IReadOnlyList<WindowsStoreSkuItem> Skus { get; set; } = Array.Empty<WindowsStoreSkuItem>();
        public object Native { get; set; }
    }

    sealed class WindowsStoreQueryResult
    {
        public IReadOnlyList<WindowsStoreItem> Items { get; set; } = Array.Empty<WindowsStoreItem>();
        public Exception ExtendedError { get; set; }
    }

    sealed class WindowsStorePurchaseResult
    {
        public WindowsStorePurchaseStatus Status { get; set; }
        public Exception ExtendedError { get; set; }
    }

    sealed class WindowsStoreConsumableResult
    {
        public WindowsStoreConsumableStatus Status { get; set; }
        public uint Balance { get; set; }
        public Exception ExtendedError { get; set; }
    }

    /// <summary>
    /// The Store operations the billing logic needs, so the logic can run against the real Store or the simulator.
    /// </summary>
    interface IWindowsStoreClient
    {
        Task<WindowsStoreQueryResult> GetProductsAsync(IReadOnlyList<string> productKinds, IReadOnlyList<string> storeIds, CancellationToken cancellationToken);
        Task<WindowsStoreQueryResult> GetAssociatedProductsAsync(IReadOnlyList<string> productKinds, CancellationToken cancellationToken);
        Task<WindowsStoreQueryResult> GetUserCollectionAsync(IReadOnlyList<string> productKinds, CancellationToken cancellationToken);
        Task<WindowsStorePurchaseResult> RequestPurchaseAsync(WindowsStoreItem item, WindowsStoreSkuItem sku, string developerOfferId, CancellationToken cancellationToken);
        Task<WindowsStoreConsumableResult> ReportConsumableFulfillmentAsync(string storeId, uint quantity, Guid trackingId, CancellationToken cancellationToken);
        Task<WindowsStoreConsumableResult> GetConsumableBalanceAsync(string storeId, CancellationToken cancellationToken);
    }

    /// <summary>
    /// The fields of StoreCollectionData.ExtendedJsonData that the StoreContext API does not expose as properties.
    /// See https://learn.microsoft.com/windows/uwp/monetize/data-schemas-for-store-products
    /// </summary>
    sealed class WindowsStoreCollectionJson
    {
        public string TransactionId { get; private set; }
        public string OrderId { get; private set; }
        public string Status { get; private set; }
        public string PurchasedCountry { get; private set; }
        public bool AutoRenew { get; private set; }
        public int? Quantity { get; private set; }

        public static WindowsStoreCollectionJson Parse(string json)
        {
            var result = new WindowsStoreCollectionJson();
            if (string.IsNullOrWhiteSpace(json))
                return result;

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return result;

                result.TransactionId = GetString(root, "transactionId");
                result.OrderId = GetString(root, "orderId");
                result.Status = GetString(root, "status");
                result.PurchasedCountry = GetString(root, "purchasedCountry");

                if (root.TryGetProperty("autoRenew", out var autoRenew) && autoRenew.ValueKind == JsonValueKind.True)
                    result.AutoRenew = true;

                if (root.TryGetProperty("quantity", out var quantity) && quantity.ValueKind == JsonValueKind.Number && quantity.TryGetInt32(out var value))
                    result.Quantity = value;
            }
            catch (JsonException)
            {
                // The JSON only adds detail to a purchase the Store already reported.
            }

            return result;
        }

        static string GetString(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    static class WindowsStoreJson
    {
        /// <summary>
        /// Reads the price in micro-units from StoreAvailability.ExtendedJsonData.
        /// The typed unformatted prices are only in Windows SDK 22621 and newer.
        /// </summary>
        public static long GetMicrosPrice(string availabilityJson)
        {
            if (string.IsNullOrWhiteSpace(availabilityJson))
                return 0;

            try
            {
                using var document = JsonDocument.Parse(availabilityJson);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("OrderManagementData", out var orderData)
                    && orderData.ValueKind == JsonValueKind.Object
                    && orderData.TryGetProperty("Price", out var price)
                    && price.ValueKind == JsonValueKind.Object
                    && price.TryGetProperty("ListPrice", out var listPrice)
                    && listPrice.ValueKind == JsonValueKind.Number
                    && listPrice.TryGetDecimal(out var amount))
                {
                    return decimal.ToInt64(decimal.Round(amount * 1_000_000m, MidpointRounding.AwayFromZero));
                }
            }
            catch (JsonException)
            {
            }
            catch (OverflowException)
            {
            }

            return 0;
        }

        public static string CreatePurchaseJson(string developerOfferId) =>
            JsonSerializer.Serialize(new Dictionary<string, string> { ["DevOfferId"] = developerOfferId });

        public static string CreateListPriceJson(decimal price, string currencyCode) =>
            string.Create(CultureInfo.InvariantCulture, $"{{\"OrderManagementData\":{{\"Price\":{{\"ListPrice\":{price},\"CurrencyCode\":{JsonSerializer.Serialize(currencyCode)}}}}}}}");
    }
}
