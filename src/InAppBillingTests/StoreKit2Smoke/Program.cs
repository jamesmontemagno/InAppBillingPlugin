using Foundation;
using Plugin.InAppBilling;
using UIKit;

UIApplication.Main(args, null, typeof(SmokeAppDelegate));

// Proves the packed plugin links in a trimmed app and that each call reaches the Swift shim and returns.
// Purchases are covered by the hosted tests in src/StoreKit2Shim because they need a StoreKit test session.
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
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var billing = CrossInAppBilling.Current;

            Console.WriteLine($"SMOKE canMakePayments={billing.CanMakePayments}");

            await ExpectStoreError("query", PurchaseError.InvalidProduct, () =>
                billing.GetProductInfoAsync(ItemType.InAppPurchase, new[] { "smoke.missing" }, timeout.Token));

            await ExpectStoreError("purchase", PurchaseError.InvalidProduct, () =>
                billing.PurchaseAsync("smoke.missing", ItemType.InAppPurchase, "user-42", cancellationToken: timeout.Token));

            var purchases = await billing.GetPurchasesAsync(ItemType.InAppPurchase, timeout.Token);
            Console.WriteLine($"SMOKE purchases={purchases.Count()}");

            var finished = (await billing.FinalizePurchaseAsync(new[] { "1" }, timeout.Token)).Single();
            if (finished.Id != "1" || finished.Success)
                throw new InvalidOperationException($"An unknown transaction was finalized: {finished}");
            Console.WriteLine("SMOKE finalize reported the unknown transaction");

            Console.WriteLine("SMOKE_OK");
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SMOKE_FAILED {ex}");
            Environment.Exit(1);
        }
    }

    // A simulator without an App Store connection may fail the lookup instead of reporting the product as missing.
    // Either way the error must come back from Swift as a mapped exception.
    static async Task ExpectStoreError(string name, PurchaseError expected, Func<Task> call)
    {
        try
        {
            await call();
            throw new InvalidOperationException($"Expected {name} to fail for a missing product.");
        }
        catch (InAppBillingPurchaseException ex)
        {
            Console.WriteLine($"SMOKE {name} error={ex.PurchaseError} expected={expected} message={ex.Message}");
        }
    }
}
