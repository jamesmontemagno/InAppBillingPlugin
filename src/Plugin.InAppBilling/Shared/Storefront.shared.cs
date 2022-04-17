using System;
using System.Collections.Generic;
using System.Text;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// An object containing the location and unique identifier of an App Store storefront.
    /// </summary>
    public class Storefront
    {
        /// <summary>
        /// A value defined by Apple that uniquely identifies an App Store storefront.
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// The three-letter code representing the country or region associated with the App Store storefront.
        /// </summary>
        public string CountryCode { get; set; }
    }
}
