using System.Runtime.InteropServices;
using System.Windows.Interop;
using TrackpadProMax.Core;
using Microsoft.Win32;

namespace TrackpadProMax.App;

internal static class GlassTheme
{
    public static AppearanceSettings Settings { get; private set; }=new();
    public static bool Dark { get; private set; }=true;
    public static bool Animate=>Settings.Animations && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
    public static Brush Brush(string key)=>(Brush)Application.Current.Resources[key];
    public static void Apply(AppearanceSettings settings)
    {
        Settings=settings;
        bool systemDark=true;
        try { using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");systemDark=key?.GetValue("AppsUseLightTheme") is not int value || value==0; }catch { }
        Dark=settings.Theme=="Dark" || (settings.Theme=="System" && systemDark);
        var resources=Application.Current.Resources;
        void Set(string key,Color value)=>resources[key]=new SolidColorBrush(value);
        Color C(string hex)=>(Color)ColorConverter.ConvertFromString(hex);
        Set("TextBrush",SystemParameters.HighContrast?SystemColors.WindowTextColor:C(Dark?"#EEF4FF":"#17263D"));
        Set("MutedBrush",SystemParameters.HighContrast?SystemColors.WindowTextColor:C(Dark?"#A6B8D0":"#4C6078"));
        Set("AccentBrush",SystemParameters.HighContrast?SystemColors.HighlightColor:C(settings.Accent));
        Set("FieldBrush",SystemParameters.HighContrast?SystemColors.WindowColor:C(Dark?"#172337":"#F4F8FF"));
        Set("LineBrush",SystemParameters.HighContrast?SystemColors.WindowTextColor:C(Dark?"#48617B":"#AABDD3"));
        Set("PanelBrush",SystemParameters.HighContrast?SystemColors.WindowColor:C(Dark?"#263A50":"#FFFFFF"));
        var surface=C(Dark?"#17273D":"#F0F6FF");surface.A=(byte)(255*(1-(SystemParameters.HighContrast?0:settings.Transparency)));
        Set("GlassBrush",SystemParameters.HighContrast?SystemColors.WindowColor:surface);
        Set("BaseBrush",SystemParameters.HighContrast?SystemColors.WindowColor:C(Dark?"#0C1626":"#E4EDF8"));
        var windowTint=C(Dark?"#0C1626":"#E4EDF8");windowTint.A=surface.A;
        Set("WindowTintBrush",SystemParameters.HighContrast?SystemColors.WindowColor:windowTint);
    }
    public static Border Card(UIElement child)=>new()
    {
        Child=child,Padding=new Thickness(22),Margin=new Thickness(0,0,0,18),CornerRadius=new CornerRadius(20),BorderThickness=new Thickness(1),
        Background=Brush("GlassBrush"),BorderBrush=Brush("LineBrush")
    };
    public static string Backdrop(Window window,bool circular=false)
    {
        var hwnd=new WindowInteropHelper(window).Handle;
        if(hwnd==0)return "Pencere hazırlanıyor";
        int dark=Dark?1:0;DwmSetWindowAttribute(hwnd,20,ref dark,4);
        int effect=Settings.Backdrop && Settings.Transparency>0 && !SystemParameters.HighContrast ?3:1;
        // Respect Windows' transparency preference as well as the app preference.
        try { using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");if(key?.GetValue("EnableTransparency") is int value && value==0) effect=1; }catch {effect=1;}
        int result=DwmSetWindowAttribute(hwnd,38,ref effect,4);
        bool enabled=result>=0 && effect==3;
        var margins=new Margins{Left=enabled?-1:0};
        int extended=DwmExtendFrameIntoClientArea(hwnd,ref margins);
        enabled &= extended>=0;
        var source=HwndSource.FromHwnd(hwnd);
        if(source?.CompositionTarget is not null)source.CompositionTarget.BackgroundColor=enabled?Colors.Transparent:((SolidColorBrush)Brush("BaseBrush")).Color;
        window.Background=enabled?Brushes.Transparent:Brush("BaseBrush");
        if(circular)
        {
            var dpi=VisualTreeHelper.GetDpi(window);
            nint region=CreateEllipticRgn(0,0,(int)(window.Width*dpi.DpiScaleX),(int)(window.Height*dpi.DpiScaleY));
            if(SetWindowRgn(hwnd,region,true)==0)DeleteObject(region); // Windows owns region on success.
        }
        return enabled?"DWM Desktop Acrylic istendi · sistem güç/erişilebilirlik nedeniyle opaklaştırabilir":"Opak cam görünümü · sistem efekti kapalı veya kullanılamıyor";
    }
    public static bool HasCircularRegion(Window window)
    {
        nint region=CreateRectRgn(0,0,0,0);
        try
        {
            var dpi=VisualTreeHelper.GetDpi(window);
            return GetWindowRgn(new WindowInteropHelper(window).Handle,region)>0
                && !PtInRegion(region,1,1)
                && PtInRegion(region,(int)(window.Width*dpi.DpiScaleX/2),(int)(window.Height*dpi.DpiScaleY/2));
        }finally{DeleteObject(region);}
    }
    [StructLayout(LayoutKind.Sequential)] private struct Margins { public int Left,Right,Top,Bottom; }
    [DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(nint hwnd,int attribute,ref int value,int size);
    [DllImport("dwmapi.dll")]private static extern int DwmExtendFrameIntoClientArea(nint hwnd,ref Margins margins);
    [DllImport("gdi32.dll")]private static extern nint CreateEllipticRgn(int left,int top,int right,int bottom);
    [DllImport("gdi32.dll")]private static extern nint CreateRectRgn(int left,int top,int right,int bottom);
    [DllImport("gdi32.dll")]private static extern bool PtInRegion(nint region,int x,int y);
    [DllImport("user32.dll")]private static extern int GetWindowRgn(nint hwnd,nint region);
    [DllImport("gdi32.dll")]private static extern bool DeleteObject(nint handle);
    [DllImport("user32.dll")]private static extern int SetWindowRgn(nint hwnd,nint region,bool redraw);
}
