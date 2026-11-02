using Android.Content;
using Android.Runtime;

namespace MRZScanner.Maui;

/// <summary>
/// Android: the SDK's MrzScan (com.barkoder.mrzscanner.ar, in the mrzscanner-ar.aar this package
/// ships) through JNI - its static JSON functions take and return strings, so no generated binding
/// of the Kotlin SDK is needed. The camera is its MrzScanActivity; the result comes back through
/// the activity's OnActivityResult (UseMrzScanner).
/// </summary>
static class Native
{
    const string Settings = "com.barkoder.mrzscanner.ar.SETTINGS";
    const string Result = "com.barkoder.mrzscanner.ar.RESULT";
    const int ScanRequest = 0x4d525a;   // "MRZ"
    static readonly Lazy<Java.Lang.Class> MrzScan = new(() => Java.Lang.Class.ForName("com.barkoder.mrzscanner.ar.MrzScan"));
    static TaskCompletionSource<string>? scanning;

    static Context Context => Android.App.Application.Context;

    public static Task<string> RegisterLicense(string key) => Task.FromResult(CallString("registerLicense",
        "(Landroid/content/Context;Ljava/lang/String;)Ljava/lang/String;", Context, new Java.Lang.String(key)));

    public static Task<string> Version() => Task.FromResult(CallString("version", "()Ljava/lang/String;"));

    public static Task<string> ScanImage(string image, string options) => Task.Run(() => CallString("scanImage",
        "(Landroid/content/Context;Ljava/lang/String;Ljava/lang/String;)Ljava/lang/String;",
        Context, new Java.Lang.String(image), new Java.Lang.String(options)));

    public static Task<string> Scan(string settings)
    {
        var activity = Platform.CurrentActivity ?? throw new InvalidOperationException("No activity to show the camera over");
        scanning?.TrySetResult("""{"status":"stopped","reads":[],"elapsedMs":0}""");
        scanning = new TaskCompletionSource<string>();
        var intent = new Intent(activity, Java.Lang.Class.ForName("com.barkoder.mrzscanner.ar.MrzScanActivity"));
        intent.PutExtra(Settings, settings);
        activity.StartActivityForResult(intent, ScanRequest);
        return scanning.Task;
    }

    internal static void OnActivityResult(int requestCode, Intent? data)
    {
        if (requestCode != ScanRequest || scanning is null) return;
        scanning.TrySetResult(data?.GetStringExtra(Result) ?? """{"status":"stopped","reads":[],"elapsedMs":0}""");
        scanning = null;
    }

    // A static String-returning method of MrzScan.
    static string CallString(string name, string signature, params Java.Lang.Object[] args)
    {
        var cls = MrzScan.Value;
        IntPtr method = JNIEnv.GetStaticMethodID(cls.Handle, name, signature);
        var values = args.Select(a => new JValue(a)).ToArray();
        IntPtr result = JNIEnv.CallStaticObjectMethod(cls.Handle, method, values);
        return JNIEnv.GetString(result, JniHandleOwnership.TransferLocalRef) ?? "null";
    }
}
