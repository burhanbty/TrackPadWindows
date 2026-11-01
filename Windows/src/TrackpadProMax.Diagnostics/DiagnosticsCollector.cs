using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Foundation.Metadata;

namespace TrackpadProMax.Diagnostics;
public enum CapabilityStatus { ConfirmedSupported, ConfirmedUnsupported, NotDetected, NotAccessible, UnknownRequiresPhysicalTesting }
public sealed record Capability(string Feature,CapabilityStatus Status,string Evidence);
public sealed record DeviceRecord(string Name,string Manufacturer,string PnpId,string Status);
public sealed record RawHidRecord(string Path,uint Type,uint VendorId,uint ProductId,uint UsagePage,uint Usage);
public sealed class DiagnosticReport
{
    public DateTimeOffset CollectedAt { get; set; } = DateTimeOffset.Now;
    public string Windows { get; set; } = "";
    public string Architecture { get; set; } = RuntimeInformation.OSArchitecture.ToString();
    public string Manufacturer { get; set; } = "Unknown";
    public string Model { get; set; } = "Unknown";
    public List<DeviceRecord> Devices { get; set; } = [];
    public List<RawHidRecord> RawInputDevices { get; set; } = [];
    public List<HidAccessEvidence> TouchpadHidAccess { get; set; } = [];
    public List<Capability> Capabilities { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true,Converters={new JsonStringEnumConverter()}});
}
public static class DiagnosticsCollector
{
    public static Task<DiagnosticReport> CollectAsync()=>Task.Run(Collect);
    public static DiagnosticReport Collect()
    {
        var r=new DiagnosticReport{Windows=RuntimeInformation.OSDescription};
        try
        {
            var version=new VersionInfo{Size=(uint)Marshal.SizeOf<VersionInfo>()};
            if(RtlGetVersion(ref version)==0) r.Windows=$"Windows {version.Major}.{version.Minor}, build {version.Build} {version.ServicePack}";
        } catch(Exception ex) { r.Errors.Add(ex.Message); }
        Query(r,"SELECT Manufacturer, Model FROM Win32_ComputerSystem",o=>{ r.Manufacturer=Text(o,"Manufacturer");r.Model=Text(o,"Model"); });
        Query(r,"SELECT Name, Manufacturer, PNPDeviceID, Status FROM Win32_PnPEntity WHERE PNPClass='HIDClass' OR PNPClass='Mouse'",o=>r.Devices.Add(new(Text(o,"Name"),Text(o,"Manufacturer"),Text(o,"PNPDeviceID"),Text(o,"Status"))));
        EnumerateRawInput(r);
        foreach(var device in r.RawInputDevices.Where(d=>d.UsagePage==13 && d.Usage==5))
        {
            try{r.TouchpadHidAccess.Add(HidAccessProbe.Probe(device.Path));}
            catch(Exception ex){r.Errors.Add("PTP read-only access probe: "+ex.Message);}
        }
        foreach(var access in r.TouchpadHidAccess)
            r.Capabilities.Add(new("Direct user-mode PTP HID read handle",access.ReadHandleOpened?CapabilityStatus.UnknownRequiresPhysicalTesting:CapabilityStatus.NotAccessible,
                $"Metadata open: {access.MetadataOpened}; descriptor: {access.DescriptorRead}; input report bytes: {access.InputReportBytes}; GENERIC_READ open: {access.ReadHandleOpened}; Win32 error: {access.ReadOpenError}. No input reports read; handle access is not physical delivery."));
        string[] exports=["RegisterTouchpadCapableWindow","RegisterTouchpadCapableThread","GetPointerTouchpadInfo","GetPointerFrameTouchpadInfo","GetPointerFrameTouchpadInfoHistory","GetPointerDevices","GetPointerInfo","RegisterRawInputDevices","RegisterHotKey","SetWindowsHookExW"];
        if(NativeLibrary.TryLoad("user32.dll",out var module))
        {
            try { foreach(var name in exports) { bool exists=NativeLibrary.TryGetExport(module,name,out _); r.Capabilities.Add(new(name,exists?CapabilityStatus.ConfirmedSupported:CapabilityStatus.ConfirmedUnsupported,exists?"DLL export present; device/event delivery is not confirmed.":"Export absent in this OS.")); } }
            finally { NativeLibrary.Free(module); }
        }
        foreach(var type in new[]{"Windows.UI.Input.TouchpadGesturesController","Windows.UI.Input.PhysicalGestureRecognizer","Windows.Devices.Haptics.SimpleHapticsController"})
        {
            try { bool exists=ApiInformation.IsTypePresent(type); r.Capabilities.Add(new(type,exists?CapabilityStatus.ConfirmedSupported:CapabilityStatus.NotDetected,"Runtime metadata probe only; not a device support claim.")); }
            catch(Exception ex) { r.Capabilities.Add(new(type,CapabilityStatus.NotAccessible,ex.Message)); }
        }
        bool ptp=r.RawInputDevices.Any(d=>d.UsagePage==13 && d.Usage==5);
        r.Capabilities.Add(new("Precision Touchpad collection",ptp?CapabilityStatus.ConfirmedSupported:CapabilityStatus.NotDetected,ptp?"Digitizer page 0x0D, Touch Pad usage 0x05 enumerated. Data access not implied.":"No PTP collection in Raw Input inventory. Windows may hide exclusive system collections; absence does not prove non-PTP hardware."));
        r.Capabilities.Add(new("Global shortcut input",CapabilityStatus.UnknownRequiresPhysicalTesting,"RegisterHotKey is the fallback; each chord must register successfully and be mapped manually in Windows Settings."));
        r.Capabilities.Add(new("Unrestricted background raw PTP",CapabilityStatus.ConfirmedUnsupported,"Windows reserves the PTP HID collection exclusively. Foreground pointer APIs do not grant global raw access."));
        r.Capabilities.Add(new("Touchpad identity",r.Devices.Any(d=>d.Name.Contains("touchpad",StringComparison.OrdinalIgnoreCase) || d.Name.Contains("dokunmatik",StringComparison.OrdinalIgnoreCase))?CapabilityStatus.ConfirmedSupported:CapabilityStatus.NotDetected,"See PnP names and vendor IDs. No model guessed from the user's report."));
        foreach(var feature in new[]{"Pressure HID usages (0x0D:0x30)","Force Touch / calibrated pressure levels","Haptic touchpad actuator","Contact count (0x0D:0x54)","Device-relative touch coordinates","Contact size","Finger orientation","Surface edge/corner acquisition"})
            r.Capabilities.Add(new(feature,CapabilityStatus.UnknownRequiresPhysicalTesting,"No verified physical touchpad sample or accessible report descriptor. HID inventory and API presence alone do not establish this capability."));
        return r;
    }
    private static string Text(ManagementBaseObject o,string name)=>o[name]?.ToString()??"Unknown";
    private static void Query(DiagnosticReport r,string query,Action<ManagementBaseObject> consume)
    {
        try { using var search=new ManagementObjectSearcher(query); using var results=search.Get(); foreach(ManagementBaseObject o in results) using(o) consume(o); }
        catch(Exception ex) { r.Errors.Add(query+": "+ex.Message); }
    }
    private static void EnumerateRawInput(DiagnosticReport r)
    {
        try
        {
            uint count=0; uint size=(uint)Marshal.SizeOf<RawDevice>();
            if(GetRawInputDeviceList(null,ref count,size)==uint.MaxValue) throw new System.ComponentModel.Win32Exception();
            if(count>4096) throw new InvalidOperationException("Unexpected device count.");
            var devices=new RawDevice[count];
            uint found=GetRawInputDeviceList(devices,ref count,size);
            if(found==uint.MaxValue) throw new System.ComponentModel.Win32Exception();
            foreach(var d in devices.Take((int)found))
            {
                uint length=0; GetRawInputDeviceInfo(d.Handle,0x20000007,null,ref length);
                var name=new StringBuilder((int)length+1); if(GetRawInputDeviceInfo(d.Handle,0x20000007,name,ref length)==uint.MaxValue) continue;
                var info=new DeviceInfo{Size=32}; uint infoSize=32;
                if(GetRawInputDeviceInfoData(d.Handle,0x2000000b,ref info,ref infoSize)==uint.MaxValue) { r.Errors.Add("Raw Input info unavailable for "+name);continue; }
                r.RawInputDevices.Add(new(name.ToString(),d.Type,d.Type==2?info.Vendor:0u,d.Type==2?info.Product:0u,d.Type==2?(uint)info.UsagePage:0u,d.Type==2?(uint)info.Usage:0u));
            }
        } catch(Exception ex) { r.Errors.Add("Raw Input inventory: "+ex.Message); }
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct VersionInfo { public uint Size,Major,Minor,Build,Platform; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string ServicePack; }
    [StructLayout(LayoutKind.Sequential)] private struct RawDevice { public nint Handle;public uint Type; }
    [StructLayout(LayoutKind.Explicit,Size=32)] private struct DeviceInfo { [FieldOffset(0)] public uint Size;[FieldOffset(4)] public uint Type;[FieldOffset(8)] public uint Vendor;[FieldOffset(12)] public uint Product;[FieldOffset(16)] public uint Version;[FieldOffset(20)] public ushort UsagePage;[FieldOffset(22)] public ushort Usage; }
    [DllImport("ntdll.dll",CharSet=CharSet.Unicode)] private static extern int RtlGetVersion(ref VersionInfo version);
    [DllImport("user32.dll",SetLastError=true)] private static extern uint GetRawInputDeviceList([Out] RawDevice[]? devices,ref uint count,uint size);
    [DllImport("user32.dll",EntryPoint="GetRawInputDeviceInfoW",CharSet=CharSet.Unicode,SetLastError=true)] private static extern uint GetRawInputDeviceInfo(nint device,uint command,StringBuilder? data,ref uint size);
    [DllImport("user32.dll",EntryPoint="GetRawInputDeviceInfoW",SetLastError=true)] private static extern uint GetRawInputDeviceInfoData(nint device,uint command,ref DeviceInfo data,ref uint size);
}
