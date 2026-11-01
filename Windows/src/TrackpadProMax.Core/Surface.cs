// Surface tracking adapted from upstream ControlSurfaceTracker's origin locking,
// relative adjustments and contact-session cancellation. Windows origin is top-left.
// Copyright 2026 Serhan Ensar. Apache-2.0; see LICENSE and NOTICE.
namespace TrackpadProMax.Core;

public enum SurfaceAxis { Horizontal, Vertical }
public sealed class SurfaceZone
{
    public Zone Zone { get; set; }
    public bool Enabled { get; set; } = true;
    public double Width { get; set; } = .18;
    public double Height { get; set; } = .18;
    public SurfaceAxis Axis { get; set; }
    public bool Repeat { get; set; }
    public ActionSpec Positive { get; set; } = new(){Kind="media",Value="play"};
    public ActionSpec Negative { get; set; } = new(){Kind="media",Value="play"};
    public bool Contains(PadPoint p,double tolerance=0) => Zone switch
    {
        Zone.LeftEdge => p.X<=Width+tolerance,
        Zone.RightEdge => p.X>=1-Width-tolerance,
        Zone.TopEdge => p.Y<=Height+tolerance,
        Zone.BottomEdge => p.Y>=1-Height-tolerance,
        Zone.TopLeft => p.X<=Width+tolerance && p.Y<=Height+tolerance,
        Zone.TopRight => p.X>=1-Width-tolerance && p.Y<=Height+tolerance,
        Zone.BottomLeft => p.X<=Width+tolerance && p.Y>=1-Height-tolerance,
        Zone.BottomRight => p.X>=1-Width-tolerance && p.Y>=1-Height-tolerance,
        _ => false
    };
}
public sealed class SurfaceSettings
{
    // Two-finger foreground manipulations are the least restrictive standard path.
    // Three/four fingers can be routed by TouchpadGesturesController. One is not
    // supplied by these APIs, but may be used with a future genuine contact provider.
    public int Fingers { get; set; } = 2;
    public bool AcceptExtraFingers { get; set; } = true;
    public double MovementThreshold { get; set; } = .04;
    public double StepDistance { get; set; } = .025;
    public double Sensitivity { get; set; } = 1.4;
    public double MinimumSeconds { get; set; } = .08;
    public double RepeatSeconds { get; set; } = .12;
    public double CrossAxisLimit { get; set; } = .10;
    public double ExitTolerance { get; set; } = .035;
    public double MaximumJump { get; set; } = .30;
    public double MaximumSeconds { get; set; } = 12;
    public List<SurfaceZone> Zones { get; set; } = Defaults();
    public static bool IsCorner(Zone z)=>z is Zone.TopLeft or Zone.TopRight or Zone.BottomLeft or Zone.BottomRight;
    public static List<SurfaceZone> Defaults()
    {
        SurfaceZone Make(Zone z,SurfaceAxis axis,bool repeat,string kind,string positive,string negative)=>new()
        {Zone=z,Axis=axis,Repeat=repeat,Positive=new(){Kind=kind,Value=positive},Negative=new(){Kind=kind,Value=negative}};
        return [Make(Zone.LeftEdge,SurfaceAxis.Vertical,true,"brightness","2","-2"),
            Make(Zone.RightEdge,SurfaceAxis.Vertical,true,"volume","2","-2"),
            Make(Zone.TopEdge,SurfaceAxis.Horizontal,false,"media","next","previous"),
            Make(Zone.BottomEdge,SurfaceAxis.Horizontal,true,"seek","2","-2"),
            Make(Zone.TopLeft,SurfaceAxis.Horizontal,false,"media","play","play"),
            Make(Zone.TopRight,SurfaceAxis.Horizontal,false,"media","mute","mute"),
            Make(Zone.BottomLeft,SurfaceAxis.Horizontal,false,"wheel","",""),
            Make(Zone.BottomRight,SurfaceAxis.Horizontal,false,"mode","Normal","Normal")];
    }
    public void Validate()
    {
        if(Fingers<1 || Fingers>4 ||
            !double.IsFinite(MovementThreshold+StepDistance+Sensitivity+MinimumSeconds+RepeatSeconds+CrossAxisLimit+ExitTolerance+MaximumJump+MaximumSeconds) ||
            MovementThreshold<.005 || MovementThreshold>.3 || StepDistance<.005 || StepDistance>.3 || Sensitivity<.1 || Sensitivity>5 ||
            MinimumSeconds<0 || MinimumSeconds>2 || RepeatSeconds<.08 || RepeatSeconds>2 || CrossAxisLimit<.01 || CrossAxisLimit>.4 ||
            ExitTolerance<0 || ExitTolerance>.1 || MaximumJump<.05 || MaximumJump>1 || MaximumSeconds<1 || MaximumSeconds>60)
            throw new ArgumentException("Yüzey eşikleri geçersiz.");
        if(Zones is null || Zones.Count!=8 || Zones.Any(z=>z is null) || Zones.Select(z=>z.Zone).Distinct().Count()!=8)
            throw new ArgumentException("Sekiz benzersiz kenar/köşe bölgesi gerekli.");
        foreach(var z in Zones)
        {
            if(z.Zone is Zone.Anywhere or Zone.Center || !Enum.IsDefined(z.Zone) || !Enum.IsDefined(z.Axis) ||
                !double.IsFinite(z.Width+z.Height) || z.Width<.05 || z.Width>.4 || z.Height<.05 || z.Height>.4)
                throw new ArgumentException("Bölge boyutları %5–40 arasında olmalı.");
            Configuration.ValidateAction(z.Positive);Configuration.ValidateAction(z.Negative);
        }
    }
    // Corners reserve their region even when disabled, preventing edge fall-through.
    public SurfaceZone? Classify(PadPoint p)=>Zones.OrderByDescending(z=>IsCorner(z.Zone)).FirstOrDefault(z=>z.Contains(p));
}
public enum SurfaceEventKind { Began, Triggered, Releasing, Ended, Cancelled }
public sealed record SurfaceEvent(SurfaceEventKind Kind,Zone Zone,PadPoint Position,int ContactId,string Direction="",ActionSpec? Action=null);

