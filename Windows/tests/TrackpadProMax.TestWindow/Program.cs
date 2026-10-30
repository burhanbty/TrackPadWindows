using System.Windows;
using System.Windows.Controls;

namespace TrackpadProMax.TestWindow;
public static class Program
{
    [STAThread] public static void Main()
    {
        var app=new Application();
        app.Run(new Window{Title="TrackpadProMax integration fixture",Width=640,Height=420,Content=new TextBlock{Text="Disposable test window. No user data.",Margin=new Thickness(20)}});
    }
}
