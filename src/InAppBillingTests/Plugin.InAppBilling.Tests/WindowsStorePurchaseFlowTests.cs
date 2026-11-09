using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Plugin.InAppBilling.Tests;

public class WindowsStorePurchaseFlowTests
{
    readonly WindowsStoreSimulator store = new();
    readonly WindowsStoreBilling billing;

    public WindowsStorePurchaseFlowTests() => billing = new WindowsStoreBilling(store);

    Task<InAppBillingPurchase> Buy(string id, ItemType type, string account = null, string profile = null, string sku = null) =>
        billing.PurchaseAsync(id, type, account, profile, sku, CancellationToken.None);

    [Fact]
    public async Task Products_resolve_by_product_id_and_store_id()
    {
        var storeId = store.AddProduct("gold", ItemType.InAppPurchase, listPrice: 7.99m);

        var products = (await billing.GetProductInfoAsync(ItemType.InAppPurchase, new[] { "gold", storeId }, false, default)).ToList();

        Assert.Equal(new[] { "gold", storeId }, products.Select(p => p.ProductId));
        Assert.All(products, p => Assert.Equal(7_990_000, p.MicrosPrice));
        Assert.Equal("USD", products[0].CurrencyCode);
        Assert.True(products[0].WindowsExtras.IsDurable);
    }

    [Fact]
    public async Task Invalid_products_throw_unless_ignored()
    {
        store.AddProduct("gold", ItemType.InAppPurchase);
        var ids = new[] { "gold", "missing" };

        var ex = await Assert.ThrowsAsync<InAppBillingPurchaseException>(() => billing.GetProductInfoAsync(ItemType.InAppPurchase, ids, false, default));
        Assert.Equal(PurchaseError.InvalidProduct, ex.PurchaseError);
        Assert.Equal(new[] { "missing" }, ex.Invalid);

        var products = await billing.GetProductInfoAsync(ItemType.InAppPurchase, ids, true, default);
        Assert.Equal("gold", Assert.Single(products).ProductId);
    }

    [Fact]
    public async Task Item_type_separates_subscriptions_from_one_time_products()
    {
        store.AddProduct("gold", ItemType.InAppPurchase);
        store.AddProduct("monthly", ItemType.Subscription);
        await Buy("gold", ItemType.InAppPurchase);
        await Buy("monthly", ItemType.Subscription);

        Assert.Equal("gold", Assert.Single(await billing.GetPurchasesAsync(ItemType.InAppPurchase, default)).ProductId);
        Assert.Equal("monthly", Assert.Single(await billing.GetPurchasesAsync(ItemType.Subscription, default)).ProductId);

        var ex = await Assert.ThrowsAsync<InAppBillingPurchaseException>(() => Buy("gold", ItemType.Subscription));
        Assert.Equal(PurchaseError.InvalidProduct, ex.PurchaseError);
    }

    [Fact]
    public async Task Purchase_reads_transaction_details_from_the_collection()
    {
        store.AddProduct("monthly", ItemType.Subscription);

        var purchase = await Buy("monthly", ItemType.Subscription, "account", "profile");

        Assert.Equal(PurchaseState.Purchased, purchase.State);
        Assert.True(purchase.AutoRenewing);
        Assert.True(purchase.IsAcknowledged);
        Assert.True(Guid.TryParse(purchase.TransactionIdentifier, out _));
        Assert.Equal("US", purchase.WindowsExtras.PurchasedCountry);
        Assert.Equal("account", purchase.ObfuscatedAccountId);
        Assert.Equal("profile", purchase.ObfuscatedProfileId);

        var restored = Assert.Single(await billing.GetPurchasesAsync(ItemType.Subscription, default));
        Assert.Equal("account", restored.ObfuscatedAccountId);
        Assert.Equal("profile", restored.ObfuscatedProfileId);
    }

    [Theory]
    [InlineData(WindowsStorePurchaseStatus.NotPurchased, PurchaseError.UserCancelled)]
    [InlineData(WindowsStorePurchaseStatus.NetworkError, PurchaseError.NetworkError)]
    [InlineData(WindowsStorePurchaseStatus.ServerError, PurchaseError.ServiceUnavailable)]
    public async Task Failed_purchases_throw_instead_of_returning_null(WindowsStorePurchaseStatus status, PurchaseError expected)
    {
        store.AddProduct("gold", ItemType.InAppPurchase);
        store.NextPurchaseStatus = status;

        var ex = await Assert.ThrowsAsync<InAppBillingPurchaseException>(() => Buy("gold", ItemType.InAppPurchase));

        Assert.Equal(expected, ex.PurchaseError);
    }

