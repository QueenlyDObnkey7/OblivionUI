# Building OblivionUI

## Prerequisites

- Windows with .NET 10 SDK for C# builds.
- A matching OBMP client SDK. See ../Dependencies/Client/README.md for the eight referenced DLLs. Put them in that directory or pass -SdkDirectory. The references use Private=false so the host supplies them at runtime.
- Unreal Engine **5.3.2** for native assets. The build checks the exact major/minor/patch version.
- Visual Studio C++ build tools with MSVC **14.38.33130** (the build script selects this version) and Windows SDK **10.0.22621.0**, matching the original project build environment.
- retoc **0.1.5** for legacy pak to IoStore conversion. See ../Tools/retoc/README.md. Supply any local compression/runtime prerequisites required by the tool; do not upload engine DLLs to this repository.

No Unreal installation is needed for the managed API tests or to install the prebuilt dist package.

## Build entry points

Run commands from the repository root:

```powershell
dotnet run --project Tests/UiChecks.csproj -c Release
dotnet build Examples/Examples.csproj -c Release
./BUILD.ps1 -ManagedOnly -SdkDirectory 'D:\OBMP-SDK\Client'
./BUILD.ps1 -SdkDirectory 'D:\OBMP-SDK\Client' -EngineDirectory 'D:\UE_5.3' -RetocPath 'D:\Tools\retoc\retoc.exe'
```

The full build compiles the client/API and examples, runs managed tests, builds the editor module, generates missing widgets, cooks assets, creates and tests a legacy pak, converts/verifies IoStore containers, and assembles Output/mods/OblivionUI. It does not install or restart a server. Exit failures stop packaging.

## Editing Unreal assets

Open OblivionRemastered.uproject in UE 5.3.2. Runtime assets live under Content/OblivionUI and Content/Mods/OblivionMp. The widget generator source is under Source/OblivionUIEditor. Existing widgets are intentionally preserved during builds so hand-edited assets are not overwritten.

Changing generator C++ does not automatically regenerate saved widgets. Back up your edited assets, then move the corresponding widget assets aside before running the relevant generation command. Book assets depend on the included texture and font assets; preserve those unless you intend to regenerate them too. Use version control to review generated asset changes.

The included BookNative.tsv is part of the C# client as an embedded resource and describes this UE version's native function layout. It must match the target game/native interface. Do not substitute another engine version for a release build.

## Release check

1. Run managed tests and build examples.
2. Run the full build after asset changes; it verifies pak and IoStore integrity.
3. In a compatible OBMP client, verify Alt+B, every control type, paging, scrolling, close/input restoration, and Alt+N for both notification sides.
4. Verify the consuming mod's gameplay/network behavior separately.
5. Copy the reviewed Output/mods/OblivionUI directory into dist/mods/OblivionUI for the next release. Keep the manifest and all five client files from the same build.

Source export validation is recorded in VALIDATION.md. That report distinguishes managed/package checks from actual in-game validation.
