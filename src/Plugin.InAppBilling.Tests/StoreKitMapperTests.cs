using System.Globalization;
using Xunit;

namespace Plugin.InAppBilling.Tests;

public class StoreKitMapperTests
{
    const string ProductsJson = """
    {
      "products": [{
        "id": "monthly",
        "displayName": "Monthly",
        "description": "Monthly plan",
        "displayPrice": "$4.99",
        "price": "4.99",
        "currencyCode": "USD",
        "isFamilyShareable": true,
        "subscriptionGroupId": "group",
        "subscriptionPeriod": { "value": 1, "unit": "month" },
        "introductoryOffer": {
          "type": "introductory", "price": "0", "displayPrice": "$0.00", "paymentMode": "freeTrial",
          "period": { "value": 1, "unit": "week" }, "periodCount": 2
        },
        "promotionalOffers": [{
          "id": "winback", "type": "promotional", "price": "1.5", "displayPrice": "$1.50", "paymentMode": "payAsYouGo",
          "period": { "value": 1, "unit": "month" }, "periodCount": 3
        }]
      }],
      "missing": ["gone"]
    }
    """;

    [Fact]
    public void Product_maps_price_and_subscription_details()
    {
        var response = StoreKitMapper.ParseResponse(ProductsJson);
        var product = StoreKitMapper.ToProduct(response.Products.Single());

        Assert.Equal(["gone"], response.Missing);
        Assert.Equal("monthly", product.ProductId);
        Assert.Equal("Monthly", product.Name);
        Assert.Equal("$4.99", product.LocalizedPrice);
        Assert.Equal(4_990_000, product.MicrosPrice);

        var extras = product.AppleExtras;
        Assert.True(extras.IsFamilyShareable);
        Assert.Equal("group", extras.SubscriptionGroupId);
        Assert.Equal(SubscriptionPeriodUnit.Month, extras.SubscriptionPeriod.Unit);

        Assert.Equal(PaymentMode.FreeTrial, extras.IntroductoryOffer.PaymentMode);
        Assert.Equal(ProductDiscountType.Introductory, extras.IntroductoryOffer.Type);
        Assert.Equal(SubscriptionPeriodUnit.Week, extras.IntroductoryOffer.SubscriptionPeriod.Unit);
        Assert.Equal(2, extras.IntroductoryOffer.NumberOfPeriods);
        Assert.Equal("USD", extras.IntroductoryOffer.CurrencyCode);

        var discount = extras.Discounts.Single();
        Assert.Equal("winback", discount.Id);
        Assert.Equal(ProductDiscountType.Subscription, discount.Type);
        Assert.Equal(1_500_000d, discount.Price);
    }

    [Fact]
    public void Price_parsing_ignores_the_device_culture()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var product = StoreKitMapper.ToProduct(new StoreKitProduct { Id = "a", Price = "1234.56" });
            Assert.Equal(1_234_560_000, product.MicrosPrice);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData(true, false, PurchaseState.Purchased)]
    [InlineData(true, true, PurchaseState.Canceled)]
    [InlineData(false, false, PurchaseState.Failed)]
    public void Purchase_state_reflects_verification_and_revocation(bool verified, bool revoked, PurchaseState expected)
    {
        var purchase = StoreKitMapper.ToPurchase(new StoreKitTransaction { Id = "1", ProductId = "a", Verified = verified, Revoked = revoked });

        Assert.Equal(expected, purchase.State);
    }

    [Fact]
    public void Purchase_exposes_the_transaction_and_signed_jws()
    {
        const string json = """
        { "status": "success", "transaction": {
            "id": "2000000000000001", "originalId": "2000000000000000", "productId": "coins",
            "purchaseDateMs": 1700000000000, "quantity": 3, "appAccountToken": "token",
            "jws": "a.b.c", "json": "{}", "revoked": false, "verified": true } }
        """;

        var purchase = StoreKitMapper.ToPurchase(StoreKitMapper.ParseResponse(json).Transaction);

        Assert.Equal("2000000000000001", purchase.Id);
        Assert.Equal("2000000000000001", purchase.TransactionIdentifier);
        Assert.Equal("2000000000000000", purchase.OriginalTransactionIdentifier);
        Assert.Equal(["coins"], purchase.ProductIds);
        Assert.Equal(3, purchase.Quantity);
        Assert.Equal("a.b.c", purchase.PurchaseToken);
        Assert.Equal("{}", purchase.OriginalJson);
        Assert.Equal("token", purchase.ApplicationUsername);
        Assert.Equal(new DateTime(2023, 11, 14, 22, 13, 20, DateTimeKind.Utc), purchase.TransactionDateUtc);
    }

    [Theory]
    [InlineData("userCancelled", PurchaseError.UserCancelled)]
    [InlineData("paymentInvalid", PurchaseError.PaymentInvalid)]
    [InlineData("paymentNotAllowed", PurchaseError.PaymentNotAllowed)]
    [InlineData("itemUnavailable", PurchaseError.ItemUnavailable)]
    [InlineData("invalidProduct", PurchaseError.InvalidProduct)]
    [InlineData("notOwned", PurchaseError.NotOwned)]
    [InlineData("network", PurchaseError.NetworkError)]
    [InlineData("termsChanged", PurchaseError.AppleTermsConditionsChanged)]
    [InlineData("general", PurchaseError.RestoreFailed)]
    public void Errors_map_to_purchase_errors(string kind, PurchaseError expected)
    {
        var exception = StoreKitMapper.ToException(new StoreKitError { Kind = kind, Message = "nope" }, PurchaseError.RestoreFailed);

        Assert.Equal(expected, exception.PurchaseError);
        Assert.Equal("nope", exception.Message);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    [InlineData("8F14E45F-CEEA-467F-A8C5-1B2F0C9E3D10", "8f14e45f-ceea-467f-a8c5-1b2f0c9e3d10")]
    // Independently computed with Python: uuid.uuid5(uuid.NAMESPACE_URL, "user-42")
    [InlineData("user-42", "4243fa49-370d-5428-b122-218655763d34")]
    public void Account_id_becomes_a_uuid_token(string accountId, string expected)
    {
        Assert.Equal(expected, StoreKitMapper.ToAppAccountToken(accountId));
    }
}
