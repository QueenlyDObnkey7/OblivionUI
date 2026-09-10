# OblivionUI

Reusable Oblivion-style UI for **Oblivion Remastered multiplayer (OBMP)**: interaction prompts, sliding torn-paper notifications and customizable parchment book menus.

Version **0.3.2**. Assets target **Unreal Engine 5.3.2**; managed projects target **.NET 10**. Requires **OblivionMP.SDK 0.2.0 or newer**, with a compatible OBMP client and its UE4SS reflection exports. Compatibility with other game/SDK versions is not guaranteed.

## Install the ready-built mod

Copy `dist/mods/OblivionUI` into your OBMP server's `mods` directory, alongside the SDK mod. Reconnect/restart the client so it receives the updated streamed mod. No Unreal installation is needed to use the prebuilt package.

Keep all five client files together:

```text
mods/OblivionUI/
  manifest.json
  kingthingsEULA.txt
  client/
    OblivionUI.Api.dll
    OblivionUI.Client.dll
    OblivionUI_P.pak
    OblivionUI_P.utoc
    OblivionUI_P.ucas
```

The `.pak`, `.utoc` and `.ucas` form one asset package. Upload the complete mod directory, not only the pak. There is no OblivionUI server DLL. Gameplay decisions belong to the consuming mod's server.

In-game demonstrations: **Alt+B** opens the book with example controls; **Alt+N** previews left then right notifications. Close the book with its Close button.

## Use the API in another mod

Add a client-side reference to `OblivionUI.Api.dll`. The installed OblivionUI mod supplies the shared assembly at runtime; do **not** bundle another copy into your own mod.

```xml
<ItemGroup>
  <Reference Include="OblivionUI.Api">
    <HintPath>PATH_TO_OBLIVIONUI/dist/mods/OblivionUI/client/OblivionUI.Api.dll</HintPath>
    <Private>false</Private>
  </Reference>
</ItemGroup>
```

Replace the example path with your actual location. Add this object to the `dependencies` array in your mod's manifest, preserving existing dependencies:

```json
{ "uniqueId": "OblivionUI", "minimumVersion": "0.3.2" }
```

All public API types below use `using OblivionUI;`. Call them from your client mod after startup. `Hud.IsReady` indicates the managed renderer has started; it does not prove that a visible frame has rendered. `Books.IsAvailable` becomes true after the native book opens successfully; do not use it to block the first call to `Books.Open`.

## Interaction prompt

```csharp
Hud.SetPrompt("[E] Open store");
// When the player leaves the interaction area:
Hud.SetPrompt(null);
```

The compact prompt is horizontally centered at about **70% of viewport height**. The bracketed prefix supplies the displayed key. This API only draws text: it does not bind E, detect locations or run an interaction. Your mod handles those actions, or uses the separate OblivionInteraction API. OblivionInteraction is optional and is not bundled here.

There is one shared prompt slot. Setting it replaces the current prompt; coordinate ownership with other prompt providers. Clearing the prompt preserves notifications. Configure the actual key in the input/interaction mod and pass the same label to this UI.

## Parchment notifications

```csharp
Notifications.Show("Journal updated", "Speak to the arena master.");
IDisposable notice = Notifications.Show(
    "Arena registration", "Waiting for another player.",
    side: NotificationSide.Left, duration: 7);

// Optional cancellation of this mod's own notification:
notice.Dispose();
```

`Notifications.Show(string title, string body = "", NotificationSide side = Right, double duration = 4)` returns an `IDisposable` cancellation handle.

- Position: near the selected screen edge at **20% of viewport height**.
- Appearance: cream paper with transparent torn edges and dark lettering.
- Timing: 0.32-second slide in, requested duration, then 0.32-second slide out.
- Duration must be finite and between **1 and 30 seconds**, inclusive.
- One notification displays at a time across both sides. Up to **16 waiting** notices are kept; when full, the oldest waiting notice is dropped.
- Titles are capped at 64 Unicode text elements and bodies at 180, with truncation. Line breaks, tabs and `|` become spaces.
- Calls are thread safe. Null text or invalid enum/duration values throw argument exceptions.

