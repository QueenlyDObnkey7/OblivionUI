using OblivionUI;
namespace OblivionUIExamples;

// UI example only. The consuming mod supplies device discovery, audio and persistence.
public sealed record Settings(string Name, double GainDb, bool Enabled, string InputDevice, int TransmitMode);
public static class SettingsBook
{
    public static BookHandle Open(string[] inputDevices, Action<Settings> save)
    {
        string[] devices = inputDevices.Length == 0 ? ["System default"] : [..inputDevices];
        string name = "My configuration";
        double gain = 0;
        bool enabled = true;
        int device = 0, mode = 0;
        return Books.Open(new BookDefinition
        {
            Title = "Voice settings",
            Subtitle = "Configure your microphone",
            Pages = [new BookPage
            {
                Title = "Input", Layout = BookLayout.List,
                Elements = [
                    new() { Id="intro", Kind=BookControl.Heading, Label="Your voice", Height=80 },
                    new() { Id="hint", Kind=BookControl.Text, Label="Choose your settings", Description="Press Save to apply them.", Height=100 },
                    new() { Id="name", Kind=BookControl.TextInput, Label="Preset name", Text=name, OnChange=e=>name=e.Text },
                    new() { Id="device", Kind=BookControl.Dropdown, Label="Input device", Options=devices, SelectedIndex=0, OnChange=e=>device=e.SelectedIndex },
                    new() { Id="gain", Kind=BookControl.Slider, Label="Gain (dB)", Description="The middle is 0 dB.", Minimum=-24, Maximum=24, Step=1, Value=0, OnChange=e=>gain=e.Value },
                    new() { Id="enabled", Kind=BookControl.Toggle, Label="Enable voice", Checked=true, OnChange=e=>enabled=e.Checked },
                    new() { Id="mode", Kind=BookControl.Dropdown, Label="Transmit mode", Options=["Push to talk", "Toggle"], SelectedIndex=0, OnChange=e=>mode=e.SelectedIndex },
                    new() { Id="save", Kind=BookControl.Button, Label="Save configuration", ButtonLabel="Save", OnChange=e=> {
                        save(new Settings(name,gain,enabled,devices[device],mode));
                        Notifications.Show("Settings saved", "Your voice preferences have been applied.");
                        e.Book.Dispose();
                    }}
                ]
            }]
        });
    }
}
