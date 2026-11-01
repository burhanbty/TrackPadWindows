using System.Diagnostics;
using Windows.Foundation.Metadata;
using Windows.UI.Core;
using Windows.UI.Input;
using TrackpadProMax.Core;

// This isolated adapter deliberately uses the experimental v19 contract. Metadata
// and IsSupported gates precede activation; callers retain the shortcut fallback.
#pragma warning disable CS8305, CA1416

namespace TrackpadProMax.Input;

// New contract is gated at runtime; no controller is enabled in the background.
public sealed class ForegroundGestureController : IDisposable
{
    private TouchpadGesturesController? controller;
    private readonly Stopwatch clock=Stopwatch.StartNew();
    private readonly NativeTouchpadProvider frames;
    public bool IsRunning { get; private set; }
    public string Status { get; private set; }="Kapalı · fiziksel test yapılmadı";
    public event Action<Gesture>? Gesture;
    public event Action<string>? ActionReceived;
    public ForegroundGestureController(NativeTouchpadProvider frames) => this.frames=frames;
    public void Start()
    {
        if(IsRunning)return;
        try
        {
            if(!ApiInformation.IsTypePresent("Windows.UI.Input.TouchpadGesturesController") || !TouchpadGesturesController.IsSupported())
            {Status="TouchpadGesturesController bu sistemde desteklenmiyor; Windows kısayol yolunu kullanın.";return;}
            controller=TouchpadGesturesController.CreateForProcess();
            controller.SupportedGestures=TouchpadGlobalGestureKinds.ThreeFingerActions | TouchpadGlobalGestureKinds.FourFingerActions | TouchpadGlobalGestureKinds.ThreeFingerManipulations | TouchpadGlobalGestureKinds.FourFingerManipulations;
            controller.PointerPressed+=OnPointer;controller.PointerMoved+=OnPointer;controller.PointerReleased+=OnPointer;
            controller.GlobalActionPerformed+=OnAction;
            controller.Enabled=true;IsRunning=true;
            Status="Denetleyici etkin · yalnızca ön plan · olay teslimi fiziksel test bekliyor";
        }
        catch(Exception ex){Stop();Status="Denetleyici başlatılamadı: "+ex.Message;}
    }
    private void OnPointer(TouchpadGesturesController sender,PointerEventArgs e)
    {
        try { frames.ReadPointer(e.CurrentPoint.PointerId); }
        catch(Exception ex){Status="Denetleyici temas okuma hatası: "+ex.Message;}
    }
    private void OnAction(TouchpadGesturesController sender,TouchpadGlobalActionEventArgs e)
    {
        ActionReceived?.Invoke(e.Action.ToString());
        if(e.Action==TouchpadGlobalAction.ThreeFingerTap)Gesture?.Invoke(new(GestureKind.Tap,3,Zone.Anywhere,clock.Elapsed.TotalSeconds));
        else if(e.Action==TouchpadGlobalAction.FourFingerTap)Gesture?.Invoke(new(GestureKind.Tap,4,Zone.Anywhere,clock.Elapsed.TotalSeconds));
    }
    public void Stop()
    {
        if(controller is not null)
        {
            try { controller.Enabled=false;controller.PointerPressed-=OnPointer;controller.PointerMoved-=OnPointer;controller.PointerReleased-=OnPointer;controller.GlobalActionPerformed-=OnAction;Status="Denetleyici kapalı · fiziksel test sonucu laboratuvar kaydındadır."; }
            catch(Exception ex){Status="Denetleyici kapatma hatası: "+ex.Message;}
            controller=null;
        }
        IsRunning=false;
    }
    public void Dispose()=>Stop();
}
