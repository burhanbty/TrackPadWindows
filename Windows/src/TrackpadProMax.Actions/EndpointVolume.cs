using System.Runtime.InteropServices;
using TrackpadProMax.Core;

namespace TrackpadProMax.Actions;
// Relative percentage points on the current default render endpoint. No cached
// device handle: changing audio devices between gestures is handled naturally.
internal static class EndpointVolume
{
    public static ActionResult Adjust(int percent)
    {
        object? enumerator=null;IMMDevice? device=null;object? endpoint=null;
        try
        {
            enumerator=Activator.CreateInstance(Type.GetTypeFromCLSID(new("BCDE0395-E52F-467C-8E3D-C4579291692E"))!);
            Marshal.ThrowExceptionForHR(((IMMDeviceEnumerator)enumerator!).GetDefaultAudioEndpoint(0,1,out device));
            var iid=typeof(IAudioEndpointVolume).GUID;
            Marshal.ThrowExceptionForHR(device.Activate(ref iid,23,0,out endpoint));
            var volume=(IAudioEndpointVolume)endpoint;
            Marshal.ThrowExceptionForHR(volume.GetMasterVolumeLevelScalar(out float current));
            var context=Guid.Empty;
            float requested=Math.Clamp(current+percent/100f,0,1);
            Marshal.ThrowExceptionForHR(volume.SetMasterVolumeLevelScalar(requested,ref context));
            return ActionResult.Ok($"Ses: %{requested*100:F0}");
        }
        catch(Exception ex){return ActionResult.Fail("Ses aygıtı: "+ex.Message);}
        finally
        {
            if(endpoint is not null)Marshal.ReleaseComObject(endpoint);
            if(device is not null)Marshal.ReleaseComObject(device);
            if(enumerator is not null)Marshal.ReleaseComObject(enumerator);
        }
    }
    [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]int EnumAudioEndpoints(int flow,uint mask,out nint devices);
        [PreserveSig]int GetDefaultAudioEndpoint(int flow,int role,out IMMDevice device);
    }
    [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]int Activate(ref Guid iid,uint context,nint parameters,[MarshalAs(UnmanagedType.IUnknown)]out object instance);
    }
    [ComImport,Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig]int RegisterControlChangeNotify(nint callback);
        [PreserveSig]int UnregisterControlChangeNotify(nint callback);
        [PreserveSig]int GetChannelCount(out uint count);
        [PreserveSig]int SetMasterVolumeLevel(float db,ref Guid context);
        [PreserveSig]int SetMasterVolumeLevelScalar(float value,ref Guid context);
        [PreserveSig]int GetMasterVolumeLevel(out float db);
        [PreserveSig]int GetMasterVolumeLevelScalar(out float value);
    }
}
