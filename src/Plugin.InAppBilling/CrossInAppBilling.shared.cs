using System;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// Cross platform InAppBilling implementations
    /// </summary>
    public class CrossInAppBilling
    {
        static Lazy<IInAppBilling> implementation = new(() => CreateInAppBilling(), System.Threading.LazyThreadSafetyMode.PublicationOnly);


		/// <summary>
		/// Gets if the plugin is supported on the current platform.
		/// </summary>
		public static bool IsSupported => implementation.Value == null ? false : true;

		/// <summary>
		/// Current plugin implementation to use
		/// </summary>
		public static IInAppBilling Current
        {
            get
            {
                var ret = implementation.Value;
                if (ret == null)
                {
                    throw NotImplementedInReferenceAssembly();
                }
                return ret;
            }
        }

#if ANDROID || IOS || MACCATALYST || MACOS || WINDOWS
        static IInAppBilling CreateInAppBilling() => new InAppBillingImplementation();
#else
        static IInAppBilling CreateInAppBilling() => null;
#endif

        internal static Exception NotImplementedInReferenceAssembly() =>
			new NotImplementedException("This functionality is not implemented in the portable version of this assembly.  You should reference the NuGet package from your main application project in order to reference the platform-specific implementation.");
        

        /// <summary>
        /// Dispose of everything 
        /// </summary>
        public static void Dispose()
        {
            if (implementation != null && implementation.IsValueCreated)
            {
                implementation.Value.Dispose();

                implementation = new Lazy<IInAppBilling>(() => CreateInAppBilling(), System.Threading.LazyThreadSafetyMode.PublicationOnly);
            }
        }
    }
}
