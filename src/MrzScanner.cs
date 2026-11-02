using System.Text;
using System.Text.Json;

namespace MRZScanner.Maui;

/// <summary>The kinds of document.</summary>
[Flags]
public enum DocumentTypes { Passport = 1, Id = 2, Visa = 4, All = Passport | Id | Visa }

/// <summary>How many agreeing frames a read needs: Normal two (more when a character no check
/// digit covers is uncertain), Fast the first frame with valid check digits.</summary>
public enum Confirmation { Normal, Fast }

/// <summary>Why <see cref="MrzScanner.ScanAsync"/> ended.</summary>
public enum ScanStatus { Read, Stopped, Timeout, PermissionDenied, LicenseInactive, Error }

/// <summary>A region of the upright camera picture, in fractions (0..1).</summary>
public record ScanRegion(double X, double Y, double Width, double Height);

/// <summary>How <see cref="MrzScanner.ScanAsync"/> scans; null properties keep the defaults.</summary>
public class ScanSettings
{
    /// <summary>false: close after the first confirmed read; true: collect each new document until Stop.</summary>
    public bool? Continuous { get; set; }
    /// <summary>Continuous only: the same MRZ is not collected again within this time (default 3 s).</summary>
    public TimeSpan? DuplicateTimeout { get; set; }
    public Confirmation? Confirmation { get; set; }
    /// <summary>Close when nothing was read for this long (default: never).</summary>
    public TimeSpan? Timeout { get; set; }
    /// <summary>The torch and the zoom factor when the camera opens.</summary>
    public bool? Torch { get; set; }
    public double? Zoom { get; set; }
    /// <summary>A haptic tick for each read (default true).</summary>
    public bool? VibrateOnRead { get; set; }
    /// <summary>The box that follows the MRZ and the read badge over it (default true).</summary>
    public bool? ArOverlay { get; set; }
    /// <summary>The kinds of document to read (default all).</summary>
    public DocumentTypes? DocumentTypes { get; set; }
    /// <summary>Only an MRZ inside this part of the picture is read (default all of it).</summary>
    public ScanRegion? RegionOfInterest { get; set; }
    /// <summary>Each read also gets <see cref="MrzRead.IsolatedMrz"/>, IsolatedHeight pixels tall.</summary>
    public bool? IsolateMrz { get; set; }
    public int? IsolatedHeight { get; set; }
    /// <summary>Each read also gets <see cref="MrzRead.Portrait"/>: the document's photo, cut out and
    /// straightened (only a photo on the MRZ's side: a passport's data page, not an ID card's back).</summary>
    public bool? CapturePortrait { get; set; }
    /// <summary>Each read also gets <see cref="MrzRead.Signature"/>, the same way, when the MRZ's side has one.</summary>
    public bool? CaptureSignature { get; set; }
    /// <summary>Each read also gets <see cref="MrzRead.Document"/>: the whole page around the MRZ, cut out,
    /// straightened, in colour.</summary>
    public bool? CaptureDocument { get; set; }
    /// <summary>Anti-blur: a second character model, trained on defocus-blurred MRZs, reads what the first
    /// cannot (a camera out of focus, a document held too close); only when the normal read fails. Names
    /// have no check digit: on a blurred MRZ a letter is occasionally wrong.</summary>
    public bool? BlurReading { get; set; }
    /// <summary>true (default): only reads whose check digits all match are returned. false: a read whose
    /// check digits do not match is returned too, with <see cref="MrzRead.CheckDigitsValid"/> false -
    /// specimens and sample documents often print digits that do not add up. On the camera such a read
    /// needs more agreeing frames than a valid one; it is the reader's best reading, not verified by the
    /// check digits: decide in the app what to do with it.</summary>
    public bool? EnforceCheckDigits { get; set; }
    /// <summary>The camera bar's title (default "Passport &amp; ID").</summary>
    public string? Title { get; set; }
    /// <summary>Mrz (default): read MRZs. Document: no MRZ reading - the camera finds an ID-1 card or a
    /// passport / visa page (e.g. a card's front), outlines it, and once it is held still captures it
    /// (<see cref="ScanResult.Documents"/>); the MRZ settings do not apply.</summary>
    public ScanMode? Mode { get; set; }

