namespace OblivionUI.Client;

internal static partial class BookRuntime
{
    // Owned by a rendered Row: rebuilding the page discards every cached UObject.
    private sealed class RowFrameState
    {
        internal bool Focused, Hovered;
        internal nint Highlight;
        internal bool? Highlighted;
        internal float? Percent;
    }

    private static readonly HashSet<int> PressedKeys=[];
    private static readonly int[] NavigationKeys=[65,68];
    private static readonly int[] CanvasKeys=[187,107,189,109];
    private static bool _hasSlotControls;

    private static void UpdateRowHighlights(UnrealBookBridge u)
    {
        foreach(var row in Rows)
        {
            var e=row.Element; var state=row.Frame;
            // Tab art already communicates selection and decorative rows cannot take input.
            if(e.Id.StartsWith("__tab",StringComparison.Ordinal))continue;
            bool interactive=row.Control!=0 && e.Enabled && e.Kind!=BookControl.ProgressBar;
            state.Focused=interactive && u.Boolean(row.Control,"HasKeyboardFocus");
            // Read hover as well for recipe previews, where it must win over keyboard focus.
            state.Hovered=interactive && (!state.Focused || e.RecipePreview.Length>0) && u.Boolean(row.Control,"IsHovered");
            bool highlighted=state.Focused || state.Hovered ||
                (e.Kind==BookControl.ItemCard ? e.Checked : e.Id==_slotSelected);
            if(state.Highlighted==highlighted)continue;
            if(state.Highlight==0)state.Highlight=u.Object(row.Widget,e.Kind switch
            {
                BookControl.RecipeRow=>"Ink",BookControl.ItemCard=>"CardPaper",_=>"FocusRing"
            });
            if(e.Kind is BookControl.RecipeRow or BookControl.ItemCard)
                u.Call(state.Highlight,"SetRenderOpacity",("InOpacity",highlighted?1f:e.Kind==BookControl.RecipeRow?.5f:.72f));
            else u.Call(state.Highlight,"SetVisibility",("InVisibility",highlighted?(byte)3:(byte)1));
            state.Highlighted=highlighted;
        }
    }

    private static void ReadPressedKey(int key)
    {
        bool down=Down(key);if(down&&!Keys[key])PressedKeys.Add(key);Keys[key]=down;
    }
}
