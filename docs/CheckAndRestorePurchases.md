## Check and Restore Purchases
When users get a new device or re-install your application it is best practice to restore their existing practices. This is usually done with a button in the settings or you can do it automatically.

```csharp
/// <summary>
/// Get all current purchases for a specified product type.
/// </summary>
/// <param name="itemType">Type of product</param>
/// <returns>The current purchases</returns>
Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType);
```

When you make a call to restore a purchase it will prompt for the user to sign in if they haven't yet, so take that into consideration.

Note, that on iOS this will only return your non-consumables, consumables that have already been `finished` are not tracked at all and your app should handle these situations.

On iOS, we auto finish all transactions when you get purchases. If you have any consumables you should pass in a `List<string>` with ids that you do not want finished.


Example:
```csharp
public async Task<bool> WasItemPurchased(string productId)
{
    var billing = CrossInAppBilling.Current;
    try
    { 
        var connected = await billing.ConnectAsync();

        if (!connected)
        {
            //Couldn't connect
            return false;
        }

        //check purchases
        var idsToNotFinish = new List<string>(new [] {"myconsumable"});

        var purchases = await billing.GetPurchasesAsync(ItemType.InAppPurchase, idsToNotFinish);

        //check for null just in case
        if(purchases?.Any(p => p.ProductId == productId) ?? false)
        {
            //Purchase restored
            // if on Android may be good to check if these purchases need to be acknowledge
            return true;
        }
        else
        {
            //no purchases found
            return false;
        }
    }    
    catch (InAppBillingPurchaseException purchaseEx)
    {
        //Billing Exception handle this based on the type
        Debug.WriteLine("Error: " + purchaseEx);
    }
    catch (Exception ex)
    {
        //Something has gone wrong
    }
    finally
    {    
        await billing.DisconnectAsync();
    }

    return false;
}
```

> Note: On iOS there is no API to determine if a purchase was a subscription or in app purchase, so all purchases regardless of type will be return. It is required on Android. It is best to query and then check for Id.

## Subscriptions

On `Android` only valid on-going subscriptions will be returned (with the original purchase date). `iOS` returns all receipts for all instances of the subscripitions. Read the iOS documentation to learn more on strategies.

Learn more about `IInAppBillingVerifyPurchase` in the [Securing Purchases](SecuringPurchases.md) documentation.


<= Back to [Table of Contents](README.md)