    internal string ToJson() => Json.Write(w =>
    {
        if (Continuous is bool c) w.WriteBoolean("continuous", c);
        if (DuplicateTimeout is TimeSpan d) w.WriteNumber("duplicateTimeoutMs", (long)d.TotalMilliseconds);
        if (Confirmation is Confirmation cf) w.WriteString("confirmation", cf == Maui.Confirmation.Fast ? "fast" : "normal");
        if (Timeout is TimeSpan t) w.WriteNumber("timeoutMs", (long)t.TotalMilliseconds);
        if (Torch is bool torch) w.WriteBoolean("torch", torch);
        if (Zoom is double z) w.WriteNumber("zoom", z);
        if (VibrateOnRead is bool v) w.WriteBoolean("vibrateOnRead", v);
        if (ArOverlay is bool a) w.WriteBoolean("arOverlay", a);
        if (DocumentTypes is DocumentTypes types) Json.WriteTypes(w, types);
        if (RegionOfInterest is ScanRegion r)
        {
            w.WriteStartObject("regionOfInterest");
            w.WriteNumber("x", r.X); w.WriteNumber("y", r.Y); w.WriteNumber("width", r.Width); w.WriteNumber("height", r.Height);
            w.WriteEndObject();
        }
        if (IsolateMrz is bool i) w.WriteBoolean("isolateMrz", i);
        if (IsolatedHeight is int h) w.WriteNumber("isolatedHeight", h);
        if (CapturePortrait is bool cp) w.WriteBoolean("capturePortrait", cp);
        if (CaptureSignature is bool cs) w.WriteBoolean("captureSignature", cs);
        if (CaptureDocument is bool cd) w.WriteBoolean("captureDocument", cd);
        if (BlurReading is bool br) w.WriteBoolean("blurReading", br);
        if (EnforceCheckDigits is bool ec) w.WriteBoolean("enforceCheckDigits", ec);
        if (Mode is ScanMode mode) w.WriteString("mode", mode == ScanMode.Document ? "document" : "mrz");
        if (Title is string title) w.WriteString("title", title);
    });
}

/// <summary>Options of <see cref="MrzScanner.ScanImageAsync(string, ImageOptions?)"/>.</summary>
public class ImageOptions
{
    /// <summary>The kinds of document to read (default all).</summary>
    public DocumentTypes? DocumentTypes { get; set; }
    /// <summary>&gt; 0: the read also gets <see cref="MrzRead.IsolatedMrz"/>, this many pixels tall.</summary>
    public int? IsolatedHeight { get; set; }
    /// <summary>The read also gets <see cref="MrzRead.Portrait"/> / <see cref="MrzRead.Signature"/> when the MRZ's side has them.</summary>
    public bool? CapturePortrait { get; set; }
    public bool? CaptureSignature { get; set; }
    /// <summary>The read also gets <see cref="MrzRead.Document"/>, the whole page around the MRZ.</summary>
    public bool? CaptureDocument { get; set; }
    /// <summary>Anti-blur, as <see cref="ScanSettings.BlurReading"/>.</summary>
    public bool? BlurReading { get; set; }
    /// <summary>As <see cref="ScanSettings.EnforceCheckDigits"/> (default true).</summary>
    public bool? EnforceCheckDigits { get; set; }

    internal string ToJson() => Json.Write(w =>
    {
        if (DocumentTypes is DocumentTypes types) Json.WriteTypes(w, types);
        if (IsolatedHeight is int h) w.WriteNumber("isolatedHeight", h);
        if (CapturePortrait is bool cp) w.WriteBoolean("capturePortrait", cp);
        if (CaptureSignature is bool cs) w.WriteBoolean("captureSignature", cs);
        if (CaptureDocument is bool cd) w.WriteBoolean("captureDocument", cd);
        if (BlurReading is bool br) w.WriteBoolean("blurReading", br);
        if (EnforceCheckDigits is bool ec) w.WriteBoolean("enforceCheckDigits", ec);
    });
}

/// <summary>One MRZ read.</summary>
public class MrzRead
{
    /// <summary>The MRZ lines joined by "\n".</summary>
    public string Text { get; }
    public bool CheckDigitsValid { get; }
    /// <summary>"Passport", "ID card", "Visa" or the document type.</summary>
    public string Kind { get; }
    /// <summary>The parsed fields: document_number, surnames, given_names, nationality, dob_raw, ...</summary>
    public IReadOnlyDictionary<string, JsonElement> Fields { get; }
    /// <summary>Where the MRZ is: 4 points (photos: in the image's pixels); null when unknown.</summary>
    public IReadOnlyList<Point>? Corners { get; }
    /// <summary>"data:image/jpeg;base64,..." when asked for.</summary>
    public string? IsolatedMrz { get; }
    /// <summary>"data:image/jpeg;base64,..." when asked for (CapturePortrait) and found.</summary>
    public string? Portrait { get; }
    /// <summary>"data:image/jpeg;base64,..." when asked for (CaptureSignature) and found.</summary>
    public string? Signature { get; }
    /// <summary>"data:image/jpeg;base64,..." when asked for (CaptureDocument) and the page could be placed.</summary>
    public string? Document { get; }

