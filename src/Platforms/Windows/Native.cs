using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace MRZScanner.Maui;

/// <summary>
/// Windows: the core's C API (mrzscanner.dll, platforms/windows; this package ships it) through
/// P/Invoke, with the legacy reader's model (embedded here, written to the app's local data once).
/// Photos only for now: <see cref="Scan"/> returns an error status until the Windows camera screen.
/// </summary>
static class Native
{
    const string Dll = "mrzscanner.dll";
    const int PixelBgra8 = 1, PlatformDesktop = 4, MrzOk = 0;
    static readonly object Gate = new();
    static IntPtr scanner;

    [StructLayout(LayoutKind.Sequential)]
    struct MrzImage { public IntPtr Pixels; public int Width, Height, Stride, Format; }

    [DllImport(Dll)] static extern IntPtr mrz_version();
    [DllImport(Dll)] static extern int mrz_register_license([MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string appId, [MarshalAs(UnmanagedType.LPUTF8Str)] string device, int platform);
    [DllImport(Dll)] static extern IntPtr mrz_scanner_create([MarshalAs(UnmanagedType.LPUTF8Str)] string tessdata, out int status);
    [DllImport(Dll)] static extern int mrz_scan_image(IntPtr scanner, ref MrzImage image, IntPtr options, out IntPtr result);
    [DllImport(Dll)] static extern void mrz_scan_options_init_ex(IntPtr options, int size);
    // mrz_scan_options (frozen, 72 bytes): ignore_invalid_check_digits is at byte 36.
    const int OptionsSize = 72, IgnoreInvalidCheckDigitsOffset = 36;
    [DllImport(Dll)] static extern IntPtr mrz_result_text(IntPtr result);
    [DllImport(Dll)] static extern IntPtr mrz_result_json(IntPtr result);
    [DllImport(Dll)] static extern int mrz_result_check_digits_valid(IntPtr result);
    [DllImport(Dll)] static extern int mrz_result_corners(IntPtr result, int[] xy);
    [DllImport(Dll)] static extern void mrz_result_free(IntPtr result);
    [DllImport(Dll)] static extern int mrz_isolate_size(int[] corners, int height, out int width);
    [DllImport(Dll)] static extern int mrz_isolate(ref MrzImage image, int[] corners, int width, int height, byte[] output, int stride);

    public static Task<string> RegisterLicense(string key)
    {
        int status = mrz_register_license(key, AppId(), "", PlatformDesktop);
        return Task.FromResult(Json.Write(w => { w.WriteNumber("status", status); w.WriteNull("reason"); }));
    }

    public static Task<string> Version()
    {
        string v = Marshal.PtrToStringUTF8(mrz_version()) ?? "";
        return Task.FromResult(Json.Write(w => { w.WriteString("core", v); w.WriteString("sdk", v); }));
    }

    public static Task<string> Scan(string settings) => Task.FromResult(
        """{"status":"error","reads":[],"elapsedMs":0,"message":"The camera scan is not available on Windows yet; use ScanImageAsync"}""");

    public static async Task<string> ScanImage(string image, string options)
    {
        using var source = JsonDocument.Parse(image);
        using var opts = JsonDocument.Parse(options);
        var (pixels, width, height) = await Decode(source.RootElement);
        return await Task.Run(() => Read(pixels, width, height, opts.RootElement));
    }

    // The picture as BGRA8 pixels, orientation applied, at most 3000 pixels a side.
    static async Task<(byte[] pixels, int width, int height)> Decode(JsonElement image)
    {
        IRandomAccessStream stream;
        if (image.TryGetProperty("base64", out var b64))
        {
            string s = b64.GetString() ?? "";
            var bytes = Convert.FromBase64String(s[(s.IndexOf("base64,", StringComparison.Ordinal) is int i && i >= 0 ? i + 7 : 0)..]);
            var memory = new InMemoryRandomAccessStream();
            await memory.WriteAsync(bytes.AsBuffer());
            memory.Seek(0);
            stream = memory;
        }
        else
        {
            string path = image.GetProperty("path").GetString() ?? "";
            if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) path = new Uri(path).LocalPath;
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
            stream = await file.OpenReadAsync();
        }
        using (stream)
        {
            var decoder = await BitmapDecoder.CreateAsync(stream);
            uint w = decoder.OrientedPixelWidth, h = decoder.OrientedPixelHeight;
            double f = Math.Min(1.0, 3000.0 / Math.Max(w, h));
            var transform = new BitmapTransform { ScaledWidth = (uint)Math.Round(w * f), ScaledHeight = (uint)Math.Round(h * f) };
            if (decoder.OrientedPixelWidth != decoder.PixelWidth)   // rotated: scale the stored (unrotated) size
                (transform.ScaledWidth, transform.ScaledHeight) = ((uint)Math.Round(decoder.PixelWidth * f), (uint)Math.Round(decoder.PixelHeight * f));
            var data = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, transform,
                ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
            int ow = (int)Math.Round(w * f), oh = (int)Math.Round(h * f);
            return (data.DetachPixelData(), ow, oh);
        }
    }

