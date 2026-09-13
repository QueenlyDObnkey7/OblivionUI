namespace OblivionUI;

public sealed record PopupDefinition
{
    public string Title { get; init; } = "Journal Updated";
    public string Heading { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string Message { get; init; } = "";
    public string ImagePath { get; init; } = "";
    public double ImageAspectRatio { get; init; } = 1;
    public string ButtonLabel { get; init; } = "Continue";
    public Action? OnContinue { get; init; }
    public Action? OnClose { get; init; }
}
/// <summary>A centered parchment dialog. Shares the modal book slot; replaces any open book.</summary>
public static class Popups
{
    public static BookHandle Show(PopupDefinition popup)
    {
        ArgumentNullException.ThrowIfNull(popup);
        if(popup.Heading is null || popup.Subtitle is null || popup.Message is null || popup.ImagePath is null || popup.Message.Length>4000)throw new ArgumentException("Invalid popup text (message limit 4000).");
        var rows=new List<BookElement>();
        bool image=popup.ImagePath.Length>0;
        if(image)rows.Add(new(){Id="popup-image",Kind=BookControl.Image,ImagePath=popup.ImagePath,ImageAspectRatio=popup.ImageAspectRatio,Width=80,Height=80});
        rows.Add(new(){Id="popup-heading",Kind=BookControl.Heading,Label=popup.Heading,Description=popup.Subtitle,X=image?100:0,Width=image?510:610,Height=76});
        int lines=popup.Message.Split('\n').Sum(line=>Math.Max(1,(line.Length+65)/66));
        rows.Add(new(){Id="popup-message",Kind=BookControl.Text,Description=popup.Message,Y=72,Width=610,Height=Math.Clamp(34+lines*24,64,1800)});
        return Books.Open(new(){Placement=BookPlacement.Popup,Title=popup.Title,CloseLabel=popup.ButtonLabel,OnConfirm=popup.OnContinue,OnClose=popup.OnClose,Pages=[new(){Layout=BookLayout.Custom,Elements=rows.ToArray()}]});
    }
}
