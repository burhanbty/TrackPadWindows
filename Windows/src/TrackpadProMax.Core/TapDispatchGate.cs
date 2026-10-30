namespace TrackpadProMax.Core;
// A host calls Flush on its timer. A deferred single is cancelled only by a matching double.
// No wall clock or OS dependency, so timing can be tested deterministically.
public sealed class TapDispatchGate(double interval,double maximumTapDuration=.22)
{
    private readonly List<(Gesture Gesture,double Due)> pending=[];
    public IReadOnlyList<Gesture> Push(Gesture gesture,bool hasDoubleBinding)
    {
        var ready=Flush(gesture.Time).ToList();
        if(gesture.Kind==GestureKind.DoubleTap)
            pending.RemoveAll(p=>p.Gesture.Fingers==gesture.Fingers && p.Gesture.Zone==gesture.Zone);
        // The second tap may START at the end of the interval and finish later.
        if(gesture.Kind==GestureKind.Tap && hasDoubleBinding) pending.Add((gesture,gesture.Time+interval+maximumTapDuration));
        else ready.Add(gesture);
        return ready;
    }
    public IReadOnlyList<Gesture> Flush(double time)
    {
        var ready=pending.Where(p=>p.Due<time).Select(p=>p.Gesture).ToArray();
        pending.RemoveAll(p=>p.Due<time);return ready;
    }
    public void Cancel()=>pending.Clear();
}
