## Purchase Consumable

Consumables are In-App Purchases that are "used" by the user and can be purchased over and over again. You must manage and know if the item was purchased, consumed, and can be purchased again.

Each app store calls them something slightly different:
* Apple: Consumable
* Android: Managed Product
* Microsoft: Developer-managed consumable

All purchases go through the `PurchaseAsync` method and you must always `ConnectAsync` before making calls and `DisconnectAsync` after making calls. 

Consumables are unique and work a bit different on each platform and the `ConsumePurchaseAsync` may need to be called after making the purchase:
* Apple: You must consume the purchase (this finishes the transaction), starting in 5.x and 6.x will not auto do this.
* Android: You must consume before purchasing again, it also acts as a way of acknowledging the transaction
* Microsoft: You must consume before purchasing again

### Purchase Item
```csharp
/// <summary>
/// Purchase a specific product or subscription
/// </summary>
/// <param name="productId">Sku or ID of product</param>
/// <param name="itemType">Type of product being requested</param>
/// <param name="obfuscatedAccountId">Specifies an optional obfuscated string that is uniquely associated with the user's account in your app.</param>
/// <param name="obfuscatedProfileId">Specifies an optional obfuscated string that is uniquely associated with the user's profile in your app.</param>
/// <returns>Purchase details</returns>
/// <exception cref="InAppBillingPurchaseException">If an error occurs during processing</exception>
Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, string obfuscatedAccountId = null, string obfuscatedProfileId = null);
```

#### obfuscatedAccountId & obfuscatedProfileId
* iOS: Optional, only obfuscatedAccountId is used at this time. See [https://developer.apple.com/documentation/storekit/skmutablepayment/1506088-applicationusername](ApplicationUsername) on the payment.
* Android: Optional, see [https://developer.android.com/reference/com/android/billingclient/api/BillingFlowParams.Builder#setObfuscatedAccountId(java.lang.String)](Android documentation) for more info

### Consume Purchase
* Android & Windows: You must consume your purchase when your user uses it before buying another one.

* iOS: Beta - In version 4 we auto finalized all transactions and after testing I decided to keep this feature on in 5/6... you can no turn that off in your iOS application with `InAppBillingImplementation.FinishAllTransactions = false;`. This would be required if you are using consumables and don't want to auto finish. You will need to finalize manually with `ConsumePurchaseAsync` or `FinalizePurchaseAsync`.


```csharp
/// <summary>
/// Consume a purchase with a purchase token.
/// </summary>
/// <param name="productId">Id or Sku of product</param>
/// <param name="transactionIdentifier">Original Purchase Token</param>
/// <returns>If consumed successful</returns>
/// <exception cref="InAppBillingPurchaseException">If an error occurs during processing</exception>
Task<InAppBillingPurchase> ConsumePurchaseAsync(string productId, string transactionIdentifier);
```


Example:
```csharp
public async Task<bool> PurchaseItem(string productId)
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
        var purchase = await billing.PurchaseAsync(productId, ItemType.InAppPurchaseConsumable);

        //possibility that a null came through.
        if(purchase == null)
        {
            //did not purchase
        }
        else if(purchase.State == PurchaseState.Purchased)
        {
            // purchased, we can now consume the item or do it later
            // here you may want to call your backend or process something in your app.
                        
            //only required on Android & Windows    
            var wasConsumed = await CrossInAppBilling.Current.ConsumePurchaseAsync(purchase.ProductId, purchase.TransactionIdentifier);

            if(wasConsumed)
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
