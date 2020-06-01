using System.Threading.Tasks;

namespace Plugin.InAppBilling
{
	[Preserve(AllMembers = true)]
	public interface IInAppBillingVerifyPurchase
    {
        Task<bool> VerifyPurchase(string signedData, string signature, string productId = null, string transactionId = null);
    }
}
