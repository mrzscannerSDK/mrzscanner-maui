using Microsoft.Maui.LifecycleEvents;

namespace MRZScanner.Maui;

public static class AppBuilderExtensions
{
    /// <summary>Sets up the MRZ scanner (Android: the camera screen's result reaches
    /// <see cref="MrzScanner.ScanAsync"/>). Call in MauiProgram.CreateMauiApp.</summary>
    public static MauiAppBuilder UseMrzScanner(this MauiAppBuilder builder)
    {
#if ANDROID
        builder.ConfigureLifecycleEvents(events => events.AddAndroid(android =>
            android.OnActivityResult((activity, requestCode, resultCode, data) => Native.OnActivityResult(requestCode, data))));
#endif
        return builder;
    }
}
