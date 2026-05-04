using CustomOBD.ViewModel;
using Microsoft.Extensions.Logging;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;

namespace CustomOBD
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton<IBluetoothLE>(t => CrossBluetoothLE.Current);
            builder.Services.AddSingleton<IAdapter>(t => CrossBluetoothLE.Current.Adapter);
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<BluetoothViewModel>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
