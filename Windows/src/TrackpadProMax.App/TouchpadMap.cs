using System.Globalization;
using TrackpadProMax.Core;

namespace TrackpadProMax.App;
// Mouse clicks select an editor region only. They never become contact frames.
public sealed class TouchpadMap : FrameworkElement
{
    public SurfaceSettings Settings { get; set; }=new();
    public Zone Selected { get; set; }=Zone.Center;
    public IReadOnlyList<Contact> Contacts { get; set; }=[];
    public int? AnchorId { get; set; }
    public event Action<Zone>? ZoneSelected;
    public TouchpadMap(){Height=270;MinWidth=280;Focusable=true;ClipToBounds=true;}
    public static string Label(Zone zone)=>zone switch
    {
        Zone.LeftEdge=>"Sol kenar",Zone.RightEdge=>"Sağ kenar",Zone.TopEdge=>"Üst kenar",Zone.BottomEdge=>"Alt kenar",
        Zone.TopLeft=>"Sol üst",Zone.TopRight=>"Sağ üst",Zone.BottomLeft=>"Sol alt",Zone.BottomRight=>"Sağ alt",_=>"Merkez"
    };
    public static string Describe(ActionSpec action)=>action.Kind switch
    {"brightness"=>$"Parlaklık {action.Value}","volume"=>$"Ses {action.Value}%","seek"=>$"Zaman {action.Value}s","media"=>$"Medya {action.Value}",_=>$"{action.Kind} {action.Value}"};
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);double w=ActualWidth,h=ActualHeight;
        if(w<=0 || h<=0)return;
        dc.DrawRoundedRectangle(GlassTheme.Brush("FieldBrush"),new(GlassTheme.Brush("LineBrush"),1),new(0,0,w,h),14,14);
        foreach(var z in Settings.Zones.AsEnumerable().Reverse().OrderBy(z=>SurfaceSettings.IsCorner(z.Zone)))
        {
            var bounds=Bounds(z,w,h);
            var fill=GlassTheme.Brush(z.Zone==Selected?"AccentBrush":"GlassBrush").Clone();fill.Opacity=z.Enabled?(z.Zone==Selected?.38:.8):.2;
            dc.DrawRectangle(fill,new(GlassTheme.Brush("LineBrush"),1),bounds);
            string label=Label(z.Zone)+(z.Enabled?"\n"+Describe(z.Positive):" (kapalı)");
            Draw(label,bounds,12);
        }
        Draw("Cihaz yüzeyi\nYalnızca yerel temaslar çizilir\n+ sağ / yukarı · − sol / aşağı",new(w*.23,h*.34,w*.54,h*.32),13);
        foreach(var c in Contacts)
        {
            var p=new Point(c.Position.X*w,c.Position.Y*h);
            dc.DrawEllipse(GlassTheme.Brush("AccentBrush"),new(GlassTheme.Brush("TextBrush"),c.Id==AnchorId?3:1),p,9,9);
            Draw("#"+c.Id,new(Math.Clamp(p.X-17,0,Math.Max(0,w-36)),Math.Clamp(p.Y+10,0,h-20),36,20),11);
        }
        void Draw(string label,Rect rect,double size)
        {
            if(rect.Width<12 || rect.Height<12)return;
            var text=new FormattedText(label,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),size,GlassTheme.Brush("TextBrush"),VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {MaxTextWidth=Math.Max(1,rect.Width-6),MaxTextHeight=Math.Max(1,rect.Height-4),Trimming=TextTrimming.CharacterEllipsis,TextAlignment=TextAlignment.Center};
            dc.DrawText(text,new(rect.Left+3,rect.Top+Math.Max(2,(rect.Height-text.Height)/2)));
        }
    }
    private static Rect Bounds(SurfaceZone z,double w,double h)=>z.Zone switch
    {
        Zone.LeftEdge=>new(0,0,w*z.Width,h),Zone.RightEdge=>new(w*(1-z.Width),0,w*z.Width,h),
        Zone.TopEdge=>new(0,0,w,h*z.Height),Zone.BottomEdge=>new(0,h*(1-z.Height),w,h*z.Height),
        Zone.TopLeft=>new(0,0,w*z.Width,h*z.Height),Zone.TopRight=>new(w*(1-z.Width),0,w*z.Width,h*z.Height),
        Zone.BottomLeft=>new(0,h*(1-z.Height),w*z.Width,h*z.Height),_=>new(w*(1-z.Width),h*(1-z.Height),w*z.Width,h*z.Height)
    };
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);var p=e.GetPosition(this);
        var region=Settings.Classify(new(p.X/ActualWidth,p.Y/ActualHeight));
        if(region is not null){Selected=region.Zone;ZoneSelected?.Invoke(Selected);InvalidateVisual();}
        e.Handled=true;
    }
}
