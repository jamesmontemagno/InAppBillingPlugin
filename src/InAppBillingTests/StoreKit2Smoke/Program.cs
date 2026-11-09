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
            var useLegacy = Environment.GetEnvironmentVariable("IAB_USE_LEGACY_STOREKIT") == "1";
            if (useLegacy)
            {
                // Existing setup code remains source-compatible and configures the registered legacy backend.
                InAppBillingImplementation.FinishAllTransactions = false;
                InAppBillingImplementation.OnShouldAddStorePayment = (_, _, _) => true;
                InAppBillingImplementation.OnPurchaseComplete = _ => Console.WriteLine("SMOKE legacy purchase callback");
                InAppBillingImplementation.OnPurchaseFailure = _ => Console.WriteLine("SMOKE legacy failure callback");
                CrossInAppBilling.RegisterImplementation(() => new LegacyInAppBillingImplementation());
            }

            var billing = CrossInAppBilling.Current;
            var expectedType = useLegacy ? typeof(LegacyInAppBillingImplementation) : typeof(InAppBillingImplementation);
            if (billing.GetType() != expectedType)
                throw new InvalidOperationException($"Expected {expectedType.Name}, got {billing.GetType().Name}");

            Console.WriteLine($"SMOKE backend={billing.GetType().Name}");

            if (useLegacy && (LegacyInAppBillingImplementation.FinishAllTransactions
                || !LegacyInAppBillingImplementation.OnShouldAddStorePayment(null!, null!, null!)
                || LegacyInAppBillingImplementation.OnPurchaseComplete != InAppBillingImplementation.OnPurchaseComplete
                || LegacyInAppBillingImplementation.OnPurchaseFailure != InAppBillingImplementation.OnPurchaseFailure))
                throw new InvalidOperationException("The legacy backend lost the existing static configuration.");

            Console.WriteLine($"SMOKE canMakePayments={billing.CanMakePayments}");

#if MACCATALYST
            if (!useLegacy)
            {
                try
                {
                    billing.PresentCodeRedemption();
                    throw new InvalidOperationException("Mac Catalyst must report that the offer code sheet is unsupported.");
                }
                catch (PlatformNotSupportedException)
                {
                    Console.WriteLine("SMOKE offer code sheet is unsupported on Mac Catalyst");
                }
            }
#endif

            await ExpectStoreError("query", PurchaseError.InvalidProduct, () =>
                billing.GetProductInfoAsync(ItemType.InAppPurchase, new[] { "smoke.missing" }, timeout.Token));

            await ExpectStoreError("purchase", PurchaseError.InvalidProduct, () =>
                billing.PurchaseAsync("smoke.missing", ItemType.InAppPurchase, "user-42", cancellationToken: timeout.Token));

            if (useLegacy)
            {
                // Legacy restore requires an App Store account. Exercise its observer disposal/recreation without prompting.
                CrossInAppBilling.Dispose();
                if (CrossInAppBilling.Current is not LegacyInAppBillingImplementation)
                    throw new InvalidOperationException("The legacy registration was lost after disposal.");
                CrossInAppBilling.Dispose();
                Console.WriteLine("SMOKE_LEGACY_OK");
                Environment.Exit(0);
                return;
            }

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
