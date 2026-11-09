using System;
using System.Collections.Generic;
using System.Linq;

namespace Plugin.InAppBilling
{
    sealed partial class WindowsStoreBilling
    {
        static InAppBillingProduct ToProduct(WindowsStoreItem item, string requestedId)
        {
            var defaultSku = item.Skus.FirstOrDefault();
            var subscriptionSku = item.Skus.FirstOrDefault(s => s.IsSubscription);

            return new InAppBillingProduct
            {
                Name = item.Title,
                Description = item.Description,
                ProductId = requestedId,
                LocalizedPrice = item.FormattedPrice,
                CurrencyCode = item.CurrencyCode,
                MicrosPrice = WindowsStoreJson.GetMicrosPrice(defaultSku?.AvailabilityJson),
                WindowsExtras = new InAppBillingProductWindowsExtras
                {
                    FormattedBasePrice = item.FormattedBasePrice,
                    FormattedRecurrencePrice = item.FormattedRecurrencePrice,
                    ImageUri = item.ImageUri,
                    LinkUri = item.LinkUri,
                    IsOnSale = item.IsOnSale,
                    SaleEndDate = item.SaleEndDate,
                    Tag = defaultSku?.CustomDeveloperData,
                    IsConsumable = ConsumableKinds.Contains(item.ProductKind),
                    IsDurable = item.ProductKind == Durable,
                    Keywords = item.Keywords,
                    StoreId = item.StoreId,
                    InAppOfferToken = item.InAppOfferToken,
                    ProductKind = item.ProductKind,
                    IsInUserCollection = item.IsInUserCollection,
                    IsSubscription = subscriptionSku != null,
                    SubscriptionPeriod = subscriptionSku?.BillingPeriod,
                    TrialPeriod = subscriptionSku?.TrialPeriod,
                    Skus = item.Skus.Select(ToSku).ToList(),
                },
            };
        }

        static InAppBillingProductWindowsSku ToSku(WindowsStoreSkuItem sku) => new()
        {
            StoreId = sku.StoreId,
            Title = sku.Title,
            Description = sku.Description,
            FormattedPrice = sku.FormattedPrice,
            FormattedRecurrencePrice = sku.FormattedRecurrencePrice,
            MicrosPrice = WindowsStoreJson.GetMicrosPrice(sku.AvailabilityJson),
            IsTrial = sku.IsTrial,
            IsSubscription = sku.IsSubscription,
            SubscriptionPeriod = sku.BillingPeriod,
            TrialPeriod = sku.TrialPeriod,
        };

        static string PreferredId(WindowsStoreItem item) =>
            string.IsNullOrEmpty(item.InAppOfferToken) ? item.StoreId : item.InAppOfferToken;

        static IList<string> AllIds(WindowsStoreItem item, string productId) =>
            new[] { productId, item.InAppOfferToken, item.StoreId }.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();

        static InAppBillingPurchase CreatePurchase(WindowsStoreItem item, WindowsStoreSkuItem sku, string productId, string identifier, DateTime transactionDateUtc)
        {
            return new InAppBillingPurchase
            {
                Id = identifier,
                TransactionIdentifier = identifier,
                OriginalTransactionIdentifier = identifier,
                PurchaseToken = identifier,
                ProductId = productId,
                ProductIds = AllIds(item, productId),
                TransactionDateUtc = transactionDateUtc,
                State = PurchaseState.Purchased,
                ConsumptionState = ConsumptionState.NoYetConsumed,
                // The Store has no acknowledgement step, it completes the purchase itself.
                IsAcknowledged = true,
                WindowsExtras = new InAppBillingPurchaseWindowsExtras
                {
                    StoreId = item.StoreId,
                    SkuStoreId = sku?.StoreId,
                    InAppOfferToken = item.InAppOfferToken,
                    ProductKind = item.ProductKind,
                },
            };
        }

        /// <summary>
        /// Maps an owned add-on to a purchase. Returns null if the entitlement is no longer current.
        /// </summary>
        static InAppBillingPurchase ToPurchase(WindowsStoreItem item, string productId, DateTimeOffset now)
        {
            var sku = item.Skus.FirstOrDefault(s => s.IsInUserCollection && s.Collection != null)
                ?? item.Skus.FirstOrDefault(s => s.IsInUserCollection)
                ?? item.Skus.FirstOrDefault();
            var collection = sku?.Collection;
            var json = WindowsStoreCollectionJson.Parse(collection?.Json);

            if (json.Status != null && !json.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                return null;

            // WinRT reports an unset date as year 1601, and a non-expiring entitlement as year 9999.
            var endDate = collection != null && collection.EndDate.Year is > 1601 and < 9999 ? collection.EndDate : (DateTimeOffset?)null;
            if (endDate < now)
                return null;

            var identifier = json.TransactionId ?? json.OrderId ?? sku?.StoreId ?? item.StoreId;
            var purchase = CreatePurchase(item, sku, productId, identifier, collection?.AcquiredDate.UtcDateTime ?? default);

            purchase.Id = json.OrderId ?? identifier;
            purchase.AutoRenewing = json.AutoRenew;
            purchase.Quantity = json.Quantity ?? 1;
            purchase.OriginalJson = collection?.Json;
            purchase.Payload = collection?.DeveloperOfferId;
            (purchase.ObfuscatedAccountId, purchase.ObfuscatedProfileId) = SplitDeveloperOfferId(collection?.DeveloperOfferId);

            var extras = purchase.WindowsExtras;
            extras.ExpirationDateUtc = endDate?.UtcDateTime;
            extras.IsTrial = collection?.IsTrial ?? false;
            extras.DeveloperOfferId = collection?.DeveloperOfferId;
            extras.CampaignId = collection?.CampaignId;
            extras.PurchasedCountry = json.PurchasedCountry;

            return purchase;
        }

        // The Store persists a single developer value per purchase, so both obfuscated IDs share it.
        internal static string JoinDeveloperOfferId(string accountId, string profileId) =>
            string.IsNullOrWhiteSpace(accountId) ? null : string.IsNullOrWhiteSpace(profileId) ? accountId : $"{accountId}|{profileId}";

        internal static (string AccountId, string ProfileId) SplitDeveloperOfferId(string developerOfferId)
        {
            if (string.IsNullOrEmpty(developerOfferId))
                return (null, null);

            var separator = developerOfferId.IndexOf('|');
            return separator < 0
                ? (developerOfferId, null)
                : (developerOfferId[..separator], developerOfferId[(separator + 1)..]);
        }
    }
}
