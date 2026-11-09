using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Plugin.InAppBilling
{
    // Shared iOS/Mac Catalyst JSON contract with src/StoreKit2Shim/Sources/InAppBillingStoreKit/Models.swift.

    internal sealed class StoreKitResponse
    {
        public StoreKitError Error { get; set; }
        public string Status { get; set; }
        public List<StoreKitProduct> Products { get; set; }
        public List<string> Missing { get; set; }
        public StoreKitTransaction Transaction { get; set; }
        public List<StoreKitTransaction> Transactions { get; set; }
        public List<StoreKitFinishResult> Results { get; set; }
        public StoreKitStorefront Storefront { get; set; }
    }

    internal sealed class StoreKitEvent
    {
        public string Kind { get; set; }
        public StoreKitTransaction Transaction { get; set; }
        public string ProductId { get; set; }
    }

    internal sealed class StoreKitError
    {
        public string Kind { get; set; }
        public string Message { get; set; }
    }

    internal sealed class StoreKitFinishResult
    {
        public string Id { get; set; }
        public bool Success { get; set; }
    }

    internal sealed class StoreKitStorefront
    {
        public string Id { get; set; }
        public string CountryCode { get; set; }
    }

    internal sealed class StoreKitTransaction
    {
        public string Id { get; set; }
        public string OriginalId { get; set; }
        public string ProductId { get; set; }
        public long PurchaseDateMs { get; set; }
        public int Quantity { get; set; }
        public string AppAccountToken { get; set; }
        public string Jws { get; set; }
        public string Json { get; set; }
        public bool Revoked { get; set; }
        public bool Verified { get; set; }
    }

    internal sealed class StoreKitPeriod
    {
        public int Value { get; set; }
        public string Unit { get; set; }
    }

    internal sealed class StoreKitOffer
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Price { get; set; }
        public string DisplayPrice { get; set; }
        public string PaymentMode { get; set; }
        public StoreKitPeriod Period { get; set; }
        public int PeriodCount { get; set; }
    }

    internal sealed class StoreKitProduct
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string DisplayPrice { get; set; }
        public string Price { get; set; }
        public string CurrencyCode { get; set; }
        public bool IsFamilyShareable { get; set; }
        public string SubscriptionGroupId { get; set; }
        public StoreKitPeriod SubscriptionPeriod { get; set; }
        public StoreKitOffer IntroductoryOffer { get; set; }
        public List<StoreKitOffer> PromotionalOffers { get; set; }
    }

    // Source generated so the JSON survives full trimming and AOT in iOS release builds.
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(StoreKitResponse))]
    [JsonSerializable(typeof(StoreKitEvent))]
    [JsonSerializable(typeof(string[]))]
    internal sealed partial class StoreKitJsonContext : JsonSerializerContext
    {
    }

    internal static class StoreKitMapper
    {
        public static StoreKitResponse ParseResponse(string json) =>
            JsonSerializer.Deserialize(json, StoreKitJsonContext.Default.StoreKitResponse);

        public static StoreKitEvent ParseEvent(string json) =>
            JsonSerializer.Deserialize(json, StoreKitJsonContext.Default.StoreKitEvent);

        public static string SerializeIds(IEnumerable<string> ids) =>
            JsonSerializer.Serialize(ids.ToArray(), StoreKitJsonContext.Default.StringArray);

        /// <summary>
        /// StoreKit 2 only accepts a UUID as the account token. A UUID is passed as is, any other value
        /// becomes a deterministic RFC 4122 version 5 UUID that a server can recompute.
        /// </summary>
        public static string ToAppAccountToken(string obfuscatedAccountId)
        {
            if (string.IsNullOrWhiteSpace(obfuscatedAccountId))
                return null;

            if (Guid.TryParse(obfuscatedAccountId, out var guid))
                return guid.ToString("D");

            // The RFC 4122 URL namespace (6ba7b811-9dad-11d1-80b4-00c04fd430c8) in network byte order.
            byte[] namespaceBytes = { 0x6b, 0xa7, 0xb8, 0x11, 0x9d, 0xad, 0x11, 0xd1, 0x80, 0xb4, 0x00, 0xc0, 0x4f, 0xd4, 0x30, 0xc8 };
            var hash = SHA1.HashData(namespaceBytes.Concat(Encoding.UTF8.GetBytes(obfuscatedAccountId)).ToArray());

            var uuid = hash.Take(16).ToArray();
            uuid[6] = (byte)((uuid[6] & 0x0F) | 0x50);
            uuid[8] = (byte)((uuid[8] & 0x3F) | 0x80);
            return new Guid(uuid, bigEndian: true).ToString("D");
        }

        public static InAppBillingProduct ToProduct(StoreKitProduct product) => new()
        {
            Name = product.DisplayName,
            Description = product.Description,
            ProductId = product.Id,
            LocalizedPrice = product.DisplayPrice,
            CurrencyCode = product.CurrencyCode ?? string.Empty,
            MicrosPrice = (long)(ParsePrice(product.Price) * 1_000_000m),
            AppleExtras = new InAppBillingProductAppleExtras
            {
                IsFamilyShareable = product.IsFamilyShareable,
                SubscriptionGroupId = product.SubscriptionGroupId,
                SubscriptionPeriod = ToPeriod(product.SubscriptionPeriod),
                IntroductoryOffer = ToDiscount(product.IntroductoryOffer, product.CurrencyCode),
                Discounts = product.PromotionalOffers?.Select(o => ToDiscount(o, product.CurrencyCode)).ToList()
            }
        };

        public static InAppBillingPurchase ToPurchase(StoreKitTransaction transaction) => new()
        {
            Id = transaction.Id,
            TransactionIdentifier = transaction.Id,
            OriginalTransactionIdentifier = transaction.OriginalId,
            TransactionDateUtc = DateTimeOffset.FromUnixTimeMilliseconds(transaction.PurchaseDateMs).UtcDateTime,
            ProductId = transaction.ProductId,
            ProductIds = new[] { transaction.ProductId },
            Quantity = transaction.Quantity,
            PurchaseToken = transaction.Jws,
            OriginalJson = transaction.Json,
            ApplicationUsername = transaction.AppAccountToken,
            State = !transaction.Verified ? PurchaseState.Failed
                : transaction.Revoked ? PurchaseState.Canceled
                : PurchaseState.Purchased
        };

        public static InAppBillingPurchase ToDeferredPurchase(string productId) => new()
        {
            ProductId = productId,
            ProductIds = new[] { productId },
            State = PurchaseState.Deferred
        };

        /// <summary>
        /// Maps a shim error. General failures use the caller's error so a failed query stays distinguishable from a failed purchase.
        /// </summary>
        public static InAppBillingPurchaseException ToException(StoreKitError error, PurchaseError generalError = PurchaseError.GeneralError)
        {
            var purchaseError = error.Kind switch
            {
                "userCancelled" => PurchaseError.UserCancelled,
                "paymentInvalid" => PurchaseError.PaymentInvalid,
                "paymentNotAllowed" => PurchaseError.PaymentNotAllowed,
                "itemUnavailable" => PurchaseError.ItemUnavailable,
                "invalidProduct" => PurchaseError.InvalidProduct,
                "notOwned" => PurchaseError.NotOwned,
                "network" => PurchaseError.NetworkError,
                "termsChanged" => PurchaseError.AppleTermsConditionsChanged,
                _ => generalError
            };
            return new InAppBillingPurchaseException(purchaseError, error.Message ?? string.Empty);
        }

        static decimal ParsePrice(string price) =>
            decimal.TryParse(price, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m;

        static SubscriptionPeriod ToPeriod(StoreKitPeriod period) => period == null ? null : new SubscriptionPeriod
        {
            NumberOfUnits = period.Value,
            Unit = period.Unit switch
            {
                "day" => SubscriptionPeriodUnit.Day,
                "week" => SubscriptionPeriodUnit.Week,
                "month" => SubscriptionPeriodUnit.Month,
                "year" => SubscriptionPeriodUnit.Year,
                _ => SubscriptionPeriodUnit.Unknown
            }
        };

        static InAppBillingProductDiscount ToDiscount(StoreKitOffer offer, string currencyCode) => offer == null ? null : new InAppBillingProductDiscount
        {
            Id = offer.Id ?? string.Empty,
            Type = offer.Type switch
            {
                "introductory" => ProductDiscountType.Introductory,
                "promotional" => ProductDiscountType.Subscription,
                _ => ProductDiscountType.Unknown
            },
            Price = (double)(ParsePrice(offer.Price) * 1_000_000m),
            LocalizedPrice = offer.DisplayPrice,
            CurrencyCode = currencyCode ?? string.Empty,
            PaymentMode = offer.PaymentMode switch
            {
                "freeTrial" => PaymentMode.FreeTrial,
                "payUpFront" => PaymentMode.PayUpFront,
                "payAsYouGo" => PaymentMode.PayAsYouGo,
                _ => PaymentMode.Unknown
            },
            NumberOfPeriods = offer.PeriodCount,
            SubscriptionPeriod = ToPeriod(offer.Period) ?? new SubscriptionPeriod()
        };
    }
}
