using Foundation;
using Plugin.InAppBilling.Abstractions;
using StoreKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// Implementation for InAppBilling
    /// </summary>
    [Preserve(AllMembers = true)]
    public class InAppBillingImplementation : IInAppBilling
    {
        /// <summary>
        /// Default constructor for In App Billing on iOS
        /// </summary>
        public InAppBillingImplementation()
        {
            paymentObserver = new PaymentObserver();
            SKPaymentQueue.DefaultQueue.AddTransactionObserver(paymentObserver);
        }

        /// <summary>
        /// Gets or sets if in testing mode. Only for UWP
        /// </summary>
        public bool InTestingMode { get; set; }

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

        /// <summary>
        /// Get product information of a specific product
        /// </summary>
        /// <param name="productIds">Sku or Id of the product(s)</param>
        /// <param name="itemType">Type of product offering</param>
        /// <returns></returns>
        public async Task<IEnumerable<InAppBillingProduct>> GetProductInfoAsync(ItemType itemType, params string[] productIds)
        {
            var products = await GetProductAsync(productIds);

            return products.Select(p => new InAppBillingProduct
            {
                LocalizedPrice = p.LocalizedPrice(),
                MicrosPrice = (long)(p.Price.DoubleValue * 1000000d),
                Name = p.LocalizedTitle,
                ProductId = p.ProductIdentifier,
                Description = p.LocalizedDescription,
                CurrencyCode = p.PriceLocale?.CurrencyCode ?? string.Empty
            });
        }

        Task<IEnumerable<SKProduct>> GetProductAsync(string[] productId)
        {
            var productIdentifiers = NSSet.MakeNSObjectSet<NSString>(productId.Select(i => new NSString(i)).ToArray());

            var productRequestDelegate = new ProductRequestDelegate();

            //set up product request for in-app purchase
            var productsRequest = new SKProductsRequest(productIdentifiers);
            productsRequest.Delegate = productRequestDelegate; // SKProductsRequestDelegate.ReceivedResponse
            productsRequest.Start();

            return productRequestDelegate.WaitForResponse();
        }


        /// <summary>
        /// Get all current purhcase for a specifiy product type.
        /// </summary>
        /// <param name="itemType">Type of product</param>
        /// <param name="verifyPurchase">Interface to verify purchase</param>
        /// <returns>The current purchases</returns>
        public async Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType, IInAppBillingVerifyPurchase verifyPurchase = null)
        {
            var purchases = await RestoreAsync();
            

            var converted = purchases.Where(p => p != null).Select(p => p.ToIABPurchase());

            //try to validate purchases
            var valid = await ValidateReceipt(verifyPurchase, string.Empty, string.Empty);

            return valid ? converted : null;
        }



        Task<SKPaymentTransaction[]> RestoreAsync()
        {
            var tcsTransaction = new TaskCompletionSource<SKPaymentTransaction[]>();

            Action<SKPaymentTransaction[]> handler = null;
            handler = new Action<SKPaymentTransaction[]>(transactions =>
            {

                // Unsubscribe from future events
                paymentObserver.TransactionsRestored -= handler;

                if (transactions == null)
                    tcsTransaction.TrySetException(new Exception("Restore Transactions Failed"));
                else
                {
                    //look through and make sure everything is finished.
                    foreach (var t in transactions)
                        SKPaymentQueue.DefaultQueue.FinishTransaction(t);

                    tcsTransaction.TrySetResult(transactions);
                }
            });

            paymentObserver.TransactionsRestored += handler;

            // Start receiving restored transactions
            SKPaymentQueue.DefaultQueue.RestoreCompletedTransactions();

            return tcsTransaction.Task;
        }





        /// <summary>
        /// Purchase a specific product or subscription
        /// </summary>
        /// <param name="productId">Sku or ID of product</param>
        /// <param name="itemType">Type of product being requested</param>
        /// <param name="payload">Developer specific payload</param>
        /// <param name="verifyPurchase">Interface to verify purchase</param>
        /// <returns></returns>
        public async Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, string payload, IInAppBillingVerifyPurchase verifyPurchase = null)
        {
            var p = await PurchaseAsync(productId);

            var reference = new DateTime(2001, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

            var purchase = new InAppBillingPurchase
            {
                TransactionDateUtc = reference.AddSeconds(p.TransactionDate.SecondsSinceReferenceDate),
                Id = p.TransactionIdentifier,
                ProductId = p.Payment?.ProductIdentifier ?? string.Empty,
                State = p.GetPurchaseState()
            };

            if (verifyPurchase == null)
                return purchase;

            var validated = await ValidateReceipt(verifyPurchase, purchase.ProductId, purchase.Id);

            return validated ? purchase : null;
        }

        Task<bool> ValidateReceipt(IInAppBillingVerifyPurchase verifyPurchase, string productId, string transactionId)
        {
            if (verifyPurchase == null)
                return Task.FromResult(true);

            // Get the receipt data for (server-side) validation.
            // See: https://developer.apple.com/library/content/releasenotes/General/ValidateAppStoreReceipt/Introduction.html#//apple_ref/doc/uid/TP40010573
            var receiptUrl = NSData.FromUrl(NSBundle.MainBundle.AppStoreReceiptUrl);
            string receipt = receiptUrl.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

            return verifyPurchase.VerifyPurchase(receipt, string.Empty, productId, transactionId);
        }

        Task<SKPaymentTransaction> PurchaseAsync(string productId)
        {
            TaskCompletionSource<SKPaymentTransaction> tcsTransaction = new TaskCompletionSource<SKPaymentTransaction>();

            Action<SKPaymentTransaction, bool> handler = null;
            handler = new Action<SKPaymentTransaction, bool>((tran, success) =>
            {

                //Make sure we finigh the transaction
                SKPaymentQueue.DefaultQueue.FinishTransaction(tran);

                // Only handle results from this request
                if (productId != tran.Payment.ProductIdentifier)
                    return;

                // Unsubscribe from future events
                paymentObserver.TransactionCompleted -= handler;

                if(success)
                {
                    tcsTransaction.TrySetResult(tran);
                    return;
                }

                var errorCode = tran?.Error?.Code ?? -1;
                var description = tran?.Error?.LocalizedDescription ?? string.Empty;
                var error = PurchaseError.GeneralError;
                switch (errorCode)
                {
                    case (int)SKError.PaymentCancelled:
                        error = PurchaseError.UserCancelled;
                        break;
                    case (int)SKError.PaymentInvalid:
                        error = PurchaseError.PaymentInvalid;
                        break;
                    case (int)SKError.PaymentNotAllowed:
                        error = PurchaseError.PaymentNotAllowed;
                        break;
                    case (int)SKError.ProductNotAvailable:
                        error = PurchaseError.ItemUnavailable;
                        break;
                    case (int)SKError.Unknown:
                        error = PurchaseError.GeneralError;
                        break;
                    case (int)SKError.ClientInvalid:
                        error = PurchaseError.BillingUnavailable;
                        break;
                }

                tcsTransaction.TrySetException(new InAppBillingPurchaseException(error, description));
                
            });

            paymentObserver.TransactionCompleted += handler;

            var payment = SKPayment.CreateFrom(productId);
            SKPaymentQueue.DefaultQueue.AddPayment(payment);

            return tcsTransaction.Task;
        }


        /// <summary>
        /// Consume a purchase with a purchase token.
        /// </summary>
        /// <param name="productId">Id or Sku of product</param>
        /// <param name="purchaseToken">Original Purchase Token</param>
        /// <returns>If consumed successful</returns>
        /// <exception cref="InAppBillingPurchaseException">If an error occures during processing</exception>
		public Task<InAppBillingPurchase> ConsumePurchaseAsync(string productId, string purchaseToken)
        {
            return null;
        }

        /// <summary>
        /// Consume a purchase
        /// </summary>
        /// <param name="productId">Id/Sku of the product</param>
        /// <param name="payload">Developer specific payload of original purchase</param>
        /// <param name="itemType">Type of product being consumed.</param>
        /// <param name="verifyPurchase">Verify Purchase implementation</param>
        /// <returns>If consumed successful</returns>
        /// <exception cref="InAppBillingPurchaseException">If an error occures during processing</exception>
        public Task<InAppBillingPurchase> ConsumePurchaseAsync(string productId, ItemType itemType, string payload, IInAppBillingVerifyPurchase verifyPurchase = null)
        {
            return null;
        }

        PaymentObserver paymentObserver;

        static DateTime NSDateToDateTimeUtc(NSDate date)
        {
            var reference = new DateTime(2001, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);


            return reference.AddSeconds(date?.SecondsSinceReferenceDate ?? 0);
        }
    }


    [Preserve(AllMembers = true)]
    class ProductRequestDelegate : NSObject, ISKProductsRequestDelegate, ISKRequestDelegate
    {
        TaskCompletionSource<IEnumerable<SKProduct>> tcsResponse = new TaskCompletionSource<IEnumerable<SKProduct>>();

        public Task<IEnumerable<SKProduct>> WaitForResponse()
        {
            return tcsResponse.Task;
        }

        [Export("request:didFailWithError:")]
        public void RequestFailed(SKRequest request, NSError error)
        {
            tcsResponse.TrySetException(new Exception(error.LocalizedDescription));
        }

        public void ReceivedResponse(SKProductsRequest request, SKProductsResponse response)
        {
            var product = response.Products;

            if (product != null)
            {
                tcsResponse.TrySetResult(product);
                return;
            }

            tcsResponse.TrySetException(new Exception("Invalid Product"));
        }
    }


    [Preserve(AllMembers = true)]
    class PaymentObserver : SKPaymentTransactionObserver
    {
        public event Action<SKPaymentTransaction, bool> TransactionCompleted;
        public event Action<SKPaymentTransaction[]> TransactionsRestored;

        List<SKPaymentTransaction> restoredTransactions = new List<SKPaymentTransaction>();

        public override void UpdatedTransactions(SKPaymentQueue queue, SKPaymentTransaction[] transactions)
        {
            var rt = transactions.Where(pt => pt.TransactionState == SKPaymentTransactionState.Restored);

            // Add our restored transactions to the list
            // We might still get more from the initial request so we won't raise the event until
            // RestoreCompletedTransactionsFinished is called
            if (rt?.Any() ?? false)
                restoredTransactions.AddRange(rt);

            foreach (SKPaymentTransaction transaction in transactions)
            {
                switch (transaction.TransactionState)
                {
                    case SKPaymentTransactionState.Purchased:
                        TransactionCompleted?.Invoke(transaction, true);
                        break;
                    case SKPaymentTransactionState.Failed:
                        TransactionCompleted?.Invoke(transaction, false);
                        break;
                    default:
                        break;
                }
            }
        }

        public override void RestoreCompletedTransactionsFinished(SKPaymentQueue queue)
        {
            // This is called after all restored transactions have hit UpdatedTransactions
            // at this point we are done with the restore request so let's fire up the event
            var rt = restoredTransactions.ToArray();
            // Clear out the list of incoming restore transactions for future requests
            restoredTransactions.Clear();

            TransactionsRestored?.Invoke(rt);
        }

        public override void RestoreCompletedTransactionsFailedWithError(SKPaymentQueue queue, Foundation.NSError error)
        {
            // Failure, just fire with null
            TransactionsRestored?.Invoke(null);
        }
    }



    [Preserve(AllMembers = true)]
    static class SKTransactionExtensions
    {

        public static InAppBillingPurchase ToIABPurchase(this SKPaymentTransaction transaction)
        {
            var p = transaction.OriginalTransaction;
            if (p == null)
                p = transaction;

            if (p == null)
                return null;

            var newP = new InAppBillingPurchase
            {
                TransactionDateUtc = NSDateToDateTimeUtc(p.TransactionDate),
                Id = p.TransactionIdentifier,
                ProductId = p.Payment?.ProductIdentifier ?? string.Empty,
                State = p.GetPurchaseState()
            };

            return newP;
        }

        static DateTime NSDateToDateTimeUtc(NSDate date)
        {
            var reference = new DateTime(2001, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

            return reference.AddSeconds(date?.SecondsSinceReferenceDate ?? 0);
        }

        public static PurchaseState GetPurchaseState(this SKPaymentTransaction transaction)
        {
            if (transaction.TransactionState == null)
                return Abstractions.PurchaseState.Unknown;

            switch (transaction.TransactionState)
            {
                case SKPaymentTransactionState.Restored:
                    return Abstractions.PurchaseState.Restored;
                case SKPaymentTransactionState.Purchasing:
                    return Abstractions.PurchaseState.Purchasing;
                case SKPaymentTransactionState.Purchased:
                    return Abstractions.PurchaseState.Purchased;
                case SKPaymentTransactionState.Failed:
                    return Abstractions.PurchaseState.Failed;
                case SKPaymentTransactionState.Deferred:
                    return Abstractions.PurchaseState.Deferred;
            }

            return Abstractions.PurchaseState.Unknown;
        }


    }


    [Preserve(AllMembers = true)]
    static class SKProductExtension
    {
        /// <remarks>
        /// Use Apple's sample code for formatting a SKProduct price
        /// https://developer.apple.com/library/ios/#DOCUMENTATION/StoreKit/Reference/SKProduct_Reference/Reference/Reference.html#//apple_ref/occ/instp/SKProduct/priceLocale
        /// Objective-C version:
        ///    NSNumberFormatter *numberFormatter = [[NSNumberFormatter alloc] init];
        ///    [numberFormatter setFormatterBehavior:NSNumberFormatterBehavior10_4];
        ///    [numberFormatter setNumberStyle:NSNumberFormatterCurrencyStyle];
        ///    [numberFormatter setLocale:product.priceLocale];
        ///    NSString *formattedString = [numberFormatter stringFromNumber:product.price];
        /// </remarks>
        public static string LocalizedPrice(this SKProduct product)
        {
            var formatter = new NSNumberFormatter();
            formatter.FormatterBehavior = NSNumberFormatterBehavior.Version_10_4;
            formatter.NumberStyle = NSNumberFormatterStyle.Currency;
            formatter.Locale = product.PriceLocale;
            var formattedString = formatter.StringFromNumber(product.Price);
            Console.WriteLine(" ** formatter.StringFromNumber(" + product.Price + ") = " + formattedString + " for locale " + product.PriceLocale.LocaleIdentifier);
            return formattedString;
        }
    }
}