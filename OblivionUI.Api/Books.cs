namespace OblivionUI;

public enum BookControl { Heading, Text, Button, Slider, Toggle, Dropdown, TextInput, Image, Canvas, ItemCard, ProgressBar, ProgressRing, Section, Keybind, Slot, RecipeRow }
public enum BookLayout { List, TwoColumns, Custom }
public enum BookPlacement { Center, Left, Right, Popup }

/// <summary>Coordinates are in the book's 630-unit wide content area. Custom layouts use X/Y/Width/Height.</summary>
public sealed record BookElement
{
    public required string Id { get; init; }
    public BookControl Kind { get; init; }
    public string Label { get; init; } = "";
    public string Description { get; init; } = "";
    public string ButtonLabel { get; init; } = "Choose";
    public string Text { get; init; } = "";
    public double Value { get; init; }
    public double Minimum { get; init; }
    public double Maximum { get; init; } = 1;
    public double Step { get; init; } = .05;
    public bool Checked { get; init; }
    public string[] Options { get; init; } = [];
    public int SelectedIndex { get; init; }
    public bool Enabled { get; init; } = true;
    public bool Visible { get; init; } = true;
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; } = 610;
    public double Height { get; init; } = 128;
    public string Tooltip {get;init;}="";
    public string RecipePreview {get;init;}="";
    public string SectionId {get;init;}="";
    public string DefaultKey {get;init;}="E";
    public string DragGroup {get;init;}="";
    public string DragPayload {get;init;}="";
    public CanvasSession? Canvas {get;init;}
    public string ImagePath { get; init; } = "";
    public double ImageAspectRatio { get; init; } = 1;
    public UiColor Tint { get; init; } = new(1,1,1,1);
    public CanvasShape[] Shapes { get; init; } = [];
    public Action<BookEvent>? OnChange { get; init; }
}

public sealed record BookPage
{
    /// <summary>Optional tab artwork texture path. Empty uses the built-in icon inferred from TabIcon or Title.</summary>
    public string TabImagePath { get; init; } = "";
    /// <summary>weapons, armour, spells, book, voice, settings or items.</summary>
    public string TabIcon { get; init; } = "";
    public bool ShowInTabs { get; init; } = true;
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public BookLayout Layout { get; init; }
    public double Gap { get; init; } = 16;
    public BookElement[] Elements { get; init; } = [];
}

public sealed record BookDefinition
{
    /// <summary>Side placements use a narrower paper panel. They remain modal.</summary>
    public BookPlacement Placement { get; init; } = BookPlacement.Center;
    public string Title { get; init; } = "Ledger";
    public string Subtitle { get; init; } = "";
    public BookPage[] Pages { get; init; } = [];
    public bool ShowTabs {get;init;}
    public bool ShowPageNavigation {get;init;}=true;
    public UiTheme? Theme {get;init;}
    public Func<SlotDropEvent,bool>? OnDrop {get;init;}
    public string CloseLabel { get; init; } = "Close";
    internal Action? OnConfirm { get; init; }
    public Action? OnClose { get; init; }
}

public sealed record BookEvent(BookHandle Book, string Id, BookControl Kind, double Value, bool Checked, string Text, int SelectedIndex);

