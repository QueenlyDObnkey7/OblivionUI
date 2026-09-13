# Popups, images and canvas art - OblivionUI 0.6.0

Reference OblivionUI.Api.dll (Private=false) and require OblivionUI >= 0.6.0. Deploy the API/client DLL and pak/utoc/ucas together.

## Parchment popup

```csharp
var notice = Popups.Show(new PopupDefinition {
    Title = "Quest Updated",
    Heading = "Saving Time Itself",
    Subtitle = "Your journal has a new entry",
    Message = "The way is open. Return to the gatekeeper when you are ready.",
    ImagePath = "/Game/MyMod/UI/T_QuestIcon.T_QuestIcon", // Optional
    ImageAspectRatio = 1, // Original image width / height
    ButtonLabel = "Continue",
    OnContinue = () => Console.WriteLine("Player acknowledged the notice"),
    OnClose = () => Console.WriteLine("Popup closed")
});
// notice.Dispose(); closes it without invoking OnContinue.
```

A popup is a centered 760x410 parchment dialog that scales down to fit smaller viewports. The world is dimmed, mouse input is enabled and movement/look are blocked until it closes. Continue fires OnContinue, followed by OnClose. Replacing or disposing the popup only fires OnClose when the runtime processes the closure. A popup shares the single modal slot with books; opening it replaces an open menu. It has no auto-dismiss timer or queue; use Notifications.Show for existing timed slide-in notices. Long messages scroll in the popup; maximum message length is 4000 characters.

## Menu images

```csharp
new BookElement {
    Id = "portrait", Kind = BookControl.Image,
    ImagePath = "/Game/MyMod/UI/T_Portrait.T_Portrait",
    ImageAspectRatio = 512d / 768,
    Tint = new UiColor(1, 1, 1, 1),
    X = 20, Y = 40, Width = 160, Height = 240
}
```

Use a cooked Texture2D object path from a mounted mod pak. PNG/JPEG source images must first be imported as Unreal textures and cooked in that mod's assets. URLs and local file paths are not supported. Set ImageAspectRatio to source width/height: art fits inside its element without stretching. Tint uses normalized RGBA channels (0 to 1). Image controls are decorative; place a separate button beside them for actions. Missing assets cannot display and do not substitute external images. Image loading is synchronous when the page renders, so use small UI textures. The optional popup image uses the same loader.

## Freestyle canvas

```csharp
new BookElement {
    Id = "route", Kind = BookControl.Canvas,
    X = 180, Y = 40, Width = 420, Height = 220,
    Shapes = [
        new CanvasShape {
            Kind = CanvasShapeKind.Rectangle,
            Points = [new(0,0), new(420,220)],
            Color = new(.3f,.18f,.07f,.15f)
        },
        new CanvasShape {
            Kind = CanvasShapeKind.Polyline,
            Points = [new(20,180),new(80,90),new(160,140),new(280,40),new(390,80)],
            Color = new(.18f,.1f,.04f,1), Thickness = 4
        },
        new CanvasShape {
            Kind = CanvasShapeKind.Line,
            Points = [new(20,200),new(400,200)], Thickness = 2
        }
    ]
}
```

Rectangle takes two opposite corners and fills the area. Line takes two endpoints. Polyline joins any number of points with straight segments; repeat the first point to close an outline. Coordinates are local to the canvas element and must fit its Width/Height. Shapes draw in array order and are clipped at the canvas boundary. Thickness is 0.5 to 32 units. Limits: 128 shapes / 512 segments per canvas; 2048 segments across a book. Definitions and point arrays are copied; change artwork using `handle.Update("route", e => e with { Shapes = newShapes })`.

For free placement of artwork, labels and normal controls together, use BookLayout.Custom. Book coordinates remain 630 units wide. Side panels scale horizontal geometry into their narrower content area. Canvas art scales horizontally with its element. Text elements now expand their text area to the specified Height, so long descriptions can occupy tall custom blocks.

This is a programmatic drawing API for maps, dividers, diagrams and custom decoration. It does not capture mouse strokes, provide a paint editor or save drawings. Those would require a separate input API.

## Try the demos

Alt+U opens the book demo. On its first page, select Show quest update. Continue returns to the demo. Page 3 includes a packaged image and a sample canvas path. Choose left/right from the first page's Panel position selector to test custom artwork in slim menus too.
