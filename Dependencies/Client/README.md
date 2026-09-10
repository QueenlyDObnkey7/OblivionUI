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

Use the SDK that matches your OBMP client. External SDK binaries are not included in this repository. The shared UI API, examples and managed tests build without them. The prebuilt package in dist requires no compilation.
