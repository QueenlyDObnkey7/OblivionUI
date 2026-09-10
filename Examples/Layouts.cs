using OblivionUI;
namespace OblivionUIExamples;
public static class Layouts
{
    public static BookHandle Open() => Books.Open(new BookDefinition
    {
        Title="Layout examples",
        Pages=[
            new() { Title="Two columns", Layout=BookLayout.TwoColumns, Gap=16, Elements=[
                new() { Id="left", Kind=BookControl.Text, Label="Supplies", Description="Left column", Height=100 },
                new() { Id="right", Kind=BookControl.Text, Label="Equipment", Description="Right column", Height=100 }
            ]},
            new() { Title="Custom positions", Layout=BookLayout.Custom, Elements=[
                new() { Id="custom-title", Kind=BookControl.Heading, Label="A custom page", X=0,Y=0,Width=610,Height=80 },
                new() { Id="custom-button", Kind=BookControl.Button, Label="Return to overview", ButtonLabel="Overview", X=0,Y=100,Width=300,Height=128, OnChange=e=>e.Book.GoToPage(0) }
            ]}
        ]
    });
}
