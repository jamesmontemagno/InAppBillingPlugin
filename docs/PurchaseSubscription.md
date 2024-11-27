## Purchase Subscription

Subscriptions are purchases that expire (or sometimes auto-renew) after a set period of time. You should track when the subscription was purchased, when it expires, or read this information from the existing purchases. They follow the same work flow as a normal Non-Consumable.

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
/// <param name="obfuscatedAccountId">Specifies an optional obfuscated string that is uniquely associated with the user's account in your app.</param>
/// <param name="obfuscatedProfileId">Specifies an optional obfuscated string that is uniquely associated with the user's profile in your app.</param>
/// <param name="cancellationToken">Cancel the request.</param>
/// <returns>Purchase details</returns>
/// <exception cref="InAppBillingPurchaseException">If an error occurs during processing</exception>
Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, string obfuscatedAccountId = null, string obfuscatedProfileId = null, CancellationToken cancellationToken = default);
```

On Android you must call `FinalizePurchaseAsync` within 3 days when a purchase is validated. Please read the [Android documentation on Pending Transactions](https://developer.android.com/google/play/billing/integrate#pending) for more information.


* iOS: In version 4 we auto finalized all transactions and after testing I decided to keep this feature on in 5/6... you can no turn that off in your iOS application with `InAppBillingImplementation.FinishAllTransactions = false;`. This would be required if you are using consumables and don't want to auto finish. You will need to finalize manually with `FinalizePurchaseAsync`.

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
        else if(purchase.State == PurchaseState.Purchased)
        {
            //only needed on android unless you turn off auto finalize
            var ack = await CrossInAppBilling.Current.FinalizePurchaseAsync([purchase.TransactionIdentifier]);

            // Handle if acknowledge was successful or not
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

* [The Ultimate Guide to iOS Subscription Testing](https://www.revenuecat.com/blog/the-ultimate-guide-to-subscription-testing-on-ios) **PLEASE READ for iOS Testing**


If you are on Android you must also now provide functionality to allow users to unsubscribe via 1 of 2 options:

1. If you sell outside of the store you need to link to your page to cancel in the app
1. if you use in app billing you need to deep link to -> https://developer.android.com/google/play/billing/subscriptions#deep-link


#### obfuscatedAccountId & obfuscatedProfileId
See [Purchase Args note](PurchaseArgs.md)


<= Back to [Table of Contents](README.md)