`Notifications.Clear()` clears **every mod's** queued/active notification. Prefer disposing the handles you own. HUD notices are hidden while normal gameplay input is unavailable, such as while a modal book is open. The timer is not paused for that hidden period. Notifications are cleared when the local player changes/disconnects.

## Open a book

```csharp
BookHandle book = Books.Open(new BookDefinition
{
    Title = "Store ledger",
    Subtitle = "Choose your supplies",
    Pages = [new BookPage
    {
        Title = "Supplies",
        Layout = BookLayout.List,
        Elements = [
            new BookElement {
                Id = "welcome", Kind = BookControl.Text,
                Label = "Welcome, traveller",
                Description = "Browse the items below.", Height = 100
            },
            new BookElement {
                Id = "buy", Kind = BookControl.Button,
                Label = "Iron arrows", ButtonLabel = "Purchase",
                OnChange = e => {
                    e.Book.Update("buy", item => item with { Enabled = false });
                    // Send your purchase request to your server here.
                }
            }
        ]
    }]
});
```

Only **one modal book** may be open across all mods. Opening another requests closure of the previous book. The native runtime captures mouse/keyboard input for the book and restores game input when it closes. Long pages scroll using the themed scrollbar.

### Controls

Every `BookElement` needs an `Id`, unique throughout the entire book. Common properties are `Label`, `Description`, `Enabled` (default true), `Visible` (default true) and geometry.

| `BookControl` | Purpose | Main properties | Event data |
| --- | --- | --- | --- |
| `Heading` | Section heading | `Label`, `Description` | Display only |
| `Text` | Body copy / subtext | `Label`, `Description` | Display only |
| `Button` | Clickable action | `ButtonLabel` (default `Choose`) | `Id`, `Book` |
| `Slider` | Numeric value | `Minimum`, `Maximum`, `Step`, `Value` | `Value` |
| `Toggle` | On/off option | `Checked` | `Checked` |
| `Dropdown` | Choose an option | `Options`, `SelectedIndex` | `SelectedIndex`, `Text` |
| `TextInput` | Editable text | `Text` | `Text` |

Set `OnChange = e => ...` for clicks and value changes. `BookEvent` contains `Book`, `Id`, `Kind`, `Value`, `Checked`, `Text`, and `SelectedIndex`. Use the fields appropriate to that control. Callbacks run on the game update thread; keep them short. Callback exceptions are logged by the client runtime.

A slider with **0 in the middle** uses a symmetric range:

```csharp
new BookElement {
    Id = "gain", Kind = BookControl.Slider,
    Label = "Gain (dB)", Minimum = -24, Maximum = 24,
    Step = 1, Value = 0,
    OnChange = e => Console.WriteLine($"Gain: {e.Value} dB")
}
```

The library does not apply audio gain or save preferences itself. A complete, compilable settings menu using **all seven controls** is in [Examples/SettingsBook.cs](Examples/SettingsBook.cs). It accepts a device list and a save callback from the consuming mod. [Examples/Layouts.cs](Examples/Layouts.cs) demonstrates multiple pages and both alternative layouts.

### Layouts and customization

Each `BookPage` supports its own `Title`, `Subtitle`, `Layout`, `Gap`, and `Elements`.

| Layout | Behavior |
| --- | --- |
| `BookLayout.List` | Stacks visible controls vertically |
| `BookLayout.TwoColumns` | Places controls in two automatically sized columns |
| `BookLayout.Custom` | Uses each element's `X`, `Y`, `Width`, `Height` |

Coordinates use a **630-unit-wide** content area; the book scales with the viewport. Default element width is 610, height 128, and page gap 16. Use at least 118 height for interactive rows to fit the native controls. Text and headings can be shorter. Custom rectangles must satisfy `X >= 0`, `Y >= 0`, `X + Width <= 630`, positive width, height 32–2000 and `Y <= 20000`. Page gap must be 0–100. Numeric values must be finite.

