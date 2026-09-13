# Book API — OblivionUI 0.9.4

The book is an actual Unreal UMG menu, supplied in the same streamed package as the interaction prompt and notifications. The OBMP client connects it to C# using the bundled UE4SS reflection exports. The current implementation targets the installed UE 5.3.2 game build; argument-size mismatches disable the book instead of guessing memory layouts.

Reference `OblivionUI.Api.dll` with `Private=false` from a client mod and declare an `OblivionUI` dependency with minimum version `0.9.4`. Keep the API and client DLL, pak, utoc and ucas together. `Alt+U` opens the UI toolkit demonstration. Close returns mouse input to the game. Only one book is shown at a time; opening another replaces it.

```csharp
using OblivionUI;

BookHandle ledger = Books.Open(new BookDefinition {
    Title = "Store ledger",
    Subtitle = "Choose your supplies",
    Pages = [new BookPage {
        Layout = BookLayout.List,
        Elements = [
            new BookElement {
                Id = "quantity", Kind = BookControl.Slider,
                Label = "Quantity", Description = "How many would you like?",
                Minimum = 1, Maximum = 20, Step = 1, Value = 1,
                OnChange = e => Console.WriteLine(e.Value)
            },
            new BookElement {
                Id = "buy", Kind = BookControl.Button,
                Label = "Iron arrows", ButtonLabel = "Buy arrows",
                OnChange = e => RequestPurchaseFromServer()
            }
        ]
    }]
});

ledger.Update("buy", item => item with { Enabled = false, Description = "Sold out" });
ledger.GoToPage(0);
ledger.Dispose();
```

Available controls: `Heading`, `Text`, `Button`, `Slider`, `Toggle`, `Dropdown`, `TextInput`. All have an ID, label, description, visibility and enabled state. Buttons have `ButtonLabel`; sliders use `Minimum`, `Maximum`, `Step`, `Value`; toggles use `Checked`; dropdowns use `Options` and `SelectedIndex`; text fields use `Text`. `OnChange` receives the handle, ID, kind and current value. Button clicks use the same callback. Callbacks run on the game update thread and exceptions are logged.

Pages support `List`, `TwoColumns` and `Custom`. `Gap` controls automatic spacing. Custom positions use `X`, `Y`, `Width`, `Height` within a 630-unit wide scroll area. Standard interactive rows need at least 118 units of height (sliders need 128 to include their scale labels); headings and text can be shorter. The whole book scales with the viewport. Titles and subtitles can be overridden per page. State is retained when changing pages; `Update` refreshes the current page and scrolls to the start.

Definitions are copied when opened, including option arrays. IDs must be unique across the entire book. Limits are 32 pages and 128 elements per page. `Dispose` requests closure on the next UI update; `IsOpen` reports whether closure has been requested, not whether a frame has already rendered. `Books.IsAvailable` becomes true after the native book opens successfully.

Purchases, rewards and permissions still belong in the consuming mod's server logic. The book supplies UI events; the demo never spends gold or starts an arena match.

Status: 37 managed checks pass. Unreal widget-event/render checks and live tests of all interactive control types pass. See BOOK-TEST-REPORT.md for evidence and remaining checks. The background now uses real transparency and the book has a themed scrollbar. These latest visual changes are ready for the user's in-game check.


Version 0.4.1: Slider controls automatically show Minimum, midpoint and Maximum beneath the bar and the current stepped Value at the right end. Signed ranges display a plus sign for positive values and zero at the middle of a symmetric range. Existing API definitions work unchanged.


## Side panels (0.5.0)

Set `BookDefinition.Placement` to `BookPlacement.Left` or `BookPlacement.Right` for a slim parchment panel on that side of the screen. `BookPlacement.Center` is the default, so existing mods keep their centered book. Consumers of this option must require OblivionUI **0.5.0** or newer.

```csharp
Books.Open(new BookDefinition {
    Placement = BookPlacement.Left, // Change to Right for the opposite side.
    Title = "Woodcutting",
    Subtitle = "Your work ledger",
    Pages = [new BookPage { Elements = [
        new BookElement {
            Id = "volume", Kind = BookControl.Slider,
            Label = "Sound volume", Minimum = 0, Maximum = 100, Step = 1, Value = 80,
            OnChange = e => SetWoodcuttingVolume(e.Value / 100)
        },
        new BookElement {
            Id = "done", Kind = BookControl.Button,
            Label = "Ready to work?", ButtonLabel = "Done",
            OnChange = e => e.Book.Dispose()
        }
    ]}]
});
```

Side paper is 500 by 940 design units, compared with the centered book's 760 by 940. It scales to fit the viewport with a 28-unit outer margin and a lighter background dim. The 416-unit scroll body provides 396-unit list controls and the existing themed scrollbar. Buttons, titles, subtitles, dropdowns, sliders with values, toggles, text inputs, paging, callbacks, and close behavior remain available.

`List` uses the narrower width. `TwoColumns` stacks into a single column in side panels to preserve readability. `Custom` retains its 630-unit horizontal coordinate space: X and Width scale proportionally into the narrower body; Y and Height remain unchanged. For very dense custom layouts, create a dedicated side-panel page with taller text rows. Panel placement is selected when opening; reopen with another placement to change it.

Side panels remain **modal**: one book/panel at a time, cursor enabled and movement/look blocked until closed. They are not persistent HUD overlays. Press **Alt+U** and use the demo's **Panel position** dropdown to compare all three placements.


Version 0.6.0 adds centered parchment dialogs (`Popups.Show`), menu images (`BookControl.Image`) and canvas shapes (`BookControl.Canvas`). See [POPUP-ART-API.md](POPUP-ART-API.md) for examples and supported drawing features.

## Extended toolkit (0.7.0)
See [TOOLKIT-API.md](TOOLKIT-API.md) for item menus, HUD panels, progress controls, tabs, tooltips, keybinds, slots, interactive drawing and themes.
# Inventory-style tabs (0.8.2)

Set `BookDefinition.ShowTabs = true` to place the icon strip above the page title. `BookPage.TabIcon` accepts `weapons`, `armour`, `spells`, `book`, `voice`, `settings`, or `items`; an empty value chooses from the page title. Use `TabImagePath` for your own cooked texture. Hovering a tab shows its page title. A/D browse categories; the strip follows the selected page. Both central and side panels show up to five categories per group. Tabs keep the original artwork proportions, stay the same size regardless of category count, and pack against the left. Narrow books scale both axes equally. Two categories show two tabs; the last group has no blank slots. The default skin keeps continuous parchment in the unused space to the right. With zero or one visible category, the strip is hidden and the title and content move up to use the full page. A book with only one page also hides the Previous/Next footer.

Set `ShowInTabs = false` on detail pages that should not appear in the strip. `ItemMenus.Open` creates tabs for categories automatically (up to 30 named categories plus All; All still includes every item). Item details return to the category that opened them.

In 0.8.4, tabs use the installed game inventory artwork. Press A/D to change categories; text entry keeps those keys for typing. Existing custom image paths override the default icons.

0.8.5 places individual tab pieces over a continuous sheet and blends their lower edge into the paper. Hidden detail pages do not count toward the visible tab count. No caller changes are required; existing Arena, Voice and toolkit menus use these rules automatically. The original textures are loaded from the installed game, not included in the mod.

0.8.8 also blends the first and last tab edges beside A/D into the default page artwork. Custom `PaperImagePath` overrides keep their own background. Tab widths, grouping, keyboard navigation and click areas are unchanged.
