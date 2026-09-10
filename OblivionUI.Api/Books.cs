namespace OblivionUI;

public enum BookControl { Heading, Text, Button, Slider, Toggle, Dropdown, TextInput }
public enum BookLayout { List, TwoColumns, Custom }

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
    public Action<BookEvent>? OnChange { get; init; }
}

public sealed record BookPage
{
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public BookLayout Layout { get; init; }
    public double Gap { get; init; } = 16;
    public BookElement[] Elements { get; init; } = [];
}

public sealed record BookDefinition
{
    public string Title { get; init; } = "Ledger";
    public string Subtitle { get; init; } = "";
    public BookPage[] Pages { get; init; } = [];
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
        if(d.Title is null || d.Subtitle is null) throw new ArgumentException("Book text cannot be null.");
        if(d.Pages is null || d.Pages.Length is <1 or >32) throw new ArgumentException("A book needs 1–32 pages.");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        var pages=d.Pages.Select(p=>{
            if(p is null || p.Title is null || p.Subtitle is null || !Enum.IsDefined(p.Layout) || !double.IsFinite(p.Gap) || p.Gap is <0 or >100 || p.Elements is null || p.Elements.Length>128) throw new ArgumentException("Invalid page.");
            return p with { Elements=p.Elements.Select(e=>{
                if(e is null || string.IsNullOrWhiteSpace(e.Id) || !ids.Add(e.Id) || !Enum.IsDefined(e.Kind)) throw new ArgumentException("Control IDs must be unique throughout the book.");
                if(e.Label is null || e.Description is null || e.ButtonLabel is null || e.Text is null) throw new ArgumentException("Control text cannot be null.");
                if(new[]{e.X,e.Y,e.Width,e.Height,e.Value,e.Minimum,e.Maximum,e.Step}.Any(n=>!double.IsFinite(n)) || e.X<0 || e.Y<0 || e.Width<=0 || e.X+e.Width>630 || e.Height is <32 or >2000 || e.Y>20000) throw new ArgumentException("Invalid control geometry or value.");
                if(e.Kind==BookControl.Slider && (e.Minimum>=e.Maximum || e.Step<=0 || e.Value<e.Minimum || e.Value>e.Maximum || new[]{e.Minimum,e.Maximum,e.Step,e.Value}.Any(n=>!float.IsFinite((float)n)) || (float)e.Minimum>=(float)e.Maximum || (float)e.Step<=0)) throw new ArgumentException("Invalid slider range.");
                if(e.Options is null || e.Options.Length>128 || e.Options.Any(x=>x is null)) throw new ArgumentException("Invalid dropdown options.");
                if(e.Kind==BookControl.Dropdown && (e.Options.Length==0 || e.SelectedIndex<0 || e.SelectedIndex>=e.Options.Length)) throw new ArgumentException("Dropdown selection must name an option.");
                return e with { Options=[..e.Options] };
            }).ToArray() };
        }).ToArray();
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
                if(e.Id!=id) return e; found=true; var updated=change(e with {Options=[..e.Options]});
                if(updated is null || updated.Id!=id) throw new ArgumentException("Update must preserve the ID."); return updated;
            }).ToArray() }).ToArray();
            if(!found) throw new KeyNotFoundException(id);
            Definition=Books.Validate(Definition with {Pages=pages}); Revision++;
        }
    }
    public void Dispose() { lock(Hud.Gate) Closed=true; }
}
