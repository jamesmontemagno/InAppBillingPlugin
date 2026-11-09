using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Plugin.InAppBilling.Tests;

public class WindowsStoreDataTests
{
    const string TransactionId = "b2f1c3a0-1111-4222-8333-444455556666";
    const string OrderId = "aaaaaaaa-1111-4222-8333-444455556666";

    sealed class ScriptedStore : IWindowsStoreClient
    {
        public List<WindowsStoreItem> Items { get; } = new();
        public Exception QueryError { get; set; }
        public Exception ThrownByCollection { get; set; }
        public WindowsStorePurchaseResult PurchaseResult { get; set; } = new() { Status = WindowsStorePurchaseStatus.Succeeded };
        public List<(uint Quantity, Guid TrackingId)> Fulfillments { get; } = new();
        public List<string> AssociatedQueries { get; } = new();

        public Task<WindowsStoreQueryResult> GetProductsAsync(IReadOnlyList<string> kinds, IReadOnlyList<string> storeIds, CancellationToken token) =>
            Task.FromResult(new WindowsStoreQueryResult { Items = Items.Where(i => storeIds.Contains(i.StoreId)).ToList(), ExtendedError = QueryError });

        public Task<WindowsStoreQueryResult> GetAssociatedProductsAsync(IReadOnlyList<string> kinds, CancellationToken token)
        {
            AssociatedQueries.Add(string.Join(",", kinds));
            return Task.FromResult(new WindowsStoreQueryResult { Items = Items, ExtendedError = QueryError });
        }

        public Task<WindowsStoreQueryResult> GetUserCollectionAsync(IReadOnlyList<string> kinds, CancellationToken token) =>
            ThrownByCollection != null ? throw ThrownByCollection : Task.FromResult(new WindowsStoreQueryResult { Items = Items, ExtendedError = QueryError });

        public Task<WindowsStorePurchaseResult> RequestPurchaseAsync(WindowsStoreItem item, WindowsStoreSkuItem sku, string developerOfferId, CancellationToken token) =>
            Task.FromResult(PurchaseResult);

        public Task<WindowsStoreConsumableResult> ReportConsumableFulfillmentAsync(string storeId, uint quantity, Guid trackingId, CancellationToken token)
        {
            Fulfillments.Add((quantity, trackingId));
            return Task.FromResult(new WindowsStoreConsumableResult { Status = WindowsStoreConsumableStatus.Succeeded });
        }

        public Task<WindowsStoreConsumableResult> GetConsumableBalanceAsync(string storeId, CancellationToken token) =>
            Task.FromResult(new WindowsStoreConsumableResult { Status = WindowsStoreConsumableStatus.ServerError });
    }

    sealed class StoreError : Exception
    {
        public StoreError(uint hresult) : base("store error") => HResult = unchecked((int)hresult);
    }

    static string CollectionJson(string status = "Active") => $$"""
        {
          "acquiredDate": "2017-02-27T13:34:57.6680551-08:00",
          "autoRenew": true,
          "endDate": "9999-12-31T15:59:59.9999999-08:00",
          "itemId": "0ae08fb4326a413a890c957b52aeda88",
          "orderId": "{{OrderId}}",
          "purchasedCountry": "DE",
          "quantity": 3,
          "status": "{{status}}",
          "transactionId": "{{TransactionId}}"
        }
        """;

    static WindowsStoreItem OwnedItem(string kind = "Durable", string json = null, DateTimeOffset? endDate = null, string offerId = null) => new()
    {
        StoreId = "9NBLGGH4R315",
        InAppOfferToken = "gold",
        ProductKind = kind,
        Skus = new[]
        {
            new WindowsStoreSkuItem
            {
                StoreId = "9NBLGGH4R315/0010",
                IsInUserCollection = true,
                Collection = new WindowsStoreCollectionInfo
                {
                    AcquiredDate = new DateTimeOffset(2017, 2, 27, 21, 34, 57, TimeSpan.Zero),
                    EndDate = endDate ?? new DateTimeOffset(9999, 12, 31, 0, 0, 0, TimeSpan.Zero),
                    DeveloperOfferId = offerId,
                    Json = json ?? CollectionJson(),
                },
            },
        },
    };

