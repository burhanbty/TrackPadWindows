using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrackpadProMax.Core;
public sealed class ActionSpec
{
    public string Kind { get; set; } = "wheel";
    public string Value { get; set; } = "";
}
public sealed class Binding
{
    public InputRoute Input { get; set; } = InputRoute.Automatic;
    [JsonIgnore] public bool UsesShortcut => Input != InputRoute.Native && !string.IsNullOrWhiteSpace(Shortcut);
    [JsonIgnore] public bool UsesNative => Input == InputRoute.Native || (Input == InputRoute.Automatic && string.IsNullOrWhiteSpace(Shortcut));
    public string Name { get; set; } = "Yeni hareket";
    public string Shortcut { get; set; } = "Ctrl+Alt+F8";
    public GestureKind Gesture { get; set; } = GestureKind.Tap;
    public int Fingers { get; set; } = 3;
    public Zone Zone { get; set; } = Zone.Anywhere;
    public bool Enabled { get; set; } = true;
    public string Mode { get; set; } = "*";
    public ActionSpec Action { get; set; } = new();
}
public sealed class AppProfile
{
    public string Name { get; set; } = "Yeni profil";
    public string ProcessName { get; set; } = "app";
    public List<Binding> Bindings { get; set; } = [];
}
public sealed class MenuEntry
{
    public string Label { get; set; } = "Yeni öğe";
    public ActionSpec Action { get; set; } = new();
    public List<MenuEntry> Children { get; set; } = [];
}
public sealed class Configuration
{
    public AppearanceSettings Appearance { get; set; } = new();
    public int Version { get; set; } = 1;
    public bool MinimizeToTray { get; set; } = true;
    public string ActiveMode { get; set; } = "Normal";
    public GestureSettings Gestures { get; set; } = new();
    public List<Binding> Bindings { get; set; } = [];
    public List<AppProfile> Profiles { get; set; } = [];
    public List<MenuEntry> Menu { get; set; } = [];
    public static MenuEntry Entry(string label,string kind,string value="") => new(){Label=label,Action=new(){Kind=kind,Value=value}};
    public static Configuration Default() => new()
    {
        Bindings=[new(){Name="Mod çarkı",Shortcut="Ctrl+Alt+F8"},new(){Name="Oynat / duraklat",Shortcut="Ctrl+Alt+P",Gesture=GestureKind.SwipeUp,Action=new(){Kind="media",Value="play"}}],
        Profiles=[new(){Name="Chrome",ProcessName="chrome",Bindings=[new(){Name="Yeni sekme",Shortcut="Ctrl+Alt+T",Action=new(){Kind="keys",Value="Ctrl+T"}},new(){Name="Sekmeyi kapat",Shortcut="Ctrl+Alt+W",Action=new(){Kind="keys",Value="Ctrl+W"}}]},
                  new(){Name="VS Code",ProcessName="Code",Bindings=[new(){Name="Komut paleti",Shortcut="Ctrl+Alt+T",Action=new(){Kind="keys",Value="Ctrl+Shift+P"}}]},
                  new(){Name="Spotify",ProcessName="Spotify",Bindings=[new(){Name="Sonraki parça",Shortcut="Ctrl+Alt+T",Action=new(){Kind="media",Value="next"}}]}],
        Menu=[new(){Label="Normal",Action=new(){Kind="mode",Value="Normal"},Children=[Entry("Modu seç","mode","Normal"),Entry("Masaüstü","keys","Win+D"),Entry("Ekran alıntısı","keys","Win+Shift+S")]},
              new(){Label="Medya",Action=new(){Kind="mode",Value="Media"},Children=[Entry("Modu seç","mode","Media"),Entry("Oynat / duraklat","media","play"),Entry("Sonraki","media","next"),Entry("Önceki","media","previous"),Entry("Ses +","media","up"),Entry("Ses −","media","down"),Entry("Sessiz","media","mute")]},
              new(){Label="Kontrol",Action=new(){Kind="mode",Value="Control"},Children=[Entry("Modu seç","mode","Control"),Entry("Görev Yöneticisi","taskmanager"),Entry("Ekranı kilitle","lock"),Entry("Parlaklık +","brightness","10"),Entry("Parlaklık −","brightness","-10")]},
              new(){Label="Pencereler",Action=new(){Kind="mode",Value="Windows"},Children=[Entry("Modu seç","mode","Windows"),Entry("Sol yarı","window","left"),Entry("Sağ yarı","window","right"),Entry("Büyüt","window","maximize"),Entry("Geri yükle","window","restore"),Entry("Küçült","window","minimize"),Entry("2 pencere","window","tile2"),Entry("3 pencere","window","tile3"),Entry("4 pencere","window","tile4"),Entry("Diğer monitör","keys","Win+Shift+Right")]},
              new(){Label="Uygulamalar",Action=new(){Kind="mode",Value="Applications"},Children=[Entry("Modu seç","mode","Applications"),Entry("Not Defteri","launch","notepad.exe"),Entry("Hesap Makinesi","launch","calc.exe")]},
              new(){Label="Hızlı Ayarlar",Action=new(){Kind="mode",Value="Quick Settings"},Children=[Entry("Modu seç","mode","Quick Settings"),Entry("Hızlı ayarlar","keys","Win+A"),Entry("Ayarlar","settings"),Entry("Dokunmatik yüzey","touchpadsettings")]}]
    };
    public Binding? Resolve(string process, Func<Binding,bool> match)
    {
        bool Eligible(Binding b)=>b.Enabled && (b.Mode=="*" || b.Mode.Equals(ActiveMode,StringComparison.OrdinalIgnoreCase)) && match(b);
        return Profiles.FirstOrDefault(p=>p.ProcessName.Equals(Path.GetFileNameWithoutExtension(process),StringComparison.OrdinalIgnoreCase))?.Bindings.FirstOrDefault(Eligible)
            ?? Bindings.FirstOrDefault(Eligible);
    }
    [JsonIgnore] public IEnumerable<Binding> AllBindings => Bindings.Concat(Profiles.SelectMany(p=>p.Bindings));
    public void Validate()
    {
        if(Gestures is null || Bindings is null || Profiles is null || Menu is null || ActiveMode is null) throw new ArgumentException("Yapılandırma alanları null olamaz.");
        if(Version!=1) throw new ArgumentException("Desteklenmeyen yapılandırma sürümü.");
        Gestures.Validate();
        if(Appearance is null) throw new ArgumentException("Görünüm ayarları boş olamaz.");
        Appearance.Validate();
        if(Menu.Count==0 || Menu.Count>12) throw new ArgumentException("Ana menü 1–12 öğe içermeli.");
        void ValidateBindings(IEnumerable<Binding> bindings)
        {
            var keys=new List<(string Mode,string Key)>();
            foreach(var b in bindings)
            {
                if(b is null || b.Mode is null || b.Action is null) throw new ArgumentException("Eşleme alanları null olamaz.");
                if(!Enum.IsDefined(b.Input) || (b.Input==InputRoute.Shortcut && string.IsNullOrWhiteSpace(b.Shortcut))) throw new ArgumentException("Kısayol yolu için bir kısayol gerekli.");
                if(b.Fingers<1 || b.Fingers>4 || !Enum.IsDefined(b.Gesture) || !Enum.IsDefined(b.Zone)) throw new ArgumentException("Geçersiz hareket.");
                if(!string.IsNullOrWhiteSpace(b.Shortcut))
                {
                    var normalized=Shortcut.Parse(b.Shortcut).Canonical;
                    if(b.Enabled && b.UsesShortcut)
                    {
                        if(keys.Any(k=>k.Key==normalized && (k.Mode=="*" || b.Mode=="*" || k.Mode.Equals(b.Mode,StringComparison.OrdinalIgnoreCase)))) throw new ArgumentException("Çakışan kısayol: "+b.Shortcut);
                        keys.Add((b.Mode,normalized));
                    }
                }
                ValidateAction(b.Action);
            }
        }
        ValidateBindings(Bindings);
        var processes=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var p in Profiles) { if(p is null || p.Bindings is null || string.IsNullOrWhiteSpace(p.ProcessName) || !processes.Add(p.ProcessName)) throw new ArgumentException("Profil işlem adları benzersiz olmalı; eşlemeler null olamaz."); ValidateBindings(p.Bindings); }
        void Walk(IEnumerable<MenuEntry> entries,int depth)
        {
            if(depth>5) throw new ArgumentException("Menü en fazla 5 katman olabilir.");
            foreach(var e in entries) { if(e is null || e.Children is null || e.Action is null || string.IsNullOrWhiteSpace(e.Label))throw new ArgumentException("Menü adı ve alanları boş olamaz.");ValidateAction(e.Action); if(e.Children.Count>12) throw new ArgumentException("Alt menü en fazla 12 öğe içerebilir."); Walk(e.Children,depth+1); }
        }
        Walk(Menu,0);
    }
    public static void ValidateAction(ActionSpec a)
    {
        if(a is null || a.Kind is null || a.Value is null)throw new ArgumentException("Eylem alanları null olamaz.");
        string[] kinds=["wheel","mode","keys","media","window","launch","url","lock","settings","touchpadsettings","taskmanager","brightness"];
        if(!kinds.Contains(a.Kind)) throw new ArgumentException("Bilinmeyen eylem: "+a.Kind);
        if(a.Kind=="keys") Shortcut.Parse(a.Value);
        if(a.Kind=="media" && !new[]{"play","next","previous","up","down","mute"}.Contains(a.Value))throw new ArgumentException("Bilinmeyen medya eylemi.");
        if(a.Kind=="window" && !new[]{"left","right","maximize","restore","minimize","tile2","tile3","tile4"}.Contains(a.Value))throw new ArgumentException("Bilinmeyen pencere eylemi.");
        if(a.Kind=="brightness" && (!int.TryParse(a.Value,out int brightness) || brightness < -100 || brightness > 100))throw new ArgumentException("Parlaklık farkı -100 ile 100 arasında olmalı.");
        if(a.Kind=="url" && (!Uri.TryCreate(a.Value,UriKind.Absolute,out var uri) || (uri.Scheme!="https" && uri.Scheme!="http"))) throw new ArgumentException("Yalnızca http/https adresleri desteklenir.");
        if(a.Kind=="launch" && (string.IsNullOrWhiteSpace(a.Value) || !a.Value.EndsWith(".exe",StringComparison.OrdinalIgnoreCase) || a.Value.Contains('"'))) throw new ArgumentException("Bir .exe yolu belirtin; bağımsız değişken desteklenmez.");
    }
}
public static class ConfigurationStore
{
    public static readonly JsonSerializerOptions Options=new(){WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,Converters={new JsonStringEnumConverter()}};
    public static string Serialize(Configuration c)=>JsonSerializer.Serialize(c,Options);
    public static Configuration Deserialize(string text) { var c=JsonSerializer.Deserialize<Configuration>(text,Options) ?? throw new ArgumentException("Boş yapılandırma."); c.Validate(); return c; }
    public static void Save(string path,Configuration c)
    {
        c.Validate(); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try { File.WriteAllText(temp,Serialize(c)); if(File.Exists(path)) File.Copy(path,path+".bak",true); File.Move(temp,path,true); }
        finally { if(File.Exists(temp)) File.Delete(temp); }
    }
}
public readonly record struct Shortcut(uint Modifiers,uint Key)
{
    public string Canonical=>$"{Modifiers}:{Key}";
    public static Shortcut Parse(string text)
    {
        uint mods=0,key=0;
        foreach(var piece in text.Split('+',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries))
        {
            switch(piece.ToLowerInvariant()) { case "ctrl": mods|=2;continue;case "alt":mods|=1;continue;case "shift":mods|=4;continue;case "win":mods|=8;continue; }
            if(key!=0) throw new ArgumentException("Kısayolda tek ana tuş olmalı.");
            var p=piece.ToUpperInvariant();
            key=p switch { "LEFT"=>0x25,"UP"=>0x26,"RIGHT"=>0x27,"DOWN"=>0x28,"SPACE"=>0x20,"TAB"=>9,"ENTER"=>13,"ESC"=>27,"DELETE"=>46,_=>0 };
            if(key==0 && p.Length==1 && char.IsAsciiLetterOrDigit(p[0])) key=p[0];
            if(key==0 && p.StartsWith('F') && int.TryParse(p[1..],out int f) && f>=1 && f<=24) key=(uint)(0x70+f-1);
            if(key==0) throw new ArgumentException("Bilinmeyen tuş: "+piece);
        }
        if(key==0) throw new ArgumentException("Ana tuş eksik.");
        return new(mods,key);
    }
}
public sealed record ActionResult(bool Success,string Message)
{
    public static ActionResult Ok(string message="Tamamlandı")=>new(true,message);
    public static ActionResult Fail(string message)=>new(false,message);
}
public interface IActionExecutor { Task<ActionResult> ExecuteAsync(ActionSpec action,nint target); }
public sealed class ActionDispatcher(IActionExecutor executor)
{
    public async Task<ActionResult> DispatchAsync(Configuration configuration,string process,Func<Binding,bool> predicate,nint target)
    {
        var binding=configuration.Resolve(process,predicate);
        if(binding is null) return ActionResult.Fail("Bu uygulama ve mod için etkin eşleme yok.");
        try { return await executor.ExecuteAsync(binding.Action,target); }
        catch(Exception ex) { return ActionResult.Fail(ex.Message); }
    }
}
