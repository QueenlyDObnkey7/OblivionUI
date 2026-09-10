# Export validation — September 10, 2026

- Standalone API/client build with .NET 10 and explicitly supplied matching local OBMP SDK references: zero warnings and errors.
- All 37 managed notification/book API checks pass from this folder.
- Both example source files compile against the shared API: zero warnings and errors.
- All five prebuilt client files match the existing OblivionUI 0.3.2 release (SHA-256 comparisons).
- UnrealPak -Test completed successfully; retoc verify reported verified for the included IoStore container.
- Upload text was scanned for local user-home paths and common credential patterns; none remained. Engine-local Android file-server settings were omitted.
- Build artifacts are kept outside the upload folder after validation and ignored for future builds.

The Unreal editor project was not fully recooked as part of this source export. The included prebuilt widgets are the existing release assets. These export checks do not constitute a new in-game test or establish compatibility with another game/SDK version.
