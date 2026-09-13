# OblivionUI 0.7.1 toolkit API

Reference `OblivionUI.Api.dll` and use `using OblivionUI;`. These are client UI APIs; gameplay authority remains in your mod/server. The existing interaction, notifications, book, popup and artwork APIs remain available. See BOOK-API.md and POPUP-ART-API.md for their base types.

## Try the workshop

Press **Alt+U**, then **Try all UI features**. The workshop includes Controls, Items/slots, Drawing, HUD and Theme pages. Page tabs show three at a time; the footer moves through the remaining pages. The item shop uses sample data and reports purchase requests without granting items or spending gold. HUD demo progress is simulated. No Ctrl shortcuts are used.

## Item grids and shop details

```csharp
var shop = ItemMenus.Open(new ItemMenuDefinition {
    Title = "Arena outfitter", CurrencyLabel="XP", Subtitle="Your server balance",
    Items = [
        new MenuItem { Id="iron-sword", Name="Iron sword", Category="Weapons",
            Price=35, Quantity=8, Description="A dependable arena weapon.",
            ImagePath="/Game/MyMod/Icons/T_Sword.T_Sword" }
    ],
    OnBuy = (item, quantity) => RequestPurchaseFromServer(item.Id, quantity)
});
```

`RequestPurchaseFromServer` represents your own networking code. The server must check price, stock, currency and inventory capacity before granting an item. `OnBuy` is a request callback, not an inventory operation. Reopen with refreshed authoritative items after a purchase. The helper provides category filtering, name search (Search button), name/price sorting, 30 cards per batch, selection, a detail page and quantity control. Maximum 512 items. Images must be cooked Texture2D object paths in a mounted mod package, not web URLs or filesystem images. Center layouts use three columns and side layouts two.

## Persistent HUD panels

```csharp
var status = HudPanels.Show(new HudPanelDefinition {
    Title="Arena", Text="Waiting for another player", X=.02, Y=.20,
    Width=320, Opacity=.95, ShowProgress=true, Progress=.25
});
status.Update(d => d with { Text="Match starting", Progress=.75 });
// Later:
status.Dispose();
```

Up to eight panels. X/Y are normalized top-left viewport coordinates; panels are clamped inside the screen. Width is 220–700 logical units. Progress and opacity are 0–1 and animate towards new values. Panels allow normal gameplay input and hide when gameplay is unavailable or a book is open. Hold the handle and dispose it when your activity ends. Use `Visible=false` for temporary hiding.

## Progress bars and rings

```csharp
new BookElement { Id="craft", Kind=BookControl.ProgressBar,
    Label="Crafting", Minimum=0, Maximum=100, Value=35 };
new BookElement { Id="cooldown", Kind=BookControl.ProgressRing,
    Label="Ready in", Minimum=0, Maximum=10, Value=6, Height=150 };
```

Update values with `book.Update("craft", e => e with { Value=50 });`. Bars interpolate; rings redraw their segmented fill. Percentages are displayed. Your mod supplies countdown/time values; the control does not advance a gameplay timer itself.

## Tooltips, tabs and collapsible sections

Set `Tooltip="Details shown on hover"` on a BookElement for a parchment tooltip. Set `BookDefinition.ShowTabs=true` to create page tabs. All page/control IDs must be unique across the book; IDs beginning `__` are reserved.

```csharp
new BookElement { Id="advanced", Kind=BookControl.Section,
    Label="Advanced options", Checked=true, Height=48 };
new BookElement { Id="gain", Kind=BookControl.Slider, SectionId="advanced",
    Label="Gain", Minimum=-24, Maximum=24, Value=0, Step=1 };
```

Activating the section header expands/collapses controls with its SectionId on the same page. Existing per-control Enabled and Visible flags still apply. `BookHandle.ReplacePages(BookPage[])` supports refreshed page content.

## Confirmation and choice dialogs

```csharp
Dialogs.Choose("Leave the arena?", "Your current match will end.",
    new DialogChoice { Id="leave", Label="Leave match", OnChoose=LeaveMatch },
    new DialogChoice { Id="stay", Label="Stay here" });
```

One to eight uniquely named choices, each optionally disabled. Choosing closes the dialog before invoking its callback. The close button cancels. Books/dialogs are modal; opening one replaces the current book. Return to your parent menu explicitly in a callback if needed.

## Keybind controls and profiles

```csharp
new BookElement { Id="my-mod.interact", Kind=BookControl.Keybind,
    Label="Interaction key", DefaultKey="E",
    OnChange=e => ApplyInteractionKey(e.Text) };
KeyBindings.Save("my-settings");
KeyBindings.Load("my-settings");
var key = KeyBindings.Get("my-mod.interact", "E");
```

Activate the control and press a key. Escape cancels capture; Backspace restores DefaultKey. Conflicts are checked against bindings registered in this shared API, case-insensitively; keys owned by the base game or unrelated mods cannot be detected. `Assign`, `Conflict`, `Get` and `Release` are available directly. Use namespaced IDs. The picker captures single keyboard keys, not modifier chords or controller rebinding. Your callback must apply the setting to the mod's actual input registration. Loading a profile replaces the shared saved binding set and does not invoke control callbacks: apply loaded settings explicitly.