/// <summary>Only accepts device-relative contact frames. No mouse/shortcut adapter.</summary>
public sealed class SurfaceTracker
{
    private readonly SurfaceSettings settings;
    private SurfaceZone? zone;
    private HashSet<int> ids=[];
    private int anchor;
    private PadPoint origin,last;
    private double start,lastTime=-1,lastEmit,accumulator;
    private bool blocked,activated,fired,releasing,rebaseInitial;
    public SurfaceTracker(SurfaceSettings settings){settings.Validate();this.settings=settings;}
    public void Cancel(bool waitForLift=true){zone=null;ids.Clear();blocked=waitForLift;activated=false;fired=false;releasing=false;accumulator=0;}
    public IReadOnlyList<SurfaceEvent> Process(TouchFrame frame)
    {
        SurfaceEvent[] Abort(string reason)
        {
            var e=zone is null?Array.Empty<SurfaceEvent>():[new SurfaceEvent(SurfaceEventKind.Cancelled,zone.Zone,last,anchor,reason)];
            Cancel();return e;
        }
        if(!double.IsFinite(frame.Time) || frame.Time<lastTime)return Abort("Geçersiz zaman");
        lastTime=frame.Time;
        if(frame.Cancelled)
        {
            // Idle mouse capture changes (e.g. clicking a test button) must not
            // consume the next physical gesture while waiting for a fictitious lift.
            if(zone is null && frame.Contacts.Count==0){Cancel(false);return [];}
            return Abort("Akış iptali");
        }
        if(frame.Contacts.Count>4 || frame.Contacts.Select(c=>c.Id).Distinct().Count()!=frame.Contacts.Count ||
            frame.Contacts.Any(c=>!double.IsFinite(c.Position.X+c.Position.Y) || c.Position.X<0 || c.Position.X>1 || c.Position.Y<0 || c.Position.Y>1))
            return Abort("Geçersiz cihaz koordinatları");
        if(frame.Contacts.Count==0)
        {
            var e=zone is null?Array.Empty<SurfaceEvent>():[new SurfaceEvent(SurfaceEventKind.Ended,zone.Zone,last,anchor)];
            Cancel(false);return e;
        }
        if(blocked)return [];
        if(zone is null)
        {
            if(frame.Contacts.Count>settings.Fingers && !settings.AcceptExtraFingers){blocked=true;return [];}
            if(frame.Contacts.Count<settings.Fingers)return [];
            // Three/four fingers span much of a laptop pad. Track the outermost
            // contact actually in a region, not an arbitrary middle finger ID.
            var candidate=frame.Contacts.Select(c=>(Contact:c,Region:settings.Classify(c.Position)))
                .Where(c=>c.Region is not null)
                .OrderByDescending(c=>SurfaceSettings.IsCorner(c.Region!.Zone))
                .ThenBy(c=>Math.Min(Math.Min(c.Contact.Position.X,1-c.Contact.Position.X),Math.Min(c.Contact.Position.Y,1-c.Contact.Position.Y)))
                .ThenBy(c=>c.Contact.Id).FirstOrDefault();
            var lead=candidate.Contact;zone=candidate.Region;
            if(zone is null || !zone.Enabled){zone=null;blocked=true;return [];}
            anchor=lead.Id;ids=frame.Contacts.Select(c=>c.Id).ToHashSet();origin=last=lead.Position;
            start=lastEmit=frame.Time;accumulator=0;activated=fired=false;rebaseInitial=frame.Initial;
            return [new(SurfaceEventKind.Began,zone.Zone,last,anchor)];
        }
        // Changing finger IDs/count cannot teleport the anchor or start a new zone.
        if(releasing)
        {
            if(frame.Contacts.Count<=ids.Count && frame.Contacts.All(c=>ids.Contains(c.Id)))
            {ids=frame.Contacts.Select(c=>c.Id).ToHashSet();return [];}
            return Abort("Kaldırma sırasında yeni temas");
        }
        if(frame.Contacts.Count<ids.Count && frame.Contacts.All(c=>ids.Contains(c.Id)))
        {
            releasing=true;ids=frame.Contacts.Select(c=>c.Id).ToHashSet();
            return [new(SurfaceEventKind.Releasing,zone.Zone,last,anchor,"Temas sayısı azaldı · kontrol durdu · tüm parmakları kaldırın")];
        }
        if(!ids.SetEquals(frame.Contacts.Select(c=>c.Id)))
        {
            // Fingers can land over several frames. Accept additions only before
            // the first action, keep the same anchor and restart the dwell period.
            if(settings.AcceptExtraFingers && !activated && ids.IsSubsetOf(frame.Contacts.Select(c=>c.Id)))
            {
                var stable=frame.Contacts.First(c=>c.Id==anchor).Position;
                if(!zone.Contains(stable,settings.ExitTolerance))return Abort("Başlangıç teması bölgeden çıktı");
                ids=frame.Contacts.Select(c=>c.Id).ToHashSet();origin=last=stable;start=lastEmit=frame.Time;accumulator=0;rebaseInitial=frame.Initial;
                return [new(SurfaceEventKind.Began,zone.Zone,last,anchor,"Parmak grubu dengeleniyor")];
            }
            return Abort("Temas kümesi değişti");
        }
        var point=frame.Contacts.First(c=>c.Id==anchor).Position;
        if(rebaseInitial)
        {
            if(frame.Initial)return [];
            // Windows reconstructs the down frame before delivering the current
            // manipulation. Rebase once, with no action or accumulated jump.
            // The original zone stays locked and must still contain the contact.
            if(!zone.Contains(point,settings.ExitTolerance))return Abort("İlk güncelleme bölge dışında");
            origin=last=point;start=lastEmit=frame.Time;accumulator=0;rebaseInitial=false;
            return [new(SurfaceEventKind.Began,zone.Zone,last,anchor,"Başlangıç dengelendi")];
        }
        if(last.Distance(point)>settings.MaximumJump)return Abort("Ani temas sıçraması");
        if(!zone.Contains(point,settings.ExitTolerance))return Abort("Bölgeden çıkıldı");
        if(frame.Time-start>settings.MaximumSeconds)return Abort("Oturum zaman aşımı");
        double along=zone.Axis==SurfaceAxis.Horizontal?point.X-origin.X:origin.Y-point.Y;
        double cross=zone.Axis==SurfaceAxis.Horizontal?point.Y-origin.Y:point.X-origin.X;
        double delta=zone.Axis==SurfaceAxis.Horizontal?point.X-last.X:last.Y-point.Y;
        last=point;
        if(Math.Abs(cross)>settings.CrossAxisLimit)return Abort("Çapraz hareket");
        if(!activated)
        {
            if(frame.Time-start<settings.MinimumSeconds || Math.Abs(along)<settings.MovementThreshold || Math.Abs(along)<Math.Abs(cross)*1.5)return [];
            activated=true;accumulator=0;lastEmit=frame.Time;fired=true;
            return [Trigger(Math.Sign(along),1)];
        }
        if(!zone.Repeat && fired)return [];
        accumulator+=delta*settings.Sensitivity;
        if(Math.Abs(accumulator)<settings.StepDistance || frame.Time-lastEmit<settings.RepeatSeconds)return [];
        int sign=Math.Sign(accumulator),steps=Math.Min(4,(int)(Math.Abs(accumulator)/settings.StepDistance));
        // Coalesce bounded relative updates; never queue a burst of stale actions.
        accumulator=0;lastEmit=frame.Time;return [Trigger(sign,steps)];
    }
    private SurfaceEvent Trigger(int sign,int steps)
    {
        var spec=sign>0?zone!.Positive:zone!.Negative;
        var action=new ActionSpec{Kind=spec.Kind,Value=spec.Value};
        if(steps>1 && action.Kind is "volume" or "brightness" or "seek" && int.TryParse(action.Value,out int amount))
            action.Value=Math.Clamp(amount*steps,-100,100).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new(SurfaceEventKind.Triggered,zone!.Zone,last,anchor,
            zone.Axis==SurfaceAxis.Horizontal?(sign>0?"Sağ":"Sol"):(sign>0?"Yukarı":"Aşağı"),action);
    }
}