    internal MrzRead(JsonElement j)
    {
        Text = j.GetProperty("text").GetString() ?? "";
        CheckDigitsValid = j.GetProperty("checkDigitsValid").GetBoolean();
        Kind = j.TryGetProperty("kind", out var k) ? k.GetString() ?? "" : "";
        var fields = new Dictionary<string, JsonElement>();
        if (j.TryGetProperty("fields", out var f) && f.ValueKind == JsonValueKind.Object)
            foreach (var p in f.EnumerateObject()) fields[p.Name] = p.Value.Clone();
        Fields = fields;
        if (j.TryGetProperty("corners", out var c) && c.ValueKind == JsonValueKind.Array)
            Corners = c.EnumerateArray().Select(p => new Point(p[0].GetDouble(), p[1].GetDouble())).ToList();
        IsolatedMrz = Image(j, "isolatedMrz");
        Portrait = Image(j, "portrait");
        Signature = Image(j, "signature");
        Document = Image(j, "document");
    }

    private static string? Image(JsonElement j, string name) =>
        j.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static byte[]? Bytes(string? dataUrl) =>
        dataUrl is string s ? Convert.FromBase64String(s[(s.IndexOf(',') + 1)..]) : null;

    /// <summary>The MRZ lines.</summary>
    public IReadOnlyList<string> Lines => Text.Split('\n');

    /// <summary><see cref="IsolatedMrz"/> as JPEG bytes; null when not asked for.</summary>
    public byte[]? IsolatedMrzBytes => Bytes(IsolatedMrz);
    /// <summary><see cref="Portrait"/> as JPEG bytes; null when not asked for or not found.</summary>
    public byte[]? PortraitBytes => Bytes(Portrait);
    /// <summary><see cref="Signature"/> as JPEG bytes; null when not asked for or not found.</summary>
    public byte[]? SignatureBytes => Bytes(Signature);

    /// <summary>A field as text (word lists such as surnames joined by spaces); "" when missing.</summary>
    public string Field(string name) =>
        !Fields.TryGetValue(name, out var v) ? ""
        : v.ValueKind == JsonValueKind.Array ? string.Join(' ', v.EnumerateArray().Select(e => e.ToString())).Trim()
        : v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString();
}

/// <summary>What <see cref="MrzScanner.ScanAsync"/> returns.</summary>
public enum ScanMode { Mrz, Document }

/// <summary>A document captured without an MRZ (<see cref="ScanSettings.Mode"/> Document).</summary>
public class MrzDocument
{
    /// <summary>"card" (ID-1) or "page" (passport / visa).</summary>
    public string Kind { get; }
    public double Score { get; }
    /// <summary>Where it was in the camera frame, in the order of the image's corners.</summary>
    public IReadOnlyList<Point> Corners { get; }
    /// <summary>"data:image/jpeg;base64,...": straightened, landscape (which way is up is not known).</summary>
    public string Image { get; }

    internal MrzDocument(JsonElement j)
    {
        Kind = j.TryGetProperty("kind", out var k) ? k.GetString() ?? "card" : "card";
        Score = j.TryGetProperty("score", out var s) ? s.GetDouble() : 0;
        Corners = j.TryGetProperty("corners", out var c) && c.ValueKind == JsonValueKind.Array
            ? c.EnumerateArray().Select(p => new Point(p[0].GetDouble(), p[1].GetDouble())).ToList() : new List<Point>();
        Image = j.TryGetProperty("image", out var i) ? i.GetString() ?? "" : "";
    }
}

public class ScanResult
{
    public ScanStatus Status { get; }
    /// <summary>The reads: one, or with continuous settings each new document (possibly none).</summary>
    public IReadOnlyList<MrzRead> Reads { get; }
    /// <summary>Document mode: the documents captured (else empty).</summary>
    public IReadOnlyList<MrzDocument> Documents { get; }
    public TimeSpan Elapsed { get; }
    /// <summary>LicenseInactive: why. Error: what went wrong.</summary>
    public string? Reason { get; }
    public string? Message { get; }

