In App Billing Plugin for Xamarin & Windows


Version 4.0 has significant updates.

1.) You must compile and target against Android 10 or higher
2.) On Android you must handle pending transactions and call AcknowledgePurchaseAsync` when done
3.) On Android HandleActivityResult has been removed.
4.) We now use Xamarin.Essentials and setup is required per docs.

Find the latest setup guides, documentation, and testing instructions at: 
https://github.com/jamesmontemagno/InAppBillingPlugin
