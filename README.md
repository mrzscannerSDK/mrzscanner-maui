# MRZScanner.Maui

Reads the machine-readable zone (MRZ) of passports, ID cards and visas, offline. .NET MAUI 10 on
Android 7+ (API 24) and iOS 15+: a full-screen camera with AR mode (a box follows the MRZ and
shows the read) and photos. Windows 10 (x64): photos (the camera scan is coming).

```
dotnet add package MRZScanner.Maui
```

`MauiProgram.cs`: `builder.UseMauiApp<App>().UseMrzScanner();`

iOS: a camera usage text in `Platforms/iOS/Info.plist`:

```xml
<key>NSCameraUsageDescription</key>
<string>Scans the MRZ of passports and ID cards.</string>
```

iOS 27 requires the scene lifecycle, which the MAUI 10 app template does not set up (the app
stops at launch in `_UIApplicationEvaluateRuntimeIssueForNoSceneLifecycleAdoption`, with or without
this package). Add to `Info.plist`:

```xml
<key>UIApplicationSceneManifest</key>
<dict>
  <key>UIApplicationSupportsMultipleScenes</key><false/>
  <key>UISceneConfigurations</key>
  <dict>
    <key>UIWindowSceneSessionRoleApplication</key>
    <array>
      <dict>
        <key>UISceneConfigurationName</key><string>__MAUI_DEFAULT_SCENE_CONFIGURATION__</string>
        <key>UISceneDelegateClassName</key><string>SceneDelegate</string>
      </dict>
    </array>
  </dict>
</dict>
```

and to `Platforms/iOS`: `[Register("SceneDelegate")] public class SceneDelegate : MauiUISceneDelegate { }`

## Use

```csharp
using MRZScanner.Maui;

await MrzScanner.RegisterLicenseAsync("MRZ2...");      // once; without it some fields are masked

var result = await MrzScanner.ScanAsync(new ScanSettings { DocumentTypes = DocumentTypes.Passport | DocumentTypes.Id });
if (result.Status == ScanStatus.Read)
{
    var read = result.Reads[0];
    Console.WriteLine($"{read.Text} {read.Field("document_number")} {read.CheckDigitsValid}");
}

var fromPhoto = await MrzScanner.ScanImageAsync(photoPath);   // or the bytes of a JPEG / PNG
```

`ScanAsync(settings)` opens the camera (asking for the permission), and closes after the first
read, or with `Continuous = true` collects each new document until Stop. `ScanResult`: `Status`
(`Read`, `Stopped`, `Timeout`, `PermissionDenied`, `LicenseInactive`, `Error`), `Reads`,
`Elapsed`. `ScanSettings`: `Continuous`, `DuplicateTimeout`, `Confirmation`, `Timeout`, `Torch`,
`Zoom`, `VibrateOnRead`, `ArOverlay`, `DocumentTypes`, `RegionOfInterest`, `IsolateMrz`,
`IsolatedHeight`, `Title`.

Each `MrzRead`: `Text` (the MRZ lines), `CheckDigitsValid`, `Kind`, `Fields` (`document_number`,
`surnames`, `given_names`, `nationality`, `dob_raw`, `expiration_date_raw`, ...; `Field(name)`
gives one as text), `Corners`, `IsolatedMrz` / `IsolatedMrzBytes`.

Version 2.7.4. Licence: a commercial MRZ scanner licence (https://mrzscanner.com/).
