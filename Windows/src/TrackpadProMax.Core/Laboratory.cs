namespace TrackpadProMax.Core;

public enum InputRoute { Automatic, Shortcut, Native }
public sealed class AppearanceSettings
{
    public string Theme { get; set; } = "Dark";
    public string Accent { get; set; } = "#79DDE8";
    public double Transparency { get; set; } = .22;
    public bool Animations { get; set; } = true;
    public bool Backdrop { get; set; } = true;
    public void Validate()
    {
        if(Theme is not ("Dark" or "Light" or "System") || Accent is null || Accent.Length!=7 || Accent[0]!='#' || !uint.TryParse(Accent[1..],System.Globalization.NumberStyles.HexNumber,null,out _) || !double.IsFinite(Transparency) || Transparency<0 || Transparency>.65)
            throw new ArgumentException("Tema, #RRGGBB vurgu veya saydamlık (0–0,65) geçersiz.");
    }
}
public enum EvidenceSource { NativePointer, NativeController, Shortcut, Simulation, System }
public sealed record LaboratoryEvent(DateTimeOffset At, EvidenceSource Source, string Detail, int? Fingers=null, IReadOnlyList<Contact>? Contacts=null, GestureKind? Gesture=null);
public sealed class LaboratoryJournal(int capacity=200)
{
    private readonly Queue<LaboratoryEvent> events=new();
    public IReadOnlyList<LaboratoryEvent> Events=>events.ToArray();
    public long NativeFrames { get; private set; }
    public void Add(LaboratoryEvent entry)
    {
        if(capacity<1) throw new ArgumentOutOfRangeException(nameof(capacity));
        if(entry.Source==EvidenceSource.NativePointer && entry.Contacts is not null) NativeFrames++;
        events.Enqueue(entry);while(events.Count>capacity) events.Dequeue();
    }
}
// Arming a test and receiving matching native evidence are both required. Shortcuts
// cannot prove a physical gesture: a user must separately confirm performing it.
public sealed class PhysicalTest(string name, GestureKind? kind, int? fingers, bool contactsRequired=false)
{
    public string Name { get; }=name;
    public GestureKind? Kind { get; }=kind;
    public int? Fingers { get; }=fingers;
    public bool ContactsRequired { get; }=contactsRequired;
    public string Status { get; private set; }="Bekliyor · fiziksel olarak uygulanmadı";
    public bool Armed { get; private set; }
    public void Arm(){Armed=true;Status="Dinleniyor · fiziksel hareketi şimdi yapın";}
    public void Observe(LaboratoryEvent e)
    {
        if(!Armed || e.Source is not (EvidenceSource.NativePointer or EvidenceSource.NativeController))return;
        if(Fingers.HasValue && e.Fingers!=Fingers)return;
        if(Kind.HasValue && e.Gesture!=Kind)return;
        if(ContactsRequired && e.Contacts is not {Count:>0})return;
        Status=$"Yerel olay alındı · {e.At:HH:mm:ss} · {e.Source}";Armed=false;
    }
}
