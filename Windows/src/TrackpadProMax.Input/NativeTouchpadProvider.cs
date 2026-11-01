using System.Diagnostics;
using System.Runtime.InteropServices;
using TrackpadProMax.Core;

namespace TrackpadProMax.Input;
// Optional foreground manipulation stream. Not a global raw touchpad reader.
public sealed class NativeTouchpadProvider : ITouchInputProvider
{
    private readonly nint window;
    private readonly nint library;
    private readonly RegisterDelegate? register;
    private readonly FrameDelegate? getFrame;
    private uint? previousFrame;
    private bool disposed;
    public long ReceivedFrames { get; private set; }
    public long ReadErrors { get; private set; }
    private readonly Stopwatch clock=Stopwatch.StartNew();
    public string Name=>"Deneysel Windows ön plan manipülasyonu";
    public bool IsAvailable=>register is not null && getFrame is not null;
    public bool IsRunning { get; private set; }
    public string Status { get; private set; }="Fiziksel hareket testi yapılmadı.";
    public event Action<TouchFrame>? Frame;
    public NativeTouchpadProvider(nint window)
    {
        this.window=window;
        if(NativeLibrary.TryLoad("user32.dll",out library))
        {
            if(NativeLibrary.TryGetExport(library,"RegisterTouchpadCapableWindow",out var p)) register=Marshal.GetDelegateForFunctionPointer<RegisterDelegate>(p);
            if(NativeLibrary.TryGetExport(library,"GetPointerFrameTouchpadInfo",out p)) getFrame=Marshal.GetDelegateForFunctionPointer<FrameDelegate>(p);
        }
    }
    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(IsRunning)return;
        if(!IsAvailable) {Status="Bu Windows sürümünde gerekli ön plan API'leri yok. Genel kısayolları kullanın.";return;}
        IsRunning=register!(window,true);
        Status=IsRunning?"Ön plan girişi kaydedildi. Olay teslimi fiziksel olarak test edilmeli.":$"Windows kayıt isteğini reddetti: {Marshal.GetLastWin32Error()}";
    }
    public void Stop()
    {
        if(disposed)return;
        if(IsRunning && !register!(window,false))Status=$"Ön plan kaydı kapatılamadı: {Marshal.GetLastWin32Error()}";
        else Status="Ön plan girişi kapalı · fiziksel test sonucu laboratuvar kaydındadır.";
        IsRunning=false;previousFrame=null;Frame?.Invoke(new(clock.Elapsed.TotalSeconds,[],true));
    }
    public void ProcessMessage(int message,nint wParam)
    {
        if(!IsRunning)return;
        if(message==0x001C && wParam==0 || message==0x215 || message==0x24C) {previousFrame=null;Frame?.Invoke(new(clock.Elapsed.TotalSeconds,[],true));return;}
        if(message is not (0x245 or 0x246 or 0x247))return;
        uint id=(uint)((long)wParam&0xffff),type=0;
        if(!GetPointerType(id,ref type) || type!=5)return;
        ReadPointer(id);
    }
    // Called synchronously inside a controller pointer callback too. The shared
    // frame ID deduplicates controller callbacks and foreground WM_POINTER.
    public void ReadPointer(uint id)
    {
        if(disposed || getFrame is null)return;
        uint count=16;var info=new TouchInfo[16];
        if(!getFrame(id,ref count,info) || count==0 || count>16) {ReadErrors++;Status=$"Touchpad çerçevesi okunamadı: {Marshal.GetLastWin32Error()}";Frame?.Invoke(new(clock.Elapsed.TotalSeconds,[],true));return;}
        if(previousFrame==info[0].Pointer.FrameId)return;previousFrame=info[0].Pointer.FrameId;
        var contacts=new List<Contact>();bool cancelled=false;
        foreach(var touch in info.Take((int)count))
        {
            var p=touch.Pointer;
            if((p.Flags&0x8000)!=0)cancelled=true;
            if((p.Flags&4)==0 || (p.Flags&0x40000)!=0)continue;
            if(!GetPointerDeviceRects(p.SourceDevice,out var device,out _) || device.Right<=device.Left || device.Bottom<=device.Top)
            {cancelled=true;continue;}
            double x=(p.HimetricRaw.X-device.Left)/(double)(device.Right-device.Left),y=(p.HimetricRaw.Y-device.Top)/(double)(device.Bottom-device.Top);
            if(!double.IsFinite(x+y) || x<0 || x>1 || y<0 || y>1){cancelled=true;continue;}
            // The pressure field may contain a default 512 even without a sensor; do not expose it as force evidence.
            contacts.Add(new((int)p.Id,new(x,y)));
        }
        ReceivedFrames++;Status=$"Gerçek yerel çerçeve alındı · {contacts.Count} temas · toplam {ReceivedFrames}";
        Frame?.Invoke(new(clock.Elapsed.TotalSeconds,contacts,cancelled));
    }
    public void Dispose(){if(disposed)return;Stop();disposed=true;if(library!=0)NativeLibrary.Free(library);}
    [UnmanagedFunctionPointer(CallingConvention.Winapi,SetLastError=true)] private delegate bool RegisterDelegate(nint hwnd,[MarshalAs(UnmanagedType.Bool)] bool enabled);
    [UnmanagedFunctionPointer(CallingConvention.Winapi,SetLastError=true)] private delegate bool FrameDelegate(uint id,ref uint count,[Out,MarshalAs(UnmanagedType.LPArray,SizeParamIndex=1)] TouchInfo[] info);
    [StructLayout(LayoutKind.Sequential)] private struct Point {public int X,Y;}
    [StructLayout(LayoutKind.Sequential)] private struct Rect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] private struct PointerInfo
    {
        public uint Type,Id,FrameId,Flags;
        public nint SourceDevice,Target;
        public Point Pixel,Himetric,PixelRaw,HimetricRaw;
        public uint Time,HistoryCount;public int InputData;public uint KeyStates;public ulong PerformanceCount;public uint ButtonChange;
    }
    [StructLayout(LayoutKind.Sequential)] private struct TouchInfo {public PointerInfo Pointer;public uint Flags,Mask;public Rect Contact,ContactRaw;public uint Orientation,Pressure;}
    [DllImport("user32.dll")] private static extern bool GetPointerType(uint id,ref uint type);
    [DllImport("user32.dll")] private static extern bool GetPointerDeviceRects(nint device,out Rect deviceRect,out Rect screenRect);
}