    internal ScanResult(JsonElement j)
    {
        Status = (j.TryGetProperty("status", out var s) ? s.GetString() : null) switch
        {
            "read" => ScanStatus.Read,
            "stopped" => ScanStatus.Stopped,
            "timeout" => ScanStatus.Timeout,
            "permissionDenied" => ScanStatus.PermissionDenied,
            "licenseInactive" => ScanStatus.LicenseInactive,
            _ => ScanStatus.Error,
        };
        Reads = j.TryGetProperty("reads", out var r) && r.ValueKind == JsonValueKind.Array
            ? r.EnumerateArray().Select(e => new MrzRead(e)).ToList() : new List<MrzRead>();
        Documents = j.TryGetProperty("documents", out var docs) && docs.ValueKind == JsonValueKind.Array
            ? docs.EnumerateArray().Select(e => new MrzDocument(e)).ToList() : new List<MrzDocument>();
        Elapsed = TimeSpan.FromMilliseconds(j.TryGetProperty("elapsedMs", out var e) ? e.GetDouble() : 0);
        Reason = j.TryGetProperty("reason", out var why) && why.ValueKind == JsonValueKind.String ? why.GetString() : null;
        Message = j.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
    }
}

/// <summary>What <see cref="MrzScanner.RegisterLicenseAsync"/> returns.</summary>
public class LicenseResult
{
    /// <summary>0: accepted; -2 invalid key, -3 other app, -4 other device, -5 expired, -6 other platform.</summary>
    public int Status { get; }
    /// <summary>null, or a reason to show (with the MRZ2 check-in protocol: no_lease, expired,
    /// denied:&lt;reason&gt;, clock).</summary>
    public string? Reason { get; }
    public bool Accepted => Status == 0;

    internal LicenseResult(JsonElement j)
    {
        Status = j.GetProperty("status").GetInt32();
        Reason = j.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
    }
}

/// <summary>
/// The MRZ scanner: reads passports, ID cards and visas with the camera (AR mode, Android and iOS)
/// or from photos (all platforms, Windows too), offline. The API of every MRZ scanner wrapper
/// (platforms/API.md); on Android and iOS the native SDKs do the work, on Windows the core's DLL.
/// Call <c>builder.UseMrzScanner()</c> in MauiProgram.
/// </summary>
public static class MrzScanner
{
    /// <summary>Registers the licence key for this app (call once at start). Without it fields are masked.</summary>
    public static async Task<LicenseResult> RegisterLicenseAsync(string key) =>
        new(Parse(await Native.RegisterLicense(key)));

    /// <summary>Opens the full-screen camera, scans, closes. Asks for the camera permission.
    /// Windows: not yet (ScanStatus.Error); use <see cref="ScanImageAsync(string, ImageOptions?)"/>.</summary>
    public static async Task<ScanResult> ScanAsync(ScanSettings? settings = null) =>
        new(Parse(await Native.Scan((settings ?? new ScanSettings()).ToJson())));

    /// <summary>Reads the MRZ in a photo file; null when there is none.</summary>
    public static Task<MrzRead?> ScanImageAsync(string path, ImageOptions? options = null) =>
        ScanImage(Json.Write(w => w.WriteString("path", path)), options);

    /// <summary>Reads the MRZ in the bytes of a JPEG or PNG; null when there is none.</summary>
    public static Task<MrzRead?> ScanImageAsync(byte[] image, ImageOptions? options = null) =>
        ScanImage(Json.Write(w => w.WriteString("base64", Convert.ToBase64String(image))), options);

    /// <summary>The core's version (and this package's, the same), e.g. "2.5.0".</summary>
    public static async Task<string> VersionAsync() =>
        Parse(await Native.Version()).GetProperty("core").GetString() ?? "";

    static async Task<MrzRead?> ScanImage(string image, ImageOptions? options)
    {
        var j = Parse(await Native.ScanImage(image, (options ?? new ImageOptions()).ToJson()));
        return j.ValueKind == JsonValueKind.Object ? new MrzRead(j) : null;
    }

    static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}

static class Json
{
    public static string Write(Action<Utf8JsonWriter> body)
    {
        var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            body(w);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    public static void WriteTypes(Utf8JsonWriter w, DocumentTypes types)
    {
        w.WriteStartArray("documentTypes");
        if (types.HasFlag(DocumentTypes.Passport)) w.WriteStringValue("passport");
        if (types.HasFlag(DocumentTypes.Id)) w.WriteStringValue("id");
        if (types.HasFlag(DocumentTypes.Visa)) w.WriteStringValue("visa");
        w.WriteEndArray();
    }
}
