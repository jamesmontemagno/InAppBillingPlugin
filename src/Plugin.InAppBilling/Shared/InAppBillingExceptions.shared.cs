using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// Type of purchase error
    /// </summary>
    public enum PurchaseError
    {
		/// <summary>
		/// Billing API version is not supported for the type requested (Android), client error (iOS)
		/// </summary>
		BillingUnavailable,
        /// <summary>
        /// Developer issue
        /// </summary>
        DeveloperError,
        /// <summary>
        /// Product sku not available
        /// </summary>
        ItemUnavailable,
        /// <summary>
        /// Other error
        /// </summary>
        GeneralError,
        /// <summary>
        /// User cancelled the purchase
        /// </summary>
        UserCancelled,
        /// <summary>
        /// App store unavailable on device
        /// </summary>
        AppStoreUnavailable,
        /// <summary>
        /// User is not allowed to authorize payments
        /// </summary>
        PaymentNotAllowed,
        /// <summary>
        /// One of the payment parameters was not recognized by app store
        /// </summary>
        PaymentInvalid,
        /// <summary>
        /// The requested product is invalid
        /// </summary>
        InvalidProduct,
        /// <summary>
        /// The product request failed
        /// </summary>
        ProductRequestFailed,
        /// <summary>
        /// Restoring the transaction failed
        /// </summary>
        RestoreFailed,
        /// <summary>
        /// Network connection is down
        /// </summary>
        ServiceUnavailable,
		/// <summary>
		/// Product is already owned
		/// </summary>
		AlreadyOwned,
		/// <summary>
		/// Item is not owned and can not be consumed
		/// </summary>
		NotOwned,
        FeatureNotSupported,
        ServiceDisconnected,
        ServiceTimeout,
        AppleTermsConditionsChanged,
        NetworkError
    }

    /// <summary>
    /// Purchase exception
    /// </summary>
    public class InAppBillingPurchaseException : Exception
    {
        /// <summary>
        /// Type of error
        /// </summary>
        public PurchaseError PurchaseError { get; }

        public string[] Invalid { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="error"></param>
        /// <param name="ex"></param>
        public InAppBillingPurchaseException(PurchaseError error, Exception ex) : base($"Unable to process purchase : {error:G}.", ex) => PurchaseError = error;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="error"></param>
        public InAppBillingPurchaseException(PurchaseError error) : base($"Unable to process purchase : {error:G}.") => PurchaseError = error;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="error"></param>
        public InAppBillingPurchaseException(PurchaseError error, string message) : base(message) => PurchaseError = error;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="error"></param>
        public InAppBillingPurchaseException(PurchaseError error, string message, string[] invalid) : base(message)
        {
            PurchaseError = error;
            Invalid = invalid;
        }
    }
}
