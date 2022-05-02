using System;
using System.IO;
using System.Xml.Serialization;
using Plugin.InAppBilling;

namespace InAppBillingTests
{
    public class PurchaseStorage
    {
        public PurchaseStorage()
        {
        }

        string fileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "failedPurchase.xml");

        public struct FailedPurchase
        {
            public string Id { get; set; }
            public DateTime TransactionDateUtc { get; set; }
            public string ProductId { get; set; }
            public string PurchaseToken { get; set; }
        }

        XmlSerializer serializer = new XmlSerializer(typeof(FailedPurchase));

        internal InAppBillingPurchase GetFailedPurchase()
        {
            try
            {
                using (var file = File.OpenRead(fileName))
                {
                    var fp = serializer.Deserialize(file) as FailedPurchase?;
                    if (fp.HasValue)
                    {
                        return new InAppBillingPurchase
                        {
                            Id = fp.Value.Id,
                            TransactionDateUtc = fp.Value.TransactionDateUtc,
                            ProductId = fp.Value.ProductId,
                            PurchaseToken = fp.Value.PurchaseToken
                        };
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        internal void SetFailedPurchase(InAppBillingPurchase purchase)
        {
            using (var file = File.OpenWrite(fileName))
            {
                var fp = new FailedPurchase
                {
                    Id = purchase.Id,
                    TransactionDateUtc = purchase.TransactionDateUtc,
                    ProductId = purchase.ProductId,
                    PurchaseToken = purchase.PurchaseToken
                };
                serializer.Serialize(file, fp);
            }
        }
    }
}

