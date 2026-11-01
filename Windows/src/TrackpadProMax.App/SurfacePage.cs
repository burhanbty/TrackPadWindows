using System.Text.Json;
using TrackpadProMax.Core;
using TrackpadProMax.Actions;

namespace TrackpadProMax.App;
public sealed partial class MainWindow
{
    private bool surfaceActive,zoneTesting,zoneActions,surfaceBusy;
    private System.Threading.CancellationTokenSource surfaceCancellation=new();
    private SurfaceTracker? surfaceTracker;
    private readonly PhysicalZoneTest[] zoneTests=SurfaceSettings.Defaults().Select(z=>new PhysicalZoneTest(z.Zone)).ToArray();
    private TouchpadMap? surfaceMap;
    private TextBlock? surfaceFeedback,surfaceState,zoneTestText;
    private SurfaceEvent? lastSurfaceEvent;
    private string surfaceResult="Henüz eylem yok";
    private void StopSurface(string reason)
    {
        surfaceCancellation.Cancel();surfaceCancellation.Dispose();surfaceCancellation=new();
        surfaceActive=false;zoneTesting=false;zoneActions=false;surfaceTracker?.Cancel();lastSurfaceEvent=null;
        foreach(var test in zoneTests)test.Stop();
        Record(new(DateTimeOffset.Now,EvidenceSource.System,"Yüzey oturumu kapandı · "+reason));
        surfaceResult=reason;
        RefreshSurface();
    }
    private void ArmSurface()
    {
        if(!IsActive || paused){status.Text="Pencereyi etkinleştirin ve duraklatmayı kaldırın.";return;}
        StopSurface("Yeni oturum");surfaceTracker=new(config.Surface);
        if(lastFrame is {Contacts.Count:>0})surfaceTracker.Cancel();
        surfaceActive=true;SetNative(true);
        if(native?.IsRunning!=true){StopSurface("Yerel giriş kaydı başarısız; yüzey kullanılamıyor.");return;}
        surfaceResult="ETKİN · fiziksel teslim henüz doğrulanmadı · Esc ile kapatın";RefreshSurface();
    }
    private async Task ProcessSurfaceAsync(TouchFrame frame)
    {
        if(!(surfaceActive || zoneTesting) || paused || !IsActive || setupVisible)return;
        surfaceTracker??=new(config.Surface);
        foreach(var e in surfaceTracker.Process(frame))
        {
            lastSurfaceEvent=e;
            foreach(var test in zoneTests)test.Observe(e,EvidenceSource.NativePointer);
            Record(new(DateTimeOffset.Now,EvidenceSource.NativePointer,$"BÖLGE · {TouchpadMap.Label(e.Zone)} · {e.Kind} · {e.Direction}"+(e.Action is null?"":" · istek: "+TouchpadMap.Describe(e.Action)),Surface:e));
            RefreshSurface();
            if(e.Action is null)continue;
            if(!surfaceActive && !(zoneTesting && zoneActions))
            {surfaceResult="Önizleme: "+TouchpadMap.Describe(e.Action)+" · dış eylem çalıştırılmadı";Record(new(DateTimeOffset.Now,EvidenceSource.System,surfaceResult));RefreshSurface();continue;}
            if(surfaceBusy || busy || WindowsActions.ActivationModifiersDown())
            {surfaceResult="Eylem atlandı · işlem meşgul / değiştirici tuş basılı";RefreshSurface();continue;}
            surfaceBusy=true;
            try
            {
                // No deferred action queue. Focus loss/disable cancels subsequent frames.
                ActionResult result;
                if(e.Action.Kind=="keys")
                {
                    // Sending keys intentionally transfers focus. End this surface
                    // session first; the executor rechecks the target after activation.
                    StopSurface("Hedef uygulamaya kısayol aktarımı");SetNative(false);
                    result=await actions.ExecuteAsync(e.Action,target);
                }
                else result=await actions.ExecuteAsync(e.Action,target,surfaceCancellation.Token);
                surfaceResult=(result.Success?"✓ API sonucu: ":"! Eylem başarısız: ")+result.Message;
                Record(new(DateTimeOffset.Now,EvidenceSource.System,$"BÖLGE EYLEM SONUCU · {e.Zone} · {surfaceResult}"));
            }
            finally{surfaceBusy=false;RefreshSurface();}
        }
    }
    private void RefreshSurface()
    {
        bool stale=(DateTimeOffset.Now-lastContactAt).TotalSeconds>1;
        if(surfaceMap is not null)
        {
            surfaceMap.Contacts=!stale && lastFrame is {Cancelled:false} f?f.Contacts:[];
            surfaceMap.AnchorId=lastSurfaceEvent?.ContactId;
            if(surfaceActive || zoneTesting)surfaceMap.Selected=lastSurfaceEvent?.Zone??Zone.Center;
            surfaceMap.InvalidateVisual();
        }
        if(surfaceState is not null)surfaceState.Text=$"{(surfaceActive?"YÜZEY KONTROLÜ AÇIK · gerçek eylemler":zoneTesting?"BÖLGE TESTİ":"YÜZEY KONTROLÜ KAPALI")} · yalnızca ön plan\n{surfaceResult}\nKoordinatlı çerçeve: {native?.PositionFrames??0} · WM_POINTER: {native?.WmPointerMessages??0} · Controller: {native?.ControllerCallbacks??0}";
        if(surfaceFeedback is not null)surfaceFeedback.Text=(lastSurfaceEvent is null?"Bölge hareketi bekleniyor":$"{TouchpadMap.Label(lastSurfaceEvent.Zone)} · {lastSurfaceEvent.Kind} · {lastSurfaceEvent.Direction} · çapa #{lastSurfaceEvent.ContactId}")+"\n"+surfaceResult;
        if(zoneTestText is not null)zoneTestText.Text=string.Join("\n",zoneTests.Select(t=>$"{TouchpadMap.Label(t.Zone)}: {t.Status}"));
    }
    private void SurfacePage()
    {
        BeginPage("Yüzey kontrolü","Surface Mode: touchpad kenarlarını ses/parlaklık/medya kontrolüne dönüştürür. Başlatınca çalışır; Esc veya başka pencereye geçmek durdurur.");
        surfaceState=Text("");page.Children.Add(Card(surfaceState));
        page.Children.Add(Row(Button("Yüzey kontrolünü başlat",ArmSurface),Button("Durdur · normal touchpad",()=>{StopSurface("Kullanıcı kapattı");SetNative(false);}),Button("Fiziksel bölge testleri",ZoneLaboratoryPage)));
        page.Children.Add(Text("İmleci bu pencerenin üzerine getirin. Ayarlanan sayıda parmakla başlayın; bölgedeki en dış temas çapa olarak kilitlenir (kalın halka), diğer parmaklar merkezde durabilir. Eylem için çapa temasını ayarlanan eksende kaydırın. Tüm parmakları kaldırınca oturum biter. Basit köşe dokunuşları ve tek parmak koordinatları standart API yolunda yoktur.",12));
        surfaceMap=new(){Settings=config.Surface};page.Children.Add(Card(surfaceMap));
        surfaceFeedback=Text("");page.Children.Add(Card(surfaceFeedback));
        SurfaceEditor();RefreshSurface();
    }
    private void SurfaceEditor()
    {
        var draft=JsonSerializer.Deserialize<SurfaceSettings>(JsonSerializer.Serialize(config.Surface,ConfigurationStore.Options),ConfigurationStore.Options)!;
        var body=new StackPanel();body.Children.Add(Text("Görsel bölge düzenleyicisi",20));
        body.Children.Add(Text("Haritada veya listede bölge seçin. + sağ/yukarı, − sol/aşağı hareketidir. Her iki yöne farklı eylem atayabilirsiniz. Köşeler kenarlardan önce seçilir; kapalı köşeler kenar eylemine dönüşmez.",12));
        var map=new TouchpadMap{Settings=draft};body.Children.Add(map);
        var selection=new ComboBox{ItemsSource=draft.Zones,DisplayMemberPath="Zone",MinWidth=180};
        var fields=new StackPanel();
        selection.SelectionChanged+=(_,_)=>
        {
            fields.Children.Clear();if(selection.SelectedItem is not SurfaceZone z)return;
            map.Selected=z.Zone;map.InvalidateVisual();
            var enabled=new CheckBox{Content="Bölge etkin",IsChecked=z.Enabled};enabled.Checked+=(_,_)=>{z.Enabled=true;map.InvalidateVisual();};enabled.Unchecked+=(_,_)=>{z.Enabled=false;map.InvalidateVisual();};fields.Children.Add(enabled);
            void Number(string label,double value,Action<double> change,double min,double max)
            {var caption=Text(label+": "+value.ToString("F3"),12);var slider=new Slider{Minimum=min,Maximum=max,Value=value,Width=280,HorizontalAlignment=HorizontalAlignment.Left,Margin=new(0,0,0,12)};slider.ValueChanged+=(_,_)=>{change(slider.Value);caption.Text=label+": "+slider.Value.ToString("F3");map.InvalidateVisual();};fields.Children.Add(caption);fields.Children.Add(slider);}
            Number("Genişlik (yüzey oranı)",z.Width,v=>z.Width=v,.05,.4);Number("Yükseklik (yüzey oranı)",z.Height,v=>z.Height=v,.05,.4);
            var axis=new ComboBox{ItemsSource=Enum.GetValues<SurfaceAxis>(),SelectedItem=z.Axis,Width=170};axis.SelectionChanged+=(_,_)=>z.Axis=(SurfaceAxis)axis.SelectedItem;fields.Children.Add(Row(Text("Hareket ekseni",12),axis));
            var repeat=new CheckBox{Content="Sürekli / tekrarlanan kontrol (kapalıysa kaldırana kadar tek eylem)",IsChecked=z.Repeat};repeat.Checked+=(_,_)=>z.Repeat=true;repeat.Unchecked+=(_,_)=>z.Repeat=false;fields.Children.Add(repeat);
            void ActionEditor(string label,ActionSpec spec)
            {
                fields.Children.Add(Text(label,14));
                var kind=new ComboBox{ItemsSource=new[]{"brightness","volume","seek","media","keys","wheel","mode","window","launch","url","settings","touchpadsettings","taskmanager","lock"},SelectedItem=spec.Kind,Width=160};
                var value=new TextBox{Text=spec.Value,MinWidth=190};
                kind.SelectionChanged+=(_,_)=>spec.Kind=(string)kind.SelectedItem;
                value.TextChanged+=(_,_)=>spec.Value=value.Text;
                fields.Children.Add(Row(kind,value));
            }
            ActionEditor("+ sağ / yukarı",z.Positive);ActionEditor("− sol / aşağı",z.Negative);
        };
        map.ZoneSelected+=z=>selection.SelectedItem=draft.Zones.First(s=>s.Zone==z);
        body.Children.Add(selection);body.Children.Add(fields);selection.SelectedIndex=0;
        body.Children.Add(Text("Eylem değerleri: brightness / volume → yüzde puanı farkı (2 / -2); seek → saniye farkı (2 / -2); media → next / previous / play / mute; keys → Ctrl+T; mode → mod adı. Zaman çizelgesi oynatıcı desteğine, parlaklık OEM WMI desteğine bağlıdır.",12));
        var globals=new StackPanel();
        var fingers=new ComboBox{ItemsSource=new[]{1,2,3,4},SelectedItem=draft.Fingers,Width=80};fingers.SelectionChanged+=(_,_)=>draft.Fingers=(int)fingers.SelectedItem;
        globals.Children.Add(Row(Text("Gerekli parmak (1: standart API sağlamaz)",12),fingers));
        var flexible=new CheckBox{Content="Başlangıçta ek parmakları kabul et (seçilen sayı ile 4 arasında)",IsChecked=draft.AcceptExtraFingers};
        flexible.Checked+=(_,_)=>draft.AcceptExtraFingers=true;flexible.Unchecked+=(_,_)=>draft.AcceptExtraFingers=false;globals.Children.Add(flexible);
        void GlobalSlider(string name,double value,double min,double max,Action<double> changed)
        {var label=Text(name+": "+value.ToString("F3"),12);var slider=new Slider{Minimum=min,Maximum=max,Value=value,Width=280,HorizontalAlignment=HorizontalAlignment.Left,Margin=new(0,0,0,12)};slider.ValueChanged+=(_,_)=>{changed(slider.Value);label.Text=name+": "+slider.Value.ToString("F3");};globals.Children.Add(label);globals.Children.Add(slider);}
        GlobalSlider("Başlangıç hareket eşiği",draft.MovementThreshold,.005,.3,v=>draft.MovementThreshold=v);
        GlobalSlider("Tekrar adımı (yüzey oranı)",draft.StepDistance,.005,.3,v=>draft.StepDistance=v);
        GlobalSlider("Hassasiyet",draft.Sensitivity,.1,5,v=>draft.Sensitivity=v);
        GlobalSlider("En kısa başlangıç süresi (s)",draft.MinimumSeconds,0,2,v=>draft.MinimumSeconds=v);
        GlobalSlider("Tekrar aralığı (s)",draft.RepeatSeconds,.08,2,v=>draft.RepeatSeconds=v);
        GlobalSlider("Çapraz hareket sınırı",draft.CrossAxisLimit,.01,.4,v=>draft.CrossAxisLimit=v);
        GlobalSlider("Bölge çıkış toleransı",draft.ExitTolerance,0,.1,v=>draft.ExitTolerance=v);
        GlobalSlider("Ani sıçrama sınırı",draft.MaximumJump,.05,1,v=>draft.MaximumJump=v);
        GlobalSlider("En uzun temas oturumu (s)",draft.MaximumSeconds,1,60,v=>draft.MaximumSeconds=v);
        globals.Children.Add(Button("Bölgeleri ve eşikleri kaydet",()=>
        {
            try{draft.Validate();StopSurface("Ayarlar değişti");SetNative(false);config.Surface=draft;if(SaveConfiguration())SurfacePage();}
            catch(Exception ex){status.Text=ex.Message;}
        }));body.Children.Add(globals);page.Children.Add(Card(body));
    }
    private void ZoneLaboratory(StackPanel body)
    {
        body.Children.Add(Text("Fiziksel kenar / köşe testleri",20));
        body.Children.Add(Text($"{config.Surface.Fingers} parmakla seçili bölgede başlayın. Kalın halka ile gösterilen çapa temasını kenar boyunca / köşede ayarlanan eksende kaydırın; sonra tüm parmakları kaldırın. Bir defada tek test dinlenir. Odak kaybı testi iptal eder. Eylem sonucunu doğrulamak için Gerçek eylem testini başlat düğmesini kullanın. API kabulü fiziksel etkiyi tek başına kanıtlamaz.",12));
        var count=new ComboBox{ItemsSource=new[]{1,2,3,4},SelectedItem=config.Surface.Fingers,Width=90};
        count.SelectionChanged+=(_,_)=>{StopSurface("Parmak sayısı değişti");SetNative(false);config.Surface.Fingers=(int)count.SelectedItem;SaveConfiguration();};
        body.Children.Add(Row(Text("Test / yüzey parmak sayısı (3: controller yolu)",12),count));
        body.Children.Add(Text(config.Surface.AcceptExtraFingers?"Seçilen sayı asgaridir; başlangıçta 4 parmağa kadar kabul edilir. Temas kaybında kontrol durur; bütün parmakları kaldırıp yeniden başlayın.":"Tam olarak seçilen sayıda parmak gerekir. Temas kaybında kontrol durur.",12));
        surfaceMap=new(){Settings=config.Surface};
        surfaceFeedback=Text("");
        void StartTest(PhysicalZoneTest test,bool executeActions)
        {
            StopSurface("Bölge testi başlatıldı");zoneActions=executeActions;
            zoneTesting=true;surfaceTracker=new(config.Surface);if(lastFrame is {Contacts.Count:>0})surfaceTracker.Cancel();
            test.Arm();SetNative(true);surfaceResult=executeActions?"GERÇEK EYLEM TESTİ · atanmış eylemler çalışır":"ÖNİZLEME · eylemler çalışmaz";
            Record(new(DateTimeOffset.Now,EvidenceSource.System,$"Fiziksel bölge testi dinleniyor · {test.Zone} · en az {config.Surface.Fingers} parmak · {surfaceResult}"));RefreshSurface();
        }
        var selected=new ComboBox{ItemsSource=zoneTests,DisplayMemberPath="Zone",SelectedItem=zoneTests.First(t=>t.Zone==Zone.RightEdge),MinWidth=170};
        var assignment=Text("",12);
        void DescribeSelection()
        {var z=config.Surface.Zones.First(z=>z.Zone==((PhysicalZoneTest)selected.SelectedItem).Zone);assignment.Text=$"{TouchpadMap.Label(z.Zone)}: + {TouchpadMap.Describe(z.Positive)} / − {TouchpadMap.Describe(z.Negative)}";}
        selected.SelectionChanged+=(_,_)=>DescribeSelection();DescribeSelection();
        surfaceMap.ZoneSelected+=z=>selected.SelectedItem=zoneTests.First(t=>t.Zone==z);
        body.Children.Add(Text("Önizleme sadece tanımayı gösterir. Gerçek eylem testi ses/parlaklık/oynatıcı dahil seçili bölgenin eylemini uygular.",12));
        body.Children.Add(Row(selected,Button("Gerçek eylem testini başlat",()=>StartTest((PhysicalZoneTest)selected.SelectedItem,true)),Button("Önizlemeyi başlat",()=>StartTest((PhysicalZoneTest)selected.SelectedItem,false))));
        body.Children.Add(assignment);
        var buttons=new WrapPanel();foreach(var test in zoneTests)buttons.Children.Add(Button("Önizleme · "+TouchpadMap.Label(test.Zone),()=>{selected.SelectedItem=test;StartTest(test,false);}));
        body.Children.Add(buttons);body.Children.Add(surfaceFeedback);body.Children.Add(Card(surfaceMap));body.Children.Add(Button("Bölge testini durdur",()=>{StopSurface("Test durduruldu");SetNative(false);}));
        zoneTestText=Text("");body.Children.Add(zoneTestText);RefreshSurface();
    }
    private async void ZoneLaboratoryPage()
    {
        BeginPage("Bölge Laboratuvarı","Yalnızca gerçek cihaz temasları fiziksel testleri tamamlar. Fare ile harita seçimi temas üretmez.");
        var body=new StackPanel();labView=body;page.Children.Add(body);
        providerText=Text("");body.Children.Add(Card(providerText));
        testText=null;contactCanvas=null;contactText=null;
        body.Children.Add(Row(Button("Yerel girişi aç",()=>SetNative(true)),Button("Kapat",()=>SetNative(false)),Button("JSON kanıtını dışa aktar",ExportLaboratory),Button("Genel Laboratuvar",LaboratoryPage)));
        ZoneLaboratory(body);
        eventText=JsonEditor("");eventText.Height=160;eventText.IsReadOnly=true;body.Children.Add(eventText);
        RefreshLaboratory();
        report??=await TrackpadProMax.Diagnostics.DiagnosticsCollector.CollectAsync();RefreshLaboratory();
    }
    private readonly System.Windows.Threading.DispatcherTimer probeTimer=new(){Interval=TimeSpan.FromSeconds(1)};
    public void BeginPhysicalProbe(string path)
    {
        path=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        ZoneLaboratoryPage();SetNative(true);
        probeTimer.Tick+=(_,_)=>
        {
            var snapshot=new{At=DateTimeOffset.Now,Foreground=IsActive,NativeRunning=native?.IsRunning,Native=native?.Status,Controller=controller?.Status,
                native?.WmPointerMessages,native?.ControllerCallbacks,native?.PositionFrames,native?.ReadErrors,native?.ReceivedFrames,
                ZoneTests=zoneTests.Select(t=>new{t.Zone,t.Status,t.Armed}),SurfaceSettings=config.Surface,SurfaceActive=surfaceActive,ZoneActionsEnabled=zoneTesting&&zoneActions,SurfaceResult=surfaceResult,RecognitionEvents=recognitionJournal.Events,Events=journal.Events};
            try{File.WriteAllText(path,JsonSerializer.Serialize(snapshot,ConfigurationStore.Options));}catch(IOException){/* A reader can momentarily hold the local evidence file. */}
        };
        probeTimer.Start();Closed+=(_,_)=>probeTimer.Stop();
    }
    public void OpenSurfaceControls()=>SurfacePage();
}
