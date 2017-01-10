using System.Threading.Tasks;

namespace Plugin.InAppBilling.Abstractions
{
    public interface IInAppBillingVerifyPurchase
    {
        Task<bool> VerifyPurchase(string signedData, string signature);
    }
}
