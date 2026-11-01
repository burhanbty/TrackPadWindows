// Adapted from TrackpadProMax's session recognition and zone classification.
// Copyright 2026 Serhan Ensar. Licensed under Apache-2.0. See LICENSE and NOTICE.
namespace TrackpadProMax.Core;

public readonly record struct PadPoint(double X, double Y)
{
    public double Distance(PadPoint other) => Math.Sqrt(Math.Pow(X-other.X,2)+Math.Pow(Y-other.Y,2));
}
public enum Zone { Anywhere, Center, TopLeft, TopRight, BottomLeft, BottomRight, TopEdge, BottomEdge, LeftEdge, RightEdge }
public enum GestureKind { Tap, DoubleTap, LongPress, SwipeLeft, SwipeRight, SwipeUp, SwipeDown }
public sealed record Contact(int Id, PadPoint Position, double? Pressure = null, double? Size = null, double? Angle = null);
// Positions must be device-relative normalized coordinates, origin top-left. Never cursor coordinates.
public sealed record TouchFrame(double Time, IReadOnlyList<Contact> Contacts, bool Cancelled = false, bool Initial = false);
public sealed record Gesture(GestureKind Kind, int Fingers, Zone Zone, double Time);
public sealed class GestureSettings
{
    public double TapSeconds { get; set; } = .22;
    public double DoubleTapSeconds { get; set; } = .30;
    public double LongPressSeconds { get; set; } = .65;
    public double TapMovement { get; set; } = .05;
    public double SwipeMovement { get; set; } = .18;
    public double ZoneMargin { get; set; } = .18;
    public void Validate()
    {
        if (!double.IsFinite(TapSeconds+DoubleTapSeconds+LongPressSeconds+TapMovement+SwipeMovement+ZoneMargin) ||
            TapSeconds <= 0 || DoubleTapSeconds <= 0 || LongPressSeconds <= TapSeconds ||
            TapMovement <= 0 || SwipeMovement <= TapMovement || SwipeMovement > 1 || ZoneMargin <= 0 || ZoneMargin >= .5)
            throw new ArgumentException("Hareket eşikleri geçersiz.");
    }
}
public sealed class GestureRecognizer
{
    private readonly GestureSettings settings;
    private readonly Dictionary<int, PadPoint> origins = new();
    private double start, lastTime = -1, movement;
    private int fingers;
    private PadPoint origin, last;
    private Gesture? previousTap;
    private bool active;
    public GestureRecognizer(GestureSettings? settings = null) { this.settings = settings ?? new(); this.settings.Validate(); }
    public void Cancel() { active=false; origins.Clear(); previousTap=null; fingers=0; movement=0; }
    public IReadOnlyList<Gesture> Process(TouchFrame frame)
    {
        if (!double.IsFinite(frame.Time) || frame.Time < lastTime) { Cancel(); throw new ArgumentException("Frames must use a monotonic clock."); }
        lastTime=frame.Time;
        if (frame.Cancelled) { Cancel(); return []; }
        if (frame.Contacts.Any(c => !double.IsFinite(c.Position.X+c.Position.Y) || c.Position.X<0 || c.Position.X>1 || c.Position.Y<0 || c.Position.Y>1) ||
            frame.Contacts.Select(c=>c.Id).Distinct().Count()!=frame.Contacts.Count || frame.Contacts.Count>4)
        { Cancel(); return []; }
        if (frame.Contacts.Count>0)
        {
            var center = new PadPoint(frame.Contacts.Average(c=>c.Position.X), frame.Contacts.Average(c=>c.Position.Y));
            if (!active) { active=true; start=frame.Time; origin=center; last=center; movement=0; fingers=0; origins.Clear(); }
            foreach(var c in frame.Contacts) { origins.TryAdd(c.Id,c.Position); movement=Math.Max(movement,c.Position.Distance(origins[c.Id])); }
            if(frame.Contacts.Count>fingers)
                origin=new(frame.Contacts.Average(c=>origins[c.Id].X),frame.Contacts.Average(c=>origins[c.Id].Y));
            fingers=Math.Max(fingers,frame.Contacts.Count);
            // Only update the end centroid with the full finger count to avoid lift-order jumps.
            if (frame.Contacts.Count == fingers) last=center;
            return [];
        }
        if (!active) return [];
        active=false;
        var elapsed=frame.Time-start;
        var zone=Classify(origin,settings.ZoneMargin);
        GestureKind? kind=null;
        var dx=last.X-origin.X; var dy=last.Y-origin.Y;
        if (Math.Sqrt(dx*dx+dy*dy)>=settings.SwipeMovement)
            kind=Math.Abs(dx)>Math.Abs(dy) ? (dx>0?GestureKind.SwipeRight:GestureKind.SwipeLeft) : (dy>0?GestureKind.SwipeDown:GestureKind.SwipeUp);
        else if (movement<=settings.TapMovement)
        {
            if (elapsed>=settings.LongPressSeconds) kind=GestureKind.LongPress;
            else if (elapsed<=settings.TapSeconds)
            {
                kind=previousTap is { } p && p.Fingers==fingers && p.Zone==zone && start-p.Time<=settings.DoubleTapSeconds
                    ? GestureKind.DoubleTap : GestureKind.Tap;
            }
        }
        if (kind is null) { previousTap=null; return []; }
        var result=new Gesture(kind.Value,fingers,zone,frame.Time);
        previousTap=kind==GestureKind.Tap ? result : null;
        return [result];
    }
    public static Zone Classify(PadPoint p, double margin)
    {
        bool l=p.X<margin,r=p.X>1-margin,t=p.Y<margin,b=p.Y>1-margin;
        return (l,r,t,b) switch { (true,_,true,_)=>Zone.TopLeft,(_,true,true,_)=>Zone.TopRight,(true,_,_,true)=>Zone.BottomLeft,(_,true,_,true)=>Zone.BottomRight,(true,_,_,_)=>Zone.LeftEdge,(_,true,_,_)=>Zone.RightEdge,(_,_,true,_)=>Zone.TopEdge,(_,_,_,true)=>Zone.BottomEdge,_=>Zone.Center };
    }
}
public interface ITouchInputProvider : IDisposable
{
    string Name { get; }
    bool IsAvailable { get; }
    event Action<TouchFrame>? Frame;
    void Start();
    void Stop();
}
// A future signed filter-driver integration must implement this contract; no driver is installed.
public interface IAdvancedTouchProvider : ITouchInputProvider { string SecurityAndConsentRequirements { get; } }

public sealed class GestureSequence
{
    private readonly Queue<Gesture> history=new();
    public bool Push(Gesture gesture, IReadOnlyList<GestureKind> pattern, double maximumGap=.8)
    {
        if (pattern.Count==0) return false;
        if (history.Count>0 && gesture.Time-history.Last().Time>maximumGap) history.Clear();
        history.Enqueue(gesture);
        while(history.Count>pattern.Count) history.Dequeue();
        if (!history.Select(g=>g.Kind).SequenceEqual(pattern)) return false;
        history.Clear(); return true;
    }
}
