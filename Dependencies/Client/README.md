# OBMP build references

Supply matching OBMP client SDK DLLs here, or pass `-SdkDirectory` to BUILD.ps1. These are build prerequisites, not files to copy into your consuming mod:

- OblivionMp.Sdk.dll
- OblivionMpCSharpMod.dll
- ReadyM.Sdk.Common.dll
- ReadyM.Modloader.dll
- ReadyM.Api.dll
- ReadyM.Api.Multiplayer.dll
- ReadyM.Relay.Common.Oblivion.dll
- Friflo.Engine.ECS.dll

To fetch them from a ReadyM server release, run from the repository root with a release URL, the release .zip, a folder holding it, or the unzipped folder:

```powershell
./GET-DEPENDENCIES.ps1 https://readycodestorage.blob.core.windows.net/oblivion-server-releases/oblivion-server-win64-1.0.1.zip
```

The script also copies OblivionMp.Sdk.Common.dll when the release has it, and records the source in SOURCE.txt.

Use the SDK that matches your OBMP client. External SDK binaries are not included in this repository. The shared UI API, examples and managed tests build without them. The prebuilt package in dist requires no compilation.
