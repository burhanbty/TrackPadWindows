using TrackpadProMax.Core;
using Xunit;

namespace TrackpadProMax.Tests;
public class CoreTests
{
    private static Contact[] Fingers(int count,double x=.5,double y=.5)=>Enumerable.Range(1,count).Select(i=>new Contact(i,new(x,y))).ToArray();
    [Theory][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)]
    public void QuickSessionIsTap(int count)
    {var r=new GestureRecognizer();Assert.Empty(r.Process(new(0,Fingers(count))));var g=Assert.Single(r.Process(new(.1,[])));Assert.Equal(GestureKind.Tap,g.Kind);Assert.Equal(count,g.Fingers);}
    [Fact] public void DoubleTapUsesStartGap()
    {var r=new GestureRecognizer();r.Process(new(0,Fingers(2)));r.Process(new(.1,[]));r.Process(new(.2,Fingers(2)));Assert.Equal(GestureKind.DoubleTap,Assert.Single(r.Process(new(.29,[]))).Kind);}
    [Fact] public void SlowSecondTapIsSingle()
    {var r=new GestureRecognizer();r.Process(new(0,Fingers(1)));r.Process(new(.1,[]));r.Process(new(.7,Fingers(1)));Assert.Equal(GestureKind.Tap,Assert.Single(r.Process(new(.8,[]))).Kind);}
    [Fact] public void DifferentFingerCountDoesNotDoubleTap()
    {var r=new GestureRecognizer();r.Process(new(0,Fingers(1)));r.Process(new(.1,[]));r.Process(new(.2,Fingers(2)));Assert.Equal(GestureKind.Tap,Assert.Single(r.Process(new(.3,[]))).Kind);}
    [Theory][InlineData(.2,.5,GestureKind.SwipeLeft)][InlineData(.8,.5,GestureKind.SwipeRight)][InlineData(.5,.2,GestureKind.SwipeUp)][InlineData(.5,.8,GestureKind.SwipeDown)]
    public void SwipeDirection(double x,double y,GestureKind expected)
    {var r=new GestureRecognizer();r.Process(new(0,Fingers(3)));r.Process(new(.1,Fingers(3,x,y)));Assert.Equal(expected,Assert.Single(r.Process(new(.2,[]))).Kind);}
    [Fact] public void LongHoldIsNotTap()
    {var r=new GestureRecognizer();r.Process(new(0,Fingers(1)));Assert.Equal(GestureKind.LongPress,Assert.Single(r.Process(new(.8,[]))).Kind);}
    [Fact] public void MediumHoldIsIgnored()
    {var r=new GestureRecognizer();r.Process(new(0,Fingers(1)));Assert.Empty(r.Process(new(.4,[])));}
    [Fact] public void MovementReturningToOriginCancelsTap()
    {var r=new GestureRecognizer();r.Process(new(0,Fingers(1)));r.Process(new(.05,Fingers(1,.6)));r.Process(new(.1,Fingers(1)));Assert.Empty(r.Process(new(.15,[])));}
    [Fact] public void CancellationClearsSessionAndDoubleTap()
    {var r=new GestureRecognizer();r.Process(new(0,Fingers(1)));r.Process(new(.1,[]));r.Process(new(.15,Fingers(1),true));r.Process(new(.2,Fingers(1)));Assert.Equal(GestureKind.Tap,Assert.Single(r.Process(new(.3,[]))).Kind);}
    [Fact] public void OutOfOrderClockRejected()
    {var r=new GestureRecognizer();r.Process(new(1,Fingers(1)));Assert.Throws<ArgumentException>(()=>r.Process(new(.1,[])));}
    [Fact] public void InvalidCoordinatesCancelWithoutFabrication()
    {var r=new GestureRecognizer();Assert.Empty(r.Process(new(0,Fingers(1,double.NaN))));Assert.Empty(r.Process(new(.1,[])));}
    [Theory][InlineData(.05,.05,Zone.TopLeft)][InlineData(.95,.05,Zone.TopRight)][InlineData(.05,.95,Zone.BottomLeft)][InlineData(.95,.95,Zone.BottomRight)][InlineData(.05,.5,Zone.LeftEdge)][InlineData(.95,.5,Zone.RightEdge)][InlineData(.5,.05,Zone.TopEdge)][InlineData(.5,.95,Zone.BottomEdge)][InlineData(.5,.5,Zone.Center)]
    public void ZonesUseDeviceCoordinates(double x,double y,Zone zone)=>Assert.Equal(zone,GestureRecognizer.Classify(new(x,y),.18));
    [Fact] public void NoPressureIsAvailableByDefault()=>Assert.Null(new Contact(1,new(.5,.5)).Pressure);
    [Fact] public void ConfigurationRoundtrip()
    {var c=ConfigurationStore.Deserialize(ConfigurationStore.Serialize(Configuration.Default()));Assert.Equal(6,c.Menu.Count);Assert.Equal(3,c.Profiles.Count);Assert.Equal(.22,c.Gestures.TapSeconds);}
    [Fact] public void ProfileOverridesGlobalAndFallsBack()
    {var c=Configuration.Default();Assert.Equal("keys",c.Resolve("CHROME.exe",b=>b.Shortcut=="Ctrl+Alt+T")!.Action.Kind);Assert.Null(c.Resolve("other",b=>b.Shortcut=="Ctrl+Alt+T"));Assert.Equal("wheel",c.Resolve("chrome",b=>b.Shortcut=="Ctrl+Alt+F8")!.Action.Kind);}
    [Fact] public void DisabledBindingIsSkipped()
    {var c=Configuration.Default();c.Bindings[0].Enabled=false;Assert.Null(c.Resolve("other",b=>b.Shortcut=="Ctrl+Alt+F8"));}
    [Fact] public void ModeFiltersBindings()
    {var c=Configuration.Default();c.Bindings[0].Mode="Media";Assert.Null(c.Resolve("other",b=>b.Shortcut=="Ctrl+Alt+F8"));c.ActiveMode="Media";Assert.NotNull(c.Resolve("other",b=>b.Shortcut=="Ctrl+Alt+F8"));}
    [Fact] public void NormalizedShortcutConflictsRejected()
    {var c=Configuration.Default();c.Bindings.Add(new(){Shortcut="ALT+ctrl+F8"});Assert.Throws<ArgumentException>(c.Validate);}
    [Fact] public void WildcardModeShortcutConflictIsRejected()
    {var c=Configuration.Default();c.Bindings.Add(new(){Mode="Media",Shortcut="Ctrl+Alt+F8"});Assert.Throws<ArgumentException>(c.Validate);}
    [Fact] public void NullConfigurationIsRejected()
    {Assert.Throws<ArgumentException>(()=>ConfigurationStore.Deserialize("{\"Menu\":null}"));}
    [Theory][InlineData("Ctrl+T+P")][InlineData("Ctrl")][InlineData("Ctrl+Unknown")]
    public void MalformedShortcutsRejected(string text)=>Assert.Throws<ArgumentException>(()=>Shortcut.Parse(text));
    [Theory][InlineData("file:///C:/test.exe")][InlineData("javascript:alert(1)")]
    public void UrlSchemeRestricted(string url)=>Assert.Throws<ArgumentException>(()=>Configuration.ValidateAction(new(){Kind="url",Value=url}));
    [Fact] public void InvalidThresholdsRejected()=>Assert.Throws<ArgumentException>(()=>new GestureRecognizer(new(){ZoneMargin=double.NaN}));
    [Fact] public void SequenceTimesOutAndMatches()
    {var s=new GestureSequence();GestureKind[] p=[GestureKind.Tap,GestureKind.SwipeLeft];Assert.False(s.Push(new(GestureKind.Tap,1,Zone.Center,0),p));Assert.False(s.Push(new(GestureKind.SwipeLeft,1,Zone.Center,2),p));s.Push(new(GestureKind.Tap,1,Zone.Center,3),p);Assert.True(s.Push(new(GestureKind.SwipeLeft,1,Zone.Center,3.2),p));}
    [Fact] public async Task DispatchUsesProfileAndReturnsErrors()
    {var executor=new FakeExecutor();var d=new ActionDispatcher(executor);var c=Configuration.Default();var result=await d.DispatchAsync(c,"chrome",b=>b.Shortcut=="Ctrl+Alt+T",42);Assert.True(result.Success);Assert.Equal("Ctrl+T",executor.Last?.Value);executor.Throw=true;Assert.False((await d.DispatchAsync(c,"chrome",b=>b.Shortcut=="Ctrl+Alt+T",42)).Success);}
    [Fact] public async Task UnmappedInputDoesNotExecute()
    {var executor=new FakeExecutor();var d=new ActionDispatcher(executor);Assert.False((await d.DispatchAsync(Configuration.Default(),"other",b=>false,0)).Success);Assert.Null(executor.Last);}
    [Fact] public void AtomicSaveKeepsPreviousVersion()
    {string folder=Path.Combine(Path.GetTempPath(),"TrackpadProMax-test-"+Guid.NewGuid());Directory.CreateDirectory(folder);string path=Path.Combine(folder,"settings.json");try{var c=Configuration.Default();ConfigurationStore.Save(path,c);c.ActiveMode="Media";ConfigurationStore.Save(path,c);Assert.Equal("Normal",ConfigurationStore.Deserialize(File.ReadAllText(path+".bak")).ActiveMode);Assert.Equal("Media",ConfigurationStore.Deserialize(File.ReadAllText(path)).ActiveMode);}finally{Directory.Delete(folder,true);}}
    [Fact] public void DoubleTapGateDoesNotDispatchSingleToo()
    {var gate=new TapDispatchGate(.3);Assert.Empty(gate.Push(new(GestureKind.Tap,2,Zone.Center,.1),true));Assert.Equal(GestureKind.DoubleTap,Assert.Single(gate.Push(new(GestureKind.DoubleTap,2,Zone.Center,.3),true)).Kind);Assert.Empty(gate.Flush(1));}
    [Fact] public void SingleTapGateWaitsAndCanCancel()
    {var gate=new TapDispatchGate(.3);gate.Push(new(GestureKind.Tap,1,Zone.Center,0),true);Assert.Empty(gate.Flush(.2));Assert.Single(gate.Flush(.53));gate.Push(new(GestureKind.Tap,1,Zone.Center,1),true);gate.Cancel();Assert.Empty(gate.Flush(2));}
    [Fact] public void SecondTapMayFinishAfterStartInterval()
    {var gate=new TapDispatchGate(.3);gate.Push(new(GestureKind.Tap,1,Zone.Center,.1),true);Assert.Equal(GestureKind.DoubleTap,Assert.Single(gate.Push(new(GestureKind.DoubleTap,1,Zone.Center,.5),true)).Kind);Assert.Empty(gate.Flush(1));}
    [Fact] public void AddingStationaryFingerDoesNotCreateSwipe()
    {var r=new GestureRecognizer();r.Process(new(0,[new(1,new(.1,.5))]));r.Process(new(.01,[new(1,new(.1,.5)),new(2,new(.9,.5))]));Assert.Equal(GestureKind.Tap,Assert.Single(r.Process(new(.1,[]))).Kind);}
    private sealed class FakeExecutor : IActionExecutor
    {public ActionSpec? Last;public bool Throw;public Task<ActionResult> ExecuteAsync(ActionSpec action,nint target){if(Throw)throw new InvalidOperationException("Unsupported hardware");Last=action;return Task.FromResult(ActionResult.Ok());}}
}
