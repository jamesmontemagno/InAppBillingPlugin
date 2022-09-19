using Foundation;
using StoreKit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Plugin.InAppBilling
{
	/// <summary>
	/// Implementation for InAppBilling
	/// </summary>
	[Preserve(AllMembers = true)]
	public class InAppBillingImplementation : BaseInAppBilling
	{
        /// <summary>
        /// Backwards compat flag that may be removed in the future to auto finish all transactions like in v4
        /// </summary>
        public static bool FinishAllTransactions { get; set; } = false;

#if __IOS__ || __TVOS__
        internal static bool HasIntroductoryOffer => UIKit.UIDevice.CurrentDevice.CheckSystemVersion(11, 2);
        internal static bool HasProductDiscounts => UIKit.UIDevice.CurrentDevice.CheckSystemVersion(12, 2);
        internal static bool HasSubscriptionGroupId => UIKit.UIDevice.CurrentDevice.CheckSystemVersion(12, 0);
        internal static bool HasStorefront => UIKit.UIDevice.CurrentDevice.CheckSystemVersion(13, 0);
        internal static bool HasFamilyShareable => UIKit.UIDevice.CurrentDevice.CheckSystemVersion(14, 0);
#else
		static bool initIntro, hasIntro, initDiscounts, hasDiscounts, initFamily, hasFamily, initSubGroup, hasSubGroup, initStore, hasStore;
		internal static bool HasIntroductoryOffer
        {
			get
            {
				if (initIntro)
					return hasIntro;

				initIntro = true;


				using var info = new NSProcessInfo();
				hasIntro = info.IsOperatingSystemAtLeastVersion(new NSOperatingSystemVersion(10,13,2));
				return hasIntro;

			}
        }
        internal static bool HasStorefront
        {
            get
            {
                if (initStore)
                    return hasStore;

                initStore = true;


                using var info = new NSProcessInfo();
                hasStore = info.IsOperatingSystemAtLeastVersion(new NSOperatingSystemVersion(10, 15, 0));
                return hasStore;

            }
        }
        internal static bool HasProductDiscounts
        {
			get
            {
				if (initDiscounts)
					return hasDiscounts;

				initDiscounts = true;


				using var info = new NSProcessInfo();
				hasDiscounts = info.IsOperatingSystemAtLeastVersion(new NSOperatingSystemVersion(10,14,4));
				return hasDiscounts;

			}
        }

        internal static bool HasSubscriptionGroupId
        {
			get
            {
				if (initSubGroup)
					return hasSubGroup;

				initSubGroup = true;


				using var info = new NSProcessInfo();
				hasSubGroup = info.IsOperatingSystemAtLeastVersion(new NSOperatingSystemVersion(10,14,0));
				return hasSubGroup;

			}
        }

        internal static bool HasFamilyShareable
        {
			get
            {
				if (initFamily)
					return hasFamily;

				initFamily = true;


				using var info = new NSProcessInfo();
				hasFamily = info.IsOperatingSystemAtLeastVersion(new NSOperatingSystemVersion(11,0,0));
				return hasFamily;

			}
        }
#endif


        /// <summary>
        /// iOS: Displays a sheet that enables users to redeem subscription offer codes that you configure in App Store Connect.
        /// </summary>
        public override void PresentCodeRedemption() 
        {
#if __IOS__ && !__MACCATALYST__
            if(HasFamilyShareable)
                SKPaymentQueue.DefaultQueue.PresentCodeRedemptionSheet();
#endif
        }

        Storefront storefront;
        /// <summary>
        /// Returns representation of storefront on iOS 13+
        /// </summary>
        public override Storefront Storefront => HasStorefront ? (storefront ??= new Storefront
        {
            CountryCode = SKPaymentQueue.DefaultQueue.Storefront.CountryCode,
            Id = SKPaymentQueue.DefaultQueue.Storefront.Identifier
        }) : null;

        /// <summary>
        /// Gets if user can make payments
        /// </summary>
        public override bool CanMakePayments => SKPaymentQueue.CanMakePayments;

        /// <summary>
        /// Gets or sets a callback for out of band purchases to complete.
        /// </summary>
        public static Action<InAppBillingPurchase> OnPurchaseComplete { get; set; } = null;


        /// <summary>
        /// Gets or sets a callback for out of band failures to complete.
        /// </summary>
        public static Action<InAppBillingPurchase> OnPurchaseFailure { get; set; } = null;

        /// <summary>
        /// 
        /// </summary>
		public static Func<SKPaymentQueue, SKPayment, SKProduct, bool> OnShouldAddStorePayment { get; set; } = null;

		/// <summary>
		/// Default constructor for In App Billing on iOS
		/// </summary>
		public InAppBillingImplementation()
		{
			Init();
		}

		void Init()
		{
			if(paymentObserver != null)
				return;

			paymentObserver = new PaymentObserver(OnPurchaseComplete, OnPurchaseFailure, OnShouldAddStorePayment);
			SKPaymentQueue.DefaultQueue.AddTransactionObserver(paymentObserver);
		}

		/// <summary>
		/// Gets or sets if in testing mode. Only for UWP
		/// </summary>
		public override bool InTestingMode { get; set; }


        /// <summary>
        /// Get product information of a specific product
        /// </summary>
        /// <param name="productIds">Sku or Id of the product(s)</param>
        /// <param name="itemType">Type of product offering</param>
        /// <returns></returns>
        public async override Task<IEnumerable<InAppBillingProduct>> GetProductInfoAsync(ItemType itemType, params string[] productIds)
		{
			Init();
			var products = await GetProductAsync(productIds);

			return products.Select(p => new InAppBillingProduct
			{
				LocalizedPrice = p.LocalizedPrice(),
				MicrosPrice = (long)(p.Price.DoubleValue * 1000000d),
				Name = p.LocalizedTitle,
                ProductId = p.ProductIdentifier,
                Description = p.LocalizedDescription,
				CurrencyCode = p.PriceLocale?.CurrencyCode ?? string.Empty,
                AppleExtras = new InAppBillingProductAppleExtras
                {
                    IsFamilyShareable = HasFamilyShareable && p.IsFamilyShareable,
                    SubscriptionGroupId = HasSubscriptionGroupId ? p.SubscriptionGroupIdentifier : null,
                    SubscriptionPeriod = p.ToSubscriptionPeriod(),
                    IntroductoryOffer = HasIntroductoryOffer ? p.IntroductoryPrice?.ToProductDiscount() : null,
                    Discounts = HasProductDiscounts ? p.Discounts?.Select(s => s.ToProductDiscount()).ToList() ?? null : null
                }
            });
		}

		Task<IEnumerable<SKProduct>> GetProductAsync(string[] productId)
		{
			var productIdentifiers = NSSet.MakeNSObjectSet<NSString>(productId.Select(i => new NSString(i)).ToArray());

			var productRequestDelegate = new ProductRequestDelegate();

			//set up product request for in-app purchase
			var productsRequest = new SKProductsRequest(productIdentifiers)
			{
				Delegate = productRequestDelegate // SKProductsRequestDelegate.ReceivedResponse
			};
			productsRequest.Start();

			return productRequestDelegate.WaitForResponse();
		}

        /// <summary>
        /// Get app purchaes
        /// </summary>
        /// <param name="itemType"></param>
        /// <returns></returns>
		public async override Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType)
		{
			Init();
			var purchases = await RestoreAsync();

			var comparer = new InAppBillingPurchaseComparer();
			return purchases
				?.Where(p => p != null)
				?.Select(p2 => p2.ToIABPurchase())
				?.Distinct(comparer);
		}



		Task<SKPaymentTransaction[]> RestoreAsync()
		{
			var tcsTransaction = new TaskCompletionSource<SKPaymentTransaction[]>();

			var allTransactions = new List<SKPaymentTransaction>();

			Action<SKPaymentTransaction[]> handler = null;
			handler = new Action<SKPaymentTransaction[]>(transactions =>
			{
                // Unsubscribe from future events
                paymentObserver.TransactionsRestored -= handler;

				if (transactions == null)
				{
					if (allTransactions.Count == 0)
						tcsTransaction.TrySetException(new InAppBillingPurchaseException(PurchaseError.RestoreFailed, "Restore Transactions Failed"));
					else
						tcsTransaction.TrySetResult(allTransactions.ToArray());
				}
				else
				{
					allTransactions.AddRange(transactions);
					tcsTransaction.TrySetResult(allTransactions.ToArray());
				}
			});


            paymentObserver.TransactionsRestored += handler;

			foreach (var trans in SKPaymentQueue.DefaultQueue.Transactions)
			{
				var original = FindOriginalTransaction(trans);
				if (original == null)
					continue;

				allTransactions.Add(original);
			}

			// Start receiving restored transactions
			SKPaymentQueue.DefaultQueue.RestoreCompletedTransactions();

			return tcsTransaction.Task;
		}



		static SKPaymentTransaction FindOriginalTransaction(SKPaymentTransaction transaction)
		{
			if (transaction == null)
				return null;

			if (transaction.TransactionState == SKPaymentTransactionState.Purchased ||
				transaction.TransactionState == SKPaymentTransactionState.Purchasing)
				return transaction;

			if (transaction.OriginalTransaction != null)
				return FindOriginalTransaction(transaction.OriginalTransaction);

			return transaction;

		}




        /// <summary>
        /// Purchase a specific product or subscription
        /// </summary>
        /// <param name="productId">Sku or ID of product</param>
        /// <param name="itemType">Type of product being requested</param>
        /// <param name="obfuscatedAccountId">Specifies an optional obfuscated string that is uniquely associated with the user's account in your app.</param>
        /// <param name="obfuscatedProfileId">Specifies an optional obfuscated string that is uniquely associated with the user's profile in your app.</param>
        /// <returns></returns>
        public async override Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, string obfuscatedAccountId = null, string obfuscatedProfileId = null)
		{
			Init();
			var p = await PurchaseAsync(productId, itemType, obfuscatedAccountId);

			var reference = new DateTime(2001, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);


			var purchase = new InAppBillingPurchase
			{
				TransactionDateUtc = reference.AddSeconds(p.TransactionDate.SecondsSinceReferenceDate),
				Id = p.TransactionIdentifier,
                TransactionIdentifier = p.TransactionIdentifier,
                ProductId = p.Payment?.ProductIdentifier ?? string.Empty,
                ProductIds = new string[] { p.Payment?.ProductIdentifier ?? string.Empty },
                State = p.GetPurchaseState(),
                ApplicationUsername = p.Payment?.ApplicationUsername ?? string.Empty,
#if __IOS__ || __TVOS__
                PurchaseToken = p.TransactionReceipt?.GetBase64EncodedString(NSDataBase64EncodingOptions.None) ?? string.Empty
#endif
			};

            return purchase;
		}


		async Task<SKPaymentTransaction> PurchaseAsync(string productId, ItemType itemType, string applicationUserName)
		{
			var tcsTransaction = new TaskCompletionSource<SKPaymentTransaction>();

			Action<SKPaymentTransaction, bool> handler = null;
			handler = new Action<SKPaymentTransaction, bool>((tran, success) =>
			{
				if (tran?.Payment == null)
					return;

				// Only handle results from this request
				if (productId != tran.Payment.ProductIdentifier)
					return;

				// Unsubscribe from future events
				paymentObserver.TransactionCompleted -= handler;

				if (success)
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
                        try 
                        { 
                            var underlyingError = tran?.Error?.UserInfo?["NSUnderlyingError"] as NSError;
                            error = underlyingError?.Code == 3038 ? PurchaseError.AppleTermsConditionsChanged : PurchaseError.GeneralError;
                        }
                        catch
                        {
                            error = PurchaseError.GeneralError;
                        }
						break;
					case (int)SKError.ClientInvalid:
						error = PurchaseError.BillingUnavailable;
						break;
				}

				tcsTransaction.TrySetException(new InAppBillingPurchaseException(error, description));

			});
            
            paymentObserver.TransactionCompleted += handler;

			var products = await GetProductAsync(new[] { productId });
			var product = products?.FirstOrDefault();
			if (product == null)
				throw new InAppBillingPurchaseException(PurchaseError.InvalidProduct);

            if (string.IsNullOrWhiteSpace(applicationUserName))
            {
                var payment = SKPayment.CreateFrom(product);
                //var payment = SKPayment.CreateFrom((SKProduct)SKProduct.FromObject(new NSString(productId)));
                
                SKPaymentQueue.DefaultQueue.AddPayment(payment);
            }
            else
            {
                var payment = SKMutablePayment.PaymentWithProduct(product);
                payment.ApplicationUsername = applicationUserName;

                SKPaymentQueue.DefaultQueue.AddPayment(payment);
            }

            return await tcsTransaction.Task;
		}

        /// <summary>
        /// (iOS not supported) Apple store manages upgrades natively when subscriptions of the same group are purchased.
        /// </summary>
        /// <exception cref="NotImplementedException">iOS not supported</exception>
        public override Task<InAppBillingPurchase> UpgradePurchasedSubscriptionAsync(string newProductId, string purchaseTokenOfOriginalSubscription, SubscriptionProrationMode prorationMode = SubscriptionProrationMode.ImmediateWithTimeProration) =>
            throw new NotImplementedException("iOS not supported. Apple store manages upgrades natively when subscriptions of the same group are purchased.");


        /// <summary>
        /// gets receipt data from bundle
        /// </summary>
        public override string ReceiptData
        {
            get
            {
                // Get the receipt data for (server-side) validation.
                // See: https://developer.apple.com/library/content/releasenotes/General/ValidateAppStoreReceipt/Introduction.html#//apple_ref/doc/uid/TP40010573
                NSData receiptUrl = null;
                if (NSBundle.MainBundle.AppStoreReceiptUrl != null)
                    receiptUrl = NSData.FromUrl(NSBundle.MainBundle.AppStoreReceiptUrl);

                return receiptUrl?.GetBase64EncodedString(NSDataBase64EncodingOptions.None);
            }
        }


        /// <summary>
        /// Consume a purchase with a purchase token.
        /// </summary>
        /// <param name="productId">Id or Sku of product</param>
        /// <param name="transactionIdentifier">Original Purchase Token</param>
        /// <returns>If consumed successful</returns>
        /// <exception cref="InAppBillingPurchaseException">If an error occurs during processing</exception>
        public override Task<bool> ConsumePurchaseAsync(string productId, string transactionIdentifier) =>
			FinalizePurchaseAsync(transactionIdentifier);

        /// <summary>
        /// Finish a transaction manually
        /// </summary>
        /// <param name="transactionIdentifier"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public async override Task<bool> FinalizePurchaseAsync(string transactionIdentifier)
        {
			if (string.IsNullOrWhiteSpace(transactionIdentifier))
				throw new ArgumentException("Purchase Token must be valid", nameof(transactionIdentifier));
            
			var purchases = await RestoreAsync();

			if (purchases == null)
				return false;

			var transaction = purchases.Where(p => p.TransactionIdentifier == transactionIdentifier).FirstOrDefault();
			if (transaction == null)
				return false;

			try
			{
				SKPaymentQueue.DefaultQueue.FinishTransaction(transaction);
			}
			catch(Exception ex)
			{
                Debug.WriteLine("Unable to finish transaction: " + ex);
				return false;
			}

			return true;
		}

		PaymentObserver paymentObserver;


		bool disposed = false;


		/// <summary>
		/// Dispose
		/// </summary>
		/// <param name="disposing"></param>
		public override void Dispose(bool disposing)
		{
			if (disposed)
			{
				base.Dispose(disposing);
				return;
			}

			disposed = true;

			if (!disposing)
			{
				base.Dispose(disposing);
				return;
			}

			if (paymentObserver != null)
			{
				SKPaymentQueue.DefaultQueue.RemoveTransactionObserver(paymentObserver);
				paymentObserver.Dispose();
				paymentObserver = null;
			}


			base.Dispose(disposing);
		}

    }


	[Preserve(AllMembers = true)]
	class ProductRequestDelegate : NSObject, ISKProductsRequestDelegate, ISKRequestDelegate
	{
        readonly TaskCompletionSource<IEnumerable<SKProduct>> tcsResponse = new();

		public Task<IEnumerable<SKProduct>> WaitForResponse() =>
			tcsResponse.Task;


		[Export("request:didFailWithError:")]
		public void RequestFailed(SKRequest request, NSError error) =>
			tcsResponse.TrySetException(new InAppBillingPurchaseException(PurchaseError.ProductRequestFailed, error.LocalizedDescription));


		public void ReceivedResponse(SKProductsRequest request, SKProductsResponse response)
		{
			var invalidProduct = response.InvalidProducts;
			if (invalidProduct?.Any() ?? false)
			{
				tcsResponse.TrySetException(new InAppBillingPurchaseException(PurchaseError.InvalidProduct, $"Invalid Product: {invalidProduct.First()}"));
				return;
			}

			var product = response.Products;
			if (product != null)
			{
				tcsResponse.TrySetResult(product);
				return;
			}
		}
	}


	[Preserve(AllMembers = true)]
	class PaymentObserver : SKPaymentTransactionObserver
	{
		public event Action<SKPaymentTransaction, bool> TransactionCompleted;
		public event Action<SKPaymentTransaction[]> TransactionsRestored;

		readonly List<SKPaymentTransaction> restoredTransactions = new ();
        readonly Action<InAppBillingPurchase> onPurchaseSuccess;
        readonly Action<InAppBillingPurchase> onPurchaseFailure;
        readonly Func<SKPaymentQueue, SKPayment, SKProduct, bool> onShouldAddStorePayment;

		public PaymentObserver(Action<InAppBillingPurchase> onPurchaseSuccess, Action<InAppBillingPurchase> onPurchaseFailure, Func<SKPaymentQueue, SKPayment, SKProduct, bool> onShouldAddStorePayment)
		{
			this.onPurchaseSuccess = onPurchaseSuccess;
            this.onPurchaseFailure = onPurchaseFailure;
            this.onShouldAddStorePayment = onShouldAddStorePayment;
		}

        public override bool ShouldAddStorePayment(SKPaymentQueue queue, SKPayment payment, SKProduct product) => 
            onShouldAddStorePayment?.Invoke(queue, payment, product) ?? false;

        public override void UpdatedTransactions(SKPaymentQueue queue, SKPaymentTransaction[] transactions)
		{
			var rt = transactions.Where(pt => pt.TransactionState == SKPaymentTransactionState.Restored);

			// Add our restored transactions to the list
			// We might still get more from the initial request so we won't raise the event until
			// RestoreCompletedTransactionsFinished is called
			if (rt?.Any() ?? false)
				restoredTransactions.AddRange(rt);

			foreach (var transaction in transactions)
			{
				if (transaction?.TransactionState == null)
					break;

				Debug.WriteLine($"Updated Transaction | {transaction.ToStatusString()}");

				switch (transaction.TransactionState)
				{
					case SKPaymentTransactionState.Restored:
					case SKPaymentTransactionState.Purchased:
						TransactionCompleted?.Invoke(transaction, true);

						onPurchaseSuccess?.Invoke(transaction.ToIABPurchase());

                        if(InAppBillingImplementation.FinishAllTransactions)
                            SKPaymentQueue.DefaultQueue.FinishTransaction(transaction);
                        break;
					case SKPaymentTransactionState.Failed:
						TransactionCompleted?.Invoke(transaction, false);
                        onPurchaseFailure?.Invoke(transaction?.ToIABPurchase());

                        if (InAppBillingImplementation.FinishAllTransactions)
                            SKPaymentQueue.DefaultQueue.FinishTransaction(transaction);
                        break;
					default:
						break;
				}
			}
		}

		public override void RestoreCompletedTransactionsFinished(SKPaymentQueue queue)
		{
			if (restoredTransactions == null)
				return;

			// This is called after all restored transactions have hit UpdatedTransactions
			// at this point we are done with the restore request so let's fire up the event
			var allTransactions = restoredTransactions.ToArray();

			// Clear out the list of incoming restore transactions for future requests
			restoredTransactions.Clear();

			TransactionsRestored?.Invoke(allTransactions);


            if (InAppBillingImplementation.FinishAllTransactions)
                foreach (var transaction in allTransactions)
                    SKPaymentQueue.DefaultQueue.FinishTransaction(transaction);
        }

		// Failure, just fire with null
		public override void RestoreCompletedTransactionsFailedWithError(SKPaymentQueue queue, NSError error) =>
			TransactionsRestored?.Invoke(null);

	}



	[Preserve(AllMembers = true)]
	static class SKTransactionExtensions
	{
		public static string ToStatusString(this SKPaymentTransaction transaction) =>
			transaction?.ToIABPurchase()?.ToString() ?? string.Empty;


		public static InAppBillingPurchase ToIABPurchase(this SKPaymentTransaction transaction)
		{
			var p = transaction?.OriginalTransaction ?? transaction;

			if (p == null)
				return null;

#if __IOS__ || __TVOS__
			var finalToken = p.TransactionReceipt?.GetBase64EncodedString(NSDataBase64EncodingOptions.None);
            if (string.IsNullOrEmpty(finalToken))
				finalToken = transaction.TransactionReceipt?.GetBase64EncodedString(NSDataBase64EncodingOptions.None);

#else
			var finalToken = string.Empty;
#endif
			return new InAppBillingPurchase
			{
				TransactionDateUtc = NSDateToDateTimeUtc(transaction.TransactionDate),
				Id = p.TransactionIdentifier,
                TransactionIdentifier = p.TransactionIdentifier,
                ProductId = p.Payment?.ProductIdentifier ?? string.Empty,
                ProductIds = new string[] { p.Payment?.ProductIdentifier ?? string.Empty },
                State = p.GetPurchaseState(),
				PurchaseToken = finalToken,
                ApplicationUsername = p.Payment?.ApplicationUsername
            };
		}

		static DateTime NSDateToDateTimeUtc(NSDate date)
		{
			var reference = new DateTime(2001, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

			return reference.AddSeconds(date?.SecondsSinceReferenceDate ?? 0);
		}

		public static PurchaseState GetPurchaseState(this SKPaymentTransaction transaction)
		{

			if (transaction?.TransactionState == null)
				return PurchaseState.Unknown;

            switch (transaction.TransactionState)
            {
                case SKPaymentTransactionState.Restored:
                    return PurchaseState.Restored;
                case SKPaymentTransactionState.Purchasing:
                    return PurchaseState.Purchasing;
                case SKPaymentTransactionState.Purchased:
                    return PurchaseState.Purchased;
                case SKPaymentTransactionState.Failed:
                    return PurchaseState.Failed;
                case SKPaymentTransactionState.Deferred:
                    return PurchaseState.Deferred;
                default:
                    break;
            }

            return PurchaseState.Unknown;
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
			if (product?.PriceLocale == null)
				return string.Empty;

			var formatter = new NSNumberFormatter()
			{
				FormatterBehavior = NSNumberFormatterBehavior.Version_10_4,
				NumberStyle = NSNumberFormatterStyle.Currency,
				Locale = product.PriceLocale
			};
			var formattedString = formatter.StringFromNumber(product.Price);
			Console.WriteLine(" ** formatter.StringFromNumber(" + product.Price + ") = " + formattedString + " for locale " + product.PriceLocale.LocaleIdentifier);
			return formattedString;
		}

        public static SubscriptionPeriod ToSubscriptionPeriod(this SKProduct p)
        {
            if (!InAppBillingImplementation.HasIntroductoryOffer)
                return SubscriptionPeriod.Unknown;

            if (p?.SubscriptionPeriod?.Unit == null)
                return SubscriptionPeriod.Unknown;

            return p.SubscriptionPeriod.Unit switch
            {
                SKProductPeriodUnit.Day => SubscriptionPeriod.Day,
                SKProductPeriodUnit.Month => SubscriptionPeriod.Month,
                SKProductPeriodUnit.Year => SubscriptionPeriod.Year,
                SKProductPeriodUnit.Week => SubscriptionPeriod.Week,
                _ => SubscriptionPeriod.Unknown,
            };
        }

        public static InAppBillingProductDiscount ToProductDiscount(this SKProductDiscount pd)
        {
            if (!InAppBillingImplementation.HasIntroductoryOffer)
                return null;
            
            if (pd == null)
                return null;
            

            var discount = new InAppBillingProductDiscount
            {
                LocalizedPrice = pd.LocalizedPrice(),
                Price = (pd.Price?.DoubleValue ?? 0) * 1000000d,
                NumberOfPeriods = (int)pd.NumberOfPeriods,
                CurrencyCode = pd.PriceLocale?.CurrencyCode ?? string.Empty
            };

            discount.SubscriptionPeriod = pd.SubscriptionPeriod.Unit switch
            {
                SKProductPeriodUnit.Day => SubscriptionPeriod.Day,
                SKProductPeriodUnit.Month => SubscriptionPeriod.Month,
                SKProductPeriodUnit.Year => SubscriptionPeriod.Year,
                SKProductPeriodUnit.Week => SubscriptionPeriod.Week,
                _ => SubscriptionPeriod.Unknown
            };

            discount.PaymentMode = pd.PaymentMode switch
            {
                SKProductDiscountPaymentMode.FreeTrial => PaymentMode.FreeTrial,
                SKProductDiscountPaymentMode.PayUpFront => PaymentMode.PayUpFront,
                SKProductDiscountPaymentMode.PayAsYouGo => PaymentMode.PayAsYouGo,
                _ => PaymentMode.Unknown,
            };

            if(InAppBillingImplementation.HasProductDiscounts)
            {
                discount.Id = pd.Identifier;
                discount.Type = pd.Type switch
                {
                    SKProductDiscountType.Introductory => ProductDiscountType.Introductory,
                    SKProductDiscountType.Subscription => ProductDiscountType.Subscription,
                    _ => ProductDiscountType.Unknown,
                };
            }

            return discount;
        }

        public static string LocalizedPrice(this SKProductDiscount product)
		{
			if (product?.PriceLocale == null)
				return string.Empty;

			var formatter = new NSNumberFormatter()
			{
				FormatterBehavior = NSNumberFormatterBehavior.Version_10_4,
				NumberStyle = NSNumberFormatterStyle.Currency,
				Locale = product.PriceLocale
			};
			var formattedString = formatter.StringFromNumber(product.Price);
			Console.WriteLine(" ** formatter.StringFromNumber(" + product.Price + ") = " + formattedString + " for locale " + product.PriceLocale.LocaleIdentifier);
			return formattedString;
		}
	}
}
