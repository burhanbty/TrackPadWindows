using System.Text.Json;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using TrackpadProMax.Core;
using TrackpadProMax.Diagnostics;
using TrackpadProMax.Input;
using TrackpadProMax.Actions;

namespace TrackpadProMax.App;
public sealed partial class MainWindow
{
    private readonly LaboratoryJournal journal=new();
    private readonly LaboratoryJournal recognitionJournal=new();
    private readonly PhysicalTest[] physicalTests=[new("Tek parmak dokunuş · yerel API bu olayı sağlamayabilir",GestureKind.Tap,1),new("İki parmak kaydırma / scroll · temas akışı",null,2,true),new("Üç parmak dokunuş",GestureKind.Tap,3),new("Dört parmak dokunuş",GestureKind.Tap,4),new("Üç parmak sola kaydırma",GestureKind.SwipeLeft,3),new("Dört parmak sağa kaydırma",GestureKind.SwipeRight,4)];
    private readonly DispatcherTimer labTimer=new(){Interval=TimeSpan.FromMilliseconds(120)};
    private ForegroundGestureController? controller;
    private bool nativeWanted;
    private StackPanel? labView;
    private bool IsLaboratoryVisible=>labView is not null && page.Children.Contains(labView);
    private Canvas? contactCanvas;
    private TextBlock? providerText,contactText,testText;
    private TextBox? eventText;
    private TouchFrame? lastFrame;
    private string backdropStatus="";
    private bool setupVisible;
    private Shortcut? verificationChord;
    private TextBlock? verificationText;
    private bool shortcutObserved;
    private readonly List<string> shortcutEvidence=[];
    private void Record(LaboratoryEvent e)
    {
        journal.Add(e);foreach(var test in physicalTests)test.Observe(e);
        if(e.Contacts is null)recognitionJournal.Add(e);
    }
    private async void OnControllerGesture(Gesture gesture)
    {
        Record(new(DateTimeOffset.Now,EvidenceSource.NativeController,"Yerel çok parmak dokunuşu · koordinat yok",gesture.Fingers,Gesture:gesture.Kind));
        if(paused || !IsActive || IsLaboratoryVisible || setupVisible || surfaceActive || zoneTesting)return;
        var binding=config.Resolve(WindowsActions.ProcessName(target),b=>b.UsesNative && b.Gesture==gesture.Kind && b.Fingers==gesture.Fingers && b.Zone==Zone.Anywhere);
        if(binding is not null)await Execute(binding.Action);
    }
    private void SetNative(bool enabled)
    {
        nativeWanted=enabled;
        if(enabled){native?.Start();if(IsActive)controller?.Start();}
        else {if(surfaceActive || zoneTesting)StopSurface("Yerel giriş kapatıldı");controller?.Stop();native?.Stop();nativeRecognizer?.Cancel();lastFrame=null;}
        status.Text=(native?.Status??"API hazırlanmadı")+" · "+(controller?.Status??"");
        Record(new(DateTimeOffset.Now,EvidenceSource.System,$"Giriş oturumu · istek={enabled} · ön plan={IsActive} · WM_POINTER kayıt={native?.IsRunning} · Controller etkin={controller?.IsRunning}"));
    }
    private async void LaboratoryPage()
    {
        BeginPage("Touchpad Laboratuvarı","Gerçek olayları gözlemleyin. Bu sayfada eşleme eylemleri çalıştırılmaz; simülasyon ayrı etiketlenir. Fiziksel testler otomatik olarak yapılmış sayılmaz.");
        var body=new StackPanel();labView=body;page.Children.Add(body);
        providerText=Text("Donanım okunuyor…");body.Children.Add(Card(providerText));
        body.Children.Add(Row(Button("Yerel girişi aç",()=>SetNative(true)),Button("Yerel girişi kapat",()=>SetNative(false)),Button("Yerel raporu dışa aktar",ExportLaboratory)));
        body.Children.Add(Text("Önce ‘Yerel girişi aç’ seçin, pencereyi ön planda tutun ve imleci aşağıdaki yüzeyin üzerine getirin. Windows hareketleri bu sırada uygulamaya yönlendirilebilir. Kapatmak normal Windows davranışını geri verir. Arka planda denetleyici devre dışıdır."));
        contactCanvas=new Canvas{Height=190,ClipToBounds=true,Background=GlassTheme.Brush("FieldBrush")};
        contactText=Text("Fiziksel temas bekleniyor. Fare konumu çizilmez; basınç/haptik bilinmiyor.",12);
        var visual=new StackPanel();visual.Children.Add(contactCanvas);visual.Children.Add(contactText);body.Children.Add(Card(visual));
        body.Children.Add(Text("Yönlendirmeli fiziksel testler",19));
        body.Children.Add(Text("Bir testi başlatın ve belirtilen hareketi fiziksel olarak yapın. Yalnızca eşleşen yerel olay sonucu tamamlar. İki parmak testi temas akışını doğrular; Windows'un scroll çıktısını ölçmez. Tek parmak dokunuşu için kısayol yolu gerekebilir.",12));
        foreach(var test in physicalTests)body.Children.Add(Button("Testi başlat · "+test.Name,()=>{test.Arm();RefreshLaboratory();}));
        testText=Text("");body.Children.Add(testText);
        body.Children.Add(Button("Özel kenar / köşe test ekranı",ZoneLaboratoryPage));
        body.Children.Add(Text("Olay akışı · en fazla 200 kayıt",19));
        eventText=JsonEditor("");eventText.Height=210;eventText.IsReadOnly=true;body.Children.Add(eventText);
        SimulationPanel();
        try { report??=await DiagnosticsCollector.CollectAsync(); }
        catch(Exception ex){Record(new(DateTimeOffset.Now,EvidenceSource.System,ex.Message));}
        RefreshLaboratory();
    }
    private void RefreshLaboratory()
    {
        if(!IsLaboratoryVisible)return;
        string devices=report is null?"Taranıyor…":string.Join(", ",report.Devices.Where(d=>d.Name.Contains("touchpad",StringComparison.OrdinalIgnoreCase)||d.Name.Contains("dokunmatik",StringComparison.OrdinalIgnoreCase)).Select(d=>d.Name));
        providerText!.Text=$"{report?.Model??"Donanım taranıyor"} · {devices}\nEtkin sağlayıcı: {(native?.IsRunning==true?"WM_POINTER ön plan":"Genel kısayollar")} / {(controller?.IsRunning==true?"3–4 parmak denetleyicisi etkin":"denetleyici kapalı")}\n{native?.Status}\n{controller?.Status}\nAPI varlığı: {native?.IsAvailable} · alınan yerel çerçeve: {native?.ReceivedFrames??0} · okuma hatası: {native?.ReadErrors??0}\nBasınç ve haptik: doğrulanmadı. Küresel ham temas erişimi: desteklenmiyor.";
        if(contactCanvas is null && surfaceMap is not null)providerText.Text=$"{report?.Model??"Donanım taranıyor"} · yalnızca ön plan · {(zoneTesting?(zoneActions?"GERÇEK EYLEMLER AÇIK":"ÖNİZLEME · eylemler kapalı"):"TEST KAPALI")}\nKayıt: WM_POINTER={native?.IsRunning} / Controller={controller?.IsRunning} · API varlığı fiziksel teslim değildir\nOlay: WM_POINTER={native?.WmPointerMessages??0} / Controller={native?.ControllerCallbacks??0} · koordinatlı çerçeve={native?.PositionFrames??0} · okuma hatası={native?.ReadErrors??0}\nTek parmak / konumlu dokunuş yok. Arka planda ham temas erişimi yok.";
        if(testText is not null)testText.Text=string.Join("\n",physicalTests.Select(t=>$"{t.Name}: {t.Status}"));
        string logs=string.Join("\n",journal.Events.Reverse().Select(e=>$"{e.At:HH:mm:ss.fff} [{SourceLabel(e.Source)}] {e.Detail}"+(e.Fingers.HasValue?$" · {e.Fingers} parmak":"")+(e.Contacts is {Count:>0}?" · "+string.Join("; ",e.Contacts.Select(c=>$"#{c.Id} ({c.Position.X:F3}, {c.Position.Y:F3})")):"")));
        if(eventText is not null && eventText.Text!=logs)eventText.Text=logs;
        if(contactCanvas is null)return;
        contactCanvas.Children.Clear();
        if(lastFrame is {Cancelled:false} frame)
        {
            // Stale observations are retained in the log, not presented as live contacts.
            bool stale=(DateTimeOffset.Now-lastContactAt).TotalSeconds>1;
            contactText!.Text=stale?"Son temas çerçevesi 1 saniyeden eski · canlı temas bilinmiyor":$"YEREL · {frame.Contacts.Count} temas · normalize cihaz koordinatları · basınç bilinmiyor";
            if(!stale)foreach(var c in frame.Contacts)
            {
                var marker=new Ellipse{Width=26,Height=26,Fill=GlassTheme.Brush("AccentBrush"),Stroke=GlassTheme.Brush("TextBrush"),StrokeThickness=1};
                Canvas.SetLeft(marker,c.Position.X*Math.Max(0,contactCanvas.ActualWidth-26));Canvas.SetTop(marker,c.Position.Y*164);contactCanvas.Children.Add(marker);
            }
        }
        else contactText!.Text=nativeWanted?"Canlı temas yok / akış iptal edildi. Yeni fiziksel çerçeve bekleniyor.":"Yerel giriş kapalı. Fiziksel temas ölçülmüyor; basınç/haptik bilinmiyor.";
    }
    private DateTimeOffset lastContactAt;
    private static string SourceLabel(EvidenceSource source)=>source switch{EvidenceSource.NativePointer=>"YEREL / Pointer API",EvidenceSource.NativeController=>"YEREL / Controller",EvidenceSource.Shortcut=>"KISAYOL / temas bilinmiyor",EvidenceSource.Simulation=>"SİMÜLASYON",_=>"SİSTEM"};
    private void ExportLaboratory()
    {
        var dialog=new SaveFileDialog{Filter="Yerel JSON raporu|*.json",FileName="TrackpadProMax-laboratory.json"};
        if(dialog.ShowDialog()!=true)return;
        try
        {
            var payload=new{At=DateTimeOffset.Now,Hardware=report,NativeProvider=native?.Status,Controller=controller?.Status,Backdrop=backdropStatus,NativeFrames=native?.ReceivedFrames,ReadErrors=native?.ReadErrors,WmPointerMessages=native?.WmPointerMessages,ControllerCallbacks=native?.ControllerCallbacks,PositionFrames=native?.PositionFrames,RejectedCoordinateFrames=native?.RejectedCoordinateFrames,SurfaceSettings=config.Surface,PhysicalZoneTests=zoneTests.Select(t=>new{t.Zone,t.Status}),SurfaceActionResult=surfaceResult,PhysicalTests=physicalTests.Select(t=>new{t.Name,t.Status}),ShortcutUserConfirmations=shortcutEvidence,RecognitionEvents=recognitionJournal.Events,Events=journal.Events,Limitations="API registration is not physical verification. Zone recognition is not action-effect verification. Shortcut source cannot prove finger count or coordinates. No pressure/haptics measured."};
            File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(payload,ConfigurationStore.Options));status.Text="Laboratuvar raporu yerel dosyaya kaydedildi.";
        }catch(Exception ex){status.Text="Dışa aktarılamadı: "+ex.Message;}
    }
    private void AppearancePanel()
    {
        var panel=new StackPanel();panel.Children.Add(Text("Liquid Glass · Görünüm",20));
        var theme=new ComboBox{ItemsSource=new[]{"Dark","Light","System"},SelectedItem=config.Appearance.Theme,Width=160};
        var accent=new TextBox{Text=config.Appearance.Accent,Width=150};
        var opacity=new Slider{Minimum=0,Maximum=.65,Value=config.Appearance.Transparency,Width=250,TickFrequency=.05,IsSnapToTickEnabled=true,Margin=new Thickness(0,6,12,12)};
        var animations=new CheckBox{Content="Hafif animasyonlar",IsChecked=config.Appearance.Animations};
        var backdrop=new CheckBox{Content="Windows DWM Acrylic (desteklenirse)",IsChecked=config.Appearance.Backdrop};
        panel.Children.Add(Text("Tema / vurgu rengi (#RRGGBB) / saydamlık (0 = opak)",12));panel.Children.Add(Row(theme,accent,opacity));panel.Children.Add(animations);panel.Children.Add(backdrop);
        panel.Children.Add(Button("Görünümü kaydet",()=>
        {
            var candidate=new AppearanceSettings{Theme=(string)theme.SelectedItem,Accent=accent.Text.Trim(),Transparency=opacity.Value,Animations=animations.IsChecked==true,Backdrop=backdrop.IsChecked==true};
            try {candidate.Validate();config.Appearance=candidate;if(SaveConfiguration())SettingsPage();}catch(Exception ex){status.Text=ex.Message;}
        }));panel.Children.Add(Text(backdropStatus+"\nWindows saydamlık, yüksek karşıtlık ve animasyon tercihleri gözetilir. Ekran görüntüsüyle bulanıklık üretilmez.",12));page.Children.Add(Card(panel));
    }
    private void SetupPage()
    {
        BeginPage("Kısayol Kurulumu","Windows hareket ayarları otomatik değiştirilmez. Üç/dört parmak hareketleri için güvenilir arka plan yolu.");setupVisible=true;
        page.Children.Add(Text("1  Hareketler sayfasında bir eşleme kaydedin. Giriş yolunu Shortcut veya kısayollu Automatic seçin.\n2  Aşağıda eşlemeyi seçip kayıt durumunu doğrulayın.\n3  Windows Ayarları → Bluetooth ve cihazlar → Dokunmatik yüzey → Gelişmiş hareketler. İlgili üç/dört parmak hareketinde Özel kısayol seçip aynı tuşları kaydedin. Bazı donanım/Windows yapılandırmaları özel kısayol sunmayabilir.\n4  Burada dinlemeyi başlatın, ardından fiziksel hareketi yapın. Klavye ile basmak yalnızca kısayol teslimini doğrular."));
        var choices=config.AllBindings.Where(b=>b.Enabled && b.UsesShortcut).ToList();
        var selection=new ComboBox{ItemsSource=choices,DisplayMemberPath="Name",SelectedIndex=choices.Count>0?0:-1,MinWidth=240};
        var result=Text("");
        void Check()
        {
            verificationChord=null;shortcutObserved=false;
            if(selection.SelectedItem is not Binding binding)return;
            var key=Shortcut.Parse(binding.Shortcut);
            var resolved=config.Resolve(WindowsActions.ProcessName(target),b=>b.UsesShortcut && Shortcut.Parse(b.Shortcut)==key);
            result.Text=$"{binding.Shortcut} · kayıt: {(hotkeys?.Registered.Contains(key)==true?"BAŞARILI":"BAŞARISIZ / ÇAKIŞMA")}\nEtkin mod: {config.ActiveMode} · hedef: {WindowsActions.ProcessName(target)} · çözümlenen eşleme: {resolved?.Name??"Bu mod/profilde eşleşme yok"}\n{registrationStatus}";
        }
        selection.SelectionChanged+=(_,_)=>Check();
        page.Children.Add(selection);page.Children.Add(Row(Button("Kısayol kayıtlarını yeniden doğrula",()=>{RegisterKeys();Check();}),Button("Windows dokunmatik yüzey ayarlarını aç",()=>_=Execute(new(){Kind="touchpadsettings"}))));page.Children.Add(result);
        verificationText=Text("Fiziksel hareket testi bekliyor.");
        page.Children.Add(Button("Bu kısayolu dinle · eylem çalıştırma",()=>
        {
            if(selection.SelectedItem is not Binding b)return;
            var key=Shortcut.Parse(b.Shortcut);
            if(hotkeys?.Registered.Contains(key)!=true){verificationText.Text="Önce kayıt çakışmasını giderin.";return;}
            shortcutObserved=false;verificationChord=key;verificationText.Text="Dinleniyor: "+b.Shortcut+" · şimdi Windows'a atadığınız hareketi fiziksel olarak yapın.";
        }));page.Children.Add(verificationText);
        page.Children.Add(Button("Hareketi fiziksel olarak yaptığımı doğrula",()=>
        {
            if(!shortcutObserved){verificationText.Text="Henüz dinlenen kısayol alınmadı; test bekliyor.";return;}
            string evidence=$"{DateTimeOffset.Now:O} · {verificationChord?.Canonical} · kullanıcı fiziksel hareket yaptığını onayladı; kaynak WM_HOTKEY, parmak/koordinat ölçülmedi.";
            shortcutEvidence.Add(evidence);verificationText.Text="Kullanıcı onaylı kısayol yolu doğrulandı. Yerel temas desteği doğrulanmış sayılmaz.";
        }));Check();
    }
}
