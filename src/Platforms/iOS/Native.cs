using System.Runtime.InteropServices;
using Foundation;
using ObjCRuntime;
using UIKit;

namespace MRZScanner.Maui;

/// <summary>
/// iOS: the SDK's MRZScanObjC (MRZScannerARFramework.framework, which this package ships with
/// MRZScannerCore.framework) through the Objective-C runtime - its class methods take and return
/// strings, so no binding project is needed. The camera reports by target / action.
/// </summary>
static class Native
{
    static IntPtr Class => ObjCRuntime.Class.GetHandle("MRZScanObjC");

    public static Task<string> RegisterLicense(string key) => Task.FromResult(Send("registerLicense:", key));

    public static Task<string> Version() => Task.FromResult(Send("coreVersion"));

    public static Task<string> ScanImage(string image, string options) => Task.Run(() => Send("scanImage:options:", image, options));

    public static Task<string> Scan(string settings)
    {
        var done = new TaskCompletionSource<string>();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var from = Platform.GetCurrentUIViewController();
            if (from is null) { done.SetException(new InvalidOperationException("No view to show the camera over")); return; }
            var target = new ScanTarget(done);
            using var json = new NSString(settings);
            Messaging.Send(Class, Selector.GetHandle("presentFrom:settings:target:action:"),
                from.Handle, json.Handle, target.Handle, Selector.GetHandle("scanDone:"));
        });
        return done.Task;
    }

    // A class method taking and returning NSStrings.
    static string Send(string selector, params string[] args)
    {
        var strings = args.Select(a => new NSString(a)).ToArray();
        try
        {
            IntPtr sel = Selector.GetHandle(selector);
            IntPtr result = strings.Length switch
            {
                0 => Messaging.Send(Class, sel),
                1 => Messaging.Send(Class, sel, strings[0].Handle),
                _ => Messaging.Send(Class, sel, strings[0].Handle, strings[1].Handle),
            };
            return NSString.FromHandle(result) ?? "null";
        }
        finally
        {
            foreach (var s in strings) s.Dispose();
        }
    }

    // Receives the ScanResult JSON (presentFrom:settings:target:action:); kept alive until then.
    sealed class ScanTarget : NSObject
    {
        static readonly HashSet<ScanTarget> Pending = new();
        readonly TaskCompletionSource<string> done;

        public ScanTarget(TaskCompletionSource<string> done)
        {
            this.done = done;
            Pending.Add(this);
        }

        [Export("scanDone:")]
        public void ScanDone(NSString json)
        {
            Pending.Remove(this);
            done.TrySetResult(json.ToString());
        }
    }

    static class Messaging
    {
        const string ObjC = "/usr/lib/libobjc.dylib";
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] public static extern IntPtr Send(IntPtr receiver, IntPtr selector);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr a);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr a, IntPtr b);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] public static extern void Send(IntPtr receiver, IntPtr selector, IntPtr a, IntPtr b, IntPtr c, IntPtr d);
    }
}
