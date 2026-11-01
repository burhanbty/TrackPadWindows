using TrackpadProMax.Core;
using Xunit;

namespace TrackpadProMax.Tests;
public class Phase2Tests
{
    [Fact]public void LegacyConfigurationRetainsBindingsAndAddsAppearanceDefaults()
    {
        var old=ConfigurationStore.Serialize(Configuration.Default());
        var node=System.Text.Json.Nodes.JsonNode.Parse(old)!;node.AsObject().Remove("Appearance");
        foreach(var b in node["Bindings"]!.AsArray())b!.AsObject().Remove("Input");
        var loaded=ConfigurationStore.Deserialize(node.ToJsonString());
        Assert.Equal("Ctrl+Alt+F8",loaded.Bindings[0].Shortcut);Assert.Equal("Dark",loaded.Appearance.Theme);
        Assert.True(loaded.Bindings[0].UsesShortcut);Assert.False(loaded.Bindings[0].UsesNative);
    }
    [Fact]public void AppearanceSurvivesSaveAndRestartWithRecoverableBackup()
    {
        string folder=Path.Combine(Path.GetTempPath(),"TrackpadPhase2-"+Guid.NewGuid());Directory.CreateDirectory(folder);
        try
        {
            string path=Path.Combine(folder,"settings.json");var c=Configuration.Default();ConfigurationStore.Save(path,c);
            c.Appearance=new(){Theme="Light",Accent="#C055AA",Transparency=.35,Animations=false,Backdrop=false};ConfigurationStore.Save(path,c);
            var restarted=ConfigurationStore.Deserialize(File.ReadAllText(path));Assert.Equal("Light",restarted.Appearance.Theme);Assert.Equal(.35,restarted.Appearance.Transparency);Assert.False(restarted.Appearance.Animations);Assert.False(restarted.Appearance.Backdrop);Assert.Equal("#C055AA",restarted.Appearance.Accent);
            Assert.Equal("Dark",ConfigurationStore.Deserialize(File.ReadAllText(path+".bak")).Appearance.Theme);
        }finally{Directory.Delete(folder,true);}
    }
    [Theory][InlineData("#oops",.2)][InlineData("#FFFFFF",double.NaN)][InlineData("#FFFFFF",.9)]
    public void InvalidAppearanceIsRejected(string accent,double transparency)=>Assert.Throws<ArgumentException>(()=>new AppearanceSettings{Accent=accent,Transparency=transparency}.Validate());
    [Fact]public void AutomaticRouteCannotDispatchBothShortcutAndNative()
    {
        var b=new Binding();Assert.True(b.UsesShortcut);Assert.False(b.UsesNative);
        b.Shortcut="";Assert.False(b.UsesShortcut);Assert.True(b.UsesNative);
        b.Shortcut="Ctrl+Alt+F8";b.Input=InputRoute.Native;Assert.False(b.UsesShortcut);Assert.True(b.UsesNative);
        b.Input=InputRoute.Shortcut;Assert.True(b.UsesShortcut);Assert.False(b.UsesNative);
    }
    [Fact]public void NativeBindingsDoNotReserveDormantShortcut()
    {
        var c=Configuration.Default();c.Bindings.Add(new(){Shortcut="Ctrl+Alt+F8",Input=InputRoute.Native});c.Validate();
        c.Bindings[^1].Input=InputRoute.Shortcut;Assert.Throws<ArgumentException>(c.Validate);
    }
    [Fact]public void ShortcutRouteRequiresChord()
    {var c=Configuration.Default();c.Bindings[0].Input=InputRoute.Shortcut;c.Bindings[0].Shortcut="";Assert.Throws<ArgumentException>(c.Validate);}
    [Fact]public void SimulationAndShortcutsCannotCompletePhysicalTest()
    {
        var test=new PhysicalTest("Tap",GestureKind.Tap,3);test.Arm();
        foreach(var source in new[]{EvidenceSource.Shortcut,EvidenceSource.Simulation,EvidenceSource.System})test.Observe(new(DateTimeOffset.Now,source,"Test",3,Gesture:GestureKind.Tap));
        Assert.True(test.Armed);Assert.StartsWith("Dinleniyor",test.Status);
        test.Observe(new(DateTimeOffset.Now,EvidenceSource.NativeController,"Test",4,Gesture:GestureKind.Tap));Assert.True(test.Armed);
        test.Observe(new(DateTimeOffset.Now,EvidenceSource.NativeController,"Test",3,Gesture:GestureKind.Tap));Assert.False(test.Armed);Assert.StartsWith("Yerel olay alındı",test.Status);
    }
    [Fact]public void UnarmedTestsRemainPendingAndContactTestRequiresRealContacts()
    {
        var test=new PhysicalTest("Scroll",null,2,true);var e=new LaboratoryEvent(DateTimeOffset.Now,EvidenceSource.NativePointer,"Test",2,[new(1,new(.1,.2)),new(2,new(.5,.6))]);
        test.Observe(e);Assert.StartsWith("Bekliyor",test.Status);test.Arm();test.Observe(e with{Contacts=null});Assert.True(test.Armed);test.Observe(e);Assert.False(test.Armed);
    }
    [Fact]public void JournalIsBoundedAndPreservesUnknownContactData()
    {
        var log=new LaboratoryJournal(3);for(int i=0;i<20;i++)log.Add(new(DateTimeOffset.Now,EvidenceSource.Shortcut,i.ToString()));
        Assert.Equal(3,log.Events.Count);Assert.Equal("17",log.Events[0].Detail);Assert.All(log.Events,e=>{Assert.Null(e.Fingers);Assert.Null(e.Contacts);});Assert.Equal(0,log.NativeFrames);
    }
}
