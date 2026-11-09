using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// P/Invoke layer over the Swift shim in src/StoreKit2Shim, linked through buildTransitive/Plugin.InAppBilling.targets.
    /// </summary>
    internal static unsafe class StoreKit2Native
    {
        const string Library = "__Internal";

        [DllImport(Library)] static extern void iab_sk2_products([MarshalAs(UnmanagedType.LPUTF8Str)] string idsJson, nuint context, delegate* unmanaged<nuint, byte*, void> callback);
        [DllImport(Library)] static extern void iab_sk2_purchase([MarshalAs(UnmanagedType.LPUTF8Str)] string productId, [MarshalAs(UnmanagedType.LPUTF8Str)] string appAccountToken, nuint context, delegate* unmanaged<nuint, byte*, void> callback);
        [DllImport(Library)] static extern void iab_sk2_current_purchases(nuint context, delegate* unmanaged<nuint, byte*, void> callback);
        [DllImport(Library)] static extern void iab_sk2_finish_transactions([MarshalAs(UnmanagedType.LPUTF8Str)] string idsJson, nuint context, delegate* unmanaged<nuint, byte*, void> callback);
        [DllImport(Library)] static extern void iab_sk2_finish_products([MarshalAs(UnmanagedType.LPUTF8Str)] string idsJson, nuint context, delegate* unmanaged<nuint, byte*, void> callback);
        [DllImport(Library)] static extern void iab_sk2_sync(nuint context, delegate* unmanaged<nuint, byte*, void> callback);
        [DllImport(Library)] static extern void iab_sk2_storefront(nuint context, delegate* unmanaged<nuint, byte*, void> callback);
        [DllImport(Library)] static extern int iab_sk2_can_make_payments();
        [DllImport(Library)] static extern void iab_sk2_present_offer_code_sheet();
        [DllImport(Library)] static extern void iab_sk2_start_listeners(delegate* unmanaged<nuint, byte*, void> callback);
        [DllImport(Library)] static extern void iab_sk2_stop_listeners();

        static Action<StoreKitEvent> eventHandler;

        public static bool CanMakePayments => iab_sk2_can_make_payments() != 0;

        public static void PresentOfferCodeSheet() => iab_sk2_present_offer_code_sheet();

        public static Task<StoreKitResponse> ProductsAsync(string[] ids) =>
            Invoke((context, callback) => iab_sk2_products(StoreKitMapper.SerializeIds(ids), context, callback));

        public static Task<StoreKitResponse> PurchaseAsync(string productId, string appAccountToken) =>
            Invoke((context, callback) => iab_sk2_purchase(productId, appAccountToken, context, callback));

        public static Task<StoreKitResponse> CurrentPurchasesAsync() =>
            Invoke((context, callback) => iab_sk2_current_purchases(context, callback));

        public static Task<StoreKitResponse> FinishTransactionsAsync(string[] ids) =>
            Invoke((context, callback) => iab_sk2_finish_transactions(StoreKitMapper.SerializeIds(ids), context, callback));

        public static Task<StoreKitResponse> FinishProductsAsync(string[] ids) =>
            Invoke((context, callback) => iab_sk2_finish_products(StoreKitMapper.SerializeIds(ids), context, callback));

        public static Task<StoreKitResponse> SyncAsync() =>
            Invoke((context, callback) => iab_sk2_sync(context, callback));

        public static Task<StoreKitResponse> StorefrontAsync() =>
            Invoke((context, callback) => iab_sk2_storefront(context, callback));

        public static void StartListeners(Action<StoreKitEvent> handler)
        {
            eventHandler = handler;
            iab_sk2_start_listeners(&OnEvent);
        }

        public static void StopListeners()
        {
            iab_sk2_stop_listeners();
            eventHandler = null;
        }

        delegate void NativeCall(nuint context, delegate* unmanaged<nuint, byte*, void> callback);

        static Task<StoreKitResponse> Invoke(NativeCall call)
        {
            // The callback arrives on a Swift concurrency thread, so continuations must not run inline on it.
            var completion = new TaskCompletionSource<StoreKitResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            var handle = GCHandle.Alloc(completion);
            try
            {
                call((nuint)(nint)GCHandle.ToIntPtr(handle), &OnResponse);
            }
            catch
            {
                handle.Free();
                throw;
            }
            return completion.Task;
        }

        // An exception escaping an UnmanagedCallersOnly method terminates the process, so nothing may escape these two.
        [UnmanagedCallersOnly]
        static void OnResponse(nuint context, byte* json)
        {
            var handle = GCHandle.FromIntPtr((nint)context);
            var completion = (TaskCompletionSource<StoreKitResponse>)handle.Target;
            handle.Free();

            try
            {
                completion.TrySetResult(StoreKitMapper.ParseResponse(Marshal.PtrToStringUTF8((nint)json)));
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }

        [UnmanagedCallersOnly]
        static void OnEvent(nuint context, byte* json)
        {
            try
            {
                var storeEvent = StoreKitMapper.ParseEvent(Marshal.PtrToStringUTF8((nint)json));
                var handler = eventHandler;
                if (handler != null)
                    Task.Run(() => handler(storeEvent));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Unable to process StoreKit event: " + ex);
            }
        }
    }
}
