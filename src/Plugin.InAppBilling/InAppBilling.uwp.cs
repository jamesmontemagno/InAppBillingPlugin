
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Windows.ApplicationModel.Store;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// Implementation for Feature
    /// </summary>
    public class InAppBillingImplementation : BaseInAppBilling
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public InAppBillingImplementation()
        {
        }

        /// <summary>
        /// Gets or sets if in testing mode. Only for UWP
        /// </summary>
        public override bool InTestingMode { get; set; }


        /// <summary>
        /// Gets product information
        /// </summary>
        /// <param name="itemType">Type of item</param>
        /// <param name="productIds">Product Ids</param>
        /// <returns></returns>
        public async override Task<IEnumerable<InAppBillingProduct>> GetProductInfoAsync(ItemType itemType, params string[] productIds)
        {
            // Get list of products from store or simulator
            var listingInformation = await CurrentAppMock.LoadListingInformationAsync(InTestingMode);

            var products = new List<InAppBillingProduct>();
            foreach (var productId in productIds)
            {
                // Check if requested product exists
                if (!listingInformation.ProductListings.ContainsKey(productId))
                    continue;

                // Get product and transform it to an InAppBillingProduct
                var product = listingInformation.ProductListings[productId];

                products.Add(new InAppBillingProduct
                {
                    Name = product.Name,
                    Description = product.Description,
                    ProductId = product.ProductId,
                    LocalizedPrice = product.FormattedPrice,
                    WindowsExtras = new InAppBillingProductWindowsExtras 
                    {
                        FormattedBasePrice = product.FormattedBasePrice,
                        ImageUri = product.ImageUri,
                        IsOnSale = product.IsOnSale,   
                        SaleEndDate = product.SaleEndDate,
                        Tag = product.Tag,
                        IsConsumable = product.ProductType == ProductType.Consumable,
                        IsDurable = product.ProductType == ProductType.Durable,
                        Keywords = product.Keywords
                    }                    
                    //CurrencyCode = product.CurrencyCode // Does not work at the moment, as UWP throws an InvalidCastException when getting CurrencyCode
                });
            }

            return products;
        }

        /// <summary>
        /// Get all purchases
        /// </summary>
        /// <param name="itemType"></param>
        /// <param name="doNotFinishTransactionIds"></param>
        /// <returns></returns>
        public async override Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType, List<string>? doNotFinishTransactionIds = null)
        {
            // Get list of product receipts from store or simulator
            var xmlReceipt = await CurrentAppMock.GetAppReceiptAsync(InTestingMode);

            // Transform it to list of InAppBillingPurchase
            return xmlReceipt.ToInAppBillingPurchase(ProductPurchaseStatus.AlreadyPurchased);
        }

        /// <summary>
        /// Purchase a specific product or subscription
        /// </summary>
        /// <param name="productId">Sku or ID of product</param>
        /// <param name="itemType">Type of product being requested</param>
        /// <param name="obfuscatedAccountId">Specifies an optional obfuscated string that is uniquely associated with the user's account in your app.</param>
        /// <param name="obfuscatedProfileId">Specifies an optional obfuscated string that is uniquely associated with the user's profile in your app.</param>
        /// <returns></returns>
        /// <exception cref="InAppBillingPurchaseException">If an error occurs during processing</exception>
        public async override Task<InAppBillingPurchase?> PurchaseAsync(string productId, ItemType itemType, string? obfuscatedAccountId = null, string? obfuscatedProfileId = null)
        {
            // Get purchase result from store or simulator
            var purchaseResult = await CurrentAppMock.RequestProductPurchaseAsync(InTestingMode, productId);


			if (purchaseResult == null)
				return null;

			if (string.IsNullOrWhiteSpace(purchaseResult.ReceiptXml))
				return null;

			// Transform it to InAppBillingPurchase
			return purchaseResult.ReceiptXml.ToInAppBillingPurchase(purchaseResult.Status).FirstOrDefault();
			
        }

        /// <summary>
        /// (UWP not supported) Upgrade/Downgrade/Change a previously purchased subscription
        /// </summary>
        /// <exception cref="NotImplementedException">UWP not supported</exception>
        [DoesNotReturn]
        public override Task<InAppBillingPurchase?> UpgradePurchasedSubscriptionAsync(string newProductId, string purchaseTokenOfOriginalSubscription, SubscriptionProrationMode prorationMode = SubscriptionProrationMode.ImmediateWithTimeProration) =>
            throw new NotImplementedException("UWP not supported.");

        /// <summary>
        /// Consume a purchase with a purchase token.
        /// </summary>
        /// <param name="productId">Id or Sku of product</param>
        /// <param name="purchaseToken">Original Purchase Token</param>
        /// <returns>If consumed successful</returns>
        /// <exception cref="InAppBillingPurchaseException">If an error occures during processing</exception>
        public async override Task<bool> ConsumePurchaseAsync(string? productId, string purchaseToken, string purchaseId, List<string>? doNotFinishProductIds = null)
        {
            var result = await CurrentAppMock.ReportConsumableFulfillmentAsync(InTestingMode, productId, new Guid(purchaseToken));
            return result switch
            {
                FulfillmentResult.ServerError => throw new InAppBillingPurchaseException(PurchaseError.AppStoreUnavailable),
                FulfillmentResult.NothingToFulfill => throw new InAppBillingPurchaseException(PurchaseError.ItemUnavailable),
                FulfillmentResult.PurchasePending or FulfillmentResult.PurchaseReverted => throw new InAppBillingPurchaseException(PurchaseError.GeneralError),
                FulfillmentResult.Succeeded => true,
                _ => false,
            };
        }
    }

    /// <summary>
    /// Unfortunately, CurrentApp and CurrentAppSimulator do not share an interface or base class
    /// This is why, we use a mocking class here
    /// </summary>
    static class CurrentAppMock
    {
        public static async Task<IEnumerable<UnfulfilledConsumable>> GetAvailableConsumables(bool isTestingMode)
        {
            return isTestingMode ? await CurrentAppSimulator.GetUnfulfilledConsumablesAsync() : await CurrentApp.GetUnfulfilledConsumablesAsync();
        }

        public static async Task<FulfillmentResult> ReportConsumableFulfillmentAsync(bool isTestingMode, string? productId, Guid transactionId)
        {
            return isTestingMode ? await CurrentAppSimulator.ReportConsumableFulfillmentAsync(productId, transactionId) : await CurrentApp.ReportConsumableFulfillmentAsync(productId, transactionId);
        }

        public static async Task<ListingInformation> LoadListingInformationAsync(bool isTestingMode)
        {
            return isTestingMode ? await CurrentAppSimulator.LoadListingInformationAsync() : await CurrentApp.LoadListingInformationAsync();
        }

        public static async Task<string> GetAppReceiptAsync(bool isTestingMode)
        {
            return isTestingMode ? await CurrentAppSimulator.GetAppReceiptAsync() : await CurrentApp.GetAppReceiptAsync();
        }

        public static async Task<PurchaseResults> RequestProductPurchaseAsync(bool isTestingMode, string productId)
        {
            return isTestingMode ? await CurrentAppSimulator.RequestProductPurchaseAsync(productId) : await CurrentApp.RequestProductPurchaseAsync(productId);
        }
    }

    static class InAppBillingHelperUwp
    {
        /// <summary>
        /// Read purchase data out of the UWP Receipt XML
        /// </summary>
        /// <param name="xml">Receipt XML</param>
        /// <param name="status">Status of the purchase</param>
        /// <returns>A list of purchases, the user has done</returns>
        public static IEnumerable<InAppBillingPurchase> ToInAppBillingPurchase(this string xml, ProductPurchaseStatus status)
        {
            var purchases = new List<InAppBillingPurchase>();

            var xmlDoc = new XmlDocument();
			try
			{
				xmlDoc.LoadXml(xml);
			}
			catch
			{
				//Invalid XML, we haven't finished this transaction yet.
			}

            // Iterate through all ProductReceipt elements
            var xmlProductReceipts = xmlDoc.GetElementsByTagName("ProductReceipt");
            for (var i = 0; i < xmlProductReceipts.Count; i++)
            {
                var xmlProductReceipt = xmlProductReceipts[i];
                Debug.Assert(xmlProductReceipt != null && xmlProductReceipt.Attributes != null);

                // Create new InAppBillingPurchase with values from the xml element
                var purchase = new InAppBillingPurchase()
                {
                    Id = xmlProductReceipt!.Attributes!["Id"]!.Value,
                    TransactionDateUtc = Convert.ToDateTime(xmlProductReceipt.Attributes["PurchaseDate"]!.Value),
                    ProductId = xmlProductReceipt.Attributes["ProductId"]!.Value,
                    AutoRenewing = false // Not supported by UWP yet
                };
                purchase.PurchaseToken = purchase.Id;
                purchase.ProductIds = new[] { purchase.ProductId };

                // Map native UWP status to PurchaseState
                purchase.State = status switch
                {
                    ProductPurchaseStatus.AlreadyPurchased or ProductPurchaseStatus.Succeeded => PurchaseState.Purchased,
                    ProductPurchaseStatus.NotFulfilled => PurchaseState.Deferred,
                    ProductPurchaseStatus.NotPurchased => PurchaseState.Canceled,
                    _ => PurchaseState.Unknown,
                };

                // Add to list of purchases
                purchases.Add(purchase);
            }

            return purchases;
        }
    }
}
