using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TrackpadProMax.Diagnostics;
public sealed record HidAccessEvidence(string Path,bool MetadataOpened,bool DescriptorRead,ushort UsagePage,ushort Usage,ushort InputReportBytes,bool ReadHandleOpened,int ReadOpenError);
// Read-only, bounded probes: no reports read/written and no driver/feature changes.
internal static class HidAccessProbe
{
    public static HidAccessEvidence Probe(string path)
    {
        bool descriptor=false;Caps caps=default;
        using var metadata=CreateFileW(path,0,3,0,3,0,0);
        if(!metadata.IsInvalid && HidD_GetPreparsedData(metadata,out var data))
        {
            try{descriptor=HidP_GetCaps(data,out caps)==0x00110000;}
            finally{HidD_FreePreparsedData(data);}
        }
        using var read=CreateFileW(path,0x80000000,3,0,3,0x40000000,0);
        int error=read.IsInvalid?Marshal.GetLastWin32Error():0;
        return new(path,!metadata.IsInvalid,descriptor,caps.UsagePage,caps.Usage,caps.InputReportByteLength,!read.IsInvalid,error);
    }
    [StructLayout(LayoutKind.Sequential)]private struct Caps
    {
        public ushort Usage,UsagePage,InputReportByteLength,OutputReportByteLength,FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray,SizeConst=17)]public ushort[] Reserved;
        public ushort LinkCollectionNodes,InputButtonCaps,InputValueCaps,InputDataIndices,OutputButtonCaps,OutputValueCaps,OutputDataIndices,FeatureButtonCaps,FeatureValueCaps,FeatureDataIndices;
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern SafeFileHandle CreateFileW(string name,uint access,uint share,nint security,uint creation,uint flags,nint template);
    [DllImport("hid.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool HidD_GetPreparsedData(SafeFileHandle device,out nint data);
    [DllImport("hid.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool HidD_FreePreparsedData(nint data);
    [DllImport("hid.dll")]private static extern int HidP_GetCaps(nint data,out Caps caps);
}
