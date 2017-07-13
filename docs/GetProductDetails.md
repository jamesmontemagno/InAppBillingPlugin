## Get Product Details

After creating the in-app purchase items in the respective app store you can then get information that you filled out for them. The items must be live in the store to properly grab the information. 

```csharp
/// <summary>
/// Get product information of a specific product
/// </summary>
/// <param name="itemType">Type of product offering</param>
/// <param name="productIds">Sku or Id of the product(s)</param>
/// <returns>List of products</returns>
Task<IEnumerable<InAppBillingProduct>> GetProductInfoAsync(ItemType itemType, params string[] productIds);
```
Note that you have to specify an `ItemType` for each call. This means you have to query your `InAppPurchase` and `Subscriptio`n items in multiple calls to the API. Additionally, the `productIds` must be specified in the app, there is no way to query the In-App Billing service to just say "give me everything".

 You will receive back a list of your products that you specified the `productIds` for with the following information:

 ```csharp
/// <summary>
/// Product being offered
/// </summary>
public class InAppBillingProduct
{
    /// <summary>
    /// Name of the product
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Description of the product
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Product ID or sku
    /// </summary>
    public string ProductId { get; set; }

    /// <summary>
    /// Localized Price (not including tax)
    /// </summary>
    public string LocalizedPrice { get; set; }

    /// <summary>
    /// ISO 4217 currency code for price. For example, if price is specified in British pounds sterling is "GBP".
    /// </summary>
    public string CurrencyCode { get; set; }

    /// <summary>
    /// Price in micro-units, where 1,000,000 micro-units equal one unit of the 
    /// currency. For example, if price is "€7.99", price_amount_micros is "7990000". 
    /// This value represents the localized, rounded price for a particular currency.
    /// </summary>
    public Int64 MicrosPrice { get; set; }
}
 ```
 

Example:
```csharp
var billing = CrossInAppBilling.Current;
try
{ 
    
    var productIds = new string []{"mysku","mysku2"};
    //You must connect
    var connected = await billing.ConnectAsync();

    if (!connected)
    {
        //Couldn't connect
        return;
    }

    //check purchases

    var items = await billing.GetProductInfoAsync(ItemType.InAppPurchase, productIds);

    foreach(var item in items)
    {
        //item info here.
    }
}
catch(InAppBillingPurchaseException pEx)
{
    //Handle IAP Billing Exception
}
catch (Exception ex)
{
    //Something has gone wrong
}
finally
{    
    await billing.DisconnectAsync();
}
```

<= Back to [Table of Contents](README.md)