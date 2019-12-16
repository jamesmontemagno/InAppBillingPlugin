## Check and Restore Purchases
When users get a new device or re-install your application it is best practice to restore their existing practices. This is usually done with a button in the settings or you can do it automatically.

```csharp
/// <summary>
/// Get all current purhcase for a specifiy product type.
/// </summary>
/// <param name="itemType">Type of product</param>
/// <param name="verifyPurchase">Verify purchase implementation</param>
/// <returns>The current purchases</returns>
Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType, IInAppBillingVerifyPurchase verifyPurchase = null);
```

When you make a call to restore a purchase it will prompt for the user to sign in if they haven't yet, so take that into consideration.

Note, that on iOS this will only return your non-consumables, consumables are not tracked at all and your app should handle these situations

Example:
```csharp
public async Task<bool> WasItemPurchased(string productId)
{
    var billing = CrossInAppBilling.Current;
    try
    { 
        var connected = await billing.ConnectAsync(ItemType.InAppPurchase);

        if (!connected)
        {
            //Couldn't connect
            return false;
        }

        //check purchases
        var purchases = await billing.GetPurchasesAsync(ItemType.InAppPurchase);

        //check for null just incase
        if(purchases?.Any(p => p.ProductId == productId) ?? false)
        {
            //Purchase restored
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

Learn more about `IInAppBillingVerifyPurchase` in the [Securing Purchases](SecuringPurchases.md) documentation.


<= Back to [Table of Contents](README.md)
