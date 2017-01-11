using Plugin.InAppBilling.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Windows.ApplicationModel.Store;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// Implementation for Feature
    /// </summary>
    public class InAppBillingImplementation : IInAppBilling
    {
        private bool testWithSimulator;

        /// <param name="testWithSimulator">UWP offers a way to test in-app purchases with the CurrentAppSimulator class instead of CurrentApp.</param>
        public InAppBillingImplementation(bool testWithSimulator = false)
        {
            this.testWithSimulator = testWithSimulator;
        }

        /// <summary>
        /// Validation public key from App Store
        /// </summary>
        public string ValidationPublicKey { get; set; }

        /// <summary>
        /// Connect to billing service
        /// </summary>
        /// <returns>If Success</returns>
        public Task<bool> ConnectAsync() => Task.FromResult(true);

        /// <summary>
        /// Disconnect from the billing service
        /// </summary>
        /// <returns>Task to disconnect</returns>
        public Task DisconnectAsync() => Task.CompletedTask;

        public async Task<IEnumerable<InAppBillingProduct>> GetProductInfoAsync(ItemType itemType, params string[] productIds)
        {
            // Get list of products from store or simulator
            var listingInformation = testWithSimulator ? await CurrentAppSimulator.LoadListingInformationAsync() : await CurrentApp.LoadListingInformationAsync();

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
                    //CurrencyCode = product.CurrencyCode // Does not work at the moment, as UWP throws an InvalidCastException when getting CurrencyCode
                });
            }

            return products;
        }

        public async Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType, IInAppBillingVerifyPurchase verifyPurchase = null)
        {
            // Get list of product receipts from store or simulator
            var xmlReceipt = testWithSimulator ? await CurrentAppSimulator.GetAppReceiptAsync() : await CurrentApp.GetAppReceiptAsync();

            // Transform it to list of InAppBillingPurchase
            return xmlReceipt.ToInAppBillingPurchase(ProductPurchaseStatus.AlreadyPurchased);
        }

        public async Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, string payload, IInAppBillingVerifyPurchase verifyPurchase = null)
        {
            // Get purchase result from store or simulator
            var purchaseResult = testWithSimulator ? await CurrentAppSimulator.RequestProductPurchaseAsync(productId) : await CurrentApp.RequestProductPurchaseAsync(productId);

            // Transform it to InAppBillingPurchase
            return purchaseResult.ReceiptXml.ToInAppBillingPurchase(purchaseResult.Status).FirstOrDefault();
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
            xmlDoc.LoadXml(xml);

            // Iterate through all ProductReceipt elements
            var xmlProductReceipts = xmlDoc.GetElementsByTagName("ProductReceipt");
            for (var i = 0; i < xmlProductReceipts.Count; i++)
            {
                var xmlProductReceipt = xmlProductReceipts[i];

                // Create new InAppBillingPurchase with values from the xml element
                var purchase = new InAppBillingPurchase();
                purchase.Id = xmlProductReceipt.Attributes["Id"].Value;
                purchase.TransactionDateUtc = Convert.ToDateTime(xmlProductReceipt.Attributes["PurchaseDate"].Value);
                purchase.ProductId = xmlProductReceipt.Attributes["ProductId"].Value;
                purchase.AutoRenewing = false; // Not supported by UWP yet

                // Map native UWP status to PurchaseState
                switch (status)
                {
                    case ProductPurchaseStatus.AlreadyPurchased:
                    case ProductPurchaseStatus.Succeeded:
                        purchase.State = PurchaseState.Purchased;
                        break;
                    case ProductPurchaseStatus.NotFulfilled:
                        purchase.State = PurchaseState.Deferred;
                        break;
                    case ProductPurchaseStatus.NotPurchased:
                        purchase.State = PurchaseState.Canceled;
                        break;
                    default:
                        purchase.State = PurchaseState.Unknown;
                        break;
                }

                // Add to list of purchases
                purchases.Add(purchase);
            }

            return purchases;
        }
    }
}