    [Fact]
    public async Task Purchase_maps_the_documented_collection_schema()
    {
        var store = new ScriptedStore();
        store.Items.Add(OwnedItem());

        var purchase = Assert.Single(await new WindowsStoreBilling(store).GetPurchasesAsync(ItemType.InAppPurchase, default));

        Assert.Equal(OrderId, purchase.Id);
        Assert.Equal(TransactionId, purchase.TransactionIdentifier);
        Assert.Equal(TransactionId, purchase.PurchaseToken);
        Assert.True(purchase.AutoRenewing);
        Assert.Equal(3, purchase.Quantity);
        Assert.Equal("gold", purchase.ProductId);
        Assert.Contains("9NBLGGH4R315", purchase.ProductIds);
        Assert.Equal(new DateTime(2017, 2, 27, 21, 34, 57, DateTimeKind.Utc), purchase.TransactionDateUtc);
        Assert.Null(purchase.WindowsExtras.ExpirationDateUtc);
        Assert.Equal("DE", purchase.WindowsExtras.PurchasedCountry);
    }

    [Fact]
    public async Task Expired_or_revoked_entitlements_are_not_current_purchases()
    {
        var store = new ScriptedStore();
        store.Items.Add(OwnedItem(endDate: DateTimeOffset.UtcNow.AddDays(-1)));
        store.Items.Add(OwnedItem(json: CollectionJson("Revoked")));

        Assert.Empty(await new WindowsStoreBilling(store).GetPurchasesAsync(ItemType.InAppPurchase, default));
    }

    [Fact]
    public async Task Future_end_date_is_reported_as_expiration()
    {
        var end = new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var store = new ScriptedStore();
        store.Items.Add(OwnedItem(endDate: end));

        var purchase = Assert.Single(await new WindowsStoreBilling(store).GetPurchasesAsync(ItemType.InAppPurchase, default));

        Assert.Equal(end.UtcDateTime, purchase.WindowsExtras.ExpirationDateUtc);
    }

    [Fact]
    public async Task Unusable_collection_json_still_produces_a_purchase()
    {
        var store = new ScriptedStore();
        store.Items.Add(OwnedItem(json: "not json"));

        var purchase = Assert.Single(await new WindowsStoreBilling(store).GetPurchasesAsync(ItemType.InAppPurchase, default));

        Assert.Equal("9NBLGGH4R315/0010", purchase.TransactionIdentifier);
    }

    [Fact]
    public async Task Failed_refresh_does_not_turn_a_successful_purchase_into_a_failure()
    {
        var store = new ScriptedStore { ThrownByCollection = new StoreError(0x80004005) };
        store.Items.Add(OwnedItem());

        var purchase = await new WindowsStoreBilling(store).PurchaseAsync("gold", ItemType.InAppPurchase, "account", null, null, default);

        Assert.Equal(PurchaseState.Purchased, purchase.State);
        Assert.Equal("gold", purchase.ProductId);
        Assert.Equal("account", purchase.ObfuscatedAccountId);
    }

    [Theory]
    [InlineData(0x803F6107u, PurchaseError.DeveloperError)]
    [InlineData(0x80070578u, PurchaseError.DeveloperError)]
    [InlineData(0x80004005u, PurchaseError.ProductRequestFailed)]
    public async Task Query_errors_map_to_purchase_errors(uint hresult, PurchaseError expected)
    {
        var store = new ScriptedStore { QueryError = new StoreError(hresult) };

        var ex = await Assert.ThrowsAsync<InAppBillingPurchaseException>(() => new WindowsStoreBilling(store).GetProductInfoAsync(ItemType.InAppPurchase, new[] { "gold" }, true, default));

        Assert.Equal(expected, ex.PurchaseError);
    }

