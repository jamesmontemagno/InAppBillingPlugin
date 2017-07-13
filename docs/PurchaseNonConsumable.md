## Purchase Non-Consumable

Non-Consumable items are my favorite! They can only ever be purchased once and if the user tries to purchase them again it will just restore them automatically. This is great when you have something that you never want to charge for again such as unlocking a game level or removing ads.

Each app store calls them something slightly different:
* Apple: Non-Consumable
* Android: Managed Product
* Microsoft: Durable with Lifetime

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
            return;
        }

        //check purchases
        var purchase = await billing.PurchaseAsync(productId, ItemType.InAppPurchase, payload);

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