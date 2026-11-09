using Foundation;
using ObjCRuntime;
using Plugin.InAppBilling;
using System.Runtime.InteropServices;
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
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            StoreKitTestSession.Start();

            var billing = CrossInAppBilling.Current;

            Console.WriteLine($"SMOKE canMakePayments={billing.CanMakePayments}");

            var products = (await billing.GetProductInfoAsync(ItemType.InAppPurchase, new[] { "test.consumable", "test.nonconsumable" }, timeout.Token)).ToList();
            Check(products.Count == 2, "both catalog products are returned");
            var pro = products.Single(p => p.ProductId == "test.nonconsumable");
            Check(pro.MicrosPrice == 4_990_000 && pro.Name == "Pro" && pro.AppleExtras.IsFamilyShareable, "product details are mapped");

            try
            {
                await billing.GetProductInfoAsync(ItemType.InAppPurchase, new[] { "test.missing" }, timeout.Token);
                throw new InvalidOperationException("Expected a missing product to be reported.");
            }
            catch (InAppBillingPurchaseException ex) when (ex.PurchaseError == PurchaseError.InvalidProduct)
            {
                Check(ex.Invalid.Single() == "test.missing", "missing products are reported");
            }

            // A consumable stays available until the app finishes it.
            InAppBillingImplementation.FinishAllTransactions = false;
            var completed = new TaskCompletionSource<InAppBillingPurchase>(TaskCreationOptions.RunContinuationsAsynchronously);
            InAppBillingImplementation.OnPurchaseComplete = p => completed.TrySetResult(p);

            var consumable = await billing.PurchaseAsync("test.consumable", ItemType.InAppPurchase, "user-42", cancellationToken: timeout.Token);
            Check(consumable.State == PurchaseState.Purchased, "the purchase completes");
            Check(consumable.PurchaseToken.Split('.').Length == 3, "the purchase token is a JWS");
            Check(consumable.ApplicationUsername == "4243fa49-370d-5428-b122-218655763d34", "the account id becomes the app account token");
            Check((await completed.Task.WaitAsync(timeout.Token)).Id == consumable.Id, "OnPurchaseComplete is raised");

            var unfinished = await billing.GetPurchasesAsync(ItemType.InAppPurchase, timeout.Token);
            Check(unfinished.Any(p => p.Id == consumable.Id), "an unfinished consumable is returned");

            Check(await billing.ConsumePurchaseAsync("test.consumable", consumable.TransactionIdentifier, timeout.Token), "the consumable is consumed");
            var consumed = await billing.GetPurchasesAsync(ItemType.InAppPurchase, timeout.Token);
            Check(!consumed.Any(p => p.Id == consumable.Id), "a finished consumable is no longer returned");
            Check(await billing.ConsumePurchaseAsync("test.consumable", consumable.TransactionIdentifier, timeout.Token), "finishing twice reports success");

            // A non-consumable finishes automatically and stays owned.
            InAppBillingImplementation.FinishAllTransactions = true;
            var nonConsumable = await billing.PurchaseAsync("test.nonconsumable", ItemType.InAppPurchase, cancellationToken: timeout.Token);
            var owned = await billing.GetPurchasesAsync(ItemType.InAppPurchase, timeout.Token);
            Check(owned.Any(p => p.Id == nonConsumable.Id && p.State == PurchaseState.Purchased), "a non-consumable stays owned");
            var finished = await billing.FinalizePurchaseOfProductAsync(new[] { "test.nonconsumable" }, timeout.Token);
            Check(finished.Single().Success, "an owned product can be finalized");

            var unknown = await billing.FinalizePurchaseAsync(new[] { "1" }, timeout.Token);
            Check(!unknown.Single().Success, "an unknown transaction can't be finalized");

            Console.WriteLine("SMOKE_OK");
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SMOKE_FAILED {ex}");
            Environment.Exit(1);
        }
    }

    static void Check(bool condition, string description)
    {
        if (!condition)
            throw new InvalidOperationException($"Failed check: {description}");

        Console.WriteLine($"SMOKE ok: {description}");
    }
}

/// <summary>
/// Serves purchases from Products.storekit instead of the App Store. .NET has no StoreKitTest bindings, so this calls its Objective-C API directly.
/// </summary>
static class StoreKitTestSession
{
    const string ObjC = "/usr/lib/libobjc.dylib";

    [DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern IntPtr Send(IntPtr receiver, IntPtr selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern void SendBool(IntPtr receiver, IntPtr selector, byte value);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern IntPtr SendUrl(IntPtr receiver, IntPtr selector, IntPtr url, out IntPtr error);

    // Keeps the session alive for the life of the app.
    static IntPtr session;

    public static void Start()
    {
        const string framework = "/Developer/Library/Frameworks/StoreKitTest.framework/StoreKitTest";
        if (Dlfcn.dlopen(framework, 0) == IntPtr.Zero)
            throw new InvalidOperationException($"Unable to load {framework}");

        var url = NSBundle.MainBundle.GetUrlForResource("Products", "storekit")
            ?? throw new InvalidOperationException("Products.storekit is not in the app bundle");

        var type = Class.GetHandle("SKTestSession");
        session = SendUrl(Send(type, Selector.GetHandle("alloc")), Selector.GetHandle("initWithContentsOfURL:error:"), url.Handle, out var error);
        if (session == IntPtr.Zero)
            throw new InvalidOperationException($"Unable to start StoreKit test session: {Runtime.GetNSObject<NSError>(error)}");

        SendBool(session, Selector.GetHandle("setDisableDialogs:"), 1);
        Send(session, Selector.GetHandle("clearTransactions"));
    }
}
