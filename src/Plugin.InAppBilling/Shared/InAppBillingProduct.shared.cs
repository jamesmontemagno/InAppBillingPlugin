
using System;
using System.Collections.Generic;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// Product info specific to Apple Platforms
    /// </summary>
    [Preserve(AllMembers = true)]
    public class InAppBillingProductAppleExtras
    {
        /// <summary>
        /// The identifier of the subscription group to which the subscription belongs.
        /// </summary>
        public string SubscriptionGroupId { get; set; }

        /// <summary>
        /// The period details for products that are subscriptions.
        /// </summary>
        public SubscriptionPeriod SubscriptionPeriod { get; set; }

        /// <summary>
        /// A Boolean value that indicates whether the product is available for family sharing in App Store Connect.
        /// </summary>
        public bool IsFamilyShareable { get; set; }

        /// <summary>
        /// iOS 11.2: gets information about product discunt
        /// </summary>
        public InAppBillingProductDiscount IntroductoryOffer { get; set; } = null;


        /// <summary>
        /// iOS 12.2: gets information about product discunt
        /// </summary>
        public List<InAppBillingProductDiscount> Discounts { get; set; } = null;
    }

    /// <summary>
    /// Product info specific to Windows platform
    /// </summary>
    [Preserve(AllMembers = true)]
    public class InAppBillingProductWindowsExtras
    {
        /// <summary>
        /// Gets the base price for the add-on (also called an in-app product or IAP) with the appropriate formatting for the current market.
        /// </summary>
        public string FormattedBasePrice { get; set; }
        /// <summary>
        /// Gets the URI of the image associated with the add-on (also called an in-app product or IAP).
        /// </summary>
        public Uri ImageUri { get; set; }
        /// <summary>
        /// Gets a value that indicates whether the add-on (also called an in-app product or IAP) is on sale.
        /// </summary>
        public bool IsOnSale { get; set; }
        /// <summary>
        /// Gets the end date of the sale period for the add-on (also called an in-app product or IAP).
        /// </summary>
        public DateTimeOffset SaleEndDate { get; set; }

        /// <summary>
        /// Gets the custom developer data string (also called a tag) that contains custom information about an add-on (also called an in-app product or IAP). This string corresponds to the value of the Custom developer data field in the properties page for the add-on in Partner Center.
        /// </summary>
        public string Tag { get; set; }

        /// <summary>
        /// Product type consumable
        /// </summary>
        public bool IsConsumable { get; set; }

        /// <summary>
        /// Product type durable
        /// </summary>
        public bool IsDurable { get; set; }

        /// <summary>
        /// Gets the list of keywords associated with the add-on (also called an in-app product or IAP). These strings correspond to the value of the Keywords field in the properties page for the add-on in Partner Center. 
        /// </summary>
        public IEnumerable<string> Keywords { get; set; }
    }
    /// <summary>
    /// Extras specific to Android
    /// </summary>
    [Preserve(AllMembers = true)]
    public class InAppBillingProductAndroidExtras
    {
        /// <summary>
        /// Subscription period, specified in ISO 8601 format.
        /// </summary>
        public string SubscriptionPeriod { get; set; }

        /// <summary>
        /// Trial period, specified in ISO 8601 format.
        /// </summary>
        public string FreeTrialPeriod { get; set; }

        /// <summary>
        /// Icon of the product if present
        /// </summary>
        public string IconUrl { get; set; }

        /// <summary>
        /// Gets or sets the localized introductory price.
        /// </summary>
        /// <value>The localized introductory price.</value>
        public string LocalizedIntroductoryPrice { get; set; }

        /// <summary>
        /// Number of subscription billing periods for which the user will be given the introductory price, such as 3
        /// </summary>
        public int IntroductoryPriceCycles { get; set; }

        /// <summary>
        /// Billing period of the introductory price, specified in ISO 8601 format
        /// </summary>
        public string IntroductoryPricePeriod { get; set; }

        /// <summary>
        /// Introductory price of the product in micro-units
        /// </summary>
        /// <value>The introductory price.</value>
        public Int64 MicrosIntroductoryPrice { get; set; }

        /// <summary>
        /// Formatted original price of the item, including its currency sign.
        /// </summary>
        public string OriginalPrice { get; set; }

        /// <summary>
        /// Original price in micro-units, where 1,000,000, micro-units equal one unit of the currency
        /// </summary>
        public long MicrosOriginalPriceAmount { get; set; }
    }


	/// <summary>
	/// Product being offered
	/// </summary>
	[Preserve(AllMembers = true)]
	public class InAppBillingProduct
    {
        /// <summary>
        /// Name of the product
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Description of the product
        /// </summary>
        public string Description { get; set; }


        /// <summary>
        /// Product ID or sku
        /// </summary>
        public string ProductId { get; set; }

        /// <summary>
        /// Localized Price (not including tax)
        /// </summary>
        public string LocalizedPrice { get; set; }

        /// <summary>
        /// ISO 4217 currency code for price. For example, if price is specified in British pounds sterling is "GBP".
        /// </summary>
        public string CurrencyCode { get; set; }

        /// <summary>
        /// Price in micro-units, where 1,000,000 micro-units equal one unit of the 
        /// currency. For example, if price is "€7.99", price_amount_micros is "7990000". 
        /// This value represents the localized, rounded price for a particular currency.
        /// </summary>
        public Int64 MicrosPrice { get; set; }

        /// <summary>
        /// Extra information for apple platforms
        /// </summary>
        public InAppBillingProductAppleExtras AppleExtras { get; set; } = null;
        /// <summary>
        /// Extra information for Android platforms
        /// </summary>
        public InAppBillingProductAndroidExtras AndroidExtras { get; set; } = null;
        /// <summary>
        /// Extra information for Windows platforms
        /// </summary>
        public InAppBillingProductWindowsExtras WindowsExtras { get; set; } = null;

    }
}