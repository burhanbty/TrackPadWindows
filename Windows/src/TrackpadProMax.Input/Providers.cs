using System.ComponentModel;
using System.Runtime.InteropServices;
using TrackpadProMax.Core;

namespace TrackpadProMax.Input;
public sealed class SimulatedInputProvider : ITouchInputProvider
{
    public string Name=>"Simülasyon (fiziksel donanım değil)";
    public bool IsAvailable=>true;
    public event Action<TouchFrame>? Frame;
    private bool running;
    public void Start()=>running=true;
    public void Stop()=>running=false;
    public void Push(TouchFrame frame) { if(running) Frame?.Invoke(frame); }
    public void Dispose()=>Stop();
}
public sealed class GlobalHotkeys(nint window) : IDisposable
{
    private readonly Dictionary<int,Shortcut> registrations=new();
    public event Action<Shortcut>? Triggered;
    public IReadOnlyList<string> Configure(IEnumerable<Binding> bindings)
    {
        Dispose(); var errors=new List<string>(); int id=1;
        foreach(var text in bindings.Where(b=>b.Enabled && !string.IsNullOrWhiteSpace(b.Shortcut)).Select(b=>b.Shortcut).Distinct())
        {
            try
            {
                var key=Shortcut.Parse(text);
                if(registrations.Values.Contains(key)) continue;
                if(key.Modifiers==0 || key.Key==0x7B) { errors.Add(text+": Değiştirici tuş gerekli; F12 ayrılmıştır."); continue; }
                if(RegisterHotKey(window,id,key.Modifiers|0x4000,key.Key)) registrations[id++]=key;
                else errors.Add(text+": "+new Win32Exception(Marshal.GetLastWin32Error()).Message);
            }
            catch(Exception ex) { errors.Add(text+": "+ex.Message); }
        }
        return errors;
    }
    public void ProcessMessage(int message,nint wParam)
    { if(message==0x312 && registrations.TryGetValue((int)wParam,out var key)) Triggered?.Invoke(key); }
    public void Dispose() { foreach(var id in registrations.Keys) UnregisterHotKey(window,id); registrations.Clear(); }
    [DllImport("user32.dll",SetLastError=true)] private static extern bool RegisterHotKey(nint hWnd,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint hWnd,int id);
}

// Deliberately reports native API availability separately from data acquisition.
// The prerelease APIs do not provide an unrestricted background touch stream.
public sealed class NativeTouchpadProbe
{
    public IReadOnlyDictionary<string,bool> Exports { get; }
    public NativeTouchpadProbe()
    {
        string[] names=["RegisterTouchpadCapableWindow","RegisterTouchpadCapableThread","GetPointerTouchpadInfo","GetPointerFrameTouchpadInfo","GetPointerFrameTouchpadInfoHistory","GetPointerInfo","GetPointerDevices"];
        var result=new Dictionary<string,bool>();
        if(NativeLibrary.TryLoad("user32.dll",out var library))
        { try { foreach(var name in names) result[name]=NativeLibrary.TryGetExport(library,name,out _); } finally { NativeLibrary.Free(library); } }
        Exports=result;
    }
}
