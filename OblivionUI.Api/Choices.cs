namespace OblivionUI;
public sealed record DialogChoice {public required string Id {get;init;} public required string Label {get;init;} public bool Enabled {get;init;}=true; public Action? OnChoose {get;init;}}
public static class Dialogs
{
    public static BookHandle Choose(string title,string message,params DialogChoice[] choices)
    {
        if(choices is null||choices.Length is <1 or >8||choices.Any(c=>c is null||string.IsNullOrWhiteSpace(c.Id)||string.IsNullOrWhiteSpace(c.Label))||choices.Select(c=>c.Id).Distinct().Count()!=choices.Length)throw new ArgumentException("Supply 1-8 unique choices.");
        var rows=new List<BookElement>{new(){Id="dialog-body",Kind=BookControl.Text,Description=message,Height=100}};
        foreach(var c in choices)rows.Add(new(){Id="choice-"+c.Id,Kind=BookControl.Button,ButtonLabel=c.Label,Label="",Enabled=c.Enabled,Height=48,OnChange=e=>{e.Book.Dispose();c.OnChoose?.Invoke();}});
        return Books.Open(new(){Placement=BookPlacement.Popup,Title=title,CloseLabel="Cancel",Pages=[new(){Gap=4,Elements=rows.ToArray()}]});
    }
}
public sealed record SlotDropEvent(BookHandle Book,string SourceId,string TargetId,string Payload);

public static class SlotMoves
{
    public static bool Request(BookHandle book,string sourceId,string targetId)
    {
        ArgumentNullException.ThrowIfNull(book);
        lock(Hud.Gate){if(book.Closed||sourceId==targetId)return false;var elements=book.Definition.Pages[book.Page].Elements;var from=elements.FirstOrDefault(e=>e.Id==sourceId);var to=elements.FirstOrDefault(e=>e.Id==targetId);
            if(from is null||to is null||from.Kind!=BookControl.Slot||to.Kind!=BookControl.Slot||!from.Enabled||!to.Enabled||!from.Visible||!to.Visible||from.DragPayload.Length==0||to.DragPayload.Length!=0||from.DragGroup.Length==0||from.DragGroup!=to.DragGroup)return false;
            if(book.Definition.OnDrop?.Invoke(new(book,sourceId,targetId,from.DragPayload))!=true||book.Closed)return false;
            var after=book.Definition.Pages[book.Page].Elements;if(!after.Any(e=>e.Id==sourceId&&e.DragPayload==from.DragPayload)||!after.Any(e=>e.Id==targetId&&e.DragPayload.Length==0))return false;
            book.Update(sourceId,e=>e with{DragPayload=""});book.Update(targetId,e=>e with{DragPayload=from.DragPayload});return true;
        }
    }
}
