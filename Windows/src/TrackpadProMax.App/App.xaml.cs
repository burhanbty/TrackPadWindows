using TrackpadProMax.Diagnostics;

namespace TrackpadProMax.App;
public partial class App : Application
{
    private System.Threading.Mutex? instance;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Diagnostics-only mode does not register shortcuts, start a tray icon, or execute actions.
        if(e.Args.Length==2 && e.Args[0]=="--diagnostics")
        {
            try { var report=await DiagnosticsCollector.CollectAsync(); var path=Path.GetFullPath(e.Args[1]); Directory.CreateDirectory(Path.GetDirectoryName(path)!); await File.WriteAllTextAsync(path,report.ToJson()); Shutdown(0); }
            catch { Shutdown(1); }
            return;
        }
        instance=new System.Threading.Mutex(true,"Local\\TrackpadProMax.Windows",out bool created);
        if(!created) { if(e.Args.Contains("--smoke")){Shutdown(2);return;}MessageBox.Show("TrackpadProMax zaten çalışıyor. Sistem tepsisinden açabilirsiniz."); Shutdown();return; }
        DispatcherUnhandledException+=(sender,args)=>
        {
            if(e.Args.Contains("--smoke"))
            {
                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory,"smoke"));
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"smoke","error.txt"),args.Exception.ToString());
                args.Handled=true;Shutdown(1);return;
            }
            MessageBox.Show(args.Exception.Message,"TrackpadProMax — hata"); args.Handled=true;
        };
        var window=new MainWindow(); MainWindow=window; window.Show();
        if(e.Args.Length==2 && e.Args[0]=="--surface-probe")window.BeginPhysicalProbe(e.Args[1]);
        if(e.Args.Contains("--surface"))window.OpenSurfaceControls();
        if(e.Args.Contains("--smoke"))
        {
            // Visual initialization smoke only; no input injection or external action.
            await Task.Delay(1500); await window.RunSmokeAsync(); await Task.Delay(1500); window.CloseForSmoke();
        }
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}
