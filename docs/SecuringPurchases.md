## Securing In-App Purchases (Receipt Validation)

Each platform handles security of In-App Purchases a bit different. To handle this whenever you make a purchase request or a request where validation needs to be done there is an optional parameter that takes in a `IInAppBillingVerifyPurchase`. 

## Recommended Reading:
* [Xamarin.iOS Securing Purchases Documentation](https://developer.xamarin.com/guides/ios/platform_features/in-app_purchasing/transactions_and_verification/#Securing_Purchases)
* [Google Play service Security and Design](https://developer.android.com/google/play/billing/billing_best_practices.html)


## Understanding IInAppBillingVerifyPurchase
Validation should be considered when using this library to do verification with the iTunes servers and Google Play services.  `IInAppBillingVerifyPurchase` is an interface that you should implement on each platform to handle security. If you pass in null to any of the methods then no verification will be done.

It has one method:

```csharp
/// <summary>
/// Verify purchase is authentic
/// </summary>
/// <param name="signedData">Data for verification</param>
/// <param name="signature">Signature of data</param>
/// <param name="productId">Id for the product purchased</param>
/// <param name="transactionId">Id of the transaction for hte purchase</param>
/// <returns>If purchase if verified and authentic.</returns>
Task<bool> VerifyPurchase(string signedData, string signature, string productId = null, string transactionId = null);
 ```

Based on the platform this data is a bit different:

### iOS
* signedData: [Full Receipt as a string in Base 64 Encoding](https://developer.apple.com/library/content/releasenotes/General/ValidateAppStoreReceipt/Introduction.html#//apple_ref/doc/uid/TP40010573)
* signature: Always empty string

### Android
* signedData: Purchase Data returned from Google
* signature: Data Signature returned from Google

### UWP
No additional authentication is provided.


Example:
```csharp
try
{
	var productId = "mysku";

	var connected = await CrossInAppBilling.Current.ConnectAsync();

	if (!connected)
	{
		//Couldn't connect to billing
		return;
	}

    var verify = DependencyService.Get<IInAppBillingVerifyPurchase>();
    //try to purchase item
    var purchase = await CrossInAppBilling.Current.PurchaseAsync(productId, ItemType.InAppPurchase, verify);
	if(purchase == null)
	{
		//Not purchased, may also throw excpetion to catch
	}
	else
	{
		//Purchased!
	}
}
catch (InAppBillingPurchaseException purchaseEx)
{
	Debug.WriteLine("Issue: " +purchaseExex);
}
catch (Exception ex)
{	
    Debug.WriteLine("Issue connecting: " + ex);
}
finally
{
    //Disconnect, it is okay if we never connected, this will never throw an exception
    await CrossInAppBilling.Current.DisconnectAsync();
}
```

### Android Security
I recommend reading the [Google Play services Security and Design](https://developer.android.com/google/play/billing/billing_best_practices.html) that will walk you through your options on storing your public key. InAppBilling Pluging offers Android developers an additional interface, `IInAppBillingVerifyPurchase` to implement to verify the purchase with their public key and helper methods to encrypt and decrypt. It is recommended to atleast follow the XOR guidance if you do not want to setup a verification server.

The simplest and easiest (not necessarily the most secure) way is to do the following:

* Take your public key and break into 3 parts
* Run each through the helper XOR method: Plugin.InAppBilling.InAppBillingImplementation.InAppBillingSecurity.TransformString
* Save each value out and put them in your app
* Implement the interface with this funcationality:

```csharp
    public class Verify : IInAppBillingVerifyPurchase
    {
        const string key1 = @"XOR_key1";
        const string key2 = @"XOR_key2";
        const string key3 = @"XOR_key3";

        public Task<bool> VerifyPurchase(string signedData, string signature)
        {

#if __ANDROID__
            var key1Transform = Plugin.InAppBilling.InAppBillingImplementation.InAppBillingSecurity.TransformString(key1, 1);
            var key2Transform = Plugin.InAppBilling.InAppBillingImplementation.InAppBillingSecurity.TransformString(key2, 2);
            var key3Transform = Plugin.InAppBilling.InAppBillingImplementation.InAppBillingSecurity.TransformString(key3, 3);
            
            return Task.FromResult(Plugin.InAppBilling.InAppBillingImplementation.InAppBillingSecurity.VerifyPurchase(key1Transform + key2Transform + key3Transform, signedData, signature));
#else
            return Task.FromResult(true);
#endif
        }
    }
```
Plugin.InAppBilling.InAppBillingImplementation.InAppBillingSecurity.VerifyPurchase takes in your public key which you now have reversed back to standard and will do proper RSA validation on the signed data. For further discussion join this [Issue](https://github.com/jamesmontemagno/InAppBillingPlugin/issues/116)

## Server Side Validation
Not only should your receipt be verified in the app, but ideally it should be verified on a server. I leave this in your hands to add the server side validation by reading Apple and Google's documentation. I provide everything you need in the `VerifyPurchase` method to handle verification.

For examples of implementation I would recommend reading a great blog series by Jonathan Peppers:

* [Securing Xamarin.iOS In-App Purchases with Azure Functions](http://jonathanpeppers.com/Blog/securing-in-app-purchases-for-xamarin-with-azure-functions)
* [Securing Xamarin.Android In-App Purchases with Azure Functions](http://jonathanpeppers.com/Blog/securing-google-play-in-app-purchases-for-xamarin-with-azure-functions)


<= Back to [Table of Contents](README.md)
