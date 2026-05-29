# Fix Build Errors

## Problem

Building `VideoCompressor.sln` (Debug|x64) with .NET SDK 9.0.309 failed with 7 errors in the C# WPF project (`VideoCompressorUI`):

```
NETSDK1083: The specified RuntimeIdentifier 'win10-x64' is not recognized.
NETSDK1083: The specified RuntimeIdentifier 'win10-arm' is not recognized.
... (5 more similar errors)
```

**Root cause:** `Microsoft.WindowsAppSDK 1.5.240428000` ships framework packs with legacy `win10-*` Runtime Identifiers. .NET SDK 9 dropped those RIDs from its catalog (only `win-x64`, `win-arm64`, etc. are recognized). The package was never actually used in any source file — it was a leftover placeholder for a planned Mica backdrop effect.

**Secondary fix:** `<AppendTargetFrameworkToOutputPath>` was not disabled, so `VideoCompressorUI.exe` was outputting to `bin\Debug\net8.0-windows\` while `VideoCompressor.exe` (C++) went to `bin\Debug\`. Since the UI locates the backend by `AppDomain.CurrentDomain.BaseDirectory`, both EXEs must share the same folder.

## Implementation Steps

1. **Remove unused `Microsoft.WindowsAppSDK` package reference** from `VideoCompressorUI\VideoCompressorUI.csproj`.
2. **Add `<AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>`** so both EXEs land in `bin\$(Configuration)\` together.
3. Run `dotnet restore --force` inside `VideoCompressorUI\` to regenerate `obj\project.assets.json`.
4. Build the solution via MSBuild (Restore target first, then Build target).

## Files Changed

| File | Change |
|------|--------|
| `VideoCompressorUI\VideoCompressorUI.csproj` | Removed `<PackageReference Include="Microsoft.WindowsAppSDK" .../>` block; added `<AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>` |

## How to Test

1. Open a terminal in the solution root.
2. Run:
   ```powershell
   dotnet restore --force .\VideoCompressorUI\VideoCompressorUI.csproj
   $msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
   & $msbuild VideoCompressor.sln /p:Configuration=Debug /p:Platform=x64 /t:Restore
   & $msbuild VideoCompressor.sln /p:Configuration=Debug /p:Platform=x64 /t:Build
   ```
3. Confirm output: `Build succeeded. 0 Warning(s) 0 Error(s)`
4. Verify both EXEs exist in the same folder:
   ```
   bin\Debug\VideoCompressor.exe    ← C++ backend
   bin\Debug\VideoCompressorUI.exe  ← C# WPF frontend
   ```
5. Launch `VideoCompressorUI.exe` and verify the UI opens without errors.
