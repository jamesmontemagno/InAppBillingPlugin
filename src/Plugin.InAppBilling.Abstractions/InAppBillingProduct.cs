
namespace Plugin.InAppBilling.Abstractions
{


    public class InAppBillingProduct
    {
        /// <summary>
        /// Name of the product
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Product ID
        /// </summary>

        public string ProductId { get; set; }

        /// <summary>
        /// Localized Price
        /// </summary>

        public string LocalizedPrice { get; set; }

    }

}