## Drag-and-drop slots

```csharp
new BookElement { Id="bag", Kind=BookControl.Slot, Label="Bag",
    DragGroup="equipment", DragPayload="iron-sword" };
new BookElement { Id="hand", Kind=BookControl.Slot, Label="Hand",
    DragGroup="equipment" };
// In BookDefinition:
OnDrop = drop => ValidateAlreadyAuthorizedMove(drop.Payload,
                                               drop.SourceId, drop.TargetId)
```

Drag a populated slot onto an empty enabled slot in the same nonempty DragGroup. Keyboard/controller users can activate the source then the destination. `OnDrop` must return true before the UI moves the payload; absent callbacks reject moves. Occupied destinations are rejected. This only moves UI payload strings and does not transfer game items. For asynchronous server validation, return false, send a request, then update both controls after the server accepts. Never block the game thread waiting for a network response. `SlotMoves.Request(book, sourceId, targetId)` exposes the same validated UI operation programmatically.

## Interactive drawing canvas

```csharp
var drawing = new CanvasSession(610,280) {
    DrawingEnabled=true, BrushThickness=3,
    BrushColor=new UiColor(.18f,.10f,.04f),
    Markers=[new CanvasMarker { Id="camp", Position=new UiPoint(180,100) }],
    Regions=[new CanvasRegion { Id="map", Position=new UiPoint(0,0),
        Width=610, Height=280 }],
    OnClick=e => HandleMapClick(e.Id,e.Position),
    OnMarkerMoved=e => HandleMarkerMove(e.Id,e.Position)
};
var element = new BookElement { Id="map", Kind=BookControl.Canvas,
    Width=610, Height=280, Canvas=drawing };
drawing.SetView(1.5,new UiPoint(20,10));
drawing.Undo(); drawing.Redo();
drawing.Save("my-map"); drawing.Load("my-map");
```

Canvas dimensions must match the element. Left mouse draws or drags a marker; right mouse pans. Plus/minus zoom while hovering the canvas. The workshop also provides buttons for view changes, undo/redo, clear and save/load, without Ctrl shortcuts. `SetView`, `MoveMarker`, `AddStroke`, `Clear`, `ToView` and `ToWorld` are public. Click callbacks return document coordinates and the last matching rectangular region ID, or null. Marker Label is metadata; markers currently render as coloured squares.

Dimensions: width 32–630, height 32–2000; zoom .25–8; up to 64 markers/regions. Drawing shares the bounded artwork limits (128 shapes, 512 line segments). A mouse stroke has at most 256 points. Undo history retains 32 stroke states; marker moves and zoom/pan are not part of stroke undo. Save/load persists drawing dimensions, strokes and markers; recreate callbacks, regions and view settings in your mod. Freehand pointer input requires a mouse; provide your own button actions for controller alternatives.

Profiles are local JSON in `%LOCALAPPDATA%\OblivionUI\Profiles`. Names use ASCII letters, digits, hyphens and underscores; keep names under 70 characters to allow suffixes. Save overwrites the named profile. Loading a missing or invalid file throws: catch exceptions and show a user-facing message. Local profiles are not authoritative server data.

## Shared theme and navigation

```csharp
UiThemes.Current = new UiTheme {
    Scale=.90, FontScale=1.05, Spacing=1.10, AnimationSpeed=1,
    Ink=new UiColor(.18f,.10f,.05f), Accent=new UiColor(.55f,.39f,.16f),
    PaperTint=new UiColor(1,1,1,1)
};
```

Use `BookDefinition.Theme` for a per-book override. Theme settings affect books and HUD panels; existing interaction prompts/speaking icons/notification animations retain their own settings. Scale accepts .5–1.5, FontScale .75–1.5, Spacing .5–2, AnimationSpeed .1–5. Book scale is capped at 1 to fit its viewport; HUD panels can enlarge. Font scaling applies title, label, description and action text; the native dropdown uses its built font. Optional PaperImagePath supplies a cooked Texture2D; empty uses the normal parchment. Excessively long text may need a taller custom control.

Buttons use native UMG focus/navigation with an accent highlight, and the first enabled control receives focus when a page renders. Existing keyboard/controller activation is used for menus. Mouse-only gestures and the keyboard key picker are documented above; this is not full controller rebinding or controller freehand drawing.

## Lifecycle and validation

Book callbacks run on the game update thread. Keep callbacks short, use server messages for gameplay, and dispose handles on teardown. APIs validate dimensions/ranges and reject invalid definitions instead of creating malformed widgets. A canvas is intentionally a live session object; ordinary book arrays are copied during validation. The workshop is safe sample UI and does not replace the arena's gameplay logic.

`ItemMenuDefinition.CurrencyLabel` defaults to `gold`; set `XP` for arena currency. `Subtitle` adds balance/status text, and `OnClose` hooks normal menu closure. Card text layouts also support catalogues without images.
