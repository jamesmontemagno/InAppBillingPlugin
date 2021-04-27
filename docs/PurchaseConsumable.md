## Purchase Consumable

Consumables are In-App Purchases that are "used" by the user and can be purchased over and over again. You must manage and know if the item was purchased, consumed, and can be purchased again.

Each app store calls them something slightly different:
* Apple: Consumable
* Android: Managed Product
* Microsoft: Developer-managed consumable

All purchases go through the `PurchaseAsync` method and you must always `ConnectAsync` before making calls and `DisconnectAsync` after making calls. 

Consumables are unique and work a bit different on each platform and the `ConsumePurchaseAsync` may need to be called after making the purchase:
* Apple: As soon as you purchase they are consumed
* Android: You must consume before purchasing again
* Microsoft: You must consume before purchasing again

### Purchase Item
```csharp
/// <summary>
/// Purchase a specific product or subscription
/// </summary>
/// <param name="productId">Sku or ID of product</param>
/// <param name="itemType">Type of product being requested</param>
/// <param name="verifyPurchase">Verify Purchase implementation</param>
/// <returns>Purchase details</returns>
/// <exception cref="InAppBillingPurchaseException">If an error occures during processing</exception>
Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, IInAppBillingVerifyPurchase verifyPurchase = null);
```

### Consume Purchase
```csharp
/// <summary>
/// Consume a purchase with a purchase token.
/// </summary>
/// <param name="productId">Id or Sku of product</param>
/// <param name="purchaseToken">Original Purchase Token</param>
/// <returns>If consumed successful</returns>
/// <exception cref="InAppBillingPurchaseException">If an error occures during processing</exception>
Task<InAppBillingPurchase> ConsumePurchaseAsync(string productId, string purchaseToken);
```


Example:
```csharp
public async Task<bool> PurchaseItem(string productId, string payload)
{
    var billing = CrossInAppBilling.Current;
    try
    {
        var connected = await billing.ConnectAsync();
        if (!connected)
        {
            //we are offline or can't connect, don't try to purchase
            return false;
        }

        //check purchases
        var purchase = await billing.PurchaseAsync(productId, ItemType.InAppPurchase);

        //possibility that a null came through.
        if(purchase == null)
        {
            //did not purchase
        }
        else if(purchase.State == PurchaseState.Purchased)
        {
            //purchased, we can now consume the item or do it later

            //If we are on iOS we are done, else try to consume the purchase
            //Device.RuntimePlatform comes from Xamarin.Forms, you can also use a conditional flag or the DeviceInfo plugin
            if(Device.RuntimePlatform == Device.iOS)
                return true;
                
            var consumedItem = await CrossInAppBilling.Current.ConsumePurchaseAsync(purchase.ProductId, purchase.PurchaseToken);

            if(consumedItem != null)
            {
                //Consumed!!
            }
        }
    }
    catch (InAppBillingPurchaseException purchaseEx)
    {
        //Billing Exception handle this based on the type
        Debug.WriteLine("Error: " + purchaseEx);
    }
    catch (Exception ex)
    {
        //Something else has gone wrong, log it
        Debug.WriteLine("Issue connecting: " + ex);
    }
    finally
    {
        await billing.DisconnectAsync();
    }
```

Learn more about `IInAppBillingVerifyPurchase` in the [Securing Purchases](SecuringPurchases.md) documentation.


<= Back to [Table of Contents](README.md)