/// <summary>One modal book may be open at a time. Callbacks run on the game update thread.</summary>
public static class Books
{
    internal static BookHandle? Current;
    public static bool IsAvailable { get; internal set; }
    public static BookHandle Open(BookDefinition definition)
    {
        var copy=Validate(definition);
        lock(Hud.Gate) { Current?.Dispose(); return Current=new BookHandle(copy); }
    }
    internal static BookDefinition Validate(BookDefinition d)
    {
        ArgumentNullException.ThrowIfNull(d);
        if(d.Theme is not null)UiThemes.Validate(d.Theme);
        if(!Enum.IsDefined(d.Placement)) throw new ArgumentException("Invalid book placement.");
        if(d.Title is null || d.Subtitle is null || string.IsNullOrWhiteSpace(d.CloseLabel)) throw new ArgumentException("Book text cannot be null.");
        if(d.Pages is null || d.Pages.Length is <1 or >32) throw new ArgumentException("A book needs 1–32 pages.");
        if(d.Placement==BookPlacement.Popup && d.Pages.Length!=1)throw new ArgumentException("Popup books support one scrollable page.");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        var pages=d.Pages.Select(p=>{
            if(p is null || p.Title is null || p.Subtitle is null || p.TabIcon is null || p.TabImagePath is null || !Enum.IsDefined(p.Layout) || !double.IsFinite(p.Gap) || p.Gap is <0 or >100 || p.Elements is null || p.Elements.Length>128) throw new ArgumentException("Invalid page.");
            return p with { Elements=p.Elements.Select(e=>{
                if(e is null || string.IsNullOrWhiteSpace(e.Id) || e.Id.StartsWith("__",StringComparison.Ordinal) || !ids.Add(e.Id) || !Enum.IsDefined(e.Kind)) throw new ArgumentException("Control IDs must be unique throughout the book.");
                if(e.RecipePreview is null || e.RecipePreview.Length>4000)throw new ArgumentException("Recipe preview must be at most 4000 characters.");
                if(e.Tooltip is null || e.Tooltip.Length>600 || e.SectionId is null || e.DefaultKey is null || e.DragGroup is null || e.DragPayload is null)throw new ArgumentException("Control metadata cannot be null.");
                if(e.Kind is BookControl.ProgressBar or BookControl.ProgressRing && (e.Minimum>=e.Maximum||e.Value<e.Minimum||e.Value>e.Maximum))throw new ArgumentException("Invalid progress range.");
                if(e.Canvas is not null && (e.Canvas.Width!=e.Width||e.Canvas.Height!=e.Height||e.Kind!=BookControl.Canvas))throw new ArgumentException("Canvas session dimensions must match its element.");
                if(e.Label is null || e.Description is null || e.ButtonLabel is null || e.Text is null) throw new ArgumentException("Control text cannot be null.");
                if(new[]{e.X,e.Y,e.Width,e.Height,e.Value,e.Minimum,e.Maximum,e.Step}.Any(n=>!double.IsFinite(n)) || e.X<0 || e.Y<0 || e.Width<=0 || e.X+e.Width>630 || e.Height is <32 or >2000 || e.Y>20000) throw new ArgumentException("Invalid control geometry or value.");
                if(e.Kind==BookControl.Slider && (e.Minimum>=e.Maximum || e.Step<=0 || e.Value<e.Minimum || e.Value>e.Maximum || new[]{e.Minimum,e.Maximum,e.Step,e.Value}.Any(n=>!float.IsFinite((float)n)) || (float)e.Minimum>=(float)e.Maximum || (float)e.Step<=0)) throw new ArgumentException("Invalid slider range.");
                if(e.Options is null || e.Options.Length>128 || e.Options.Any(x=>x is null)) throw new ArgumentException("Invalid dropdown options.");
                if(e.Kind==BookControl.Dropdown && (e.Options.Length==0 || e.SelectedIndex<0 || e.SelectedIndex>=e.Options.Length)) throw new ArgumentException("Dropdown selection must name an option.");
                return e with { Options=[..e.Options], Shapes=CanvasArt.Validate(e) };
            }).ToArray() };
        }).ToArray();
        if(pages.SelectMany(p=>p.Elements).Sum(e=>e.Shapes.Sum(s=>s.Points.Length-1))>2048)throw new ArgumentException("A book supports at most 2048 drawing segments.");
        foreach(var p in pages)foreach(var e in p.Elements)if(e.SectionId.Length>0&&!p.Elements.Any(h=>h.Id==e.SectionId&&h.Kind==BookControl.Section))throw new ArgumentException("Unknown section header.");
        return d with { Pages=pages };
    }
}

public sealed class BookHandle : IDisposable
{
    internal BookDefinition Definition;
    internal int Page, Revision;
    internal bool Closed;
    internal BookHandle(BookDefinition definition) { Definition=definition; }
    public bool IsOpen { get { lock(Hud.Gate) return !Closed; } }
    public void GoToPage(int index)
    {
        lock(Hud.Gate) { if(Closed) return; if(index<0 || index>=Definition.Pages.Length) throw new ArgumentOutOfRangeException(nameof(index)); Page=index; Revision++; }
    }
    /// <summary>Replaces a control definition and refreshes the page. IDs cannot be renamed.</summary>
    public void Update(string id, Func<BookElement,BookElement> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock(Hud.Gate) {
            if(Closed) return;
            bool found=false;
            var pages=Definition.Pages.Select(p=>p with { Elements=p.Elements.Select(e=>{
                if(e.Id!=id) return e; found=true; var updated=change(e with {Options=[..e.Options],Shapes=e.Shapes.Select(s=>s with {Points=[..s.Points]}).ToArray()});
                if(updated is null || updated.Id!=id) throw new ArgumentException("Update must preserve the ID."); return updated;
            }).ToArray() }).ToArray();
            if(!found) throw new KeyNotFoundException(id);
            Definition=Books.Validate(Definition with {Pages=pages}); Revision++;
        }
    }
    public void ReplacePages(params BookPage[] pages){lock(Hud.Gate){if(Closed)return;Definition=Books.Validate(Definition with{Pages=pages});Page=Math.Min(Page,pages.Length-1);Revision++;}}
    public void Dispose() { lock(Hud.Gate) Closed=true; }
}
