# Export validation — September 13, 2026

- Version 0.9.4 managed API/client and examples build successfully with .NET 10 and matching local OBMP SDK references: zero warnings and errors.
- All 1,364 managed UI checks and the transparent-button native metadata check pass from this repository.
- The five prebuilt client files and manifest were copied from the current local server deployment. SHA256SUMS.txt records the exported package hashes.
- UnrealPak integrity testing and retoc IoStore verification pass for the exported assets.
- Official SDK dependencies, local caches, server state, and unrelated gameplay mod assets are excluded.

The Unreal editor project was not recooked for this export. Prebuilt widgets are the current deployed assets. These checks are not a new in-game test and do not establish FPS gains or compatibility with another game/SDK version. See PERFORMANCE-NOTES.md for the scope and limitations of the optimization work.
