using System.Text.Json.Nodes;
using TrackpadProMax.Core;
using Xunit;

namespace TrackpadProMax.Tests;
public class SurfaceTests
{
    private static Contact[] Contacts(double x,double y)=>[new(10,new(x,y)),new(20,new(Math.Min(.99,x+.01),Math.Min(.99,y+.01)))];
    private static TouchFrame Frame(double t,double x,double y)=>new(t,Contacts(x,y));
    private static IEnumerable<SurfaceEvent> Triggers(IEnumerable<SurfaceEvent> events)=>events.Where(e=>e.Kind==SurfaceEventKind.Triggered);
    [Theory]
    [InlineData(.05,.5,.05,.4,Zone.LeftEdge,"brightness","2")]
    [InlineData(.95,.5,.95,.4,Zone.RightEdge,"volume","2")]
    [InlineData(.5,.05,.6,.05,Zone.TopEdge,"media","next")]
    [InlineData(.5,.95,.6,.95,Zone.BottomEdge,"seek","2")]
    [InlineData(.05,.05,.13,.05,Zone.TopLeft,"media","play")]
    [InlineData(.95,.05,.87,.05,Zone.TopRight,"media","mute")]
    [InlineData(.05,.95,.13,.95,Zone.BottomLeft,"wheel","")]
    [InlineData(.95,.95,.87,.95,Zone.BottomRight,"mode","Normal")]
    public void EveryRegionDispatchesFromNormalizedDeviceContacts(double x,double y,double endX,double endY,Zone zone,string kind,string value)
    {
        var r=new SurfaceTracker(new());Assert.Equal(zone,Assert.Single(r.Process(Frame(0,x,y))).Zone);
        var e=Assert.Single(Triggers(r.Process(Frame(.15,endX,endY))));Assert.Equal(zone,e.Zone);Assert.Equal(kind,e.Action!.Kind);Assert.Equal(value,e.Action.Value);
        Assert.Equal(SurfaceEventKind.Ended,Assert.Single(r.Process(new(.2,[]))).Kind);
    }
    [Fact]public void DefaultSettingsMigrateWithoutChangingExistingBindings()
    {
        var json=JsonNode.Parse(ConfigurationStore.Serialize(Configuration.Default()))!;json.AsObject().Remove("Surface");
        var c=ConfigurationStore.Deserialize(json.ToJsonString());Assert.Equal(8,c.Surface.Zones.Count);Assert.Equal(2,c.Surface.Fingers);Assert.Equal(2,c.Bindings.Count);Assert.Equal(3,c.Profiles.Count);Assert.Equal(1,c.Version);
    }
    [Fact]public void ZoneConfigurationSurvivesSaveAndPreservesBackup()
    {
        var folder=Path.Combine(Path.GetTempPath(),"SurfaceTest-"+Guid.NewGuid());Directory.CreateDirectory(folder);
        try
        {
            var file=Path.Combine(folder,"settings.json");var c=Configuration.Default();ConfigurationStore.Save(file,c);
            c.Surface.Zones[0].Positive=new(){Kind="keys",Value="Ctrl+P"};c.Surface.Zones[0].Width=.3;c.Surface.Sensitivity=2.1;
            ConfigurationStore.Save(file,c);var loaded=ConfigurationStore.Deserialize(File.ReadAllText(file));Assert.Equal("Ctrl+P",loaded.Surface.Zones[0].Positive.Value);Assert.Equal(.3,loaded.Surface.Zones[0].Width);Assert.Equal(2.1,loaded.Surface.Sensitivity);
            Assert.Equal("brightness",ConfigurationStore.Deserialize(File.ReadAllText(file+".bak")).Surface.Zones[0].Positive.Kind);
        }
        finally{Directory.Delete(folder,true);}
    }
    [Theory][InlineData(0)][InlineData(5)][InlineData(-1)]
    public void InvalidFingerCountRejected(int n)=>Assert.Throws<ArgumentException>(()=>new SurfaceSettings{Fingers=n}.Validate());
    [Theory][InlineData(double.NaN)][InlineData(double.PositiveInfinity)][InlineData(0)][InlineData(9)]
    public void InvalidSensitivityRejected(double n)=>Assert.Throws<ArgumentException>(()=>new SurfaceSettings{Sensitivity=n}.Validate());
    [Fact]public void NullAndDuplicateZonesAndInvalidActionsRejected()
    {
        var s=new SurfaceSettings();s.Zones[0].Zone=Zone.TopLeft;Assert.Throws<ArgumentException>(s.Validate);
        s=new();s.Zones[0].Width=.45;Assert.Throws<ArgumentException>(s.Validate);
        s=new();s.Zones[0].Positive.Kind="unknown";Assert.Throws<ArgumentException>(s.Validate);
        Assert.Throws<ArgumentException>(()=>ConfigurationStore.Deserialize("{\"Surface\":null}"));
    }
    [Fact]public void DisabledCornerDoesNotFallThroughToEdge()
    {
        var s=new SurfaceSettings();s.Zones.First(z=>z.Zone==Zone.TopLeft).Enabled=false;var r=new SurfaceTracker(s);
        Assert.Empty(r.Process(Frame(0,.05,.05)));Assert.Empty(r.Process(Frame(.2,.05,.4)));Assert.Empty(r.Process(new(.3,[])));
    }
    [Fact]public void EnteringEdgeFromCenterDoesNotActivate()
    {var r=new SurfaceTracker(new());Assert.Empty(r.Process(Frame(0,.5,.5)));Assert.Empty(r.Process(Frame(.2,.95,.5)));Assert.Empty(r.Process(Frame(.4,.95,.3)));}
    [Fact]public void ThresholdDwellAndDirectionPreventAccidentalTrigger()
    {
        var r=new SurfaceTracker(new());r.Process(Frame(0,.05,.5));Assert.Empty(r.Process(Frame(.03,.05,.42)));
        Assert.Empty(r.Process(Frame(.2,.05,.48)));Assert.Empty(r.Process(Frame(.3,.10,.47)));Assert.DoesNotContain(r.Process(new(.4,[])),e=>e.Action is not null);
    }
    [Fact]public void ContinuousControlThrottlesCoalescesAndReverses()
    {
        var r=new SurfaceTracker(new());r.Process(Frame(0,.95,.6));Assert.Equal("2",Assert.Single(Triggers(r.Process(Frame(.2,.95,.54)))).Action!.Value);
        Assert.Empty(r.Process(Frame(.22,.95,.49)));Assert.Equal("6",Assert.Single(Triggers(r.Process(Frame(.4,.95,.48)))).Action!.Value);
        Assert.Equal("-2",Assert.Single(Triggers(r.Process(Frame(.6,.95,.51)))).Action!.Value);
        Assert.Empty(Triggers(r.Process(Frame(.9,.95,.51))));
    }
    [Fact]public void DiscreteMediaFiresOnceUntilAllContactsLift()
    {
        var r=new SurfaceTracker(new());r.Process(Frame(0,.4,.05));Assert.Single(Triggers(r.Process(Frame(.2,.46,.05))));
        Assert.Empty(Triggers(r.Process(Frame(.5,.6,.05))));r.Process(new(.6,[]));r.Process(Frame(.7,.4,.05));Assert.Single(Triggers(r.Process(Frame(.9,.46,.05))));
    }
    [Fact]public void SequentialLiftDoesNotTeleportAnchorOrCancelCompletedGesture()
    {
        var r=new SurfaceTracker(new());r.Process(Frame(0,.95,.5));r.Process(Frame(.2,.95,.4));
        Assert.Equal(SurfaceEventKind.Releasing,Assert.Single(r.Process(new(.3,[new(20,new(.95,.41))]))).Kind);Assert.Empty(r.Process(new(.4,[new(20,new(.95,.1))])));
        Assert.Equal(SurfaceEventKind.Ended,Assert.Single(r.Process(new(.5,[]))).Kind);
    }
    [Fact]public void ReplacingContactOrAddingFingerCancelsUntilLift()
    {
        var r=new SurfaceTracker(new());r.Process(Frame(0,.95,.5));
        Assert.Equal(SurfaceEventKind.Cancelled,Assert.Single(r.Process(new(.2,[new(10,new(.95,.4)),new(30,new(.96,.41))]))).Kind);
        Assert.Empty(r.Process(Frame(.4,.95,.3)));r.Process(new(.5,[]));Assert.Equal(SurfaceEventKind.Began,Assert.Single(r.Process(Frame(.6,.95,.5))).Kind);
    }
    [Fact]public void TooManyFingersBlockAndOneFingerCanSettleToRequiredCount()
    {
        var r=new SurfaceTracker(new(){AcceptExtraFingers=false});Assert.Empty(r.Process(new(0,[new(10,new(.05,.5))])));Assert.Single(r.Process(Frame(.05,.05,.5)));
        Assert.Equal(SurfaceEventKind.Cancelled,Assert.Single(r.Process(new(.2,[..Contacts(.05,.4),new(30,new(.1,.4))]))).Kind);
    }
    [Theory][InlineData(.95,.05)][InlineData(.5,.4)]
    public void LeavingBandOrJumpingCancels(double x,double y)
    {var r=new SurfaceTracker(new());r.Process(Frame(0,.05,.5));Assert.Equal(SurfaceEventKind.Cancelled,Assert.Single(r.Process(Frame(.2,x,y))).Kind);}
    [Fact]public void CrossAxisAndSessionTimeoutCancel()
    {
        var s=new SurfaceSettings{ExitTolerance=.1};var r=new SurfaceTracker(s);r.Process(Frame(0,.05,.5));Assert.Equal(SurfaceEventKind.Cancelled,Assert.Single(r.Process(Frame(.2,.16,.49))).Kind);
        r=new(new(){MaximumSeconds=1});r.Process(Frame(0,.95,.5));Assert.Equal(SurfaceEventKind.Cancelled,Assert.Single(r.Process(Frame(2,.95,.4))).Kind);
    }
    [Fact]public void CancelAndBadCoordinatesNeverDispatch()
    {
        var r=new SurfaceTracker(new());r.Process(Frame(0,.95,.5));r.Cancel();Assert.Empty(r.Process(Frame(.2,.95,.4)));r.Process(new(.3,[]));r.Process(Frame(.4,.95,.5));
        Assert.Equal(SurfaceEventKind.Cancelled,Assert.Single(r.Process(Frame(.6,double.NaN,.4))).Kind);Assert.Empty(r.Process(Frame(.8,.95,.3)));
    }
    [Fact]public void NativeCancellationAndClockRegressionCancel()
    {
        var r=new SurfaceTracker(new());r.Process(Frame(1,.95,.5));Assert.Equal(SurfaceEventKind.Cancelled,Assert.Single(r.Process(new(1.1,[],true))).Kind);
        r.Process(new(1.2,[]));r.Process(Frame(1.3,.95,.5));Assert.Equal(SurfaceEventKind.Cancelled,Assert.Single(r.Process(Frame(1.25,.95,.4))).Kind);
    }
    [Fact]public void SynthesizedInitialDownRebasesOnceWithoutActionOrBurst()
    {
        var r=new SurfaceTracker(new());r.Process(Frame(0,.83,.5) with{Initial=true});
        var rebased=Assert.Single(r.Process(Frame(.3,.96,.35)));Assert.Equal(SurfaceEventKind.Began,rebased.Kind);Assert.Null(rebased.Action);
        Assert.Equal("2",Assert.Single(Triggers(r.Process(Frame(.5,.96,.29)))).Action!.Value);
        Assert.Equal(SurfaceEventKind.Cancelled,Assert.Single(r.Process(Frame(.7,.83,.28))).Kind);
    }
    [Fact]public void InitialRebaseCannotSwitchRegions()
    {
        var r=new SurfaceTracker(new());r.Process(Frame(0,.95,.5) with{Initial=true});
        Assert.Equal(SurfaceEventKind.Cancelled,Assert.Single(r.Process(Frame(.2,.5,.5))).Kind);Assert.Empty(r.Process(Frame(.4,.95,.4)));
    }
    [Fact]public void IdleCancellationDoesNotConsumeFirstPhysicalGesture()
    {
        var r=new SurfaceTracker(new());Assert.Empty(r.Process(new(0,[],true)));
        Assert.Equal(SurfaceEventKind.Began,Assert.Single(r.Process(Frame(.1,.95,.5))).Kind);
        Assert.Single(Triggers(r.Process(Frame(.3,.95,.4))));
    }
    [Fact]public void FlexibleGroupCanStartWithThreeAndAddFourthBeforeFirstAction()
    {
        var r=new SurfaceTracker(new());Contact[] Group(double y,bool fourth)=>[..Contacts(.95,y),new(30,new(.5,.5)),..(fourth?new Contact[]{new(40,new(.4,.5))}:Array.Empty<Contact>())];
        Assert.Equal(SurfaceEventKind.Began,Assert.Single(r.Process(new(0,Group(.5,false)))).Kind);
        Assert.Equal(SurfaceEventKind.Began,Assert.Single(r.Process(new(.03,Group(.49,true)))).Kind);
        Assert.Empty(Triggers(r.Process(new(.05,Group(.44,true)))));Assert.Single(Triggers(r.Process(new(.2,Group(.43,true)))));
    }
    [Fact]public void ContactLossAndAdditionalFingerAfterActivationCannotKeepAdjusting()
    {
        var r=new SurfaceTracker(new());r.Process(Frame(0,.95,.5));r.Process(Frame(.2,.95,.4));
        Assert.Equal(SurfaceEventKind.Cancelled,Assert.Single(r.Process(new(.3,[..Contacts(.95,.35),new(30,new(.5,.5))]))).Kind);
        Assert.Empty(r.Process(Frame(.5,.95,.3)));
    }
    [Fact]public void ZoneSizeAndAxisAndCustomActionsAreHonored()
    {
        var s=new SurfaceSettings();var z=s.Zones.First(z=>z.Zone==Zone.LeftEdge);z.Width=.3;z.Axis=SurfaceAxis.Horizontal;z.Positive=new(){Kind="keys",Value="Ctrl+T"};z.Repeat=false;
        var r=new SurfaceTracker(s);r.Process(Frame(0,.2,.5));Assert.Equal("Ctrl+T",Assert.Single(Triggers(r.Process(Frame(.2,.26,.5)))).Action!.Value);
    }
    [Fact]public void AnchorIdentityIgnoresFrameOrdering()
    {
        var r=new SurfaceTracker(new());r.Process(Frame(0,.95,.5));var e=Assert.Single(Triggers(r.Process(new(.2,Contacts(.95,.4).Reverse().ToArray()))));Assert.Equal(20,e.ContactId);Assert.Equal(Zone.RightEdge,e.Zone);
    }
    [Theory][InlineData(3)][InlineData(4)]
    public void ControllerContactGroupUsesOuterContactEvenIfLowestIdIsCentral(int count)
    {
        var r=new SurfaceTracker(new(){Fingers=count});
        Contact[] Group(double y)=>Enumerable.Range(1,count).Select(id=>new Contact(id,new(id==count?.96:.4+id*.05,id==count?y:.5))).ToArray();
        var began=Assert.Single(r.Process(new(0,Group(.5))));Assert.Equal(count,began.ContactId);Assert.Equal(Zone.RightEdge,began.Zone);
        Assert.Equal("volume",Assert.Single(Triggers(r.Process(new(.2,Group(.4))))).Action!.Kind);
    }
    [Fact]public void PhysicalTestRejectsSyntheticShortcutControllerAndIncompleteSessions()
    {
        var test=new PhysicalZoneTest(Zone.RightEdge);test.Arm();
        SurfaceEvent[] events=[new(SurfaceEventKind.Began,Zone.RightEdge,new(.95,.5),1),new(SurfaceEventKind.Triggered,Zone.RightEdge,new(.95,.4),1,"Yukarı",new(){Kind="volume",Value="2"}),new(SurfaceEventKind.Ended,Zone.RightEdge,new(.95,.4),1)];
        foreach(var source in new[]{EvidenceSource.Simulation,EvidenceSource.Shortcut,EvidenceSource.NativeController,EvidenceSource.System})foreach(var e in events)test.Observe(e,source);
        Assert.True(test.Armed);test.Observe(events[2],EvidenceSource.NativePointer);Assert.True(test.Armed);
        foreach(var e in events)test.Observe(e,EvidenceSource.NativePointer);Assert.False(test.Armed);Assert.StartsWith("Yerel bölge",test.Status);
    }
    [Fact]public void CancelledZoneTestCannotPassOnLift()
    {
        var test=new PhysicalZoneTest(Zone.TopLeft);test.Arm();
        foreach(var kind in new[]{SurfaceEventKind.Began,SurfaceEventKind.Triggered,SurfaceEventKind.Cancelled,SurfaceEventKind.Ended})test.Observe(new(kind,Zone.TopLeft,new(.05,.05),1),EvidenceSource.NativePointer);
        Assert.True(test.Armed);test.Stop();Assert.False(test.Armed);Assert.StartsWith("İptal",test.Status);
    }
    [Theory][InlineData("volume","0")][InlineData("volume","101")][InlineData("seek","1.2")][InlineData("seek","NaN")]
    public void InvalidContinuousActionsRejected(string kind,string value)=>Assert.Throws<ArgumentException>(()=>Configuration.ValidateAction(new(){Kind=kind,Value=value}));
}
