using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using TrackpadProMax.Core;
using Windows.Media.Control;

namespace TrackpadProMax.Actions;
public sealed class WindowsActions : IActionExecutor
{
    public event Action? WheelRequested;
    public event Action<string>? ModeRequested;
    public static nint ForegroundWindow=>Win32.GetForegroundWindow();
    public static string ProcessName(nint window)
    {
        try { Win32.GetWindowThreadProcessId(window,out var pid); using var p=Process.GetProcessById((int)pid); return p.ProcessName; } catch { return ""; }
    }
    public static bool IsExternalWindow(nint hwnd)=>hwnd!=0 && Win32.IsWindow(hwnd) && !ProcessName(hwnd).Equals(Process.GetCurrentProcess().ProcessName,StringComparison.OrdinalIgnoreCase);
    public async Task<ActionResult> ExecuteAsync(ActionSpec action,nint target)
    {
        try
        {
            Configuration.ValidateAction(action);
            switch(action.Kind)
            {
                case "wheel": WheelRequested?.Invoke(); break;
                case "mode": ModeRequested?.Invoke(action.Value); break;
                case "keys":
                    if(!IsExternalWindow(target) || !Win32.SetForegroundWindow(target)) return ActionResult.Fail("Hedef pencere etkinleştirilemedi.");
                    await Task.Delay(70);
                    if(Win32.GetForegroundWindow()!=target) return ActionResult.Fail("Hedef pencere odağı değişti; kısayol iptal edildi.");
                    SendKeys(Shortcut.Parse(action.Value)); break;
                case "media":
                    if(action.Value is "play" or "next" or "previous")
                    {
                        try
                        {
                            var manager=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();var session=manager.GetCurrentSession();
                            if(session is not null)
                            {
                                bool accepted=action.Value=="play"?await session.TryTogglePlayPauseAsync():action.Value=="next"?await session.TrySkipNextAsync():await session.TrySkipPreviousAsync();
                                if(accepted)return ActionResult.Ok("Windows medya oturumu eylemi kabul etti.");
                            }
                        }catch { /* Unsupported session: fall back to the documented virtual media key. */ }
                    }
                    var key=action.Value switch {"play"=>0xB3,"next"=>0xB0,"previous"=>0xB1,"up"=>0xAF,"down"=>0xAE,"mute"=>0xAD,_=>throw new ArgumentException("Bilinmeyen medya eylemi.")};
                    SendKeys(new(0,(uint)key)); break;
                case "window": return ManageWindow(action.Value,target);
                case "launch": Start(action.Value); break;
                case "url": Start(action.Value); break;
                case "settings": Start("ms-settings:"); break;
                case "touchpadsettings": Start("ms-settings:devices-touchpad"); break;
                case "taskmanager": Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"Taskmgr.exe")); break;
                case "lock": if(!Win32.LockWorkStation()) throw new Win32Exception(); break;
                case "brightness": return await BrightnessAsync(int.Parse(action.Value,System.Globalization.CultureInfo.InvariantCulture));
                default: return ActionResult.Fail("Eylem desteklenmiyor.");
            }
            return ActionResult.Ok();
        }
        catch(Exception ex) { return ActionResult.Fail(ex.Message); }
    }
    private static void Start(string path)=>Process.Start(new ProcessStartInfo(path){UseShellExecute=true});
    private static void SendKeys(Shortcut shortcut)
    {
        int[] modifierKeys=[0x11,0x12,0x10,0x5B]; uint[] masks=[2,1,4,8];
        // The UI host waits asynchronously for activation modifiers to lift first.
        // No keyboard hook, typed-text collection or password capture is used.
        var inputs=new List<Win32.Input>();
        void Add(uint key,bool up)=>inputs.Add(new(){Type=1,Data=new(){Keyboard=new(){Key=(ushort)key,Flags=up?2u:0u}}});
        for(int i=0;i<4;i++) if((shortcut.Modifiers&masks[i])!=0) Add((uint)modifierKeys[i],false);
        Add(shortcut.Key,false); Add(shortcut.Key,true);
        for(int i=3;i>=0;i--) if((shortcut.Modifiers&masks[i])!=0) Add((uint)modifierKeys[i],true);
        if(Win32.SendInput((uint)inputs.Count,inputs.ToArray(),Marshal.SizeOf<Win32.Input>())!=inputs.Count)
            throw new InvalidOperationException("Windows girişi kabul etmedi (yükseltilmiş hedef/UIPI olabilir).");
    }
    public static bool ActivationModifiersDown()=>new[]{0x11,0x12,0x10,0x5B,0x5C}.Any(k=>(Win32.GetAsyncKeyState(k)&0x8000)!=0);
    private static ActionResult ManageWindow(string operation,nint target)
    {
        if(!IsExternalWindow(target)) return ActionResult.Fail("Önce yönetilecek bir uygulama penceresi seçin, ardından genel kısayolu kullanın.");
        if(operation is "maximize" or "restore" or "minimize") { Win32.ShowWindow(target,operation=="maximize"?3:operation=="restore"?9:6); return ActionResult.Ok(); }
        var monitor=Win32.MonitorFromWindow(target,2);
        var info=new Win32.MonitorInfo{Size=Marshal.SizeOf<Win32.MonitorInfo>()};
        if(!Win32.GetMonitorInfo(monitor,ref info)) throw new Win32Exception();
        var work=info.Work; int width=work.Right-work.Left,height=work.Bottom-work.Top;
        void Place(nint hwnd,int x,int y,int w,int h)
        {
            Win32.ShowWindow(hwnd,9);
            if(!Win32.SetWindowPos(hwnd,0,x,y,w,h,0x0014)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        if(operation=="left" || operation=="right") { int half=width/2; Place(target,work.Left+(operation=="right"?half:0),work.Top,operation=="right"?width-half:half,height); return ActionResult.Ok(); }
        if(!operation.StartsWith("tile") || !int.TryParse(operation[4..],out int count) || count<2 || count>4) return ActionResult.Fail("Geçersiz pencere işlemi.");
        var windows=new List<nint>{target};
        Win32.EnumWindows((hwnd,_)=>
        {
            if(hwnd==target || !IsExternalWindow(hwnd) || !Win32.IsWindowVisible(hwnd) || Win32.IsIconic(hwnd) || Win32.GetWindow(hwnd,4)!=0 || Win32.MonitorFromWindow(hwnd,2)!=monitor) return true;
            if(Win32.DwmGetWindowAttribute(hwnd,14,out int cloaked,4)==0 && cloaked!=0) return true;
            var name=new StringBuilder(256); Win32.GetWindowText(hwnd,name,256); if(name.Length>0) windows.Add(hwnd);
            return windows.Count<count;
        },0);
        if(windows.Count<count) return ActionResult.Fail($"Aynı monitörde {count} uygun açık pencere gerekli; {windows.Count} bulundu.");
        for(int i=0;i<count;i++)
        {
            int half=width/2,topHalf=height/2;
            if(count==2) Place(windows[i],work.Left+i*half,work.Top,i==1?width-half:half,height);
            else if(count==3 && i==0) Place(windows[i],work.Left,work.Top,half,height);
            else if(count==3) Place(windows[i],work.Left+half,work.Top+(i-1)*topHalf,width-half,i==2?height-topHalf:topHalf);
            else Place(windows[i],work.Left+(i%2)*half,work.Top+(i/2)*topHalf,i%2==1?width-half:half,i/2==1?height-topHalf:topHalf);
        }
        return ActionResult.Ok($"{count} pencere yerleştirildi.");
    }
    public static async Task<string> NowPlayingAsync()
    {
        try { var manager=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync(); var session=manager.GetCurrentSession(); if(session is null) return "Etkin medya oturumu yok."; var props=await session.TryGetMediaPropertiesAsync(); return $"{props.Title} · {props.Artist}"; }
        catch(Exception ex) { return "Medya bilgisi erişilemiyor: "+ex.Message; }
    }
    private static async Task<ActionResult> BrightnessAsync(int delta)
    {
        // OEM-dependent WMI method, invoked only by an explicit user action.
        return await Task.Run(()=>
        {
            try
            {
                using var current=new System.Management.ManagementObjectSearcher("root\\WMI","SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active=True");
                using var readings=current.Get(); var value=readings.Cast<System.Management.ManagementObject>().FirstOrDefault();
                if(value is null) return ActionResult.Fail("Bu ekran parlaklık WMI arayüzünü sunmuyor.");
                byte level=(byte)Math.Clamp(Convert.ToInt32(value["CurrentBrightness"])+delta,0,100);
                using var search=new System.Management.ManagementObjectSearcher("root\\WMI","SELECT * FROM WmiMonitorBrightnessMethods WHERE Active=True");
                using var methods=search.Get(); bool changed=false;
                foreach(System.Management.ManagementObject method in methods) { using(method) { method.InvokeMethod("WmiSetBrightness",[1u,level]); changed=true; } }
                return changed?ActionResult.Ok($"Parlaklık %{level}"):ActionResult.Fail("Parlaklık aygıtı bulunamadı.");
            } catch(Exception ex) { return ActionResult.Fail("Parlaklık desteklenmiyor / erişilemiyor: "+ex.Message); }
        });
    }
}
