using System.Diagnostics;
using System.Runtime.InteropServices;
using TrackpadProMax.Actions;
using TrackpadProMax.Core;
using TrackpadProMax.Input;
using Xunit;

namespace TrackpadProMax.WindowsTests;
public class WindowsTests
{
    [Fact] public async Task RealWindowActionsAndLauncherWorkOnDisposableWindow()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../"));
        var fixture=Path.Combine(root,"TrackpadProMax.TestWindow","bin","Release","net10.0-windows10.0.19041.0","TrackpadProMax.TestWindow.exe");
        Assert.True(File.Exists(fixture),fixture);
        var actions=new WindowsActions();
        var prior=Process.GetProcessesByName("TrackpadProMax.TestWindow").Select(p=>p.Id).ToHashSet();
        Process? owned=null;
        try
        {
            Assert.True((await actions.ExecuteAsync(new(){Kind="launch",Value=fixture},0)).Success);
            for(int i=0;i<50;i++)
            {
                owned=Process.GetProcessesByName("TrackpadProMax.TestWindow").FirstOrDefault(p=>!prior.Contains(p.Id));
                if(owned?.MainWindowHandle!=0 && owned is not null)break;
                await Task.Delay(100);
            }
            Assert.NotNull(owned);owned.Refresh();var hwnd=owned.MainWindowHandle;Assert.NotEqual(0,hwnd);
            Assert.Equal("TrackpadProMax.TestWindow",WindowsActions.ProcessName(hwnd));
            foreach(var operation in new[]{"left","right"})
            {
                Assert.True((await actions.ExecuteAsync(new(){Kind="window",Value=operation},hwnd)).Success);
                await Task.Delay(100);Assert.True(GetWindowRect(hwnd,out var rect));
                var monitor=MonitorFromWindow(hwnd,2);var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};Assert.True(GetMonitorInfo(monitor,ref info));
                int half=(info.Work.Right-info.Work.Left)/2;
                Assert.InRange(Math.Abs(rect.Left-(info.Work.Left+(operation=="right"?half:0))),0,2);
                Assert.InRange(Math.Abs(rect.Top-info.Work.Top),0,2);
                Assert.InRange(Math.Abs((rect.Right-rect.Left)-half),0,2);
            }
            Assert.True((await actions.ExecuteAsync(new(){Kind="window",Value="maximize"},hwnd)).Success);await Task.Delay(100);Assert.True(IsZoomed(hwnd));
            Assert.True((await actions.ExecuteAsync(new(){Kind="window",Value="restore"},hwnd)).Success);await Task.Delay(100);Assert.False(IsZoomed(hwnd));
            Assert.True((await actions.ExecuteAsync(new(){Kind="window",Value="minimize"},hwnd)).Success);await Task.Delay(100);Assert.True(IsIconic(hwnd));
            Assert.True((await actions.ExecuteAsync(new(){Kind="window",Value="restore"},hwnd)).Success);await Task.Delay(100);Assert.False(IsIconic(hwnd));
        }
        finally { if(owned is not null) { if(!owned.HasExited){owned.CloseMainWindow();if(!owned.WaitForExit(3000))owned.Kill();}owned.Dispose(); } }
    }
    [Fact] public async Task InvalidWindowAndUnsupportedActionFailGracefully()
    {var a=new WindowsActions();Assert.False((await a.ExecuteAsync(new(){Kind="window",Value="left"},0)).Success);Assert.False((await a.ExecuteAsync(new(){Kind="unknown"},0)).Success);}
    [Fact] public void SimulationDoesNotEmitWhenStopped()
    {using var p=new SimulatedInputProvider();int count=0;p.Frame+=_=>count++;p.Push(new(0,[]));Assert.Equal(0,count);p.Start();p.Push(new(1,[]));Assert.Equal(1,count);p.Stop();p.Push(new(2,[]));Assert.Equal(1,count);}
    [Fact] public void NativeProbeDoesNotClaimHardwareSupport()
    {var p=new NativeTouchpadProbe();Assert.True(p.Exports["GetPointerInfo"]);using var native=new NativeTouchpadProvider(0);Assert.False(native.IsRunning);Assert.Contains("test",native.Status);}
    [StructLayout(LayoutKind.Sequential)] private struct Rect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo{public int Size;public Rect Monitor,Work;public uint Flags;}
    [DllImport("user32.dll")]private static extern bool GetWindowRect(nint hwnd,out Rect rect);
    [DllImport("user32.dll")]private static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")]private static extern bool IsZoomed(nint hwnd);
    [DllImport("user32.dll")]private static extern nint MonitorFromWindow(nint hwnd,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool GetMonitorInfo(nint monitor,ref MonitorInfo info);
}