Content, pages, controls, callbacks, geometry and visibility are customizable through C#. Fonts, paper, colours, native widget styling and the outer book frame are Unreal assets; changing those requires asset editing and a rebuild. This API has no per-book theme/texture parameter.

### Updating and closing

```csharp
book.Update("buy", item => item with {
    Enabled = true, Description = "Back in stock"
});
book.GoToPage(0); // zero-based page index
bool open = book.IsOpen;
book.Dispose();
```

`Update` must preserve the element ID. An unknown ID throws `KeyNotFoundException`; invalid definitions or changed IDs throw argument exceptions. A successful update refreshes the page and scrolls to its start. Values are retained across page changes. Opening and updating copy definitions, including dropdown option arrays.

`BookDefinition.OnClose` is an optional callback invoked when the native runtime closes a rendered book. `Dispose()` requests closure on the next UI update; `IsOpen` reports that closure has not been requested, not whether a native frame is currently visible. Do not rely solely on `OnClose` for cleaning up a book that was never rendered. Keep and dispose your handles when your mod unloads.

A definition must contain **1–32 pages**, with **0–128 elements per page**. Dropdowns need 1–128 non-null options and a valid selected index. Slider minimum must be below maximum, step positive, and initial value within range. Invalid definitions throw before replacing the current book.

## Integration and compatibility

The UI is presentation only. Purchases, rewards, match registration and permission checks must still be validated by your server. Use callbacks to invoke your own RPCs; do not trust a clicked button as authorization.

Prompts and notifications use OBMP's shared `/Game/Mods/OblivionMp/WBP_GameMessage` widget. Other mods replacing that same asset, or directly calling `SDK.GameMessage.ShowGameMessage` / `HideGameMessage`, can overwrite this display. Use the shared `Hud` and `Notifications` APIs to coordinate. The separate SDK info-message widget is not replaced.

The native book bridge validates expected UE function parameter sizes against `BookNative.tsv`. An incompatible game/native interface can disable the book and log an error. Do not patch guessed offsets to force an unsupported build to work.

## Repository contents

```text
OblivionUI.Api/             Shared C# API (no SDK dependency)
OblivionUI.Client/          OBMP runtime, native book bridge and demos
Source/                    Unreal editor widget generators
Content/                   Editable Unreal widget, texture and font assets
Art/                       Original and processed source artwork
Config/                    Portable Unreal project settings
ThirdParty/Kingthings/     Original font and EULA
Tests/                     Managed API regression checks
Examples/                  Compilable consuming-mod examples
Dependencies/Client/       Instructions for external SDK references
Tools/retoc/               Instructions for asset packaging prerequisite
BUILD.ps1                  Managed / complete asset build
OblivionRemastered.uproject Unreal 5.3 project
manifest.json              Mod metadata
dist/mods/OblivionUI/      Ready-built installable package
```

The repository includes source and prebuilt assets, not engine tools, SDK distributions, caches, backups or server/player data. The largest included file is approximately 13.2 MB. `.gitattributes` treats assets as binary without requiring Git LFS.

## Build and test

See [docs/BUILD.md](docs/BUILD.md) for prerequisites and asset editing. API, tests and examples need only .NET 10:

```powershell
dotnet run --project Tests/UiChecks.csproj -c Release
dotnet build Examples/Examples.csproj -c Release
```

Build the managed client using your matching local SDK:

```powershell
./BUILD.ps1 -ManagedOnly -SdkDirectory 'D:\OBMP-SDK\Client'
```

Full Unreal build and cook:

```powershell
./BUILD.ps1 -EngineDirectory 'C:\Program Files\Epic Games\UE_5.3' `
    -SdkDirectory 'D:\OBMP-SDK\Client' `
    -RetocPath 'D:\Tools\retoc\retoc.exe'
```

Full build output is `Output/mods/OblivionUI`. The checked-in `dist` snapshot is not overwritten automatically. Review and test the rebuilt package before replacing the snapshot for a release.

## Credits and licensing

Kingthings Petrock by **Kevin King**, used with its included EULA. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) and [Art/README.md](Art/README.md) for font terms, artwork provenance and external prerequisites. No project-wide open-source license has been selected yet.
