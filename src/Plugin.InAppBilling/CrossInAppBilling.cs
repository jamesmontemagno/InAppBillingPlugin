using Plugin.InAppBilling.Abstractions;
using System;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// Cross platform InAppBilling implemenations
    /// </summary>
    public class CrossInAppBilling
    {
        static Lazy<IInAppBilling> Implementation = new Lazy<IInAppBilling>(() => CreateInAppBilling(), System.Threading.LazyThreadSafetyMode.PublicationOnly);

        /// <summary>
        /// Current settings to use
        /// </summary>
        public static IInAppBilling Current
        {
            get
            {
                var ret = Implementation.Value;
                if (ret == null)
                {
                    throw NotImplementedInReferenceAssembly();
                }
                return ret;
            }
        }

        static IInAppBilling CreateInAppBilling()
        {
#if PORTABLE
            return null;
#else
            return new InAppBillingImplementation();
#endif
        }

        internal static Exception NotImplementedInReferenceAssembly()
        {
            return new NotImplementedException("This functionality is not implemented in the portable version of this assembly.  You should reference the NuGet package from your main application project in order to reference the platform-specific implementation.");
        }
    }
}
