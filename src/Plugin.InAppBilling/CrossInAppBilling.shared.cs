using System;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// Cross platform InAppBilling implementations
    /// </summary>
    public class CrossInAppBilling
    {
        static readonly object implementationLock = new();
        static Func<IInAppBilling> implementationFactory = CreateInAppBilling;
        static Lazy<IInAppBilling> implementation = new(() => implementationFactory());

        /// <summary>
        /// Registers the factory used to create Current. Call at startup, before accessing Current or IsSupported.
        /// The registration is retained when Dispose is called.
        /// </summary>
        /// <param name="factory">Creates a new billing instance, which CrossInAppBilling owns and disposes.</param>
        /// <exception cref="ArgumentNullException">The factory is null.</exception>
        /// <exception cref="InvalidOperationException">Billing has already been initialized, or the factory returns null.</exception>
        public static void RegisterImplementation(Func<IInAppBilling> factory)
        {
            ArgumentNullException.ThrowIfNull(factory);

            lock (implementationLock)
            {
                if (implementation.IsValueCreated)
                    throw new InvalidOperationException("Register billing before accessing Current or IsSupported. Dispose the existing instance before changing its registration.");

                implementationFactory = () => factory() ?? throw new InvalidOperationException("The billing factory must return an implementation.");
                implementation = new Lazy<IInAppBilling>(() => implementationFactory());
            }
        }

        static IInAppBilling GetImplementation()
        {
            lock (implementationLock)
                return implementation.Value;
        }


		/// <summary>
		/// Gets if the plugin is supported on the current platform.
		/// </summary>
		public static bool IsSupported => GetImplementation() != null;

		/// <summary>
		/// Current plugin implementation to use
		/// </summary>
		public static IInAppBilling Current
        {
            get
            {
                var ret = GetImplementation();
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
            lock (implementationLock)
            {
                if (!implementation.IsValueCreated)
                    return;

                implementation.Value?.Dispose();
                implementation = new Lazy<IInAppBilling>(() => implementationFactory());
            }
        }
    }
}
