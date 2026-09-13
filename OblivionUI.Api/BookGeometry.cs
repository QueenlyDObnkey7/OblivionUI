namespace OblivionUI;

// Shared layout rules keep the runtime and API checks in agreement. UMG side bodies are 416 units wide.
internal static class BookGeometry
{
    internal static bool HasTabs(BookDefinition book) => book.ShowTabs && book.Placement != BookPlacement.Popup && book.Pages.Count(p => p.ShowInTabs) > 1;
    internal const double TabAspect = (1024 * .168) / (256 * .44);
    internal static (int[] Pages, int Selected, int Start, int Count, double X, double Width, double Height) Tabs(BookDefinition book, int page)
    {
        var pages = book.Pages.Select((p, i) => (p, i)).Where(entry => entry.p.ShowInTabs).Select(entry => entry.i).ToArray();
        // Detail pages retain the preceding visible category's group without creating a blank tab.
        int selected = Array.FindLastIndex(pages, index => index <= page);
        selected = Math.Max(0, selected);
        int start = selected / 5 * 5, count = Math.Min(5, pages.Length - start);
        double margin = book.Placement is BookPlacement.Left or BookPlacement.Right ? 42 : 65;
        // Tab artwork keeps its native aspect and size, regardless of category count.
        // Narrow books scale the complete tab uniformly so five tabs can still fit.
        double width = Math.Min(80 * TabAspect, (ContentWidth(book.Placement) + 2 * margin - 130) / 5);
        return (pages, selected, start, count, 65 - margin, width, width / TabAspect);
    }
    internal static double ContentWidth(BookPlacement placement) => placement is BookPlacement.Left or BookPlacement.Right ? 416 : 630;
    internal static BookLayout Layout(BookPlacement placement, BookLayout layout) =>
        placement is (BookPlacement.Left or BookPlacement.Right) && layout == BookLayout.TwoColumns ? BookLayout.List : layout;
    internal static (double X, double Width) Row(BookPlacement placement, BookPage page, BookElement element, int index)
    {
        double contentWidth = ContentWidth(placement), rowWidth = contentWidth - 20;
        var layout = Layout(placement, page.Layout);
        if(layout == BookLayout.Custom) return (element.X * contentWidth / 630, element.Width * contentWidth / 630);
        if(layout == BookLayout.TwoColumns) {
            double width = (rowWidth - page.Gap) / 2;
            return ((index % 2) * (width + page.Gap), width);
        }
        return (0, rowWidth);
    }
}