    [Fact]
    public async Task Buying_an_owned_durable_reports_already_owned()
    {
        store.AddProduct("gold", ItemType.InAppPurchase);
        await Buy("gold", ItemType.InAppPurchase);

        var ex = await Assert.ThrowsAsync<InAppBillingPurchaseException>(() => Buy("gold", ItemType.InAppPurchase));

        Assert.Equal(PurchaseError.AlreadyOwned, ex.PurchaseError);
    }

    [Fact]
    public async Task Sub_offer_token_selects_a_sku()
    {
        store.AddProduct("monthly", ItemType.Subscription, hasTrial: true);
        var product = Assert.Single(await billing.GetProductInfoAsync(ItemType.Subscription, new[] { "monthly" }, false, default));
        var skus = product.WindowsExtras.Skus;

        Assert.Equal(2, skus.Count);
        Assert.True(skus[0].IsTrial);
        Assert.Equal(0, product.MicrosPrice);
        Assert.Equal(990_000, skus[1].MicrosPrice);
        Assert.Equal(SubscriptionPeriodUnit.Month, product.WindowsExtras.TrialPeriod.Unit);

        var purchase = await Buy("monthly", ItemType.Subscription, sku: skus[1].StoreId);
        Assert.Equal(skus[1].StoreId, purchase.WindowsExtras.SkuStoreId);

        await Assert.ThrowsAsync<InAppBillingPurchaseException>(() => Buy("monthly", ItemType.Subscription, sku: "unknown"));
    }

    [Fact]
    public async Task Profile_id_requires_an_account_id()
    {
        store.AddProduct("gold", ItemType.InAppPurchase);

        await Assert.ThrowsAsync<ArgumentNullException>(() => Buy("gold", ItemType.InAppPurchase, profile: "profile"));
    }

    [Fact]
    public async Task Developer_managed_consumable_can_be_bought_again_after_consuming()
    {
        store.AddProduct("potion", ItemType.InAppPurchaseConsumable);
        var purchase = await Buy("potion", ItemType.InAppPurchaseConsumable);

        await Assert.ThrowsAsync<InAppBillingPurchaseException>(() => Buy("potion", ItemType.InAppPurchaseConsumable));
        Assert.True(await billing.ConsumePurchaseAsync("potion", purchase.TransactionIdentifier, default));

        Assert.Empty(await billing.GetPurchasesAsync(ItemType.InAppPurchaseConsumable, default));
        Assert.NotNull(await Buy("potion", ItemType.InAppPurchaseConsumable));
    }

    [Fact]
    public async Task Store_managed_consumable_tracks_balance()
    {
        store.AddProduct("coins", ItemType.InAppPurchaseConsumable, storeManagedQuantity: 100);
        await Buy("coins", ItemType.InAppPurchaseConsumable);
        var purchase = await Buy("coins", ItemType.InAppPurchaseConsumable);

        Assert.Equal(200, purchase.Quantity);
        Assert.Equal(200u, await billing.GetConsumableBalanceAsync("coins", default));

        Assert.True(await billing.ConsumePurchaseAsync("coins", 50, Guid.NewGuid(), default));
        Assert.Equal(150u, await billing.GetConsumableBalanceAsync("coins", default));

        Assert.True(await billing.ConsumePurchaseAsync("coins", purchase.TransactionIdentifier, default));
        Assert.Empty(await billing.GetPurchasesAsync(ItemType.InAppPurchaseConsumable, default));

        var ex = await Assert.ThrowsAsync<InAppBillingPurchaseException>(() => billing.ConsumePurchaseAsync("coins", purchase.TransactionIdentifier, default));
        Assert.Equal(PurchaseError.NotOwned, ex.PurchaseError);
    }

    [Fact]
    public async Task Consuming_a_durable_is_rejected()
    {
        store.AddProduct("gold", ItemType.InAppPurchase);

        var ex = await Assert.ThrowsAsync<InAppBillingPurchaseException>(() => billing.ConsumePurchaseAsync("gold", "token", default));

        Assert.Equal(PurchaseError.InvalidProduct, ex.PurchaseError);
    }
}