// A zone test requires a completed, non-cancelled native movement session.
// It proves recognition only; action success requires separate executor evidence.
public sealed class PhysicalZoneTest(Zone zone)
{
    public Zone Zone { get; }=zone;
    public string Status { get; private set; }="Bekliyor · fiziksel test yapılmadı";
    public bool Armed { get; private set; }
    private bool began,triggered;
    public void Arm(){Armed=true;began=triggered=false;Status="Dinleniyor · bölgede başlayın, eşik kadar kaydırın, tüm parmakları kaldırın";}
    public void Stop(){if(Armed)Status="İptal edildi · testi yeniden başlatın";Armed=began=triggered=false;}
    public void Observe(SurfaceEvent e,EvidenceSource source)
    {
        if(!Armed || source!=EvidenceSource.NativePointer || e.Zone!=Zone)return;
        if(e.Kind==SurfaceEventKind.Began){began=true;triggered=false;}
        if(e.Kind==SurfaceEventKind.Triggered && began)triggered=true;
        if(e.Kind==SurfaceEventKind.Cancelled){began=triggered=false;Status="İptal · "+e.Direction;}
        if(e.Kind==SurfaceEventKind.Ended && began && triggered)
        {Status="Yerel bölge hareketi alındı · eylem sonucu ayrıca doğrulanmalı";Armed=false;}
    }
}
