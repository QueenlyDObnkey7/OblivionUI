using System.Text.Json;
namespace OblivionUI;
public static class KeyBindings
{
    private static readonly Dictionary<string,string> Assigned=new(StringComparer.Ordinal);
    public static string? Conflict(string id,string key){lock(Hud.Gate)return Assigned.FirstOrDefault(p=>p.Key!=id&&p.Value.Equals(key,StringComparison.OrdinalIgnoreCase)).Key;}
    public static void Assign(string id,string key){if(string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(key)||key.Length>32)throw new ArgumentException("Invalid binding.");lock(Hud.Gate){if(Conflict(id,key) is {} owner)throw new ArgumentException($"Key already assigned to {owner}.");Assigned[id]=key;}}
    public static void Release(string id){lock(Hud.Gate)Assigned.Remove(id);}
    public static string Get(string id,string fallback="E"){lock(Hud.Gate)return Assigned.GetValueOrDefault(id,fallback);}
    public static void Save(string profile){lock(Hud.Gate)UiStorage.Write(profile+"-keys",JsonSerializer.Serialize(Assigned));}
    public static void Load(string profile){var loaded=JsonSerializer.Deserialize<Dictionary<string,string>>(UiStorage.Read(profile+"-keys"))??throw new ArgumentException("Invalid profile.");if(loaded.Count>256||loaded.Any(p=>string.IsNullOrWhiteSpace(p.Key)||string.IsNullOrWhiteSpace(p.Value)||p.Value.Length>32)||loaded.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=loaded.Count)throw new ArgumentException("Invalid or conflicting bindings.");lock(Hud.Gate){Assigned.Clear();foreach(var p in loaded)Assigned.Add(p.Key,p.Value);}}
}
internal static class UiStorage
{
    private static string PathFor(string name){if(name.Length is <1 or >80||name.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'&&c!='_'))throw new ArgumentException("Use a short alphanumeric profile name.");var dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OblivionUI","Profiles");Directory.CreateDirectory(dir);return Path.Combine(dir,name+".json");}
    internal static void Write(string name,string data){var path=PathFor(name);File.WriteAllText(path+".tmp",data);File.Move(path+".tmp",path,true);}
    internal static string Read(string name){var path=PathFor(name);if(new FileInfo(path).Length>1_000_000)throw new ArgumentException("Profile too large.");return File.ReadAllText(path);}
}