    [Fact]
    public async Task Cancelled_dialog_with_unknown_error_keeps_it_as_inner_exception()
    {
        var error = new StoreError(0x80004005);
        var store = new ScriptedStore { PurchaseResult = new WindowsStorePurchaseResult { Status = WindowsStorePurchaseStatus.NotPurchased, ExtendedError = error } };
        store.Items.Add(OwnedItem());

        var ex = await Assert.ThrowsAsync<InAppBillingPurchaseException>(() => new WindowsStoreBilling(store).PurchaseAsync("gold", ItemType.InAppPurchase, null, null, null, default));

        Assert.Equal(PurchaseError.UserCancelled, ex.PurchaseError);
        Assert.Same(error, ex.InnerException);
    }

    [Fact]
    public async Task Missing_owner_window_is_reported_as_developer_error_not_cancellation()
    {
        var store = new ScriptedStore { PurchaseResult = new WindowsStorePurchaseResult { Status = WindowsStorePurchaseStatus.NotPurchased, ExtendedError = new StoreError(0x80070578) } };
        store.Items.Add(OwnedItem());

        var ex = await Assert.ThrowsAsync<InAppBillingPurchaseException>(() => new WindowsStoreBilling(store).PurchaseAsync("gold", ItemType.InAppPurchase, null, null, null, default));

        Assert.Equal(PurchaseError.DeveloperError, ex.PurchaseError);
    }

    [Fact]
    public async Task Developer_managed_fulfillment_retries_with_the_same_tracking_id()
    {
        var store = new ScriptedStore();
        store.Items.Add(OwnedItem("UnmanagedConsumable"));
        var billing = new WindowsStoreBilling(store);

        await billing.ConsumePurchaseAsync("gold", TransactionId, default);
        await billing.ConsumePurchaseAsync("gold", TransactionId, default);

        Assert.All(store.Fulfillments, f => Assert.Equal((1u, Guid.Parse(TransactionId)), f));
    }

    [Fact]
    public void Non_guid_transactions_map_to_a_stable_tracking_id()
    {
        Assert.Equal(WindowsStoreBilling.ToTrackingId("9NBL/0010"), WindowsStoreBilling.ToTrackingId("9NBL/0010"));
        Assert.NotEqual(WindowsStoreBilling.ToTrackingId("9NBL/0010"), WindowsStoreBilling.ToTrackingId("9NBL/0020"));
        Assert.NotEqual(WindowsStoreBilling.ToTrackingId(null), WindowsStoreBilling.ToTrackingId(null));
    }

    [Fact]
    public async Task Store_ids_that_are_not_found_fall_back_to_product_id_lookup()
    {
        var store = new ScriptedStore();
        var item = OwnedItem();
        item.InAppOfferToken = "GOLDEDITION1";
        store.Items.Add(item);

        var product = Assert.Single(await new WindowsStoreBilling(store).GetProductInfoAsync(ItemType.InAppPurchase, new[] { "GOLDEDITION1" }, false, default));

        Assert.Equal("GOLDEDITION1", product.ProductId);
        Assert.Single(store.AssociatedQueries);
    }

    [Theory]
    [InlineData("""{"OrderManagementData":{"Price":{"ListPrice":7.99,"CurrencyCode":"EUR"}}}""", 7_990_000)]
    [InlineData("""{"OrderManagementData":null}""", 0)]
    [InlineData("""{}""", 0)]
    [InlineData("""not json""", 0)]
    [InlineData(null, 0)]
    public void Micros_price_is_read_from_availability_json(string json, long expected) =>
        Assert.Equal(expected, WindowsStoreJson.GetMicrosPrice(json));

    [Fact]
    public void Developer_offer_id_round_trips_and_escapes()
    {
        Assert.Equal(("a", "b"), WindowsStoreBilling.SplitDeveloperOfferId(WindowsStoreBilling.JoinDeveloperOfferId("a", "b")));
        Assert.Equal(("a", null), WindowsStoreBilling.SplitDeveloperOfferId(WindowsStoreBilling.JoinDeveloperOfferId("a", null)));
        Assert.Equal("""{"DevOfferId":"a\u0022b"}""", WindowsStoreJson.CreatePurchaseJson("a\"b"));
    }
}
