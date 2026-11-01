using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using TrackpadProMax.Actions;
using TrackpadProMax.Core;
using TrackpadProMax.Diagnostics;
using TrackpadProMax.Input;

namespace TrackpadProMax.App;
public sealed partial class MainWindow : Window
{
    private readonly string configPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TrackpadProMax","settings.json");
    private Configuration config=Configuration.Default();
    private readonly WindowsActions actions=new();
    private readonly StackPanel page=new();
    private readonly TextBlock status=new(){Text="Hazır",TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Color.FromRgb(117,212,218))};
    private readonly TextBlock modeText=new(){Text="NORMAL",FontSize=12,Foreground=new SolidColorBrush(Color.FromRgb(117,212,218))};
    private readonly DispatcherTimer foregroundTimer=new(){Interval=TimeSpan.FromMilliseconds(350)};
    private GlobalHotkeys? hotkeys;
    private System.Windows.Forms.NotifyIcon? tray;
    private HwndSource? source;
    private nint target;
    private bool paused,exiting,busy;
    private RadialWindow? wheel;
    private DiagnosticReport? report;
    private string registrationStatus="";
    private string committedJson="";
    private readonly List<string> smokePages=[];
    public MainWindow()
    {
        Style=(Style)FindResource(typeof(Window));
        Title="TrackpadProMax · Windows";Width=1160;Height=790;MinWidth=920;MinHeight=640;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        try { if(File.Exists(configPath)) config=ConfigurationStore.Deserialize(File.ReadAllText(configPath)); }
        catch(Exception ex) { status.Text="Ayarlar okunamadı; özgün dosya korunuyor. "+ex.Message; }
        GlassTheme.Apply(config.Appearance);
        committedJson=ConfigurationStore.Serialize(config);
        actions.WheelRequested+=OpenWheel;
        actions.ModeRequested+=mode=>{ StopSurface("Mod değişti");if(nativeWanted)SetNative(false);config.ActiveMode=mode;if(SaveConfiguration()){modeText.Text=config.ActiveMode.ToUpperInvariant();status.Text="Etkin mod: "+config.ActiveMode;if(mode.Equals("Surface",StringComparison.OrdinalIgnoreCase)){ShowSettings();SurfacePage();ArmSurface();}} };
        var root=new Grid();root.ColumnDefinitions.Add(new(){Width=new GridLength(226)});root.ColumnDefinitions.Add(new());
        root.SetResourceReference(Panel.BackgroundProperty,"WindowTintBrush");modeText.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush");
        var nav=new StackPanel{Margin=new Thickness(20,30,14,20)};
        nav.Children.Add(new TextBlock{Text="◉  TRACKPAD",FontSize=21,FontWeight=FontWeights.Bold});
        nav.Children.Add(new TextBlock{Text="PRO MAX  /  WINDOWS",FontSize=10,Margin=new Thickness(0,6,0,32),Foreground=Brushes.LightSlateGray});
        void Nav(string label,Action show) { var b=Button(label,show);b.HorizontalContentAlignment=HorizontalAlignment.Left;b.SetResourceReference(Control.BackgroundProperty,"GlassBrush");nav.Children.Add(b); }
        Nav("◈   Genel Bakış",Dashboard);Nav("⌁   Hareketler",GesturesPage);Nav("▤   Uygulama Profilleri",ProfilesPage);Nav("◎   Radyal Menü",MenuPage);Nav("↗   Eylemler",ActionsPage);Nav("▥   Aygıt Tanılama",DiagnosticsPage);Nav("⚙   Ayarlar",SettingsPage);
        Nav("◌   Laboratuvar",LaboratoryPage);Nav("↪   Kısayol Kurulumu",SetupPage);
        Nav("▧   Yüzey Denetimi",SurfacePage);
        nav.Children.Add(new Border{Height=28});nav.Children.Add(modeText);
        nav.Children.Add(new TextBlock{Text="Yerel. Sürücüsüz.\nTelemetri yok.",FontSize=12,Foreground=Brushes.LightSlateGray,Margin=new Thickness(0,20,0,0)});
        var navigation=new Border{Child=new ScrollViewer{Content=nav,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled},BorderThickness=new Thickness(0,0,1,0)};navigation.SetResourceReference(Border.BackgroundProperty,"GlassBrush");navigation.SetResourceReference(Border.BorderBrushProperty,"LineBrush");root.Children.Add(navigation);
        var main=new Grid{Margin=new Thickness(30,28,30,18)};main.RowDefinitions.Add(new());main.RowDefinitions.Add(new(){Height=GridLength.Auto});Grid.SetColumn(main,1);
        main.Children.Add(new ScrollViewer{Content=page,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        var footer=new Border{Child=status,Padding=new Thickness(12),Margin=new Thickness(0,14,0,0),CornerRadius=new CornerRadius(12)};footer.SetResourceReference(Border.BackgroundProperty,"GlassBrush");status.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush");Grid.SetRow(footer,1);main.Children.Add(footer);root.Children.Add(main);Content=root;
        SourceInitialized+=(_,_)=>InitializeInput();
        StateChanged+=(_,_)=>{ if(WindowState==WindowState.Minimized && config.MinimizeToTray) Hide(); };
        Closing+=(_,e)=>{ if(!exiting && config.MinimizeToTray) {e.Cancel=true;Hide();} };
        Closed+=(_,_)=>{hotkeys?.Dispose();controller?.Dispose();native?.Dispose();labTimer.Stop();foregroundTimer.Stop();source?.RemoveHook(WndProc);tray?.Dispose();if(!exiting)Application.Current.Shutdown();};
        Activated+=(_,_)=>{if(nativeWanted)controller?.Start();};
        Deactivated+=(_,_)=>{StopSurface("Odak kaybı · yeniden etkinleştirin");SetNative(false);nativeRecognizer?.Cancel();};
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape && (surfaceActive || zoneTesting || nativeWanted)){StopSurface("Esc ile kapatıldı");SetNative(false);e.Handled=true;}};
        labTimer.Tick+=(_,_)=>{RefreshLaboratory();RefreshSurface();};labTimer.Start();
        Dashboard();
    }
    private void InitializeInput()
    {
        source=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);source.AddHook(WndProc);
        hotkeys=new(source.Handle);hotkeys.Triggered+=async key=>
        {
            Record(new(DateTimeOffset.Now,EvidenceSource.Shortcut,"WM_HOTKEY · "+key.Canonical+" · fiziksel hareket/temas bilinmiyor"));
            if(setupVisible)
            {
                if(verificationChord==key){shortcutObserved=true;if(verificationText is not null)verificationText.Text="Kısayol alındı · kaynak WM_HOTKEY. Fiziksel hareket yaptıysanız aşağıdan onaylayın.";}
                return;
            }
            if(paused || busy) return;
            var foreground=WindowsActions.ForegroundWindow;if(WindowsActions.IsExternalWindow(foreground)) target=foreground;
            var binding=config.Resolve(WindowsActions.ProcessName(target),b=>b.UsesShortcut && Shortcut.Parse(b.Shortcut)==key);
            if(IsLaboratoryVisible)return;
            if(binding is not null) await Execute(binding.Action);
        };
        RegisterKeys();
        Dashboard();
        native=new NativeTouchpadProvider(source.Handle);
        native.Frame+=OnNativeFrame;
        controller=new(native);
        controller.Gesture+=OnControllerGesture;
        controller.ActionReceived+=name=>Record(new(DateTimeOffset.Now,EvidenceSource.NativeController,"GlobalActionPerformed · "+name));
        backdropStatus=GlassTheme.Backdrop(this);
        foregroundTimer.Tick+=(_,_)=>{ if(wheel is null) { var current=WindowsActions.ForegroundWindow;if(WindowsActions.IsExternalWindow(current)) target=current; } };
        foregroundTimer.Start();
        tray=new(){Icon=System.Drawing.SystemIcons.Application,Text="TrackpadProMax",Visible=true};
        var menu=new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Ayarları aç",null,(_,_)=>ShowSettings());menu.Items.Add("Mod çarkı",null,(_,_)=>OpenWheel());
        var pauseItem=menu.Items.Add("Hareketleri duraklat",null,(_,_)=>{ paused=!paused;if(paused){StopSurface("Duraklatıldı");SetNative(false);}status.Text=paused?"Hareketler duraklatıldı.":"Hareketler etkin."; });
        menu.Opening+=(_,_)=>pauseItem.Text=paused?"Hareketleri sürdür":"Hareketleri duraklat";
        menu.Items.Add("Aygıt tanılama",null,(_,_)=>{ShowSettings();DiagnosticsPage();});menu.Items.Add("Çıkış",null,(_,_)=>Exit());
        tray.ContextMenuStrip=menu;tray.DoubleClick+=(_,_)=>ShowSettings();modeText.Text=config.ActiveMode.ToUpperInvariant();
    }
    private NativeTouchpadProvider? native;
    private GestureRecognizer? nativeRecognizer;
    private async void OnNativeFrame(TouchFrame frame)
    {
        Record(new(DateTimeOffset.Now,frame.Cancelled?EvidenceSource.System:EvidenceSource.NativePointer,frame.Cancelled?"Akış iptal edildi":"HIMETRIC · cihaz koordinatları · "+native?.LastRoute,frame.Contacts.Count,frame.Cancelled?null:frame.Contacts.ToArray()));
        lastFrame=frame;lastContactAt=DateTimeOffset.Now;
        if(frame.Cancelled) { nativeRecognizer?.Cancel();await ProcessSurfaceAsync(frame);return; }
        if(paused || !IsActive) return;
        if(surfaceActive || zoneTesting){nativeRecognizer?.Cancel();await ProcessSurfaceAsync(frame);return;}
        nativeRecognizer??=new(config.Gestures);
        try
        {
            foreach(var gesture in nativeRecognizer.Process(frame))
            {
                // The native API delivers manipulation frames, not taps or holds.
                if(gesture.Kind is GestureKind.Tap or GestureKind.DoubleTap or GestureKind.LongPress) continue;
                Record(new(DateTimeOffset.Now,EvidenceSource.NativePointer,gesture.Kind.ToString(),gesture.Fingers,Gesture:gesture.Kind));
                var binding=config.Resolve(WindowsActions.ProcessName(target),b=>b.UsesNative && b.Gesture==gesture.Kind && b.Fingers==gesture.Fingers && (b.Zone==Zone.Anywhere || b.Zone==gesture.Zone));
                status.Text=$"Yerel ön plan: {gesture.Fingers} · {gesture.Kind}";
                if(binding is not null && !IsLaboratoryVisible && !setupVisible) await Execute(binding.Action);
            }
        } catch(Exception ex) { status.Text=ex.Message; nativeRecognizer.Cancel(); }
    }
    private nint WndProc(nint hwnd,int message,nint wParam,nint lParam,ref bool handled)
    { hotkeys?.ProcessMessage(message,wParam);native?.ProcessMessage(message,wParam);return 0; }
    private void RegisterKeys()
    {
        var errors=hotkeys?.Configure(config.AllBindings)??[];
        registrationStatus=errors.Count==0?"Genel kısayollar kaydedildi.":string.Join("\n",errors);
        status.Text=registrationStatus;
    }
    public void ShowSettings() { Show();WindowState=WindowState.Normal;Activate(); }
    public void Exit() { exiting=true;wheel?.Close();Close();Application.Current.Shutdown(); }
    public void CloseForSmoke() { config.MinimizeToTray=false;wheel?.Close();Close(); }
    private bool SaveConfiguration()
    {
        try { ConfigurationStore.Save(configPath,config);committedJson=ConfigurationStore.Serialize(config);nativeRecognizer=null;GlassTheme.Apply(config.Appearance);backdropStatus=GlassTheme.Backdrop(this);RegisterKeys();modeText.Text=config.ActiveMode.ToUpperInvariant();status.Text="Kaydedildi. "+registrationStatus;return true; }
        catch(Exception ex) { config=ConfigurationStore.Deserialize(committedJson);status.Text="Kaydedilemedi; önceki ayarlar etkin: "+ex.Message;return false; }
    }
    private async Task Execute(ActionSpec action)
    {
        if(paused || busy) return;busy=true;
        try
        {
            if(action.Kind is "keys" or "media")
            {
                var until=DateTime.UtcNow.AddSeconds(3);
                while(WindowsActions.ActivationModifiersDown() && DateTime.UtcNow<until) await Task.Delay(20);
                if(WindowsActions.ActivationModifiersDown()) { status.Text="Değiştirici tuşlar bırakılmadı; eylem iptal edildi.";return; }
            }
            var result=await actions.ExecuteAsync(action,target);status.Text=(result.Success?"✓ ":"! ")+result.Message;
        }
        finally {busy=false;}
    }
    private void OpenWheel()
    {
        if(paused) {status.Text="Önce hareketleri sürdürün.";return;}
        if(wheel is not null) {wheel.Activate();return;}
        wheel=new(config.Menu,Execute);wheel.Closed+=(_,_)=>wheel=null;wheel.Show();
        // Place inside the captured target monitor, converting physical pixels to current WPF DIPs.
        var screen=System.Windows.Forms.Screen.FromHandle(target);
        var dpi=VisualTreeHelper.GetDpi(wheel);
        wheel.Left=(screen.WorkingArea.Left+(screen.WorkingArea.Width-wheel.Width*dpi.DpiScaleX)/2)/dpi.DpiScaleX;
        wheel.Top=(screen.WorkingArea.Top+(screen.WorkingArea.Height-wheel.Height*dpi.DpiScaleY)/2)/dpi.DpiScaleY;
        wheel.RefreshComposition();
    }
    private static Button Button(string text,Action click)
    {var b=new Button{Content=text};b.Click+=(_,_)=>click();return b;}
    private static TextBlock Text(string text,double size=14)=>new(){Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};
    private void BeginPage(string title,string subtitle)
    {StopSurface("Sayfa değişti");if(nativeWanted)SetNative(false);surfaceMap=null;surfaceFeedback=null;surfaceState=null;zoneTestText=null;labView=null;setupVisible=false;verificationChord=null;page.Children.Clear();page.Children.Add(Text(title,30));var description=new TextBlock{Text=subtitle,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,24)};description.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");page.Children.Add(description);}
    private static Border Card(UIElement child)=>GlassTheme.Card(child);
    private static WrapPanel Row(params UIElement[] children) {var row=new WrapPanel();foreach(var c in children) row.Children.Add(c);return row;}
    private void Dashboard()
    {
        BeginPage("Dokunmatik yüzeyiniz.\nSizin iş akışınız.","TrackpadProMax Windows · Liquid Glass / Phase 3");
        page.Children.Add(Card(Row(Button("▧  Yüzey kontrolünü aç",SurfacePage),Text("Kenar ve köşe kontrolleri · açık oturumda / ön planda",12))));
        var hero=new StackPanel();hero.Children.Add(Text("HAREKETTEN EYLEME",12));hero.Children.Add(Text("Tek kısayol. Altı kontrol alanı.",23));
        hero.Children.Add(Text("Ctrl + Alt + F8 ile mod çarkını her uygulamadan açın. Ok tuşlarıyla gezin, Enter ile seçin, Esc ile kapatın."));
        hero.Children.Add(Row(Button("◎  Mod çarkını aç",OpenWheel),Button("Windows hareketlerini ayarla",()=>_=Execute(new(){Kind="touchpadsettings"}))));page.Children.Add(Card(hero));
        var info=new StackPanel();info.Children.Add(Text("Güvenilir başlangıç",19));info.Children.Add(Text("Windows Ayarları → Bluetooth ve cihazlar → Dokunmatik yüzey → Gelişmiş hareketler. Üç/dört parmak hareketi için ‘Özel kısayol’ seçip burada tanımladığınız kısayolu kaydedin. Windows ayarları otomatik değiştirilmez."));info.Children.Add(Text("Basınç ve haptik geri bildirim doğrulanmadı. Fare imleci konumu yüzey temas konumu olarak kullanılmaz."));info.Children.Add(Row(Button("Hareket eşlemeleri",GesturesPage),Button("Donanımı incele",DiagnosticsPage)));page.Children.Add(Card(info));
        var controls=new StackPanel();controls.Children.Add(Text("Giriş durumu",19));controls.Children.Add(Text(registrationStatus));controls.Children.Add(Button(paused?"Hareketleri sürdür":"Hareketleri duraklat",()=>{paused=!paused;nativeRecognizer?.Cancel();StopSurface("Duraklatma değişti");SetNative(false);Dashboard();}));page.Children.Add(Card(controls));
    }
    private DataGrid BindingsGrid(IEnumerable<Binding> bindings)
    {
        var grid=new DataGrid{ItemsSource=bindings,MinHeight=180,MaxHeight=330};
        grid.Columns.Add(new DataGridCheckBoxColumn{Header="Etkin",Binding=new System.Windows.Data.Binding("Enabled")});
        void Col(string header,string path,int width)=>grid.Columns.Add(new DataGridTextColumn{Header=header,Binding=new System.Windows.Data.Binding(path),Width=width});
        Col("Ad","Name",150);Col("Kısayol","Shortcut",130);
        grid.Columns.Add(new DataGridComboBoxColumn{Header="Giriş yolu",ItemsSource=Enum.GetValues<InputRoute>(),SelectedItemBinding=new System.Windows.Data.Binding("Input"),Width=115});
        grid.Columns.Add(new DataGridComboBoxColumn{Header="Hareket",ItemsSource=Enum.GetValues<GestureKind>(),SelectedItemBinding=new System.Windows.Data.Binding("Gesture"),Width=115});
        Col("Parmak","Fingers",65);Col("Mod (*=tümü)","Mode",120);
        grid.Columns.Add(new DataGridComboBoxColumn{Header="Bölge",ItemsSource=Enum.GetValues<Zone>(),SelectedItemBinding=new System.Windows.Data.Binding("Zone"),Width=95});
        Col("Eylem türü","Action.Kind",105);Col("Değer","Action.Value",180);
        return grid;
    }
    private void GesturesPage()
    {
        BeginPage("Hareketler","Kısayol yolu küreseldir. Temas hareketleri yalnızca yeterli cihaz verisi varsa tanınır.");
        var collection=new ObservableCollection<Binding>(ConfigurationStore.Deserialize(ConfigurationStore.Serialize(config)).Bindings);var grid=BindingsGrid(collection);page.Children.Add(grid);
        page.Children.Add(Row(Button("+ Eşleme",()=>collection.Add(new(){Name="Yeni eşleme",Shortcut=""})),Button("Seçileni kaldır",()=>{if(grid.SelectedItem is Binding b) collection.Remove(b);}),Button("Kaydet",()=>{if(!CommitGrid(grid))return;config.Bindings=collection.ToList();SaveConfiguration();})));
        page.Children.Add(Text("Eylem türleri: wheel, media, window, keys, launch, url, mode, settings, touchpadsettings, taskmanager, lock, brightness. Ayrıntılar Eylemler sayfasında. Aynı kapsam ve moddaki yinelenen kısayollar kayıtta reddedilir. Genel tuş kaydı çakışmaları alt durum alanında gösterilir."));
        page.Children.Add(Text("Automatic: kısayol varsa yalnızca kısayol, yoksa yerel giriş. Native: yalnızca ön planda deneysel yerel giriş; Shortcut: Windows'ta elle atanır. Yerel tek parmak, çift dokunuş/uzun basış desteklenmez. Basınç/haptik yok."));
        BindingComposer();
        page.Children.Add(Button("Görsel touchpad haritası · bölge atamalarını düzenle",SurfacePage));
        page.Children.Add(Row(Button("Touchpad Laboratuvarı",LaboratoryPage),Button("Türkçe kısayol kurulumu",SetupPage)));
    }
    private void SimulationPanel()
    {
        var body=new StackPanel();body.Children.Add(Text("Hareket laboratuvarı · SİMÜLASYON",18));body.Children.Add(Text("Sentetik temaslar yalnızca motor sonucunu gösterir; dış uygulamalarda eylem çalıştırmaz."));
        var kind=new ComboBox{ItemsSource=Enum.GetValues<GestureKind>(),SelectedIndex=0,Width=160};var fingers=new ComboBox{ItemsSource=new[]{1,2,3,4},SelectedIndex=2,Width=80};var zone=new ComboBox{ItemsSource=Enum.GetValues<Zone>().Where(z=>z!=Zone.Anywhere),SelectedItem=Zone.Center,Width=130};
        var output=Text("Sonuç bekleniyor.");
        body.Children.Add(Row(kind,fingers,zone,Button("Simüle et",()=>
        {
            var recognizer=new GestureRecognizer(config.Gestures);using var provider=new SimulatedInputProvider();var events=new List<Gesture>();provider.Frame+=f=>events.AddRange(recognizer.Process(f));provider.Start();
            var selected=(GestureKind)kind.SelectedItem;int n=(int)fingers.SelectedItem;var z=(Zone)zone.SelectedItem;
            double x=z is Zone.TopLeft or Zone.BottomLeft or Zone.LeftEdge?.05:z is Zone.TopRight or Zone.BottomRight or Zone.RightEdge?.95:.5;
            double y=z is Zone.TopLeft or Zone.TopRight or Zone.TopEdge?.05:z is Zone.BottomLeft or Zone.BottomRight or Zone.BottomEdge?.95:.5;
            Contact[] Contacts(double dx=0,double dy=0)=>Enumerable.Range(1,n).Select(id=>new Contact(id,new(Math.Clamp(x+dx,0,1),Math.Clamp(y+dy,0,1)))).ToArray();
            void Push(double t,Contact[] contacts)=>provider.Push(new(t,contacts));
            Push(0,Contacts());double duration=selected==GestureKind.LongPress?config.Gestures.LongPressSeconds+.1:.1;
            double shift=config.Gestures.SwipeMovement+.05;
            if(selected.ToString().StartsWith("Swipe")) { x=.5;y=.5; recognizer.Cancel();Push(.01,Contacts());Push(.1,Contacts(selected==GestureKind.SwipeLeft?-shift:selected==GestureKind.SwipeRight?shift:0,selected==GestureKind.SwipeUp?-shift:selected==GestureKind.SwipeDown?shift:0)); }
            Push(duration,[]);if(selected==GestureKind.DoubleTap) {Push(.2,Contacts());Push(.28,[]);}
            output.Text=string.Join(" → ",events.Select(g=>$"{g.Fingers} parmak · {g.Kind} · {g.Zone}"));
            foreach(var g in events)Record(new(DateTimeOffset.Now,EvidenceSource.Simulation,$"{g.Kind} · {g.Zone}",g.Fingers,Gesture:g.Kind));
        })));
        body.Children.Add(output);page.Children.Add(Card(body));
    }
    private void ProfilesPage()
    {
        BeginPage("Uygulama profilleri","Ön plandaki .exe işlem adı eşleştiğinde profil, genel eşlemelerden önce uygulanır.");
        var list=new ComboBox{ItemsSource=config.Profiles,DisplayMemberPath="Name",MinWidth=220};var editor=new StackPanel();
        void Edit()
        {
            editor.Children.Clear();if(list.SelectedItem is not AppProfile p)return;
            editor.Children.Add(Text("Profil adı"));var name=new TextBox{Text=p.Name};editor.Children.Add(name);editor.Children.Add(Text("İşlem adı (.exe olmadan, ör. chrome veya Code)"));var process=new TextBox{Text=p.ProcessName};editor.Children.Add(process);
            var bindings=new ObservableCollection<Binding>(JsonSerializer.Deserialize<List<Binding>>(JsonSerializer.Serialize(p.Bindings,ConfigurationStore.Options),ConfigurationStore.Options)!);var grid=BindingsGrid(bindings);editor.Children.Add(grid);
            editor.Children.Add(Row(Button("+ Eşleme",()=>bindings.Add(new(){Shortcut=""})),Button("Eşlemeyi kaldır",()=>{if(grid.SelectedItem is Binding b)bindings.Remove(b);}),Button("Profili kaydet",()=>{if(!CommitGrid(grid))return;p.Name=name.Text;p.ProcessName=Path.GetFileNameWithoutExtension(process.Text.Trim());p.Bindings=bindings.ToList();if(SaveConfiguration())ProfilesPage();}),Button("Profili sil",()=>{config.Profiles.Remove(p);SaveConfiguration();ProfilesPage();})));
        }
        list.SelectionChanged+=(_,_)=>Edit();page.Children.Add(Row(list,Button("+ Profil",()=>{config.Profiles.Add(new(){Name="Yeni profil",ProcessName="newapp"});ProfilesPage();})));page.Children.Add(editor);if(config.Profiles.Count>0)list.SelectedIndex=0;
    }
    private void MenuPage()
    {
        BeginPage("Radyal menü düzenleyicisi","Kategorilere alt öğeler ekleyin, sıralayın ve eylem atayın. Değişiklikleri kaydedince çarkta kullanılır.");
        MenuEditorPanel();
        var advanced=new StackPanel();var editor=JsonEditor(JsonSerializer.Serialize(config.Menu,ConfigurationStore.Options));advanced.Children.Add(editor);
        advanced.Children.Add(Row(Button("JSON menüsünü kaydet",()=>
        {
            try { var menu=JsonSerializer.Deserialize<List<MenuEntry>>(editor.Text,ConfigurationStore.Options)??[];var clone=ConfigurationStore.Deserialize(ConfigurationStore.Serialize(config));clone.Menu=menu;clone.Validate();config=clone;SaveConfiguration(); }catch(Exception ex){status.Text=ex.Message;}
        }),Button("Çarkı aç",OpenWheel)));
        page.Children.Add(new Expander{Header="Gelişmiş · JSON düzenleyicisi",Content=advanced,Margin=new Thickness(0,0,0,20)});
        var launcher=new StackPanel();launcher.Children.Add(Text("Hızlı uygulama ekle",18));var name=new TextBox{Text="Uygulamam"};var path=new TextBox{Text="https://example.com"};launcher.Children.Add(Text("Görünen ad"));launcher.Children.Add(name);launcher.Children.Add(Text(".exe yolu veya http/https adresi"));launcher.Children.Add(path);
        launcher.Children.Add(Row(Button("Dosya seç",()=>{var dialog=new OpenFileDialog{Filter="Uygulama (*.exe)|*.exe"};if(dialog.ShowDialog()==true){path.Text=dialog.FileName;name.Text=Path.GetFileNameWithoutExtension(dialog.FileName);}}),Button("Başlatıcıya ekle",()=>
        {
            try { var action=new ActionSpec{Kind=Uri.TryCreate(path.Text,UriKind.Absolute,out var uri) && (uri.Scheme=="http"||uri.Scheme=="https")?"url":"launch",Value=path.Text.Trim()};Configuration.ValidateAction(action);
                var folder=config.Menu.FirstOrDefault(m=>m.Label=="Uygulamalar");if(folder is null){folder=new(){Label="Uygulamalar"};config.Menu.Add(folder);}folder.Children.Add(new(){Label=name.Text,Action=action});if(SaveConfiguration())MenuPage();
            } catch(Exception ex){status.Text=ex.Message;}
        })));
        launcher.Children.Add(Text("Öğeleri kaldırmak veya yeniden sıralamak için yukarıdaki JSON'u düzenleyin. Başlatıcı kısayolu atamak için Hareketler sayfasında launch veya url eylemi oluşturun."));page.Children.Add(Card(launcher));
    }
    private void ActionsPage()
    {
        BeginPage("Eylemler","Pencere ve klavye eylemleri son seçilen dış uygulamayı hedefler. En güvenilir yol: hedef uygulamadayken genel mod çarkı kısayolunu kullanın.");
        var windows=new StackPanel();windows.Children.Add(Text("Pencere yönetimi",19));
        var wr=new WrapPanel();foreach(var (label,value) in new[]{("Sol yarı","left"),("Sağ yarı","right"),("Büyüt","maximize"),("Geri yükle","restore"),("Küçült","minimize"),("2 pencere","tile2"),("3 pencere","tile3"),("4 pencere","tile4")}) wr.Children.Add(Button(label,()=>_=Execute(new(){Kind="window",Value=value})));windows.Children.Add(wr);page.Children.Add(Card(windows));
        var media=new StackPanel();media.Children.Add(Text("Medya ve ses",19));var mr=new WrapPanel();foreach(var (label,value) in new[]{("Önceki","previous"),("Oynat / duraklat","play"),("Sonraki","next"),("Ses +","up"),("Ses −","down"),("Sessiz","mute")})mr.Children.Add(Button(label,()=>_=Execute(new(){Kind="media",Value=value})));media.Children.Add(mr);
        var now=Text("Medya bilgisi isteğe bağlı okunur.");media.Children.Add(now);media.Children.Add(Button("Çalan parçayı yenile",async()=>now.Text=await WindowsActions.NowPlayingAsync()));page.Children.Add(Card(media));
        var system=new StackPanel();system.Children.Add(Text("Sistem",19));system.Children.Add(Row(Button("Windows Ayarları",()=>_=Execute(new(){Kind="settings"})),Button("Görev Yöneticisi",()=>_=Execute(new(){Kind="taskmanager"})),Button("Masaüstünü göster",()=>_=Execute(new(){Kind="keys",Value="Win+D"})),Button("Ekran alıntısı",()=>_=Execute(new(){Kind="keys",Value="Win+Shift+S"})),Button("Ekranı kilitle",()=>_=Execute(new(){Kind="lock"}))));page.Children.Add(Card(system));
        page.Children.Add(Text("Değer örnekleri: media → play / next / previous / up / down / mute; window → left / right / maximize / restore / minimize / tile2 / tile3 / tile4; keys → Ctrl+Shift+P veya Win+Shift+Right; launch → tam .exe yolu; url → https://…; brightness → 10 veya -10. Parlaklık OEM WMI desteği gerektirir. Media tuşları SendInput ile; parça bilgisi Windows medya oturum API'siyle sağlanır."));
    }
    private async void DiagnosticsPage()
    {
        BeginPage("Aygıt tanılama","Salt okunur, yerel inceleme. Dışa aktarılan rapor aygıt kimlikleri içerir; paylaşmadan önce inceleyin.");
        var body=new StackPanel();page.Children.Add(body);
        var buttons=Row(Button("Yeniden tara",()=>{report=null;DiagnosticsPage();}),Button("JSON raporu dışa aktar",()=>
        {
            if(report is null){status.Text="Tarama henüz tamamlanmadı.";return;}
            var dialog=new SaveFileDialog{Filter="JSON raporu|*.json",FileName="TrackpadProMax-diagnostics.json"};if(dialog.ShowDialog()==true) {try{File.WriteAllText(dialog.FileName,report.ToJson());status.Text="Rapor dışa aktarıldı.";}catch(Exception ex){status.Text=ex.Message;}}
        }));body.Children.Add(buttons);var waiting=Text("Donanım ve API'ler taranıyor…");body.Children.Add(waiting);
        report??=await DiagnosticsCollector.CollectAsync();if(!page.Children.Contains(body)) return;
        body.Children.Remove(waiting);body.Children.Add(Text($"{report.Manufacturer} · {report.Model}\n{report.Windows} · {report.Architecture}",18));
        body.Children.Add(Text("Durumlar: DOĞRULANDI · DESTEKLENMİYOR · ALGILANMADI · ERİŞİLEMİYOR · FİZİKSEL TEST GEREKLİ"));
        foreach(var cap in report.Capabilities)
        {
            string state=cap.Status switch{CapabilityStatus.ConfirmedSupported=>"DOĞRULANDI",CapabilityStatus.ConfirmedUnsupported=>"DESTEKLENMİYOR",CapabilityStatus.NotDetected=>"ALGILANMADI",CapabilityStatus.NotAccessible=>"ERİŞİLEMİYOR",_=>"FİZİKSEL TEST GEREKLİ"};
            var card=new StackPanel();card.Children.Add(Text(cap.Feature+"  ·  "+state,15));card.Children.Add(Text(cap.Evidence,12));body.Children.Add(Card(card));
        }
        body.Children.Add(Text("PnP / HID aygıtları",20));foreach(var d in report.Devices)body.Children.Add(Text($"{d.Name} · {d.Manufacturer}\n{d.PnpId} · {d.Status}",12));foreach(var error in report.Errors)body.Children.Add(Text("Erişim sonucu: "+error));
    }
    private static TextBox JsonEditor(string text)=>new(){Text=text,FontFamily=new FontFamily("Cascadia Mono, Consolas"),FontSize=12,AcceptsReturn=true,AcceptsTab=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,Height=340,TextWrapping=TextWrapping.NoWrap};
    private void SettingsPage()
    {
        BeginPage("Ayarlar","Dosya yerel kullanıcı dizininde saklanır. Önceki kayıt .bak olarak korunur. Başlangıçta otomatik çalıştırma kapalıdır.");
        var trayCheck=new CheckBox{Content="Pencereyi kapatınca / küçültünce sistem tepsisine al",IsChecked=config.MinimizeToTray};page.Children.Add(trayCheck);
        page.Children.Add(Button("Davranışı kaydet",()=>{config.MinimizeToTray=trayCheck.IsChecked==true;SaveConfiguration();}));
        AppearancePanel();
        page.Children.Add(Text("Hareket hassasiyeti ve tüm gelişmiş ayarlar",20));var editor=JsonEditor(ConfigurationStore.Serialize(config));page.Children.Add(editor);
        page.Children.Add(Row(Button("Doğrula ve kaydet",()=>{try{var candidate=ConfigurationStore.Deserialize(editor.Text);config=candidate;SaveConfiguration();modeText.Text=config.ActiveMode.ToUpperInvariant();}catch(Exception ex){status.Text="Geçersiz ayarlar: "+ex.Message;}}),Button("Varsayılanlara dön",()=>{if(MessageBox.Show("Mevcut ayarları varsayılanlarla değiştir? Önceki dosya .bak olarak korunur.","Ayarları sıfırla",MessageBoxButton.YesNo)==MessageBoxResult.Yes){config=Configuration.Default();SaveConfiguration();SettingsPage();}})));
        page.Children.Add(Text(configPath,12));
        page.Children.Add(Button("Yerel girişi Touchpad Laboratuvarında test et",LaboratoryPage));
        page.Children.Add(Button("Yüzey bölgeleri ve hassasiyet",SurfacePage));
        var unsupported=Button("Basınç / haptik",()=>{});unsupported.IsEnabled=false;unsupported.ToolTip="Fiziksel aygıt desteği doğrulanmadı; bu sürümde kullanılamıyor.";page.Children.Add(unsupported);
        page.Children.Add(Button("Uygulamadan çık",Exit));
    }
    public async Task RunSmokeAsync()
    {
        foreach(var (name,show) in new (string,Action)[]{("Dashboard",Dashboard),("Gestures",GesturesPage),("Profiles",ProfilesPage),("RadialEditor",MenuPage),("Actions",ActionsPage),("Settings",SettingsPage),("Laboratory",LaboratoryPage),("Setup",SetupPage),("Surface",SurfacePage),("ZoneLaboratory",ZoneLaboratoryPage)}) {show();smokePages.Add(name);}
        report=await DiagnosticsCollector.CollectAsync();DiagnosticsPage();smokePages.Add("Diagnostics");
        Dashboard();Activate();native?.Start();controller?.Start();
        string? controllerStatus=controller?.Status;bool? controllerRegistered=controller?.IsRunning;
        controller?.Stop();
        OpenWheel();
        await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        bool circularRegion=wheel is not null && GlassTheme.HasCircularRegion(wheel);
        bool navigation=wheel is not null && await wheel.CheckNavigationAsync();
        native?.Start();string? nativeStatus=native?.Status;bool? nativeRegistered=native?.IsRunning;native?.Stop();
        bool shortcutsRegistered=config.AllBindings.Where(b=>b.Enabled&&b.UsesShortcut).All(b=>hotkeys?.Registered.Contains(Shortcut.Parse(b.Shortcut))==true);
        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory,"smoke"));File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"smoke","ui-smoke.json"),JsonSerializer.Serialize(new{Pages=smokePages,RadialOpened=wheel is not null,NestedNavigation=navigation,CircularWindowRegion=circularRegion,NativeRegistered=nativeRegistered,NativeStatus=nativeStatus,ControllerRegistered=controllerRegistered,ControllerStatus=controllerStatus,PhysicalDeliveryVerified=false,Backdrop=backdropStatus,RadialBackdrop=wheel?.BackdropStatus,ShortcutsRegistered=shortcutsRegistered,Registration=registrationStatus,At=DateTimeOffset.Now},ConfigurationStore.Options));
        if(!navigation || wheel is null || !circularRegion || !shortcutsRegistered)throw new InvalidOperationException("UI smoke: menü/daire/kısayol doğrulaması başarısız.");
    }
}
