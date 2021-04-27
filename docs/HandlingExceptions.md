## Handling InAppBilling Exceptions

Due to the complex nature of In-App Billing itself there are a lot of different instances where something can go wrong. This library does its best to abstract away these and bubble them up to you as an `InAppBillingPurchaseException` that can be thrown during any of the calls except for `DisconnectAsync` as it should never throw an exception.


### InAppBillingPurchaseException
This exception will return a message that came from the billing server and a `PurchaseError` code that you can use to display information to the user. 

### PurchaseError
Follow this nifty guide:

```csharp
    /// <summary>
    /// Type of purchase error
    /// </summary>
    public enum PurchaseError
    {
        /// <summary>
        /// Billing system unavailable
        /// </summary>
        BillingUnavailable,
        /// <summary>
        /// Developer issue
        /// </summary>
        DeveloperError,
        /// <summary>
        /// Product sku not available
        /// </summary>
        ItemUnavailable,
        /// <summary>
        /// Other error
        /// </summary>
        GeneralError,
        /// <summary>
        /// User cancelled the purchase
        /// </summary>
        UserCancelled,
        /// <summary>
        /// App store unavailable on device
        /// </summary>
        AppStoreUnavailable,
        /// <summary>
        /// User is not allowed to authorize payments
        /// </summary>
        PaymentNotAllowed,
        /// <summary>
        /// One of hte payment parameters was not recognized by app store
        /// </summary>
        PaymentInvalid,
        /// <summary>
        /// The requested product is invalid
        /// </summary>
        InvalidProduct,
        /// <summary>
        /// The product request failed
        /// </summary>
        ProductRequestFailed,
        /// <summary>
        /// Restoring the transaction failed
        /// </summary>
        RestoreFailed,
        /// <summary>
        /// Network connection is down
        /// </summary>
        ServiceUnavailable
    }
```

Here is an example of how to handle exceptions:

```csharp
var billing = CrossInAppBilling.Current;
try
{
    var connected = await billing.ConnectAsync(ItemType.InAppPurchase);

    if (!connected)
    {
       //we are offline or can't connect, don't try to purchase
        return;
    }

    //check purchases
    var purchase = await billing.PurchaseAsync(productId, ItemType.InAppPurchase);

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
    var message = string.Empty;
    switch (purchaseEx.PurchaseError)
    {
        case PurchaseError.AppStoreUnavailable:
            message = "Currently the app store seems to be unavailble. Try again later.";
            break;
        case PurchaseError.BillingUnavailable:
            message = "Billing seems to be unavailable, please try again later.";
            break;
        case PurchaseError.PaymentInvalid:
            message = "Payment seems to be invalid, please try again.";
            break;
        case PurchaseError.PaymentNotAllowed:
            message = "Payment does not seem to be enabled/allowed, please try again.";
            break;
    }

    //Decide if it is an error we care about
    if (string.IsNullOrWhiteSpace(message))
        return;

    //Display message to user
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

<= Back to [Table of Contents](README.md)
