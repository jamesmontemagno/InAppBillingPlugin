using Plugin.InAppBilling;

namespace InAppBillingMauiTest
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {

#if IOS
            var test = new InAppBillingImplementation();
#endif
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            return builder.Build();
        }
    }
}