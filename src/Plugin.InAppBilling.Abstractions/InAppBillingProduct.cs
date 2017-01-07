
namespace Plugin.InAppBilling.Abstractions
{

    /// <summary>
    /// Product being offered
    /// </summary>
    public class InAppBillingProduct
    {
        /// <summary>
        /// Name of the product
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Product ID or sku
        /// </summary>
        public string ProductId { get; set; }

        /// <summary>
        /// Localized Price
        /// </summary>
        public string LocalizedPrice { get; set; }

    }

}