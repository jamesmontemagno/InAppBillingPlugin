using Foundation;
using Plugin.InAppBilling;
using UIKit;

UIApplication.Main(args, null, typeof(SmokeAppDelegate));

[Register(nameof(SmokeAppDelegate))]
public class SmokeAppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        Window = new UIWindow(UIScreen.MainScreen.Bounds) { RootViewController = new UIViewController() };
        Window.MakeKeyAndVisible();

        _ = RunAsync();
        return true;
    }

    static async Task RunAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var billing = CrossInAppBilling.Current;
            billing.IgnoreInvalidProducts = true;

            Console.WriteLine($"SMOKE canMakePayments={billing.CanMakePayments}");

            var products = await billing.GetProductInfoAsync(ItemType.InAppPurchase, new[] { "smoke.missing" }, timeout.Token);
            Console.WriteLine($"SMOKE products={products.Count()}");

            var purchases = await billing.GetPurchasesAsync(ItemType.InAppPurchase, timeout.Token);
            Console.WriteLine($"SMOKE purchases={purchases.Count()}");

            var finished = await billing.FinalizePurchaseAsync(new[] { "1" }, timeout.Token);
            Console.WriteLine($"SMOKE finished={finished.Single().Success}");

            billing.IgnoreInvalidProducts = false;
            try
            {
                await billing.GetProductInfoAsync(ItemType.InAppPurchase, new[] { "smoke.missing" }, timeout.Token);
                throw new InvalidOperationException("Expected a missing product to be reported.");
            }
            catch (InAppBillingPurchaseException ex) when (ex.PurchaseError == PurchaseError.InvalidProduct)
            {
                Console.WriteLine($"SMOKE invalid={string.Join(",", ex.Invalid)}");
            }

            Console.WriteLine("SMOKE_OK");
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SMOKE_FAILED {ex}");
            Environment.Exit(1);
        }
    }
}
