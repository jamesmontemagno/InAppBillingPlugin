using System;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// Cross platform InAppBilling implementations
    /// </summary>
    public class CrossInAppBilling
    {
        static Lazy<IInAppBilling> implementation = new Lazy<IInAppBilling>(() => CreateInAppBilling(), System.Threading.LazyThreadSafetyMode.PublicationOnly);


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

        static IInAppBilling CreateInAppBilling()
        {
#if NETSTANDARD1_0 || NETSTANDARD2_0 || NETSTANDARD
            return null;
#else
#pragma warning disable IDE0022 // Use expression body for methods
			return new InAppBillingImplementation();
#pragma warning restore IDE0022 // Use expression body for methods
#endif
		}

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