    static string Read(byte[] pixels, int width, int height, JsonElement options)
    {
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            var image = new MrzImage { Pixels = handle.AddrOfPinnedObject(), Width = width, Height = height, Stride = width * 4, Format = PixelBgra8 };
            IntPtr result;
            bool enforce = !options.TryGetProperty("enforceCheckDigits", out var ecd) || ecd.ValueKind != JsonValueKind.False;
            IntPtr scanOptions = Marshal.AllocHGlobal(OptionsSize);
            try
            {
                mrz_scan_options_init_ex(scanOptions, OptionsSize);
                Marshal.WriteInt32(scanOptions, IgnoreInvalidCheckDigitsOffset, enforce ? 1 : 0);
                lock (Gate)   // one scanner, one thread at a time
                {
                    if (mrz_scan_image(Scanner(), ref image, scanOptions, out result) != MrzOk || result == IntPtr.Zero)
                    {
                        if (result != IntPtr.Zero) mrz_result_free(result);
                        return "null";
                    }
                }
            }
            finally { Marshal.FreeHGlobal(scanOptions); }
            try
            {
                string text = Marshal.PtrToStringUTF8(mrz_result_text(result)) ?? "";
                if (options.TryGetProperty("documentTypes", out var types) && !Matches(text, types)) return "null";
                using var fields = JsonDocument.Parse(Marshal.PtrToStringUTF8(mrz_result_json(result)) ?? "{}");
                var xy = new int[8];
                bool hasCorners = mrz_result_corners(result, xy) == 1;
                int isolatedHeight = options.TryGetProperty("isolatedHeight", out var ih) ? ih.GetInt32() : 0;
                string? isolated = hasCorners && isolatedHeight > 0 ? Isolate(ref image, xy, isolatedHeight) : null;
                return Json.Write(w =>
                {
                    w.WriteString("text", text);
                    w.WriteBoolean("checkDigitsValid", mrz_result_check_digits_valid(result) == 1);
                    w.WriteString("kind", Kind(text, fields.RootElement));
                    w.WritePropertyName("fields");
                    fields.RootElement.WriteTo(w);
                    w.WritePropertyName("corners");
                    if (hasCorners)
                    {
                        w.WriteStartArray();
                        for (int k = 0; k < 4; k++) { w.WriteStartArray(); w.WriteNumberValue(xy[2 * k]); w.WriteNumberValue(xy[2 * k + 1]); w.WriteEndArray(); }
                        w.WriteEndArray();
                    }
                    else w.WriteNullValue();
                    if (isolated is null) w.WriteNull("isolatedMrz"); else w.WriteString("isolatedMrz", isolated);
                });
            }
            finally { mrz_result_free(result); }
        }
        finally { handle.Free(); }
    }

    // The MRZ alone, straightened, as a JPEG data URL.
    static string? Isolate(ref MrzImage image, int[] corners, int height)
    {
        if (mrz_isolate_size(corners, height, out int width) != MrzOk || width <= 0) return null;
        var output = new byte[width * height * 4];
        if (mrz_isolate(ref image, corners, width, height, output, width * 4) != MrzOk) return null;
        var stream = new InMemoryRandomAccessStream();
        var encoder = BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream).AsTask().GetAwaiter().GetResult();
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)width, (uint)height, 96, 96, output);
        encoder.FlushAsync().AsTask().GetAwaiter().GetResult();
        var bytes = new byte[stream.Size];
        stream.Seek(0);
        stream.AsStreamForRead().ReadExactly(bytes);
        return "data:image/jpeg;base64," + Convert.ToBase64String(bytes);
    }

    static bool Matches(string text, JsonElement types)
    {
        var wanted = types.EnumerateArray().Select(t => t.GetString()).ToHashSet();
        return text.Length == 0 ? true : text[0] switch
        {
            'P' => wanted.Contains("passport"),
            'V' => wanted.Contains("visa"),
            'I' or 'A' or 'C' => wanted.Contains("id"),
            _ => true,
        };
    }

    static string Kind(string text, JsonElement fields) => text.Length == 0 ? "Document" : text[0] switch
    {
        'P' => "Passport",
        'I' or 'A' or 'C' => "ID card",
        'V' => "Visa",
        _ => fields.TryGetProperty("document_type_readable", out var t) && t.GetString() is string s && s.Length > 0 ? s : "Document",
    };

    static IntPtr Scanner()
    {
        if (scanner != IntPtr.Zero) return scanner;
        // The legacy reader's model, from this assembly to <local app data>/mrzscanner/<version>/tessdata/.
        string version = Marshal.PtrToStringUTF8(mrz_version()) ?? "0";
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mrzscanner", version, "tessdata");
        string model = Path.Combine(dir, "mrz.traineddata");
        if (!File.Exists(model))
        {
            Directory.CreateDirectory(dir);
            using var input = typeof(Native).Assembly.GetManifestResourceStream("mrz.traineddata")
                ?? throw new InvalidOperationException("mrz.traineddata missing from the package");
            string tmp = model + ".tmp";
            using (var output = File.Create(tmp)) input.CopyTo(output);
            File.Move(tmp, model, true);
        }
        scanner = mrz_scanner_create(dir + Path.DirectorySeparatorChar, out int status);
        if (scanner == IntPtr.Zero) throw new InvalidOperationException($"MRZ scanner could not be created ({status})");
        return scanner;
    }

    // The app id for the licence: the package name of a packaged app, else the executable's name.
    static string AppId()
    {
        try { return Windows.ApplicationModel.Package.Current.Id.Name; }
        catch { return Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "app"); }
    }
}
