using System.Windows.Media.Animation;
using System.Windows.Shapes;
using TrackpadProMax.Core;

namespace TrackpadProMax.App;
public sealed class RadialWindow : Window
{
    private readonly Canvas canvas=new(){Width=560,Height=560};
    private readonly Stack<(string Title,List<MenuEntry> Items)> history=new();
    private readonly Func<ActionSpec,Task> execute;
    private List<MenuEntry> items;
    private string title="Mod çarkı";
    private readonly List<System.Windows.Shapes.Path> sectors=[];
    private TextBlock? selectionLabel;
    private int selected;
    private bool closing;
    public string BackdropStatus { get; private set; }="";
    public void RefreshComposition()=>BackdropStatus=GlassTheme.Backdrop(this,true);
    public RadialWindow(List<MenuEntry> entries,Func<ActionSpec,Task> execute)
    {
        this.execute=execute;items=entries;
        Style=(Style)FindResource(typeof(Window));
        Title="TrackpadProMax · Mod çarkı";Width=560;Height=560;ResizeMode=ResizeMode.NoResize;
        // A non-layered HWND lets DWM render a real system backdrop. The window
        // region clips it to a circle; no desktop image is captured or blurred.
        WindowStyle=WindowStyle.None;AllowsTransparency=false;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=false;
        Content=canvas;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        SourceInitialized+=(_,_)=>BackdropStatus=GlassTheme.Backdrop(this,true);
        DpiChanged+=(_,_)=>BackdropStatus=GlassTheme.Backdrop(this,true);
        Loaded+=(_,_)=>{Draw();BackdropStatus=GlassTheme.Backdrop(this,true);if(GlassTheme.Animate){Opacity=0;BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(180)));}Activate();Focus();};
        Closing+=(_,_)=>closing=true;
        Deactivated+=(_,_)=>{if(!closing)Close();};
        PreviewKeyDown+=OnKey;
    }
    private void Draw()
    {
        canvas.Children.Clear();sectors.Clear();selected=0;
        var tint=((SolidColorBrush)GlassTheme.Brush("GlassBrush")).Color;
        var sheen=GlassTheme.Dark?Color.FromArgb(160,77,105,133):Color.FromArgb(190,255,255,255);
        var disk=new Ellipse{Width=558,Height=558,Fill=new LinearGradientBrush(new GradientStopCollection{new(sheen,0),new(tint,.35),new(tint,1)},new Point(0,0),new Point(.8,1)),Stroke=GlassTheme.Brush("LineBrush"),StrokeThickness=1.5};
        Canvas.SetLeft(disk,1);Canvas.SetTop(disk,1);canvas.Children.Add(disk);
        var reflection=new Ellipse{Width=534,Height=534,Stroke=new LinearGradientBrush(Color.FromArgb(140,255,255,255),Colors.Transparent,90),StrokeThickness=1,IsHitTestVisible=false};
        Canvas.SetLeft(reflection,13);Canvas.SetTop(reflection,13);canvas.Children.Add(reflection);
        double sector=360.0/items.Count;
        for(int i=0;i<items.Count;i++)
        {
            int index=i;double a=i*sector-sector/2-90,b=a+sector;
            var shape=new System.Windows.Shapes.Path{Data=Wedge(a+1,b-1,127,267),Fill=GlassTheme.Brush("GlassBrush"),Stroke=GlassTheme.Brush("LineBrush"),StrokeThickness=1,Cursor=Cursors.Hand};
            shape.MouseEnter+=(_,_)=>Select(index);shape.MouseLeftButtonUp+=async(_,_)=>await Choose(index);
            System.Windows.Automation.AutomationProperties.SetName(shape,items[i].Label);
            sectors.Add(shape);canvas.Children.Add(shape);
            double angle=(i*sector-90)*Math.PI/180;
            var label=new StackPanel{Width=98,IsHitTestVisible=false};
            label.Children.Add(new TextBlock{Text=EntryIcon(items[i]),FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),FontSize=24,TextAlignment=TextAlignment.Center,Margin=new Thickness(0,0,0,7)});
            label.Children.Add(new TextBlock{Text=items[i].Label+(items[i].Children.Count>0?" ›":""),TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap,FontSize=items.Count>8?11:13,MaxHeight=46,TextTrimming=TextTrimming.CharacterEllipsis});
            Canvas.SetLeft(label,280+Math.Cos(angle)*198-49);Canvas.SetTop(label,280+Math.Sin(angle)*198-29);canvas.Children.Add(label);
        }
        var centerContent=new StackPanel{Width=180};
        centerContent.Children.Add(new TextBlock{Text=history.Count==0?"TRACKPAD PRO MAX":"‹  "+title,TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap,FontSize=11,Foreground=GlassTheme.Brush("MutedBrush"),Margin=new Thickness(0,0,0,12)});
        selectionLabel=new TextBlock{TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap,FontSize=19,FontWeight=FontWeights.SemiBold,MaxHeight=76};centerContent.Children.Add(selectionLabel);
        centerContent.Children.Add(new TextBlock{Text=history.Count>0?"← Geri  ·  Esc kapat":"Enter seç  ·  Esc kapat",TextAlignment=TextAlignment.Center,FontSize=11,Foreground=GlassTheme.Brush("MutedBrush"),Margin=new Thickness(0,12,0,0)});
        var center=new Button{Content=centerContent,Width=232,Height=232,Background=GlassTheme.Brush("GlassBrush"),Margin=new Thickness(0),ToolTip=history.Count>0?"Üst menüye dön":"Çarkı kapat"};
        var template=new ControlTemplate(typeof(Button));var border=new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty,new CornerRadius(116));border.SetValue(Border.BackgroundProperty,GlassTheme.Brush("GlassBrush"));border.SetValue(Border.BorderBrushProperty,GlassTheme.Brush("LineBrush"));border.SetValue(Border.BorderThicknessProperty,new Thickness(1));
        var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(content);template.VisualTree=border;center.Template=template;
        center.Click+=(_,_)=>Back();Canvas.SetLeft(center,164);Canvas.SetTop(center,164);canvas.Children.Add(center);
        Select(0);
        if(GlassTheme.Animate)canvas.BeginAnimation(OpacityProperty,new DoubleAnimation(.6,1,TimeSpan.FromMilliseconds(130)));
    }
    private static string EntryIcon(MenuEntry item)=>item.Action.Kind switch
    {
        "media"=>item.Action.Value switch{"play"=>"\uE768","next"=>"\uE893","previous"=>"\uE892","mute"=>"\uE74F",_=>"\uE767"},
        "window"=>"\uE7F4","launch"=>"\uE8A7","url"=>"\uE774","keys"=>"\uE765","lock"=>"\uE72E","brightness"=>"\uE706","settings" or "touchpadsettings"=>"\uE713","taskmanager"=>"\uE9D9",_=>item.Label switch{"Medya"=>"\uE8D6","Pencereler"=>"\uE737","Uygulamalar"=>"\uE71D","Kontrol"=>"\uE713","Hızlı Ayarlar"=>"\uE713",_=>"\uE80F"}
    };
    private void Select(int index)
    {
        if(index<0 || index>=items.Count)return;
        int previous=selected;selected=index;
        for(int i=0;i<sectors.Count;i++)
        {
            sectors[i].BeginAnimation(OpacityProperty,null);
            sectors[i].Fill=GlassTheme.Brush(i==index?"AccentBrush":"GlassBrush");
            sectors[i].Stroke=GlassTheme.Brush(i==index?"TextBrush":"LineBrush");
            // The accent is a translucent wash so dark/light text stays legible.
            sectors[i].Opacity=i==index?.43:.7;
            if(GlassTheme.Animate && i==index && previous!=index)sectors[i].BeginAnimation(OpacityProperty,new DoubleAnimation(.2,.43,TimeSpan.FromMilliseconds(130)));
        }
        if(selectionLabel is not null)selectionLabel.Text=items[index].Label;
    }
    private async Task Choose(int index)
    {
        if(closing || index<0 || index>=items.Count)return;
        var entry=items[index];
        if(entry.Children.Count>0){history.Push((title,items));title=entry.Label;items=entry.Children;Draw();return;}
        Close();await execute(entry.Action);
    }
    private void Back(){if(history.TryPop(out var previous)){title=previous.Title;items=previous.Items;Draw();}else Close();}
    internal async Task<bool> CheckNavigationAsync()
    {
        var root=items;int branch=root.FindIndex(e=>e.Children.Count>0);if(branch<0)return true;
        await Choose(branch);bool entered=ReferenceEquals(items,root[branch].Children)&&history.Count==1;
        Select((selected+1)%items.Count);Back();return entered&&ReferenceEquals(root,items)&&history.Count==0;
    }
    private async void OnKey(object sender,KeyEventArgs e)
    {
        e.Handled=true;
        switch(e.Key)
        {
            case Key.Escape:Close();break;
            case Key.Back:Back();break;
            case Key.Left:case Key.Up:Select((selected-1+items.Count)%items.Count);break;
            case Key.Right:case Key.Down:Select((selected+1)%items.Count);break;
            case Key.Tab:Select((selected+(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)?-1:1)+items.Count)%items.Count);break;
            case Key.Enter:case Key.Space:await Choose(selected);break;
            default:int number=(int)e.Key-(int)Key.D1;if(number>=0&&number<items.Count&&number<9)await Choose(number);else e.Handled=false;break;
        }
    }
    private static Geometry Wedge(double start,double end,double inner,double outer)
    {
        Point At(double degrees,double r)=>new(280+Math.Cos(degrees*Math.PI/180)*r,280+Math.Sin(degrees*Math.PI/180)*r);
        if(end-start>350)end=start+349;
        var geometry=new StreamGeometry();
        using(var c=geometry.Open()){c.BeginFigure(At(start,outer),true,true);c.ArcTo(At(end,outer),new Size(outer,outer),0,end-start>180,SweepDirection.Clockwise,true,false);c.LineTo(At(end,inner),true,false);c.ArcTo(At(start,inner),new Size(inner,inner),0,end-start>180,SweepDirection.Counterclockwise,true,false);}
        geometry.Freeze();return geometry;
    }
}
