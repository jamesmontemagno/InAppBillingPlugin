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
/// <param name="payload">Developer specific payload (can not be null)</param>
/// <param name="verifyPurchase">Verify Purchase implementation</param>
/// <returns>Purchase details</returns>
/// <exception cref="InAppBillingPurchaseException">If an error occures during processing</exception>
Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, string payload, IInAppBillingVerifyPurchase verifyPurchase = null);
```

The `payload` attribute is a special payload that is sent and then returned from the server for additional validation. It can be whatever you want it to be, but should be a constant that is used anywhere the `payload` is used.

A subscription can also be upgraded/downgraded/sidegraded to another subscription. This implementation is Android specific because iOS handles this automatically when purchasing subscriptions from the same subscriptions group.

```csharp
/// <summary>
/// (Android specific) Upgrade/Downagrade a previously purchased subscription
/// </summary>
/// <param name="oldProductId">Sku or ID of product that needs to be upgraded</param>
/// <param name="newProductId">Sku or ID of product that will replace the old one</param>
/// <param name="payload">Developer specific payload (can not be null)</param>
/// <param name="verifyPurchase">Verify Purchase implementation</param>
/// <returns>Purchase details</returns>
/// <exception cref="InAppBillingPurchaseException">If an error occures during processing</exception>
Task<InAppBillingPurchase> UpgradePurchasedSubscriptionAsync(string oldProductId, string newProductId, string payload, IInAppBillingVerifyPurchase verifyPurchase = null);
```

Example:
```csharp
public async Task<bool> PurchaseItem(string productId, string payload)
{
    var billing = CrossInAppBilling.Current;
    try
    {
        var connected = await billing.ConnectAsync(ItemType.Subscription);
        if (!connected)
        {
            //we are offline or can't connect, don't try to purchase
            return;
        }

        //check purchases
        var purchase = await billing.PurchaseAsync(productId, ItemType.Subscription, payload);

        //possibility that a null came through.
        if(purchase == null)
        {
            //did not purchase
        }
        else
        {
            //purchased!
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