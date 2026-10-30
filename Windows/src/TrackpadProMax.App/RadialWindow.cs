using System.Windows.Media.Animation;
using System.Windows.Shapes;
using TrackpadProMax.Core;

namespace TrackpadProMax.App;
public sealed class RadialWindow : Window
{
    private readonly Canvas canvas=new(){Width=520,Height=520};
    private readonly Stack<(string Title,List<MenuEntry> Items)> history=new();
    private readonly Func<ActionSpec,Task> execute;
    private List<MenuEntry> items;
    private string title="Mod çarkı";
    private readonly List<System.Windows.Shapes.Path> sectors=[];
    private int selected;
    private bool closing;
    public RadialWindow(List<MenuEntry> entries,Func<ActionSpec,Task> execute)
    {
        this.execute=execute;items=entries;
        Title="TrackpadProMax · Mod çarkı"; Width=540;Height=540;ResizeMode=ResizeMode.NoResize;
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=false;
        Content=canvas; WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Loaded+=(_,_)=>{ Draw(); Opacity=0;BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(160)));Activate();Focus(); };
        Closing+=(_,_)=>closing=true;
        Deactivated+=(_,_)=>{if(!closing)Close();};
        PreviewKeyDown+=OnKey;
    }
    private void Draw()
    {
        canvas.Children.Clear();sectors.Clear();selected=0;
        var disk=new Ellipse{Width=510,Height=510,Fill=new SolidColorBrush(Color.FromRgb(15,23,38)),Stroke=new SolidColorBrush(Color.FromRgb(61,85,116)),StrokeThickness=1};
        Canvas.SetLeft(disk,5);Canvas.SetTop(disk,5);canvas.Children.Add(disk);
        double sector=360.0/items.Count;
        for(int i=0;i<items.Count;i++)
        {
            int index=i;double a=i*sector-sector/2-90,b=a+sector;
            var shape=new System.Windows.Shapes.Path{Data=Wedge(a+1,b-1,112,247),Fill=BrushFor(false),Stroke=new SolidColorBrush(Color.FromRgb(51,69,94)),StrokeThickness=1,Cursor=Cursors.Hand};
            shape.MouseEnter+=(_,_)=>Select(index);shape.MouseLeftButtonUp+=async(_,_)=>await Choose(index);
            sectors.Add(shape);canvas.Children.Add(shape);
            double angle=(i*sector-90)*Math.PI/180;
            var label=new TextBlock{Text=$"{i+1:00}\n{items[i].Label}"+(items[i].Children.Count>0?"  ›":""),Width=122,TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap,FontSize=14,IsHitTestVisible=false};
            Canvas.SetLeft(label,260+Math.Cos(angle)*179-61);Canvas.SetTop(label,260+Math.Sin(angle)*179-23);canvas.Children.Add(label);
        }
        var center=new Button{Content=new TextBlock{Text=title+"\n"+(history.Count>0?"← Geri":"Esc · Kapat"),TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap},Width=194,Height=194,Background=new SolidColorBrush(Color.FromRgb(29,46,68)),Margin=new Thickness(0)};
        var template=new ControlTemplate(typeof(Button));var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(97));border.SetValue(Border.BackgroundProperty,new SolidColorBrush(Color.FromRgb(29,46,68)));var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(content);template.VisualTree=border;center.Template=template;
        center.Click+=(_,_)=>Back();Canvas.SetLeft(center,163);Canvas.SetTop(center,163);canvas.Children.Add(center);
        Select(0);
    }
    private static Brush BrushFor(bool selected)=>new SolidColorBrush(selected?Color.FromRgb(42,107,133):Color.FromRgb(24,36,54));
    private void Select(int index) { selected=index;for(int i=0;i<sectors.Count;i++) { var color=(SolidColorBrush)BrushFor(i==index); sectors[i].BeginAnimation(OpacityProperty,new DoubleAnimation(.7,1,TimeSpan.FromMilliseconds(100))); sectors[i].Fill=color; } }
    private async Task Choose(int index)
    {
        var entry=items[index];
        if(entry.Children.Count>0) { history.Push((title,items));title=entry.Label;items=entry.Children;Draw();return; }
        Close();await execute(entry.Action);
    }
    private void Back() { if(history.TryPop(out var previous)) { title=previous.Title;items=previous.Items;Draw(); } else Close(); }
    internal async Task<bool> CheckNavigationAsync()
    {
        var root=items;
        int branch=root.FindIndex(e=>e.Children.Count>0);
        if(branch<0)return true;
        await Choose(branch);
        bool entered=ReferenceEquals(items,root[branch].Children) && history.Count==1;
        Select((selected+1)%items.Count);
        Back();
        return entered && ReferenceEquals(root,items) && history.Count==0;
    }
    private async void OnKey(object sender,KeyEventArgs e)
    {
        e.Handled=true;
        switch(e.Key)
        {
            case Key.Escape: Close();break;
            case Key.Back: Back();break;
            case Key.Left: case Key.Up: Select((selected-1+items.Count)%items.Count);break;
            case Key.Right: case Key.Down: case Key.Tab: Select((selected+1)%items.Count);break;
            case Key.Enter: case Key.Space: await Choose(selected);break;
            default: int number=(int)e.Key-(int)Key.D1; if(number>=0 && number<items.Count && number<9) await Choose(number);else e.Handled=false;break;
        }
    }
    private static Geometry Wedge(double start,double end,double inner,double outer)
    {
        Point At(double degrees,double r)=>new(260+Math.Cos(degrees*Math.PI/180)*r,260+Math.Sin(degrees*Math.PI/180)*r);
        if(end-start>350) end=start+349; // Single-entry menus retain a visible gap and valid arc.
        var geometry=new StreamGeometry();
        using(var c=geometry.Open()) { c.BeginFigure(At(start,outer),true,true);c.ArcTo(At(end,outer),new Size(outer,outer),0,end-start>180,SweepDirection.Clockwise,true,false);c.LineTo(At(end,inner),true,false);c.ArcTo(At(start,inner),new Size(inner,inner),0,end-start>180,SweepDirection.Counterclockwise,true,false); }
        geometry.Freeze();return geometry;
    }
}
