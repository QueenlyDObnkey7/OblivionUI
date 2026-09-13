# Speaking indicator API — OblivionUI 0.4.0

Reference OblivionUI.Api and declare an OblivionUI >= 0.4.0 dependency. Refresh from a mod update system roughly 20 times per second:

```csharp
SpeakerIndicators.Publish(localSpeaking, activeRemotePlayerIds);
```

`activeRemotePlayerIds` is an `IEnumerable<string>` containing the SDK's `PlayerId.ToString()` values. Set `localSpeaking` only when microphone speech is being transmitted. Call `SpeakerIndicators.Clear()` when your voice provider disconnects or shuts down. The single active voice provider owns this state; it expires after one second without refresh. At most 64 unique remote IDs are accepted and 32 visible icons are drawn per frame.

The UI applies all visibility rules; callers cannot bypass them:

- Local icon: bottom-right anchor, 48 UI units, 28 units from the right and 30 from the bottom. The actual symbol has transparent padding.
- Remote icon: 36 UI units, above the upper collision bounds. Omit self, dead/unspawned and sneaking players.
- Trace from the current camera to the target's head/upper body with Unreal's complex Visibility collision query, ignoring the two player pawns. Blocking walls and corners hide the icon. Project to DPI-adjusted screen coordinates; off-screen targets have no directional arrow.
- Re-evaluate occlusion and sneaking every UI frame at 30 Hz. No visibility linger; audio activity may linger briefly to avoid flicker between words.
- Hide in book menus, loading screens, disconnected state, and if required SDK/Unreal functions are unavailable. No SDK text-marker fallback.

The original speaker artwork is generated from geometric shapes by `BuildVoiceUI.cpp`: warm ivory fill, bronze outline and dark relief shadow on transparent pixels. Both positions use this same texture. No emoji/font glyph dependency.

Runtime requires the complete new cooked UI package. Restart the game after upgrading, since existing loaded widgets may retain old class references. The SDK's internal `ReadyMainCharacter.Entity.Pawn` mapping is accessed by named managed reflection; missing mappings fail closed. Unreal parameter layouts are exported from UE 5.3.2 and checked before calls.

Validation: `dotnet run --project Tests/UiChecks.csproj -c Release` covers visibility predicates and API state. The editor commandlet `-run=BuildInteractionUI -VoiceChecks -unattended -nullrhi` checks real Unreal wall/corner/clear-path collisions and the local widget's anchor/default visibility. Actual OBMP multiplayer integration must additionally be tested with a speaking player across a wall and while sneaking.


Version 0.4.1: Overhead placement uses the remote character head socket, avoiding empty collision-only bounds. Engine parameter layouts are resolved from runtime reflection. Wall occlusion, sneaking, living-player and on-screen filters remain required. The local speaker icon stays at the bottom right; overhead icons are for other players. Managed privacy checks pass; the new runtime placement needs a two-player in-game test.
