## Purchase Subsription

Subscriptions are purchases that expire (or sometimes auto-renew) after a set perior of time. You should track when the subscription was purchased, when it expires, or read this information from the existing purchases. They follow the same work flow as a normal Non-Consumable.

Each app store calls them something slightly different:
* Apple: Auto-Renewable and Non-Renewing Subscription
* Android: Subscription
* Microsoft: Durable with expiration period

All purchases go through the `PurchaseAsync` method and you must always `ConnectAsync` before making calls and `DisconnectAsync` after making calls:

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

On Android you must call `AcknowledgePurchaseAsync` within 3 days when a purchase is validated. Please read the [Android documentation on Pending Transactions](https://developer.android.com/google/play/billing/integrate#pending) for more information.

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
        var purchase = await billing.PurchaseAsync(productId, ItemType.Subscription);

        //possibility that a null came through.
        if(purchase == null)
        {
            //did not purchase
        }
        else
        {
            //purchased!
             if(Device.RuntimePlatform == Device.Android)
             {
                // Must call AcknowledgePurchaseAsync else the purchase will be refunded
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
