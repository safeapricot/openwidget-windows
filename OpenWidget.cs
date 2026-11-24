using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shell;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Device.Location;
using System.Management;

namespace OpenWidget
{
    // CoreAudio COM Interfaces for Native Windows Microphone Control
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr pNotify);
        int UnregisterControlChangeNotify(IntPtr pNotify);
        int GetChannelCount(out int pnChannelCount);
        int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        int GetMasterVolumeLevel(out float pfLevelDB);
        int GetMasterVolumeLevelScalar(out float pfLevel);
        int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
        int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
        int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
        int GetMute(out bool pbMute);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        int Activate(ref Guid id, int clsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumeratorComObject { }

    // Direct C-API Interop for NVIDIA Management Library (nvml.dll) - Zero Subprocess Overhead
    internal static class NvmlNative
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct nvmlUtilization_t
        {
            public uint gpu;
            public uint memory;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct nvmlMemory_t
        {
            public ulong total;
            public ulong free;
            public ulong used;
        }

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nvmlInit_v2();

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nvmlShutdown();

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nvmlDeviceGetName(IntPtr device, StringBuilder name, uint length);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nvmlDeviceGetTemperature(IntPtr device, int sensorType, out uint temp);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nvmlDeviceGetUtilizationRates(IntPtr device, out nvmlUtilization_t util);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nvmlDeviceGetMemoryInfo(IntPtr device, out nvmlMemory_t mem);
    }

    public class WirelessDeviceReading
    {
        public string IdKey;
        public string Icon;
        public string Name;
        public string Detail;
        public string PctText;
        public int BarPct;
        public bool IsCharging;
        public bool IsAsleep;
    }

    // Universal Zero-Bloat Wireless Peripherals Detector & Battery Reader (HID, PnP Bluetooth/BLE, XInput, WinMM)
    internal static class WirelessBatteryNative
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVPROPKEY
        {
            public Guid fmtid;
            public uint pid;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NativeOverlapped
        {
            public IntPtr InternalLow;
            public IntPtr InternalHigh;
            public int OffsetLow;
            public int OffsetHigh;
            public IntPtr EventHandle;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XINPUT_BATTERY_INFORMATION
        {
            public byte BatteryType;
            public byte BatteryLevel;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
            public byte[] Gamepad;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JOYINFOEX
        {
            public int dwSize;
            public int dwFlags;
            public int dwXpos;
            public int dwYpos;
            public int dwZpos;
            public int dwRpos;
            public int dwUpos;
            public int dwVpos;
            public int dwButtons;
            public int dwButtonNumber;
            public int dwPOV;
            public int dwReserved1;
            public int dwReserved2;
        }

        [DllImport("xinput1_4.dll")]
        public static extern uint XInputGetState(uint dwUserIndex, out XINPUT_STATE pState);

        [DllImport("xinput1_4.dll")]
        public static extern uint XInputGetBatteryInformation(uint dwUserIndex, byte devType, out XINPUT_BATTERY_INFORMATION pBatteryInformation);

        [DllImport("winmm.dll")]
        public static extern int joyGetNumDevs();

        [DllImport("winmm.dll")]
        public static extern int joyGetPosEx(int uJoyID, ref JOYINFOEX pji);

        [StructLayout(LayoutKind.Sequential)]
        public struct HIDD_ATTRIBUTES
        {
            public int Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        [StructLayout(LayoutKind.Explicit, Size = 72)]
        public struct HIDP_VALUE_CAPS
        {
            [FieldOffset(0)] public ushort UsagePage;
            [FieldOffset(2)] public byte ReportID;
            [FieldOffset(3)] public byte IsAlias;
            [FieldOffset(4)] public ushort BitField;
            [FieldOffset(6)] public ushort LinkCollection;
            [FieldOffset(8)] public ushort LinkUsage;
            [FieldOffset(10)] public ushort LinkUsagePage;
            [FieldOffset(12)] public byte IsRange;
            [FieldOffset(13)] public byte IsStringRange;
            [FieldOffset(14)] public byte IsDesignatorRange;
            [FieldOffset(15)] public byte IsAbsolute;
            [FieldOffset(16)] public byte HasNull;
            [FieldOffset(17)] public byte Reserved;
            [FieldOffset(18)] public ushort BitSize;
            [FieldOffset(20)] public ushort ReportCount;
            [FieldOffset(32)] public uint UnitsExp;
            [FieldOffset(36)] public uint Units;
            [FieldOffset(40)] public int LogicalMin;
            [FieldOffset(44)] public int LogicalMax;
            [FieldOffset(48)] public int PhysicalMin;
            [FieldOffset(52)] public int PhysicalMax;
            [FieldOffset(56)] public ushort UsageMin;
            [FieldOffset(58)] public ushort UsageMax;
        }

        [DllImport("hid.dll")]
        public static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("hid.dll", CharSet = CharSet.Unicode)]
        public static extern bool HidD_GetProductString(IntPtr HidDeviceObject, StringBuilder Buffer, int BufferLength);

        [DllImport("hid.dll")]
        public static extern bool HidD_GetAttributes(IntPtr HidDeviceObject, ref HIDD_ATTRIBUTES Attributes);

        [DllImport("hid.dll")]
        public static extern bool HidD_GetPreparsedData(IntPtr HidDeviceObject, out IntPtr PreparsedData);

        [DllImport("hid.dll")]
        public static extern bool HidD_FreePreparsedData(IntPtr PreparsedData);

        [DllImport("hid.dll")]
        public static extern int HidP_GetCaps(IntPtr PreparsedData, out HIDP_CAPS Capabilities);

        [DllImport("hid.dll")]
        public static extern int HidP_GetValueCaps(int ReportType, [Out] HIDP_VALUE_CAPS[] ValueCaps, ref ushort ValueCapsLength, IntPtr PreparsedData);

        [DllImport("hid.dll")]
        public static extern int HidP_GetUsageValue(int ReportType, ushort UsagePage, ushort LinkCollection, ushort Usage, out uint UsageValue, IntPtr PreparsedData, byte[] Report, uint ReportLength);

        [DllImport("hid.dll")]
        public static extern uint HidP_MaxUsageListLength(int ReportType, ushort UsagePage, IntPtr PreparsedData);

        [DllImport("hid.dll")]
        public static extern int HidP_GetUsages(int ReportType, ushort UsagePage, ushort LinkCollection, [Out] ushort[] UsageList, ref uint UsageLength, IntPtr PreparsedData, byte[] Report, uint ReportLength);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetFeature(IntPtr HidDeviceObject, [Out] byte[] ReportBuffer, uint ReportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_SetFeature(IntPtr HidDeviceObject, byte[] ReportBuffer, uint ReportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetInputReport(IntPtr HidDeviceObject, [Out] byte[] ReportBuffer, uint ReportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_FlushQueue(IntPtr HidDeviceObject);

        [DllImport("kernel32.dll")]
        public static extern bool ResetEvent(IntPtr hEvent);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, IntPtr Enumerator, IntPtr hwndParent, int Flags);

        [DllImport("setupapi.dll", EntryPoint = "SetupDiGetClassDevs", SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevsAll(IntPtr ClassGuid, IntPtr Enumerator, IntPtr hwndParent, uint Flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInterfaces(IntPtr DeviceInfoSet, IntPtr DeviceInfoData, ref Guid InterfaceClassGuid, int MemberIndex, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr DeviceInfoSet, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData, IntPtr DeviceInterfaceDetailData, int DeviceInterfaceDetailDataSize, out int RequiredSize, IntPtr DeviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr DeviceInfoSet, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData, IntPtr DeviceInterfaceDetailData, int DeviceInterfaceDetailDataSize, out int RequiredSize, ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInfo(IntPtr DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiGetDevicePropertyW(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, ref DEVPROPKEY PropertyKey, out uint PropertyType, byte[] PropertyBuffer, uint PropertyBufferSize, out uint RequiredSize, uint Flags);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SetupDiGetDeviceRegistryProperty(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, uint Property, out uint PropertyRegDataType, StringBuilder PropertyBuffer, uint PropertyBufferSize, out uint RequiredSize);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInstanceIdW(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, StringBuilder DeviceInstanceId, int DeviceInstanceIdSize, out int RequiredSize);

        [DllImport("setupapi.dll")]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        [DllImport("cfgmgr32.dll")]
        public static extern uint CM_Get_DevNode_Status(out uint pulStatus, out uint pulProblemNumber, uint dnDevInst, uint ulFlags);

        [StructLayout(LayoutKind.Sequential)]
        public struct BLUETOOTH_DEVICE_SEARCH_PARAMS
        {
            public uint dwSize;
            public int fReturnAuthenticated;
            public int fReturnRemembered;
            public int fReturnUnknown;
            public int fReturnConnected;
            public int fIssueInquiry;
            public byte cTimeoutMultiplier;
            public IntPtr hRadio;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BLUETOOTH_SYSTEMTIME
        {
            public ushort wYear, wMonth, wDayOfWeek, wDay, wHour, wMinute, wSecond, wMilliseconds;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct BLUETOOTH_DEVICE_INFO
        {
            public uint dwSize;
            public ulong Address;
            public uint ulClassofDevice;
            [MarshalAs(UnmanagedType.Bool)] public bool fConnected;
            [MarshalAs(UnmanagedType.Bool)] public bool fRemembered;
            [MarshalAs(UnmanagedType.Bool)] public bool fAuthenticated;
            public BLUETOOTH_SYSTEMTIME stLastSeen;
            public BLUETOOTH_SYSTEMTIME stLastUsed;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)]
            public string szName;
        }

        [DllImport("bthprops.cpl", SetLastError = true)]
        public static extern IntPtr BluetoothFindFirstDevice(ref BLUETOOTH_DEVICE_SEARCH_PARAMS pbtsp, ref BLUETOOTH_DEVICE_INFO pbtdi);

        [DllImport("bthprops.cpl", SetLastError = true)]
        public static extern bool BluetoothFindNextDevice(IntPtr hFind, ref BLUETOOTH_DEVICE_INFO pbtdi);

        [DllImport("bthprops.cpl", SetLastError = true)]
        public static extern bool BluetoothFindDeviceClose(IntPtr hFind);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, int dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteFile(IntPtr hFile, IntPtr lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadFile(IntPtr hFile, IntPtr lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetOverlappedResult(IntPtr hFile, IntPtr lpOverlapped, out uint lpNumberOfBytesTransferred, bool bWait);

        [DllImport("kernel32.dll")]
        public static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);

        [DllImport("kernel32.dll")]
        public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll")]
        public static extern bool CancelIo(IntPtr hFile);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr hObject);

        public static bool OverlappedHidQuery(IntPtr hFile, byte[] tx, out byte[] rx, int len, uint timeoutMs)
        {
            rx = new byte[len];
            IntPtr evW = CreateEvent(IntPtr.Zero, true, false, null);
            IntPtr evR = CreateEvent(IntPtr.Zero, true, false, null);
            int ovSize = Marshal.SizeOf(typeof(NativeOverlapped));
            IntPtr pOvW = Marshal.AllocHGlobal(ovSize);
            IntPtr pOvR = Marshal.AllocHGlobal(ovSize);
            IntPtr pTx = Marshal.AllocHGlobal(len);
            IntPtr pRx = Marshal.AllocHGlobal(len);
            try
            {
                Marshal.Copy(tx, 0, pTx, len);
                for (int i = 0; i < len; i++) Marshal.WriteByte(pRx, i, 0);

                NativeOverlapped ovW = new NativeOverlapped { EventHandle = evW };
                Marshal.StructureToPtr(ovW, pOvW, false);
                uint wBytes;
                if (!WriteFile(hFile, pTx, (uint)len, out wBytes, pOvW))
                {
                    if (WaitForSingleObject(evW, timeoutMs) != 0)
                    {
                        CancelIo(hFile);
                        uint dummyW;
                        GetOverlappedResult(hFile, pOvW, out dummyW, true);
                        return false;
                    }
                }

                Thread.Sleep(20);

                NativeOverlapped ovR = new NativeOverlapped { EventHandle = evR };
                Marshal.StructureToPtr(ovR, pOvR, false);
                uint rBytes;
                if (ReadFile(hFile, pRx, (uint)len, out rBytes, pOvR))
                {
                    Marshal.Copy(pRx, rx, 0, len);
                    return rBytes > 0;
                }
                uint waitRes = WaitForSingleObject(evR, timeoutMs);
                if (waitRes == 0)
                {
                    uint transferred;
                    GetOverlappedResult(hFile, pOvR, out transferred, false);
                    Marshal.Copy(pRx, rx, 0, len);
                    return transferred > 0;
                }
                else
                {
                    CancelIo(hFile);
                    uint dummyR;
                    GetOverlappedResult(hFile, pOvR, out dummyR, true);
                    return false;
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                Marshal.FreeHGlobal(pTx);
                Marshal.FreeHGlobal(pRx);
                Marshal.FreeHGlobal(pOvW);
                Marshal.FreeHGlobal(pOvR);
                if (evW != IntPtr.Zero) CloseHandle(evW);
                if (evR != IntPtr.Zero) CloseHandle(evR);
            }
        }

        private static readonly Dictionary<string, string> usbProdNameCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string ReadUsbProductName(string hidPath, string fallback)
        {
            string cached;
            if (usbProdNameCache.TryGetValue(hidPath, out cached)) return cached;
            try
            {
                IntPtr h = CreateFile(hidPath, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                if (h != IntPtr.Zero && h != new IntPtr(-1))
                {
                    try
                    {
                        StringBuilder sb = new StringBuilder(128);
                        if (HidD_GetProductString(h, sb, 256))
                        {
                            string s = sb.ToString().Trim();
                            if (!string.IsNullOrEmpty(s))
                            {
                                usbProdNameCache[hidPath] = s;
                                return s;
                            }
                        }
                    }
                    finally
                    {
                        CloseHandle(h);
                    }
                }
            }
            catch { }
            return fallback;
        }

        private static bool IsGenericBluetoothNodeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return true;
            string n = name.Trim();
            return n.Equals("Bluetooth Device", StringComparison.OrdinalIgnoreCase) ||
                   n.Equals("Bluetooth Low Energy GATT compliant HID device", StringComparison.OrdinalIgnoreCase) ||
                   n.Equals("Microsoft Bluetooth Hands-Free Profile AudioGateway role", StringComparison.OrdinalIgnoreCase) ||
                   n.Equals("HID-compliant consumer control device", StringComparison.OrdinalIgnoreCase) ||
                   n.Equals("HID-compliant headset", StringComparison.OrdinalIgnoreCase) ||
                   n.Equals("Microsoft Bluetooth A2dp Source", StringComparison.OrdinalIgnoreCase) ||
                   n.IndexOf("RFCOMM Protocol TDI", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string CleanWirelessDeviceName(string rawName, string fallbackBaseName)
        {
            string cleanedRaw = StripAudioProfileSuffixes(rawName);
            string cleanedBase = StripAudioProfileSuffixes(fallbackBaseName);

            if (!string.IsNullOrEmpty(cleanedBase) && !IsGenericBluetoothNodeName(cleanedBase))
            {
                if (string.IsNullOrEmpty(cleanedRaw) || IsGenericBluetoothNodeName(cleanedRaw) ||
                    cleanedBase.Length <= cleanedRaw.Length)
                {
                    return cleanedBase;
                }
            }
            if (!string.IsNullOrEmpty(cleanedRaw)) return cleanedRaw;
            return !string.IsNullOrEmpty(cleanedBase) ? cleanedBase : (rawName ?? "").Trim();
        }

        private static string StripAudioProfileSuffixes(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            string s = Regex.Replace(name.Trim(), @"\s+", " ");
            string prev;
            do
            {
                prev = s;
                s = Regex.Replace(
                    s,
                    @"(?i)[\s\-–—_]*(?:\(\s*)?(?:hands[\s\-]*free(?:\s+ag)?(?:\s+audio)?|hfp(?:\s+ag)?|stereo(?:\s+audio)?|avrcp\s+transport|a2dp(?:\s+snk)?|audio\s+gateway|audio\s+sink|headset\s+earphone|le\s+audio)(?:\s*\))?\s*$",
                    "").Trim();
                s = s.TrimEnd('-', '–', '—', '_', ' ');
            } while (s != prev && s.Length > 0);
            return s;
        }

        public static bool IsBluetoothTransport(string enumerator, string instanceId, string hardwareIds)
        {
            string en = (enumerator ?? "").Trim();
            string id = (instanceId ?? "").Trim();
            string hw = (hardwareIds ?? "").Trim();

            if (en.StartsWith("BTH", StringComparison.OrdinalIgnoreCase)) return true;
            if (id.StartsWith("BTH", StringComparison.OrdinalIgnoreCase)) return true;
            if (id.IndexOf("BTHENUM", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("BTHLE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("{00001812-", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("{0000180F-", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (hw.IndexOf("BTHENUM", StringComparison.OrdinalIgnoreCase) >= 0 ||
                hw.IndexOf("BTHLE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                hw.IndexOf("{00001812-", StringComparison.OrdinalIgnoreCase) >= 0 ||
                hw.IndexOf("{0000180F-", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }

        private static string ExtractBtMacFromInstanceId(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return "";
            Match m = Regex.Match(instanceId, @"(?i)(?:DEV_|&|_)([0-9A-F]{12})(?:_|&|\\|$)");
            return m.Success ? m.Groups[1].Value.ToUpperInvariant() : "";
        }

        private static void QueryClassicBluetoothStates(out Dictionary<string, bool> connectedByMac, out Dictionary<string, bool> connectedByName)
        {
            connectedByMac = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            connectedByName = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            try
            {
                BLUETOOTH_DEVICE_SEARCH_PARAMS sp = new BLUETOOTH_DEVICE_SEARCH_PARAMS
                {
                    dwSize = (uint)Marshal.SizeOf(typeof(BLUETOOTH_DEVICE_SEARCH_PARAMS)),
                    fReturnAuthenticated = 1,
                    fReturnRemembered = 1,
                    fReturnUnknown = 0,
                    fReturnConnected = 1,
                    fIssueInquiry = 0,
                    cTimeoutMultiplier = 0,
                    hRadio = IntPtr.Zero
                };
                BLUETOOTH_DEVICE_INFO info = new BLUETOOTH_DEVICE_INFO
                {
                    dwSize = (uint)Marshal.SizeOf(typeof(BLUETOOTH_DEVICE_INFO))
                };

                IntPtr hFind = BluetoothFindFirstDevice(ref sp, ref info);
                if (hFind != IntPtr.Zero)
                {
                    try
                    {
                        do
                        {
                            string mac = info.Address.ToString("X12");
                            bool prevMac;
                            if (!connectedByMac.TryGetValue(mac, out prevMac) || info.fConnected)
                                connectedByMac[mac] = info.fConnected;

                            string cleanName = StripAudioProfileSuffixes(info.szName);
                            if (!string.IsNullOrEmpty(cleanName))
                            {
                                bool prevNm;
                                if (!connectedByName.TryGetValue(cleanName, out prevNm) || info.fConnected)
                                    connectedByName[cleanName] = info.fConnected;
                            }
                        }
                        while (BluetoothFindNextDevice(hFind, ref info));
                    }
                    finally
                    {
                        BluetoothFindDeviceClose(hFind);
                    }
                }
            }
            catch { }
        }

        private class HidCollectionInfo
        {
            public string Path;
            public ushort Vid;
            public ushort Pid;
            public ushort UsagePage;
            public ushort Usage;
            public ushort InLen;
            public ushort OutLen;
            public ushort FeatLen;
            public ushort NumInputValueCaps;
            public ushort NumFeatureValueCaps;
            public Guid ContainerId;
        }

        private class PnpBatteryCandidate
        {
            public string RawName;
            public string DevClass;
            public string InstanceId;
            public string Enumerator;
            public string HardwareIds;
            public Guid ContainerId;
            public int Pct;
            public bool IsConnected;
        }

        private static bool OverlappedHidWriteOnly(IntPtr hFile, byte[] tx, int timeoutMs)
        {
            if (hFile == IntPtr.Zero || hFile == new IntPtr(-1) || tx == null || tx.Length == 0) return false;
            IntPtr evW = CreateEvent(IntPtr.Zero, true, false, null);
            int ovSize = Marshal.SizeOf(typeof(NativeOverlapped));
            IntPtr pOvW = Marshal.AllocHGlobal(ovSize);
            IntPtr pTx = Marshal.AllocHGlobal(tx.Length);
            try
            {
                Marshal.Copy(tx, 0, pTx, tx.Length);
                NativeOverlapped ovW = new NativeOverlapped { EventHandle = evW };
                Marshal.StructureToPtr(ovW, pOvW, false);
                uint wBytes;
                if (WriteFile(hFile, pTx, (uint)tx.Length, out wBytes, pOvW)) return true;
                if (WaitForSingleObject(evW, (uint)Math.Max(50, timeoutMs)) == 0)
                {
                    return GetOverlappedResult(hFile, pOvW, out wBytes, false);
                }
                CancelIo(hFile);
                uint dummyW;
                GetOverlappedResult(hFile, pOvW, out dummyW, true);
                return false;
            }
            catch { return false; }
            finally
            {
                Marshal.FreeHGlobal(pTx);
                Marshal.FreeHGlobal(pOvW);
                if (evW != IntPtr.Zero) CloseHandle(evW);
            }
        }

        private static int OverlappedHidReadOne(IntPtr hFile, int rxLen, out byte[] rxOut, int timeoutMs, bool stripLeadingZero)
        {
            rxOut = null;
            if (hFile == IntPtr.Zero || hFile == new IntPtr(-1) || rxLen <= 0) return -1;
            IntPtr evR = CreateEvent(IntPtr.Zero, true, false, null);
            int ovSize = Marshal.SizeOf(typeof(NativeOverlapped));
            IntPtr pOvR = Marshal.AllocHGlobal(ovSize);
            IntPtr pRx = Marshal.AllocHGlobal(rxLen);
            try
            {
                for (int i = 0; i < rxLen; i++) Marshal.WriteByte(pRx, i, 0);
                NativeOverlapped ovR = new NativeOverlapped { EventHandle = evR };
                Marshal.StructureToPtr(ovR, pOvR, false);
                uint rBytes = 0;
                bool got = false;
                if (ReadFile(hFile, pRx, (uint)rxLen, out rBytes, pOvR))
                {
                    got = rBytes > 0;
                }
                else
                {
                    uint waitRes = WaitForSingleObject(evR, (uint)Math.Max(1, timeoutMs));
                    if (waitRes == 0)
                    {
                        if (GetOverlappedResult(hFile, pOvR, out rBytes, false)) got = rBytes > 0;
                    }
                    else
                    {
                        CancelIo(hFile);
                        uint dummyR;
                        GetOverlappedResult(hFile, pOvR, out dummyR, true);
                        return 0;
                    }
                }
                if (got)
                {
                    int copyLen = (int)Math.Min(rBytes, (uint)rxLen);
                    if (copyLen > 0)
                    {
                        byte[] buf = new byte[copyLen];
                        Marshal.Copy(pRx, buf, 0, copyLen);
                        int offset = (stripLeadingZero && copyLen > 1 && buf[0] == 0x00) ? 1 : 0;
                        byte[] payload = new byte[copyLen - offset];
                        Array.Copy(buf, offset, payload, 0, payload.Length);
                        rxOut = payload;
                        return payload.Length;
                    }
                }
                return 0;
            }
            catch { return -1; }
            finally
            {
                Marshal.FreeHGlobal(pRx);
                Marshal.FreeHGlobal(pOvR);
                if (evR != IntPtr.Zero) CloseHandle(evR);
            }
        }

        private static int InterpolateBatteryCurve(int mv, int[] v, int[] p)
        {
            if (v == null || p == null || v.Length == 0 || v.Length != p.Length) return 0;
            if (mv >= v[0]) return p[0];
            int last = v.Length - 1;
            if (mv <= v[last]) return p[last];
            for (int i = 0; i < last; i++)
            {
                if (mv <= v[i] && mv >= v[i + 1])
                {
                    int span = v[i] - v[i + 1];
                    if (span <= 0) return p[i];
                    double ratio = (double)(mv - v[i + 1]) / span;
                    return Math.Max(0, Math.Min(100, (int)Math.Round(p[i + 1] + ratio * (p[i] - p[i + 1]))));
                }
            }
            return p[last];
        }

        private static int LogitechLiIonPercent(int mv)
        {
            int[] v = { 4186, 4067, 3989, 3922, 3859, 3811, 3778, 3751, 3717, 3671, 3646, 3579, 3500 };
            int[] p = { 100, 90, 80, 70, 60, 50, 40, 30, 20, 10, 5, 2, 0 };
            return InterpolateBatteryCurve(mv, v, p);
        }

        private static int LogitechHeadsetPercent(ushort pid, int mv)
        {
            int[] g533v = { 4200, 3850, 3790, 3750, 3680, 3330 }, g533p = { 100, 50, 30, 20, 5, 0 };
            int[] g535v = { 4175, 3817, 3766, 3730, 3664, 3310 }, g535p = { 100, 50, 30, 20, 5, 0 };
            int[] g633v = { 4100, 3950, 3850, 3750, 3650, 3500, 3300, 3150 }, g633p = { 100, 80, 60, 40, 20, 10, 5, 0 };
            int[] g733v = { 4175, 4127, 4086, 4054, 4001, 3968, 3944, 3914, 3875, 3840, 3818, 3800, 3783, 3771, 3759, 3744, 3725, 3695, 3675, 3630, 3341 };
            int[] g733p = { 100, 95, 90, 85, 80, 75, 70, 65, 60, 55, 50, 45, 40, 35, 30, 25, 20, 15, 10, 5, 0 };
            int[] gprov = { 4150, 3830, 3780, 3740, 3670, 3320 }, gprop = { 100, 50, 30, 20, 5, 0 };
            int[] proxv = { 4102, 4046, 3993, 3950, 3915, 3877, 3838, 3809, 3789, 3772, 3758, 3747, 3736, 3722, 3707, 3687, 3661, 3644, 3548, 3426, 3293 };
            int[] proxp = { 100, 95, 90, 85, 80, 75, 70, 65, 60, 55, 50, 45, 40, 35, 30, 25, 20, 15, 10, 5, 0 };
            int[] defv = { 4100, 3850, 3780, 3730, 3650, 3300 }, defp = { 100, 50, 30, 20, 5, 0 };
            switch (pid)
            {
                case 0x0A66: return InterpolateBatteryCurve(mv, g533v, g533p);
                case 0x0AC4: return InterpolateBatteryCurve(mv, g535v, g535p);
                case 0x0A5C: case 0x0A89: case 0x0A5B: case 0x0A87: case 0x0AFE: return InterpolateBatteryCurve(mv, g633v, g633p);
                case 0x0AB5: return InterpolateBatteryCurve(mv, g733v, g733p);
                case 0x0AAA: case 0x0ABA: return InterpolateBatteryCurve(mv, proxv, proxp);
                case 0x0AA7: case 0x0AFB: return InterpolateBatteryCurve(mv, gprov, gprop);
                default: return InterpolateBatteryCurve(mv, defv, defp);
            }
        }

        private static string GetHidPhysicalKey(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            string p = path.ToLowerInvariant();
            int a = p.IndexOf('#');
            int b = a < 0 ? -1 : p.IndexOf('#', a + 1);
            int e = b < 0 ? -1 : p.IndexOf('#', b + 1);
            if (e < 0) return "";
            string hw = p.Substring(a + 1, b - a - 1);
            string inst = p.Substring(b + 1, e - b - 1);
            int col = hw.IndexOf("&col", StringComparison.Ordinal);
            if (col >= 0 && col + 6 <= hw.Length) hw = hw.Remove(col, 6);
            int amp = inst.LastIndexOf('&');
            if (amp >= 0) inst = inst.Substring(0, amp);
            return hw + "#" + inst;
        }

        private static int HidppRequest(IntPtr hShort, IntPtr hLong, byte idx, byte b2, byte b3, byte[] prms, out byte[] replyParams, out byte err, bool forceShort, int timeoutMs)
        {
            replyParams = null;
            err = 0;
            bool hasLong = hLong != IntPtr.Zero && hLong != new IntPtr(-1);
            bool hasShort = hShort != IntPtr.Zero && hShort != new IntPtr(-1);
            bool useLong = hasLong && !forceShort;
            if (!useLong && !hasShort) return -1;

            byte[] msg = new byte[useLong ? 20 : 7];
            msg[0] = (byte)(useLong ? 0x11 : 0x10);
            msg[1] = idx;
            msg[2] = b2;
            msg[3] = b3;
            if (prms != null)
            {
                for (int i = 0; i < prms.Length && (4 + i) < msg.Length; i++) msg[4 + i] = prms[i];
            }

            if (hasLong) HidD_FlushQueue(hLong);
            if (hasShort) HidD_FlushQueue(hShort);
            if (!OverlappedHidWriteOnly(useLong ? hLong : hShort, msg, 120)) return -1;

            Stopwatch sw = Stopwatch.StartNew();
            IntPtr[] handles = new IntPtr[] { hLong, hShort };
            int[] rxLens = new int[] { 20, 7 };
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                for (int hi = 0; hi < 2; hi++)
                {
                    IntPtr h = handles[hi];
                    if (h == IntPtr.Zero || h == new IntPtr(-1)) continue;
                    while (sw.ElapsedMilliseconds < timeoutMs)
                    {
                        int left = Math.Min(20, Math.Max(5, (int)(timeoutMs - sw.ElapsedMilliseconds)));
                        byte[] r;
                        int n = OverlappedHidReadOne(h, rxLens[hi], out r, left, false);
                        if (n < 7 || r == null) break;
                        if ((r[0] != 0x10 && r[0] != 0x11) || r[1] != idx) continue;
                        if (r[2] == b2 && r[3] == b3)
                        {
                            replyParams = new byte[r.Length - 4];
                            Array.Copy(r, 4, replyParams, 0, replyParams.Length);
                            return 1;
                        }
                        if ((r[2] == 0xFF || r[2] == 0x8F) && r[3] == b2 && r[4] == b3)
                        {
                            err = r.Length > 5 ? r[5] : (byte)0;
                            return 0;
                        }
                    }
                }
            }
            return -1;
        }

        private static bool HidppCall20(IntPtr hShort, IntPtr hLong, byte idx, byte feature, byte fn, byte[] prms, out byte[] replyParams)
        {
            byte err;
            return HidppRequest(hShort, hLong, idx, feature, (byte)((fn << 4) | 0x0D), prms, out replyParams, out err, false, 450) == 1 && replyParams != null;
        }

        private static byte HidppFeatureIndex(IntPtr hShort, IntPtr hLong, byte idx, ushort featureId)
        {
            byte[] p;
            if (HidppCall20(hShort, hLong, idx, 0x00, 0, new byte[] { (byte)(featureId >> 8), (byte)(featureId & 0xFF) }, out p) && p.Length > 0)
                return p[0];
            return 0;
        }

        private static void PollLogitechHidpp(
            List<HidCollectionInfo> colls,
            bool isEn,
            HashSet<uint> claimedVidPids,
            Action<ushort, ushort, Guid> claimDev,
            Dictionary<string, int> lastKnownPcts,
            HashSet<string> addedNames,
            List<WirelessDeviceReading> results)
        {
            Dictionary<ushort, List<HidCollectionInfo>> byPid = new Dictionary<ushort, List<HidCollectionInfo>>();
            foreach (HidCollectionInfo c in colls)
            {
                if (c.Vid != 0x046D) continue;
                List<HidCollectionInfo> list;
                if (!byPid.TryGetValue(c.Pid, out list)) { list = new List<HidCollectionInfo>(); byPid[c.Pid] = list; }
                list.Add(c);
            }

            HashSet<string> seenUnitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in byPid)
            {
                ushort pid = kv.Key;
                uint vpk = ((uint)0x046D << 16) | pid;
                if (claimedVidPids.Contains(vpk)) continue;

                bool passthrough = (pid == 0xC54F); // PRO X3 SUPERSTRIKE passthrough dongle
                List<HidCollectionInfo> allShorts = new List<HidCollectionInfo>();
                List<HidCollectionInfo> allLongs = new List<HidCollectionInfo>();
                foreach (HidCollectionInfo c in kv.Value)
                {
                    if (c.OutLen == 7 && ((c.UsagePage == 0xFF00 && c.Usage == 0x0001) || (passthrough && c.UsagePage == 0xFF43 && c.Usage == 0x0301)))
                        allShorts.Add(c);
                    else if (c.OutLen == 20 && ((c.UsagePage == 0xFF00 && c.Usage == 0x0002) || (c.UsagePage == 0xFF43 && (c.Usage == 0x0202 || (passthrough && c.Usage == 0x0302)))))
                        allLongs.Add(c);
                }
                if (allShorts.Count == 0 && allLongs.Count == 0) continue;

                List<string> groups = new List<string>();
                foreach (HidCollectionInfo c in allLongs) { string k = GetHidPhysicalKey(c.Path); if (!groups.Contains(k)) groups.Add(k); }
                foreach (HidCollectionInfo c in allShorts) { string k = GetHidPhysicalKey(c.Path); if (!groups.Contains(k)) groups.Add(k); }

                bool anyFoundForPid = false;
                for (int gi = 0; gi < groups.Count; gi++)
                {
                    string gk = groups[gi];
                    HidCollectionInfo shortC = null, longC = null;
                    foreach (HidCollectionInfo c in allShorts) if (GetHidPhysicalKey(c.Path) == gk) { shortC = c; break; }
                    foreach (HidCollectionInfo c in allLongs) if (GetHidPhysicalKey(c.Path) == gk) { longC = c; break; }

                    IntPtr hShort = (shortC != null) ? CreateFile(shortC.Path, 0xC0000000U, 3, IntPtr.Zero, 3, 0x40000000U, IntPtr.Zero) : IntPtr.Zero;
                    IntPtr hLong = (longC != null) ? CreateFile(longC.Path, 0xC0000000U, 3, IntPtr.Zero, 3, 0x40000000U, IntPtr.Zero) : IntPtr.Zero;
                    if (hShort == new IntPtr(-1)) hShort = IntPtr.Zero;
                    if (hLong == new IntPtr(-1)) hLong = IntPtr.Zero;
                    if (hShort == IntPtr.Zero && hLong == IntPtr.Zero) continue;

                    try
                    {
                        HidCollectionInfo refC = longC ?? shortC;
                        string product = ReadUsbProductName(refC.Path, "");
                        bool isReceiver = product.IndexOf("Receiver", StringComparison.OrdinalIgnoreCase) >= 0;
                        string lowPath = (refC.Path ?? "").ToLowerInvariant();
                        bool isBt = lowPath.IndexOf("{00001812-", StringComparison.Ordinal) >= 0 || lowPath.IndexOf("{00001124-", StringComparison.Ordinal) >= 0;

                        byte[] indices;
                        byte[] pingP;
                        byte pingErr;
                        if (passthrough)
                            indices = new byte[] { 1 };
                        else if (isBt || (!isReceiver && HidppRequest(hShort, hLong, 0xFF, 0x00, 0x1D, new byte[] { 0, 0, 0x5A }, out pingP, out pingErr, false, 350) == 1 && pingP != null && pingP.Length >= 3 && pingP[0] >= 2))
                            indices = new byte[] { 0xFF };
                        else
                            indices = new byte[] { 1, 2, 3, 4, 5, 6 };

                        for (int ii = 0; ii < indices.Length; ii++)
                        {
                            byte idx = indices[ii];
                            string cacheKey = string.Format("LOGI_{0:X4}_{1}_{2}", pid, gi, idx);
                            int pingTimeout = (idx == 0xFF) ? 350 : 600;
                            byte[] p;
                            byte err;
                            int r = HidppRequest(hShort, hLong, idx, 0x00, 0x1D, new byte[] { 0, 0, 0x5A }, out p, out err, false, pingTimeout);

                            int pct = -1;
                            bool charging = false;
                            int mv = 0;
                            string devName = (idx == 0xFF && !string.IsNullOrEmpty(product)) ? product : "Logitech Wireless";
                            string kindHint = passthrough ? "Mouse" : "";
                            string unitId = "";
                            bool found = false;

                            if (r == 1 && p != null && p.Length >= 3 && p[0] >= 2)
                            {
                                // Name & Type (0x0005)
                                byte fName = HidppFeatureIndex(hShort, hLong, idx, 0x0005);
                                if (fName != 0)
                                {
                                    byte[] np;
                                    if (HidppCall20(hShort, hLong, idx, fName, 0, null, out np) && np.Length > 0)
                                    {
                                        int nameLen = Math.Min(64, (int)np[0]);
                                        StringBuilder nsb = new StringBuilder(nameLen);
                                        while (nsb.Length < nameLen)
                                        {
                                            byte[] chunk;
                                            if (!HidppCall20(hShort, hLong, idx, fName, 1, new byte[] { (byte)nsb.Length }, out chunk) || chunk.Length == 0) break;
                                            int before = nsb.Length;
                                            for (int ci = 0; ci < chunk.Length && nsb.Length < nameLen; ci++)
                                            {
                                                if (chunk[ci] == 0) break;
                                                nsb.Append((char)chunk[ci]);
                                            }
                                            if (nsb.Length == before) break;
                                        }
                                        string nStr = nsb.ToString().Trim();
                                        if (!string.IsNullOrEmpty(nStr))
                                        {
                                            devName = (nStr.StartsWith("Logitech", StringComparison.OrdinalIgnoreCase) ||
                                                       nStr.StartsWith("G ", StringComparison.OrdinalIgnoreCase) ||
                                                       nStr.StartsWith("MX", StringComparison.OrdinalIgnoreCase) ||
                                                       nStr.StartsWith("PRO", StringComparison.OrdinalIgnoreCase))
                                                ? nStr : ("Logitech " + nStr);
                                        }
                                    }
                                    if (HidppCall20(hShort, hLong, idx, fName, 2, null, out np) && np.Length > 0)
                                    {
                                        byte t = np[0];
                                        if (t == 0x00 || t == 0x02) kindHint = "Keyboard";
                                        else if (t == 0x03 || t == 0x04 || t == 0x05) kindHint = "Mouse";
                                        else if (t == 0x08) kindHint = "Headset";
                                        else if (t == 0x0B || t == 0x0C) kindHint = "Gamepad";
                                    }
                                }

                                // Battery: 0x1004 (Unified), 0x1000 (Status), 0x1001 (Voltage), 0x1F20 (Headset ADC)
                                byte[] bp;
                                byte fBat;
                                if ((fBat = HidppFeatureIndex(hShort, hLong, idx, 0x1004)) != 0 && HidppCall20(hShort, hLong, idx, fBat, 1, null, out bp) && bp.Length >= 3)
                                {
                                    int soc = bp[0], lvl = bp[1], st = bp[2];
                                    if (soc > 0 && soc <= 100) pct = soc;
                                    else if (lvl != 0) pct = (lvl & 8) != 0 ? 100 : ((lvl & 4) != 0 ? 60 : ((lvl & 2) != 0 ? 20 : 5));
                                    charging = (st == 1 || st == 2 || st == 3);
                                    found = (pct > 0);
                                }
                                else if ((fBat = HidppFeatureIndex(hShort, hLong, idx, 0x1000)) != 0 && HidppCall20(hShort, hLong, idx, fBat, 0, null, out bp) && bp.Length >= 3)
                                {
                                    int st = bp[2];
                                    charging = (st >= 1 && st <= 4);
                                    pct = bp[0] <= 100 ? (int)bp[0] : -1;
                                    if (pct == 0 && st == 3) pct = 100;
                                    found = (pct > 0);
                                }
                                else if ((fBat = HidppFeatureIndex(hShort, hLong, idx, 0x1001)) != 0 && HidppCall20(hShort, hLong, idx, fBat, 0, null, out bp) && bp.Length >= 3)
                                {
                                    mv = (bp[0] << 8) | bp[1];
                                    charging = (bp[2] & 0x80) != 0;
                                    if (mv > 0) { pct = LogitechLiIonPercent(mv); found = true; }
                                }
                                else if ((fBat = HidppFeatureIndex(hShort, hLong, idx, 0x1F20)) != 0 && HidppCall20(hShort, hLong, idx, fBat, 0, null, out bp) && bp.Length >= 3)
                                {
                                    mv = (bp[0] << 8) | bp[1];
                                    charging = (bp[2] == 0x03);
                                    if (mv > 0) { pct = LogitechHeadsetPercent(pid, mv); found = true; }
                                    if (string.IsNullOrEmpty(kindHint)) kindHint = "Headset";
                                }

                                if (found)
                                {
                                    byte fInfo = HidppFeatureIndex(hShort, hLong, idx, 0x0003);
                                    byte[] ip;
                                    if (fInfo != 0 && HidppCall20(hShort, hLong, idx, fInfo, 0, null, out ip) && ip.Length >= 5 && (ip[1] != 0 || ip[2] != 0 || ip[3] != 0 || ip[4] != 0))
                                        unitId = string.Format("{0:X2}{1:X2}{2:X2}{3:X2}", ip[1], ip[2], ip[3], ip[4]);
                                    if (string.IsNullOrEmpty(kindHint) && longC != null && longC.UsagePage == 0xFF43)
                                        kindHint = "Headset";
                                }
                            }
                            else if (r == 0 && err == 0x01)
                            {
                                // HID++ 1.0 fallback (registers 0x0D and 0x07)
                                byte[] bp;
                                byte bErr;
                                if (HidppRequest(hShort, hLong, idx, 0x81, 0x0D, null, out bp, out bErr, true, 350) == 1 && bp != null && bp.Length >= 3 && bp[0] > 0 && bp[0] <= 100)
                                {
                                    pct = bp[0];
                                    charging = (bp[2] >= 0x21 && bp[2] <= 0x26);
                                    found = true;
                                }
                                else if (HidppRequest(hShort, hLong, idx, 0x81, 0x07, null, out bp, out bErr, true, 350) == 1 && bp != null && bp.Length >= 2 && bp[0] > 0)
                                {
                                    int[] lvlPct = { 0, 5, 10, 20, 35, 50, 75, 100 };
                                    pct = lvlPct[bp[0] > 7 ? 7 : bp[0]];
                                    charging = (bp[1] >= 0x21 && bp[1] <= 0x26);
                                    found = true;
                                }
                            }

                            if (found && pct > 0 && pct <= 100)
                            {
                                anyFoundForPid = true;
                                if (!string.IsNullOrEmpty(unitId) && !seenUnitIds.Add(unitId)) continue;
                                if (addedNames.Contains(devName)) continue;
                                lastKnownPcts[cacheKey] = pct;

                                string icon, typeLabel;
                                ClassifyDeviceTypeAndIcon(devName, kindHint, isEn, out icon, out typeLabel);
                                string linkText = isBt ? "Bluetooth" : (idx == 0xFF ? "USB" : "2.4GHz");
                                string detail = charging
                                    ? (typeLabel + " • " + (isEn ? "⚡ Charging" : "⚡ Şarjda"))
                                    : (mv > 3000 && mv < 4500 ? string.Format("{0} • {1:0.00}V", typeLabel, mv / 1000.0) : (typeLabel + " • " + linkText));
                                results.Add(new WirelessDeviceReading
                                {
                                    IdKey = !string.IsNullOrEmpty(unitId) ? ("LOGI_UNIT_" + unitId) : cacheKey,
                                    Icon = icon,
                                    Name = devName,
                                    Detail = detail,
                                    PctText = isEn ? (pct + "%") : ("%" + pct),
                                    BarPct = pct,
                                    IsCharging = charging
                                });
                                addedNames.Add(devName);
                            }
                        }
                    }
                    finally
                    {
                        if (hShort != IntPtr.Zero) CloseHandle(hShort);
                        if (hLong != IntPtr.Zero) CloseHandle(hLong);
                    }
                }

                if (anyFoundForPid)
                {
                    foreach (HidCollectionInfo c in kv.Value) claimDev(c.Vid, c.Pid, c.ContainerId);
                }
            }
        }

        private static byte RazerKeyboardTid(ushort pid)
        {
            switch (pid)
            {
                case 0x025C: case 0x0271: case 0x0290: case 0x0296:
                case 0x02BA: case 0x02C9: case 0x02D2: case 0x02D5: return 0x9F;
                case 0x0258: case 0x0292: case 0x0298: case 0x02B9:
                case 0x02CD: case 0x02D4: case 0x02D7: return 0x1F;
                case 0x025A: return 0x3F;
                default: return 0;
            }
        }

        // Returns: 1 = Ok, 2 = Offline, 0 = Unsupported/NoReply
        private static int RazerFeatureCommand(IntPtr hDev, byte tid, byte cls, byte id, byte size, out byte[] args, byte firstArg, bool fastSlotQuery)
        {
            args = null;
            byte[] rep = new byte[91];
            rep[0] = 0x00;
            rep[1 + 1] = tid;
            rep[1 + 5] = size;
            rep[1 + 6] = cls;
            rep[1 + 7] = id;
            rep[1 + 8] = firstArg;
            byte crc = 0;
            for (int i = 2; i < 88; i++) crc ^= rep[1 + i];
            rep[1 + 88] = crc;

            byte[] resp = new byte[91];
            bool send = true;
            int res = 0;
            int maxAttempts = fastSlotQuery ? 4 : 6;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (send && !HidD_SetFeature(hDev, rep, (uint)rep.Length)) return 0;
                System.Threading.Thread.Sleep(fastSlotQuery ? 6 : (attempt == 0 ? 15 : 30));
                send = true;
                Array.Clear(resp, 0, resp.Length);
                resp[0] = 0x00;
                if (!HidD_GetFeature(hDev, resp, (uint)resp.Length)) continue;

                byte replyCrc = 0;
                for (int i = 2; i < 88; i++) replyCrc ^= resp[1 + i];
                if (resp[1 + 1] != tid || resp[1 + 2] != 0 || resp[1 + 3] != 0 || resp[1 + 4] != 0 ||
                    resp[1 + 5] != size || resp[1 + 6] != cls || resp[1 + 7] != id || resp[1 + 88] != replyCrc)
                    continue;

                byte st = resp[1 + 0];
                if (st == 0x01) { send = false; continue; } // busy
                if (st == 0x02)
                {
                    args = new byte[80];
                    Array.Copy(resp, 1 + 8, args, 0, 80);
                    return 1;
                }
                if (st == 0x04) { res = 2; continue; } // offline/timeout
                if (st == 0x03 || st == 0x05) return 0; // failed / unsupported
            }
            return res;
        }

        private static string CleanRazerName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Razer Device";
            string n = Regex.Replace(raw.Trim(), "(?i)\\s*(hyperspeed\\s*dongle|wireless\\s*receiver|dongle|receiver|\\(wired\\)|\\(wireless\\))$", "").Trim();
            return string.IsNullOrEmpty(n) ? "Razer Device" : n;
        }

        private static void PollRazerHid(
            List<HidCollectionInfo> colls,
            bool isEn,
            HashSet<uint> claimedVidPids,
            Action<ushort, ushort, Guid> claimDev,
            Dictionary<string, int> lastKnownPcts,
            HashSet<string> addedNames,
            List<WirelessDeviceReading> results)
        {
            Dictionary<ushort, List<HidCollectionInfo>> byPid = new Dictionary<ushort, List<HidCollectionInfo>>();
            foreach (HidCollectionInfo c in colls)
            {
                if (c.Vid != 0x1532) continue;
                List<HidCollectionInfo> list;
                if (!byPid.TryGetValue(c.Pid, out list)) { list = new List<HidCollectionInfo>(); byPid[c.Pid] = list; }
                list.Add(c);
            }

            foreach (var kv in byPid)
            {
                ushort pid = kv.Key;
                uint vpk = ((uint)0x1532 << 16) | pid;
                if (claimedVidPids.Contains(vpk)) continue;

                // 1F-A: Razer BlackShark V2 HyperSpeed (1532:0565, MediaTek 64-byte Report 0x02)
                if (pid == 0x0565)
                {
                    HidCollectionInfo targetC = null;
                    foreach (HidCollectionInfo c in kv.Value)
                        if (c.UsagePage == 0xFF14 && c.Usage == 0x01 && c.OutLen >= 64 && c.InLen >= 64) { targetC = c; break; }
                    if (targetC == null)
                        foreach (HidCollectionInfo c in kv.Value)
                            if (c.UsagePage == 0xFF00 && c.Usage == 0x01 && c.OutLen >= 64 && c.InLen >= 64) { targetC = c; break; }

                    if (targetC != null)
                    {
                        IntPtr hBs = CreateFile(targetC.Path, 0xC0000000U, 3, IntPtr.Zero, 3, 0x40000000U, IntPtr.Zero);
                        if (hBs != IntPtr.Zero && hBs != new IntPtr(-1))
                        {
                            try
                            {
                                Func<byte, byte, int> queryBs = (cmd, seq) =>
                                {
                                    byte[] tx = new byte[64];
                                    tx[0] = 0x02; tx[2] = seq; tx[6] = 0x04; tx[9] = 0x80; tx[10] = cmd;
                                    byte xor = 0;
                                    for (int i = 0; i < 62; i++) xor ^= tx[i];
                                    tx[62] = xor;
                                    HidD_FlushQueue(hBs);
                                    byte[] rx;
                                    if (OverlappedHidReadWithRetry(hBs, tx, targetC.InLen, out rx, r =>
                                    {
                                        if (r.Length < 64 || r[0] != 0x02 || r[2] != seq || r[10] != cmd || r[12] < 1) return false;
                                        byte csum = 0;
                                        for (int i = 1; i < 62; i++) csum ^= r[i];
                                        return csum == r[62];
                                    }, 500))
                                    {
                                        return rx[13];
                                    }
                                    return -1;
                                };
                                int pct = queryBs(0x21, 0x61);
                                if (pct > 0 && pct <= 100)
                                {
                                    int chg = queryBs(0x2A, 0x62);
                                    bool charging = chg > 0;
                                    string name = "Razer BlackShark V2 HyperSpeed";
                                    if (!addedNames.Contains(name))
                                    {
                                        results.Add(new WirelessDeviceReading
                                        {
                                            IdKey = "RAZER_BS_0565",
                                            Icon = "🎧",
                                            Name = name,
                                            Detail = charging ? (isEn ? "Headset • ⚡ Charging" : "Kulaklık • ⚡ Şarjda") : (isEn ? "Headset • 2.4GHz" : "Kulaklık • 2.4GHz"),
                                            PctText = isEn ? (pct + "%") : ("%" + pct),
                                            BarPct = pct,
                                            IsCharging = charging
                                        });
                                        addedNames.Add(name);
                                    }
                                    foreach (HidCollectionInfo c in kv.Value) claimDev(c.Vid, c.Pid, c.ContainerId);
                                    continue;
                                }
                            }
                            finally { CloseHandle(hBs); }
                        }
                    }
                }

                // 1F-B: Standard Razer 90-byte Feature Report protocol (mice, keyboards, HyperSpeed dongles)
                foreach (HidCollectionInfo c in kv.Value)
                {
                    if (c.FeatLen != 91) continue;
                    // Open with access=0 (feature-only) so we never interfere with mouse/keyboard input or Synapse
                    IntPtr hRz = CreateFile(c.Path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                    if (hRz == IntPtr.Zero || hRz == new IntPtr(-1)) continue;
                    try
                    {
                        // Check HyperSpeed Multi-Device dongle slots first (cls 0x00, id 0xBF)
                        byte[] slotArgs;
                        if (RazerFeatureCommand(hRz, 0x1F, 0x00, 0xBF, 0x31, out slotArgs, 0x10, true) == 1 &&
                            slotArgs != null && slotArgs[0] == 2 && slotArgs[1] <= 1 && slotArgs[4] <= 1)
                        {
                            ushort s0Pid = (ushort)((slotArgs[2] << 8) | slotArgs[3]);
                            ushort s1Pid = (ushort)((slotArgs[5] << 8) | slotArgs[6]);
                            if (s0Pid != 0 && s0Pid != 0xFFFF && s1Pid != 0 && s1Pid != 0xFFFF && s0Pid != s1Pid)
                            {
                                ushort[] sPids = { s0Pid, s1Pid };
                                byte[] sTids = { 0x1F, 0x9F };
                                bool[] sAct = { slotArgs[1] == 1, slotArgs[4] == 1 };
                                string[] sKinds = { "Mouse", "Keyboard" };
                                for (int si = 0; si < 2; si++)
                                {
                                    if (!sAct[si]) continue;
                                    byte[] batArgs;
                                    if (RazerFeatureCommand(hRz, sTids[si], 0x07, 0x80, 0x02, out batArgs, 0, false) == 1 && batArgs != null)
                                    {
                                        int pct = Math.Max(0, Math.Min(100, (batArgs[1] * 100 + 127) / 255));
                                        if (pct <= 0) continue;
                                        byte[] chArgs;
                                        bool charging = RazerFeatureCommand(hRz, sTids[si], 0x07, 0x84, 0x02, out chArgs, 0, false) == 1 && chArgs != null && chArgs[1] == 1;
                                        string sName = (sKinds[si] == "Mouse" ? "Razer Mouse (" : "Razer Keyboard (") + sPids[si].ToString("X4") + ")";
                                        if (sPids[si] == 0x00A8) sName = "Razer Naga V2 Pro";
                                        else if (sPids[si] == 0x00DF) sName = "Razer Viper V3 Pro SE";
                                        else if (sPids[si] == 0x0296) sName = "Razer DeathStalker V2 Pro TKL";
                                        else if (sPids[si] == 0x02D5) sName = "Razer BlackWidow V4 TKL HyperSpeed";
                                        if (addedNames.Contains(sName)) continue;
                                        string slotIcon, slotTypeLabel;
                                        ClassifyDeviceTypeAndIcon(sName, sKinds[si], isEn, out slotIcon, out slotTypeLabel);
                                        results.Add(new WirelessDeviceReading
                                        {
                                            IdKey = "RAZER_" + sPids[si].ToString("X4"),
                                            Icon = slotIcon,
                                            Name = sName,
                                            Detail = charging ? (slotTypeLabel + " • " + (isEn ? "⚡ Charging" : "⚡ Şarjda")) : (slotTypeLabel + " • 2.4GHz"),
                                            PctText = isEn ? (pct + "%") : ("%" + pct),
                                            BarPct = pct,
                                            IsCharging = charging
                                        });
                                        addedNames.Add(sName);
                                    }
                                }
                                foreach (HidCollectionInfo cc in kv.Value) claimDev(cc.Vid, cc.Pid, cc.ContainerId);
                                break;
                            }
                        }

                        // Single-device battery probe across documented Razer transaction IDs
                        byte kbdTid = RazerKeyboardTid(pid);
                        byte[] tids = kbdTid != 0 ? new byte[] { kbdTid } : new byte[] { 0x1F, 0x3F, 0xFF, 0x08, 0x9F };
                        byte matchedTid = 0;
                        byte[] bArgs = null;
                        int rRes = 0;
                        bool sawOffline = false;
                        for (int ti = 0; ti < tids.Length; ti++)
                        {
                            byte[] tmpArgs;
                            int rr = RazerFeatureCommand(hRz, tids[ti], 0x07, 0x80, 0x02, out tmpArgs, 0, false);
                            if (rr == 1) { rRes = 1; matchedTid = tids[ti]; bArgs = tmpArgs; break; }
                            if (rr == 2) sawOffline = true;
                        }
                        if (rRes != 1 && !sawOffline) continue;

                        string cleanName = CleanRazerName(ReadUsbProductName(c.Path, "Razer Wireless"));
                        string idKey = (pid == 0x00A5 || pid == 0x00A6) ? "RAZER_00A6" : ("RAZER_" + pid.ToString("X4"));
                        string kindHint = (kbdTid != 0 || (pid >= 0x0200 && pid <= 0x02FF)) ? "Keyboard" : "Mouse";
                        string icon, typeLabel;
                        ClassifyDeviceTypeAndIcon(cleanName, kindHint, isEn, out icon, out typeLabel);

                        if (rRes == 1 && bArgs != null)
                        {
                            int pct = Math.Max(0, Math.Min(100, (bArgs[1] * 100 + 127) / 255));
                            if (pct > 0 && !addedNames.Contains(cleanName))
                            {
                                byte[] chArgs;
                                bool charging = RazerFeatureCommand(hRz, matchedTid, 0x07, 0x84, 0x02, out chArgs, 0, false) == 1 && chArgs != null && chArgs[1] == 1;
                                lastKnownPcts[idKey] = pct;
                                results.Add(new WirelessDeviceReading
                                {
                                    IdKey = idKey,
                                    Icon = icon,
                                    Name = cleanName,
                                    Detail = charging ? (typeLabel + " • " + (isEn ? "⚡ Charging" : "⚡ Şarjda")) : (typeLabel + " • " + (pid == 0x00A5 ? "USB" : "2.4GHz")),
                                    PctText = isEn ? (pct + "%") : ("%" + pct),
                                    BarPct = pct,
                                    IsCharging = charging
                                });
                                addedNames.Add(cleanName);
                            }
                            foreach (HidCollectionInfo cc in kv.Value) claimDev(cc.Vid, cc.Pid, cc.ContainerId);
                            break;
                        }
                        else if (sawOffline && lastKnownPcts.ContainsKey(idKey) && lastKnownPcts[idKey] > 0 && !addedNames.Contains(cleanName))
                        {
                            int lastPct = lastKnownPcts[idKey];
                            results.Add(new WirelessDeviceReading
                            {
                                IdKey = idKey,
                                Icon = icon,
                                Name = cleanName,
                                Detail = typeLabel + " • " + (isEn ? "Sleep" : "Uyku"),
                                PctText = isEn ? (lastPct + "%") : ("%" + lastPct),
                                BarPct = lastPct,
                                IsCharging = false,
                                IsAsleep = true
                            });
                            addedNames.Add(cleanName);
                            foreach (HidCollectionInfo cc in kv.Value) claimDev(cc.Vid, cc.Pid, cc.ContainerId);
                            break;
                        }
                    }
                    finally { CloseHandle(hRz); }
                }
            }
        }

        private static bool BragiGetProperty(IntPtr hDev, bool numbered, int outLen, int inLen, ref int mode, byte target, byte prop, out uint value)
        {
            value = 0;
            // mode: 0 = unknown, 1 = prefixed, 2 = plain
            int[] tryModes = mode == 1 ? new int[] { 1 } : (mode == 2 ? new int[] { 2 } : new int[] { 1, 2 });
            byte source = (byte)(target & 0x07);
            for (int mi = 0; mi < tryModes.Length; mi++)
            {
                bool plain = (tryModes[mi] == 2);
                byte[] tx = new byte[Math.Max(64, outLen)];
                if (plain)
                {
                    tx[0] = 0x00; tx[1] = target; tx[2] = 0x02; tx[3] = prop;
                }
                else if (numbered)
                {
                    tx[0] = 0x02; tx[1] = target; tx[2] = 0x02; tx[3] = prop;
                }
                else
                {
                    tx[0] = 0x00; tx[1] = 0x02; tx[2] = target; tx[3] = 0x02; tx[4] = prop;
                }
                int o = plain ? 0 : 1;
                HidD_FlushQueue(hDev);
                byte[] rx;
                if (OverlappedHidReadWithRetry(hDev, tx, inLen, out rx, r => r.Length >= o + 7 && r[o] == source && r[o + 1] == 0x02, 280))
                {
                    if (rx[o + 2] == 0x00)
                    {
                        value = (uint)(rx[o + 3] | (rx[o + 4] << 8) | (rx[o + 5] << 16) | (rx[o + 6] << 24));
                        mode = tryModes[mi];
                        return true;
                    }
                    // Device explicitly answered with non-zero status (e.g. property unsupported) -> framing is valid
                    mode = tryModes[mi];
                    return false;
                }
            }
            return false;
        }

        private static void PollCorsairHid(
            List<HidCollectionInfo> colls,
            bool isEn,
            HashSet<uint> claimedVidPids,
            Action<ushort, ushort, Guid> claimDev,
            HashSet<string> addedNames,
            List<WirelessDeviceReading> results)
        {
            HashSet<ushort> voidPids = new HashSet<ushort>
            {
                0x1B1C, 0x1B27, 0x0A14, 0x0A16, 0x0A17, 0x0A1D, 0x0A1A, 0x1B2A, 0x1B23,
                0x1B29, 0x0A55, 0x0A51, 0x0A52, 0x0A38, 0x0A4F, 0x0A2B, 0x0A75, 0x0A56
            };
            HashSet<ushort> bragiPids = new HashSet<ushort>
            {
                0x2A08, 0x2A02, 0x0A97, 0x0A64, 0x0A62, 0x0A3E, 0x0A3D, 0x2B00
            };

            Dictionary<ushort, List<HidCollectionInfo>> byPid = new Dictionary<ushort, List<HidCollectionInfo>>();
            foreach (HidCollectionInfo c in colls)
            {
                if (c.Vid != 0x1B1C) continue;
                List<HidCollectionInfo> list;
                if (!byPid.TryGetValue(c.Pid, out list)) { list = new List<HidCollectionInfo>(); byPid[c.Pid] = list; }
                list.Add(c);
            }

            foreach (var kv in byPid)
            {
                ushort pid = kv.Key;
                uint vpk = ((uint)0x1B1C << 16) | pid;
                if (claimedVidPids.Contains(vpk)) continue;

                // 1G-A: Corsair Void / Void Pro / Void Elite / HS70 / HS80 legacy headsets (0xC9 0x64)
                if (voidPids.Contains(pid))
                {
                    foreach (HidCollectionInfo c in kv.Value)
                    {
                        if ((c.UsagePage != 0xFFC5 && c.UsagePage != 0xFF00) || c.OutLen <= 0 || c.InLen < 5) continue;
                        IntPtr hVoid = CreateFile(c.Path, 0xC0000000U, 3, IntPtr.Zero, 3, 0x40000000U, IntPtr.Zero);
                        if (hVoid == IntPtr.Zero || hVoid == new IntPtr(-1)) continue;
                        try
                        {
                            byte[] tx = new byte[Math.Max(2, (int)c.OutLen)];
                            tx[0] = 0xC9; tx[1] = 0x64;
                            HidD_FlushQueue(hVoid);
                            byte[] rx;
                            if (OverlappedHidReadWithRetry(hVoid, tx, c.InLen, out rx, r => r.Length >= 5, 600))
                            {
                                byte st = rx[4];
                                bool connected = st != 0;
                                bool charging = (st == 4 || st == 5);
                                int pct = (st == 1 || st == 2 || charging) ? Math.Max(0, Math.Min(100, rx[2] & 0x7F)) : 0;
                                foreach (HidCollectionInfo cc in kv.Value) claimDev(cc.Vid, cc.Pid, cc.ContainerId);
                                if (connected && pct > 0)
                                {
                                    string name = ReadUsbProductName(c.Path, "Corsair Wireless Headset");
                                    if (!addedNames.Contains(name))
                                    {
                                        results.Add(new WirelessDeviceReading
                                        {
                                            IdKey = "CORSAIR_" + pid.ToString("X4"),
                                            Icon = "🎧",
                                            Name = name,
                                            Detail = charging ? (isEn ? "Headset • ⚡ Charging" : "Kulaklık • ⚡ Şarjda") : (isEn ? "Headset • 2.4GHz" : "Kulaklık • 2.4GHz"),
                                            PctText = isEn ? (pct + "%") : ("%" + pct),
                                            BarPct = pct,
                                            IsCharging = charging
                                        });
                                        addedNames.Add(name);
                                    }
                                }
                                break;
                            }
                        }
                        finally { CloseHandle(hVoid); }
                    }
                    continue;
                }

                // 1G-B: Corsair CompX mice (Sabre V2 Pro Ultralight 2B2A / MG 2B34)
                if (pid == 0x2B2A || pid == 0x2B34)
                {
                    foreach (HidCollectionInfo c in kv.Value)
                    {
                        if (c.UsagePage != 0xFF02 || c.Usage != 0x0002 || c.OutLen < 17 || c.InLen < 17) continue;
                        IntPtr hCx = CreateFile(c.Path, 0xC0000000U, 3, IntPtr.Zero, 3, 0x40000000U, IntPtr.Zero);
                        if (hCx == IntPtr.Zero || hCx == new IntPtr(-1)) continue;
                        try
                        {
                            byte[] tx = new byte[17];
                            tx[0] = 0x08; tx[1] = 0x04; tx[16] = 0x49;
                            byte[] rx;
                            if (OverlappedHidQuery(hCx, tx, out rx, 17, 150) && rx[0] == 0x08 && rx[1] == 0x04)
                            {
                                int pct = rx[6];
                                bool charging = rx[7] != 0;
                                int mv = (rx[8] << 8) | rx[9];
                                foreach (HidCollectionInfo cc in kv.Value) claimDev(cc.Vid, cc.Pid, cc.ContainerId);
                                if (pct > 0 && pct <= 100)
                                {
                                    string name = pid == 0x2B2A ? "Corsair Sabre V2 Pro Ultralight" : "Corsair Sabre V2 Pro MG";
                                    if (!addedNames.Contains(name))
                                    {
                                        results.Add(new WirelessDeviceReading
                                        {
                                            IdKey = "CORSAIR_" + pid.ToString("X4"),
                                            Icon = "🖱",
                                            Name = name,
                                            Detail = charging ? (isEn ? "Mouse • ⚡ Charging" : "Fare • ⚡ Şarjda")
                                                : (mv > 3000 && mv < 4500 ? string.Format("{0} • {1:0.00}V", isEn ? "Mouse" : "Fare", mv / 1000.0) : (isEn ? "Mouse • 2.4GHz" : "Fare • 2.4GHz")),
                                            PctText = isEn ? (pct + "%") : ("%" + pct),
                                            BarPct = pct,
                                            IsCharging = charging
                                        });
                                        addedNames.Add(name);
                                    }
                                }
                                break;
                            }
                        }
                        finally { CloseHandle(hCx); }
                    }
                    continue;
                }

                // 1G-C: Corsair Bragi / Slipstream protocol (Virtuoso, HS80 MAX, Void V2, Slipstream mice/keyboards)
                bool knownBragi = bragiPids.Contains(pid);
                foreach (HidCollectionInfo c in kv.Value)
                {
                    bool sizes = (c.OutLen == 64 || c.OutLen == 65) && (c.InLen == 64 || c.InLen == 65);
                    if (!sizes) continue;
                    if (!(c.UsagePage == 0xFF42 || (knownBragi && c.UsagePage >= 0xFF00))) continue;

                    IntPtr hBr = CreateFile(c.Path, 0xC0000000U, 3, IntPtr.Zero, 3, 0x40000000U, IntPtr.Zero);
                    if (hBr == IntPtr.Zero || hBr == new IntPtr(-1)) continue;
                    try
                    {
                        bool numbered = (c.OutLen == 64);
                        int brMode = 0;
                        bool any = false;
                        string product = ReadUsbProductName(c.Path, "Corsair Wireless");
                        byte[] targets = new byte[] { 0x09, 0x0A, 0x0B, 0x08 };
                        for (int ti = 0; ti < targets.Length; ti++)
                        {
                            byte target = targets[ti];
                            if (target == 0x08 && any) break;
                            uint level;
                            bool ok = BragiGetProperty(hBr, numbered, c.OutLen, c.InLen, ref brMode, target, 0x0F, out level);
                            for (int retry = 0; ok && (level == 0 || level > 1000) && retry < 2; retry++)
                                ok = BragiGetProperty(hBr, numbered, c.OutLen, c.InLen, ref brMode, target, 0x0F, out level);
                            if (!ok || level == 0 || level > 1000) continue;

                            int pct = Math.Max(0, Math.Min(100, (int)((level + 5) / 10)));
                            if (pct <= 0) continue;
                            uint chVal;
                            bool charging = BragiGetProperty(hBr, numbered, c.OutLen, c.InLen, ref brMode, target, 0x10, out chVal) && (chVal == 1 || chVal == 3);

                            string devName = Regex.Replace(product, "(?i)\\s*(slipstream|wireless)?\\s*(gaming)?\\s*(receiver|dongle)$", "").Trim();
                            if (string.IsNullOrEmpty(devName)) devName = "Corsair Wireless";
                            string kindHint = ((pid >> 8) == 0x0A) ? "Headset" : "";

                            uint pairedPid;
                            if (target != 0x08 && BragiGetProperty(hBr, numbered, c.OutLen, c.InLen, ref brMode, target, 0x12, out pairedPid))
                            {
                                if (pairedPid == 0x1B7E) { devName = "Corsair Dark Core RGB Pro SE"; kindHint = "Mouse"; }
                                else if ((pairedPid >> 8) == 0x0A)
                                {
                                    kindHint = "Headset";
                                    if (product.IndexOf("Receiver", StringComparison.OrdinalIgnoreCase) >= 0) devName = "Corsair Wireless Headset";
                                }
                            }

                            if (!addedNames.Contains(devName))
                            {
                                string icon, typeLabel;
                                ClassifyDeviceTypeAndIcon(devName, kindHint, isEn, out icon, out typeLabel);
                                results.Add(new WirelessDeviceReading
                                {
                                    IdKey = string.Format("CORSAIR_{0:X4}_{1:X2}", pid, target),
                                    Icon = icon,
                                    Name = devName,
                                    Detail = charging ? (typeLabel + " • " + (isEn ? "⚡ Charging" : "⚡ Şarjda")) : (typeLabel + " • " + (target == 0x08 ? "USB" : "2.4GHz")),
                                    PctText = isEn ? (pct + "%") : ("%" + pct),
                                    BarPct = pct,
                                    IsCharging = charging
                                });
                                addedNames.Add(devName);
                            }
                            any = true;
                        }
                        if (any)
                        {
                            foreach (HidCollectionInfo cc in kv.Value) claimDev(cc.Vid, cc.Pid, cc.ContainerId);
                            break;
                        }
                    }
                    finally { CloseHandle(hBr); }
                }
            }
        }

        private static bool OverlappedHidReadWithRetry(IntPtr hFile, byte[] tx, int rxLen, out byte[] rxOut, Func<byte[], bool> matcher, int timeoutMs)
        {
            rxOut = null;
            if (rxLen <= 0) return false;
            IntPtr evW = CreateEvent(IntPtr.Zero, true, false, null);
            IntPtr evR = CreateEvent(IntPtr.Zero, true, false, null);
            int ovSize = Marshal.SizeOf(typeof(NativeOverlapped));
            IntPtr pOvW = Marshal.AllocHGlobal(ovSize);
            IntPtr pOvR = Marshal.AllocHGlobal(ovSize);
            IntPtr pTx = (tx != null && tx.Length > 0) ? Marshal.AllocHGlobal(tx.Length) : IntPtr.Zero;
            IntPtr pRx = Marshal.AllocHGlobal(rxLen);
            try
            {
                if (pTx != IntPtr.Zero)
                {
                    Marshal.Copy(tx, 0, pTx, tx.Length);
                    NativeOverlapped ovW = new NativeOverlapped { EventHandle = evW };
                    Marshal.StructureToPtr(ovW, pOvW, false);
                    uint wBytes;
                    if (!WriteFile(hFile, pTx, (uint)tx.Length, out wBytes, pOvW))
                    {
                        if (WaitForSingleObject(evW, (uint)Math.Max(80, timeoutMs)) != 0)
                        {
                            CancelIo(hFile);
                            uint dummyW;
                            GetOverlappedResult(hFile, pOvW, out dummyW, true);
                            return false;
                        }
                    }
                }

                Stopwatch sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < timeoutMs)
                {
                    int remaining = (int)(timeoutMs - sw.ElapsedMilliseconds);
                    if (remaining <= 0) break;

                    ResetEvent(evR);
                    for (int i = 0; i < rxLen; i++) Marshal.WriteByte(pRx, i, 0);
                    NativeOverlapped ovR = new NativeOverlapped { EventHandle = evR };
                    Marshal.StructureToPtr(ovR, pOvR, false);

                    uint rBytes = 0;
                    bool got = false;
                    if (ReadFile(hFile, pRx, (uint)rxLen, out rBytes, pOvR))
                    {
                        got = rBytes > 0;
                    }
                    else
                    {
                        uint waitRes = WaitForSingleObject(evR, (uint)remaining);
                        if (waitRes == 0)
                        {
                            if (GetOverlappedResult(hFile, pOvR, out rBytes, false)) got = rBytes > 0;
                        }
                        else
                        {
                            CancelIo(hFile);
                            uint dummyR;
                            GetOverlappedResult(hFile, pOvR, out dummyR, true);
                            break;
                        }
                    }

                    if (got)
                    {
                        int copyLen = (int)Math.Min(rBytes, (uint)rxLen);
                        if (copyLen > 0)
                        {
                            byte[] buf = new byte[copyLen];
                            Marshal.Copy(pRx, buf, 0, copyLen);
                            int offset = (copyLen > 1 && buf[0] == 0x00) ? 1 : 0;
                            byte[] payload = new byte[copyLen - offset];
                            Array.Copy(buf, offset, payload, 0, payload.Length);
                            if (matcher == null || matcher(payload))
                            {
                                rxOut = payload;
                                return true;
                            }
                        }
                    }
                }
            }
            catch { }
            finally
            {
                if (pTx != IntPtr.Zero) Marshal.FreeHGlobal(pTx);
                Marshal.FreeHGlobal(pRx);
                Marshal.FreeHGlobal(pOvW);
                Marshal.FreeHGlobal(pOvR);
                if (evW != IntPtr.Zero) CloseHandle(evW);
                if (evR != IntPtr.Zero) CloseHandle(evR);
            }
            return false;
        }

        private static List<HidCollectionInfo> EnumHidCollections()
        {
            List<HidCollectionInfo> list = new List<HidCollectionInfo>();
            try
            {
                Guid hidGuid;
                HidD_GetHidGuid(out hidGuid);
                IntPtr hDevInfo = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, 0x12);
                if (hDevInfo == IntPtr.Zero || hDevInfo == new IntPtr(-1)) return list;
                try
                {
                    DEVPROPKEY containerKey = new DEVPROPKEY { fmtid = new Guid("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C"), pid = 2 };
                    byte[] cBuf = new byte[16];
                    int index = 0;
                    SP_DEVICE_INTERFACE_DATA devData = new SP_DEVICE_INTERFACE_DATA();
                    devData.cbSize = Marshal.SizeOf(devData);
                    while (SetupDiEnumDeviceInterfaces(hDevInfo, IntPtr.Zero, ref hidGuid, index, ref devData))
                    {
                        index++;
                        int reqSize;
                        SetupDiGetDeviceInterfaceDetail(hDevInfo, ref devData, IntPtr.Zero, 0, out reqSize, IntPtr.Zero);
                        if (reqSize <= 0) continue;

                        IntPtr detailBuf = Marshal.AllocHGlobal(reqSize);
                        try
                        {
                            Marshal.WriteInt32(detailBuf, IntPtr.Size == 8 ? 8 : 6);
                            SP_DEVINFO_DATA devInfo = new SP_DEVINFO_DATA();
                            devInfo.cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVINFO_DATA));
                            if (!SetupDiGetDeviceInterfaceDetail(hDevInfo, ref devData, detailBuf, reqSize, out reqSize, ref devInfo)) continue;

                            string p = Marshal.PtrToStringAuto(new IntPtr(detailBuf.ToInt64() + 4));
                            if (string.IsNullOrEmpty(p)) continue;

                            Guid cid = Guid.Empty;
                            uint pType, pReq;
                            if (SetupDiGetDevicePropertyW(hDevInfo, ref devInfo, ref containerKey, out pType, cBuf, 16, out pReq, 0) && pReq >= 16)
                            {
                                cid = new Guid(cBuf);
                            }

                            IntPtr h = CreateFile(p, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                            if (h == IntPtr.Zero || h == new IntPtr(-1)) continue;
                            try
                            {
                                HIDD_ATTRIBUTES attr = new HIDD_ATTRIBUTES { Size = Marshal.SizeOf(typeof(HIDD_ATTRIBUTES)) };
                                if (!HidD_GetAttributes(h, ref attr)) continue;

                                IntPtr pp;
                                if (!HidD_GetPreparsedData(h, out pp) || pp == IntPtr.Zero) continue;
                                try
                                {
                                    HIDP_CAPS caps;
                                    if (HidP_GetCaps(pp, out caps) == 0x00110000)
                                    {
                                        list.Add(new HidCollectionInfo
                                        {
                                            Path = p,
                                            Vid = attr.VendorID,
                                            Pid = attr.ProductID,
                                            UsagePage = caps.UsagePage,
                                            Usage = caps.Usage,
                                            InLen = caps.InputReportByteLength,
                                            OutLen = caps.OutputReportByteLength,
                                            FeatLen = caps.FeatureReportByteLength,
                                            NumInputValueCaps = caps.NumberInputValueCaps,
                                            NumFeatureValueCaps = caps.NumberFeatureValueCaps,
                                            ContainerId = cid
                                        });
                                    }
                                }
                                finally
                                {
                                    HidD_FreePreparsedData(pp);
                                }
                            }
                            finally
                            {
                                CloseHandle(h);
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(detailBuf);
                        }
                    }
                }
                finally
                {
                    SetupDiDestroyDeviceInfoList(hDevInfo);
                }
            }
            catch { }
            return list;
        }

        private static bool KindFromClassOfDevice(uint cod, bool isEn, out string icon, out string typeLabel)
        {
            uint major = (cod >> 8) & 0x1Fu;
            uint minor = (cod >> 2) & 0x3Fu;
            if (major == 0x04u)
            {
                icon = "🎧";
                typeLabel = isEn ? "Headset" : "Kulaklık";
                return true;
            }
            if (major == 0x05u)
            {
                if ((minor & 0x0Fu) == 0x01u || (minor & 0x0Fu) == 0x02u)
                {
                    icon = "🎮";
                    typeLabel = isEn ? "Controller" : "Kumanda";
                    return true;
                }
                if ((minor >> 4) == 1u)
                {
                    icon = "⌨";
                    typeLabel = isEn ? "Keyboard" : "Klavye";
                    return true;
                }
                if ((minor >> 4) == 2u)
                {
                    icon = "🖱";
                    typeLabel = isEn ? "Mouse" : "Fare";
                    return true;
                }
            }
            icon = "";
            typeLabel = "";
            return false;
        }

        public static void ClassifyDeviceTypeAndIcon(string rawName, string devClass, bool isEn, out string icon, out string typeLabel)
        {
            string lower = ((rawName ?? "") + " " + (devClass ?? "")).ToLowerInvariant();
            if (lower.Contains("keyboard") || lower.Contains("klavye") || lower.Contains("kbd") || lower.Contains("keychron") || lower.Contains("mx keys"))
            {
                icon = "⌨";
                typeLabel = isEn ? "Keyboard" : "Klavye";
            }
            else if (lower.Contains("headset") || lower.Contains("headphone") || lower.Contains("kulaklık") || lower.Contains("airpods") ||
                     lower.Contains("buds") || lower.Contains("cloud") || lower.Contains("arctis") || lower.Contains("nova") ||
                     lower.Contains("steelseries") || lower.Contains("kraken") || lower.Contains("blackshark") || lower.Contains("barracuda") ||
                     lower.Contains("wh-1000") || lower.Contains("wf-1000") || lower.Contains("soundcore") || lower.Contains("jbl") ||
                     lower.Contains("tune") || lower.Contains("quantum") || lower.Contains("sennheiser") || lower.Contains("audio"))
            {
                icon = "🎧";
                typeLabel = isEn ? "Headset" : "Kulaklık";
            }
            else if (lower.Contains("controller") || lower.Contains("gamepad") || lower.Contains("kumanda") || lower.Contains("xbox") ||
                     lower.Contains("dualshock") || lower.Contains("dualsense") || lower.Contains("8bitdo") || lower.Contains("gamesir") ||
                     lower.Contains("flydigi") || lower.Contains("joystick"))
            {
                icon = "🎮";
                typeLabel = isEn ? "Controller" : "Kumanda";
            }
            else
            {
                icon = "🖱";
                typeLabel = isEn ? "Mouse" : "Fare";
            }
        }

        private static int QueryAtkBitmouse(IntPtr hDev, byte cmd, bool isZeroCable)
        {
            byte[] req = new byte[64];
            req[0] = 0x08;
            req[2] = 0x72;
            req[3] = 0x02;
            req[5] = isZeroCable ? (byte)0x00 : (byte)0x01;
            req[6] = cmd;
            req[7] = 0x01;
            if (isZeroCable)
            {
                req[1] = 0x7D;
            }
            else
            {
                int sum = 0;
                for (int i = 2; i < req.Length; i++) sum += req[i];
                req[1] = (byte)(sum & 0xFF);
            }
            byte[] rx;
            if (OverlappedHidReadWithRetry(hDev, req, 64, out rx, r =>
                r.Length >= 8 && r[0] == 0x08 && r[1] == 0x72 && r[5] == cmd && (!isZeroCable || r[6] == 0x01), 600))
            {
                if (isZeroCable ? (rx[2] != 0x00) : (rx[2] == 0xFF)) return -1;
                return rx[7];
            }
            return -1;
        }

        public static string ResolveGenericDongleFriendlyName(ushort vid, string cleanedUsbName, Guid containerId, IEnumerable<string> knownPnpNames)
        {
            string trimmed = (cleanedUsbName ?? "").Trim();
            bool isGeneric = string.IsNullOrEmpty(trimmed) || Regex.IsMatch(
                trimmed,
                @"(?i)^(?:nk\s*mouse(?:\s*nano)?|wireless\s*mouse|2\.4g(?:\s*wireless)?(?:\s*mouse)?|usb\s*receiver|gaming\s*mouse|atk\s*(?:wireless\s*)?(?:dongle|mouse)|vgn\s*dongle)$");
            if (!isGeneric) return trimmed;

            if (vid == 0x373B || vid == 0x3554)
            {
                if (knownPnpNames != null)
                {
                    foreach (string rawCandidate in knownPnpNames)
                    {
                        if (string.IsNullOrWhiteSpace(rawCandidate)) continue;
                        string cand = StripAudioProfileSuffixes(rawCandidate);
                        string lc = cand.ToLowerInvariant();
                        if (lc.Contains("headset") || lc.Contains("headphone") || lc.Contains("kulaklık") ||
                            lc.Contains("keyboard") || lc.Contains("klavye") || lc.Contains("arctis") || lc.Contains("jbl"))
                            continue;
                        if (lc.Contains("nearlink") || lc.Contains("atk") || lc.Contains("vxe") || lc.Contains("vgn"))
                            return cand;
                    }
                }
                if (trimmed.IndexOf("nk mouse", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "ATK NearLink Mouse";
                if (vid == 0x373B)
                    return "ATK Wireless Mouse";
            }
            return string.IsNullOrEmpty(trimmed) ? "Wireless Mouse" : trimmed;
        }

        private static List<string> CollectKnownWirelessPnpNames()
        {
            List<string> names = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Action<string> addName = (n) =>
            {
                if (string.IsNullOrWhiteSpace(n)) return;
                string s = StripAudioProfileSuffixes(n);
                if (!string.IsNullOrEmpty(s) && !IsGenericBluetoothNodeName(s) && seen.Add(s))
                    names.Add(s);
            };

            try
            {
                Dictionary<string, bool> btMac, btName;
                QueryClassicBluetoothStates(out btMac, out btName);
                foreach (string k in btName.Keys) addName(k);
            }
            catch { }

            try
            {
                IntPtr hPnp = SetupDiGetClassDevsAll(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0x04);
                if (hPnp != IntPtr.Zero && hPnp != new IntPtr(-1))
                {
                    try
                    {
                        SP_DEVINFO_DATA devInfo = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVINFO_DATA)) };
                        StringBuilder sbInst = new StringBuilder(256);
                        StringBuilder sbName = new StringBuilder(256);
                        uint idx = 0;
                        while (SetupDiEnumDeviceInfo(hPnp, idx, ref devInfo))
                        {
                            idx++;
                            sbInst.Clear();
                            int req;
                            SetupDiGetDeviceInstanceIdW(hPnp, ref devInfo, sbInst, sbInst.Capacity, out req);
                            string id = sbInst.ToString();
                            if (!id.StartsWith("BTH", StringComparison.OrdinalIgnoreCase) &&
                                id.IndexOf("VID_373B", StringComparison.OrdinalIgnoreCase) < 0 &&
                                id.IndexOf("VID_3554", StringComparison.OrdinalIgnoreCase) < 0)
                                continue;

                            sbName.Clear();
                            uint rType, rReq;
                            if (SetupDiGetDeviceRegistryProperty(hPnp, ref devInfo, 0x0C, out rType, sbName, (uint)sbName.Capacity, out rReq) && sbName.Length > 0)
                            {
                                addName(sbName.ToString());
                            }
                        }
                    }
                    finally { SetupDiDestroyDeviceInfoList(hPnp); }
                }
            }
            catch { }
            return names;
        }

        private static bool IsGenericHidBatteryUsage(ushort page, ushort usage)
        {
            return (page == 0x06 && usage == 0x20) || (page == 0x85 && usage == 0x65);
        }

        public static List<WirelessDeviceReading> ScanActiveWirelessDevices(bool isEn, Dictionary<string, int> lastKnownPcts, Dictionary<string, string> lastKnownHz)
        {
            List<WirelessDeviceReading> results = new List<WirelessDeviceReading>();
            HashSet<string> addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<uint> claimedVidPids = new HashSet<uint>();
            HashSet<Guid> claimedContainers = new HashSet<Guid>();

            Func<ushort, ushort, uint> vpKey = (v, p) => ((uint)v << 16) | p;
            Action<ushort, ushort, Guid> claimDev = (v, p, cid) =>
            {
                claimedVidPids.Add(vpKey(v, p));
                if (cid != Guid.Empty) claimedContainers.Add(cid);
            };

            List<HidCollectionInfo> colls = EnumHidCollections();
            List<string> knownPnpNames = CollectKnownWirelessPnpNames();

            // 1A. ATK NearLink BITMOUSE (373B:1216/124C/1154/124F/1155) & 8K Nano Dongle (373B:1178)
            foreach (HidCollectionInfo c in colls)
            {
                if (c.Vid != 0x373B) continue;
                bool isBitmouse = (c.Pid == 0x1216 || c.Pid == 0x124C || c.Pid == 0x1154 || c.Pid == 0x124F || c.Pid == 0x1155);
                if (isBitmouse && c.UsagePage == 0xFF05 && c.Usage == 0x0001 && c.OutLen == 64 && c.InLen == 64 && !claimedVidPids.Contains(vpKey(c.Vid, c.Pid)))
                {
                    claimDev(c.Vid, c.Pid, c.ContainerId);
                    IntPtr hDev = CreateFile(c.Path, 0xC0000000U, 3, IntPtr.Zero, 3, 0x40000000U, IntPtr.Zero);
                    if (hDev != IntPtr.Zero && hDev != new IntPtr(-1))
                    {
                        try
                        {
                            bool isZero = (c.Pid == 0x1154 || c.Pid == 0x124F || c.Pid == 0x1155);
                            bool isZeroCable = (c.Pid == 0x1154);
                            int level = QueryAtkBitmouse(hDev, 0x07, isZeroCable);
                            if (level >= 1 && level <= 100)
                            {
                                bool isCharging = isZero
                                    ? (isZeroCable && QueryAtkBitmouse(hDev, 0x08, true) == 0x02)
                                    : (c.Pid == 0x124C || QueryAtkBitmouse(hDev, 0x0F, false) == 1);

                                string rawProd = ReadUsbProductName(c.Path, isZero ? "ATK Zero" : "ATK Wireless Mouse");
                                string cleanName = Regex.Replace(rawProd, "(?i)\\s*(8k|4k|1k)?\\s*(nano|wireless)?\\s*(dongle|receiver)", "").Trim();
                                cleanName = ResolveGenericDongleFriendlyName(c.Vid, cleanName, c.ContainerId, knownPnpNames);

                                string icon, typeLabel;
                                ClassifyDeviceTypeAndIcon(cleanName, "Mouse", isEn, out icon, out typeLabel);
                                string proto = (c.Pid == 0x124C || isZeroCable) ? "USB" : "2.4GHz";
                                string det = isCharging
                                    ? (typeLabel + " • " + (isEn ? "⚡ Charging" : "⚡ Şarjda"))
                                    : (typeLabel + " • " + proto);

                                string idKey = "HID_373B_" + c.Pid.ToString("X4");
                                results.Add(new WirelessDeviceReading
                                {
                                    IdKey = idKey,
                                    Icon = icon,
                                    Name = cleanName,
                                    Detail = det,
                                    PctText = isEn ? (level + "%") : ("%" + level),
                                    BarPct = level,
                                    IsCharging = isCharging
                                });
                                addedNames.Add(cleanName);
                            }
                        }
                        finally { CloseHandle(hDev); }
                    }
                }
                else if (c.Pid == 0x1178 && c.UsagePage == 0xFF10 && c.Usage == 0x0001 && c.FeatLen >= 9 && c.InLen >= 11 && !claimedVidPids.Contains(vpKey(c.Vid, c.Pid)))
                {
                    claimDev(c.Vid, c.Pid, c.ContainerId);
                    IntPtr hDev = CreateFile(c.Path, 0xC0000000U, 3, IntPtr.Zero, 3, 0x40000000U, IntPtr.Zero);
                    if (hDev != IntPtr.Zero && hDev != new IntPtr(-1))
                    {
                        try
                        {
                            byte[] feat = new byte[c.FeatLen];
                            feat[0] = 0x00;
                            feat[1] = 0x82;
                            if (HidD_SetFeature(hDev, feat, (uint)feat.Length))
                            {
                                byte[] rx;
                                if (OverlappedHidReadWithRetry(hDev, null, c.InLen, out rx, r => r.Length >= 10 && r[0] == 0x82, 600) && rx[9] != 0)
                                {
                                    int level = rx[8] & 0x7F;
                                    bool isCharging = (rx[8] & 0x80) != 0;
                                    if (level >= 1 && level <= 100)
                                    {
                                        string rawProd = ReadUsbProductName(c.Path, "ATK 8K Mouse");
                                        string cleanName = Regex.Replace(rawProd, "(?i)\\s*(8k|4k|1k)?\\s*(nano|wireless)?\\s*(dongle|receiver)", "").Trim();
                                        cleanName = ResolveGenericDongleFriendlyName(c.Vid, cleanName, c.ContainerId, knownPnpNames);

                                        string icon, typeLabel;
                                        ClassifyDeviceTypeAndIcon(cleanName, "Mouse", isEn, out icon, out typeLabel);
                                        results.Add(new WirelessDeviceReading
                                        {
                                            IdKey = "HID_373B_1178",
                                            Icon = icon,
                                            Name = cleanName,
                                            Detail = isCharging ? (typeLabel + " • " + (isEn ? "⚡ Charging" : "⚡ Şarjda")) : (typeLabel + " • 2.4GHz"),
                                            PctText = isEn ? (level + "%") : ("%" + level),
                                            BarPct = level,
                                            IsCharging = isCharging
                                        });
                                        addedNames.Add(cleanName);
                                    }
                                }
                            }
                        }
                        finally { CloseHandle(hDev); }
                    }
                }
            }

            // 1B. CompX 17-byte Wireless Mice (Scyrox, VXE, ATK, Kysona, Pulsar, Redragon)
            foreach (HidCollectionInfo c in colls)
            {
                bool isCompxVendor = (c.Vid == 0x3554 || c.Vid == 0x373B || c.Vid == 0x3710 || c.Vid == 0x3770 || c.Vid == 0x33E4 || c.Vid == 0x35BB);
                if (!isCompxVendor) continue;
                if (claimedVidPids.Contains(vpKey(c.Vid, c.Pid))) continue;
                if (c.Vid == 0x373B && (c.Pid == 0x11CA || c.Pid == 0x11CB || c.Pid == 0x1203 || c.Pid == 0x1204 || c.Pid == 0x1178)) continue;
                if (c.Vid == 0x3554 && c.Pid == 0xFA09) continue;

                bool isCompxColl = (c.OutLen == 17 && c.InLen == 17 &&
                    ((c.UsagePage == 0xFF02 && c.Usage == 0x0002) ||
                     (c.Vid == 0x3554 && c.Path.IndexOf("&col05", StringComparison.OrdinalIgnoreCase) >= 0)));
                if (!isCompxColl) continue;

                claimDev(c.Vid, c.Pid, c.ContainerId);
                string rawProd = ReadUsbProductName(c.Path, c.Vid == 0x373B ? "ATK Wireless Mouse" : "Wireless Mouse");
                string cleanName = rawProd;
                if ((c.Vid == 0x3554 && c.Pid == 0xF5F7) || rawProd.IndexOf("SCYROX", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    cleanName = "Scyrox V8";
                }
                else
                {
                    cleanName = Regex.Replace(rawProd, "(?i)\\s*(8k|4k|1k)?\\s*(wireless)?\\s*(dongle|receiver)", "").Trim();
                    cleanName = ResolveGenericDongleFriendlyName(c.Vid, cleanName, c.ContainerId, knownPnpNames);
                }

                string icon, typeLabel;
                ClassifyDeviceTypeAndIcon(cleanName, "Mouse", isEn, out icon, out typeLabel);

                IntPtr hDev = CreateFile(c.Path, 0xC0000000U, 3, IntPtr.Zero, 3, 0x40000000U, IntPtr.Zero);
                if (hDev != IntPtr.Zero && hDev != new IntPtr(-1))
                {
                    try
                    {
                        string hzText = lastKnownHz.ContainsKey(c.Path) ? lastKnownHz[c.Path] : "1000 Hz";
                        byte[] txHz = new byte[17];
                        txHz[0] = 0x08; txHz[1] = 0x08; txHz[5] = 0x02; txHz[16] = 0x43;
                        byte[] rxHz;
                        if (OverlappedHidQuery(hDev, txHz, out rxHz, 17, 100) && rxHz[0] == 0x08 && rxHz[1] == 0x08)
                        {
                            byte rc = rxHz[6];
                            if (rc == 0x01) hzText = "1000 Hz";
                            else if (rc == 0x02) hzText = "500 Hz";
                            else if (rc == 0x04) hzText = "250 Hz";
                            else if (rc == 0x08) hzText = "125 Hz";
                            else if (rc == 0x10) hzText = "2000 Hz";
                            else if (rc == 0x20) hzText = "4000 Hz";
                            else if (rc == 0x40) hzText = "8000 Hz";
                            lastKnownHz[c.Path] = hzText;
                        }

                        byte[] txBat = new byte[17];
                        txBat[0] = 0x08; txBat[1] = 0x04; txBat[16] = 0x49;
                        byte[] rxBat;
                        int pct = 0;
                        bool charging = false;
                        int mv = 0;
                        if (OverlappedHidQuery(hDev, txBat, out rxBat, 17, 130) && rxBat[0] == 0x08 && rxBat[1] == 0x04)
                        {
                            pct = rxBat[6];
                            charging = rxBat[7] == 1;
                            mv = (rxBat[8] << 8) | rxBat[9];
                        }

                        if (pct > 0 && pct <= 100)
                        {
                            lastKnownPcts[c.Path] = pct;
                            string det = charging
                                ? (hzText + " • " + (isEn ? "⚡ Charging" : "⚡ Şarjda"))
                                : (mv > 3000 && mv < 4500 ? string.Format("{0} • {1:0.00}V", hzText, mv / 1000.0) : (hzText + " • 2.4GHz"));
                            results.Add(new WirelessDeviceReading
                            {
                                IdKey = c.Path,
                                Icon = icon,
                                Name = cleanName,
                                Detail = det,
                                PctText = isEn ? (pct + "%") : ("%" + pct),
                                BarPct = pct,
                                IsCharging = charging
                            });
                            addedNames.Add(cleanName);
                        }
                        else if (lastKnownPcts.ContainsKey(c.Path) && lastKnownPcts[c.Path] > 0)
                        {
                            int lastPct = lastKnownPcts[c.Path];
                            results.Add(new WirelessDeviceReading
                            {
                                IdKey = c.Path,
                                Icon = icon,
                                Name = cleanName,
                                Detail = hzText + " • " + (isEn ? "Sleep" : "Uyku"),
                                PctText = isEn ? (lastPct + "%") : ("%" + lastPct),
                                BarPct = lastPct,
                                IsCharging = false,
                                IsAsleep = true
                            });
                            addedNames.Add(cleanName);
                        }
                    }
                    finally { CloseHandle(hDev); }
                }
            }

            // 1C. SteelSeries Wireless Headsets (VID 1038) via Direct HID 2.4GHz Dongle
            foreach (HidCollectionInfo c in colls)
            {
                if (c.Vid != 0x1038 || claimedVidPids.Contains(vpKey(c.Vid, c.Pid))) continue;
                bool isNovaColl = (c.UsagePage == 0xFFC0 && c.Usage == 0x0001 && c.OutLen > 0 && c.InLen >= 7);
                bool isArctisColl = (c.UsagePage == 0xFF43 && c.Usage == 0x0202 && c.OutLen > 0 && c.InLen >= 4);
                if (!isNovaColl && !isArctisColl) continue;

                IntPtr hSs = CreateFile(c.Path, 0xC0000000U, 3, IntPtr.Zero, 3, 0x40000000U, IntPtr.Zero);
                if (hSs == IntPtr.Zero || hSs == new IntPtr(-1)) continue;
                try
                {
                    int pct = 0;
                    bool charging = false;
                    bool connected = false;
                    string cleanName = Regex.Replace(ReadUsbProductName(c.Path, "SteelSeries Headset"), "(?i)\\s*(dongle|receiver|transmitter)", "").Trim();
                    if (string.IsNullOrEmpty(cleanName)) cleanName = "SteelSeries Headset";

                    if (isNovaColl)
                    {
                        byte[] tx = new byte[c.OutLen];
                        tx[0] = 0x00;
                        tx[1] = 0xB0;
                        byte[] rx;
                        if (OverlappedHidReadWithRetry(hSs, tx, c.InLen, out rx, r => r.Length >= 7 && r[0] == 0xB0, 500))
                        {
                            if (c.Pid == 0x220E || c.Pid == 0x2212 || c.Pid == 0x2216 || c.Pid == 0x2236)
                            {
                                connected = rx[1] != 0x01;
                                pct = Math.Max(0, Math.Min(100, (int)Math.Round(rx[2] * 25.0)));
                                charging = rx[3] == 0x01;
                            }
                            else if (c.Pid == 0x2232 || c.Pid == 0x2253 || c.Pid == 0x2255 || c.Pid == 0x2269 || c.Pid == 0x226D ||
                                     cleanName.IndexOf("Nova 5", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     cleanName.IndexOf("Nova 3", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                connected = rx[1] != 0x02;
                                pct = Math.Max(0, Math.Min(100, (int)rx[3]));
                                charging = rx[4] == 0x01;
                                if (cleanName.Equals("SteelSeries Headset", StringComparison.OrdinalIgnoreCase))
                                    cleanName = (c.Pid == 0x2232 || c.Pid == 0x2253 || c.Pid == 0x2255) ? "SteelSeries Arctis Nova 5" : "SteelSeries Arctis Nova 3P";
                            }
                            else
                            {
                                bool discrete = (c.Pid == 0x2202 || c.Pid == 0x2206 || c.Pid == 0x220A || c.Pid == 0x223A || c.Pid == 0x227A || c.Pid == 0x22A4);
                                connected = rx[3] != 0x00;
                                charging = (rx[3] == 0x01 || rx[3] == 0x02);
                                pct = Math.Max(0, Math.Min(100, discrete ? (int)Math.Round(rx[2] * 25.0) : (int)rx[2]));
                            }
                        }
                    }
                    else if (isArctisColl)
                    {
                        bool isA1 = (c.Pid == 0x12B3 || c.Pid == 0x12B6 || c.Pid == 0x12D7);
                        bool isA7 = (c.Pid == 0x1260 || c.Pid == 0x12AD || c.Pid == 0x1252);
                        if (isA1 || isA7)
                        {
                            byte[] tx = new byte[c.OutLen];
                            tx[0] = 0x06;
                            tx[1] = isA1 ? (byte)0x12 : (byte)0x18;
                            byte[] rx;
                            if (OverlappedHidReadWithRetry(hSs, tx, c.InLen, out rx, r => r.Length >= 4, 500))
                            {
                                if (isA1) { connected = rx[2] != 0x01; pct = rx[3]; }
                                else { pct = rx[2]; connected = pct > 0; }
                            }
                        }
                    }

                    claimDev(c.Vid, c.Pid, c.ContainerId);
                    if (connected && pct > 0 && pct <= 100 && !addedNames.Contains(cleanName))
                    {
                        results.Add(new WirelessDeviceReading
                        {
                            IdKey = "HID_1038_" + c.Pid.ToString("X4"),
                            Icon = "🎧",
                            Name = cleanName,
                            Detail = charging ? (isEn ? "Headset • ⚡ Charging" : "Kulaklık • ⚡ Şarjda") : (isEn ? "Headset • 2.4GHz" : "Kulaklık • 2.4GHz"),
                            PctText = isEn ? (pct + "%") : ("%" + pct),
                            BarPct = pct,
                            IsCharging = charging
                        });
                        addedNames.Add(cleanName);
                    }
                }
                finally { CloseHandle(hSs); }
            }

            // 1D. HyperX / HP Wireless Headsets (VID 03F0 / 0951, MI_03 Col01, Report 0x66)
            foreach (HidCollectionInfo c in colls)
            {
                if (!((c.Vid == 0x03F0 && (c.Pid == 0x05B7 || (c.Pid & 0xFF00) == 0x0900)) ||
                      (c.Vid == 0x0951 && (c.Pid & 0xFF00) == 0x1700))) continue;
                if (c.Path.IndexOf("&mi_03&col01", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (claimedVidPids.Contains(vpKey(c.Vid, c.Pid))) continue;

                claimDev(c.Vid, c.Pid, c.ContainerId);
                string rawProd = ReadUsbProductName(c.Path, "HyperX Wireless");
                string cleanName = Regex.Replace(rawProd, "(?i)\\s*wireless\\s*$", "").Trim();
                if (string.IsNullOrEmpty(cleanName)) cleanName = rawProd;

                IntPtr hHeadset = CreateFile(c.Path, 0xC0000000U, 3, IntPtr.Zero, 3, 0x40000000U, IntPtr.Zero);
                if (hHeadset != IntPtr.Zero && hHeadset != new IntPtr(-1))
                {
                    try
                    {
                        byte[] tx = new byte[62];
                        tx[0] = 0x66; tx[1] = 0x89;
                        byte[] rx;
                        if (OverlappedHidQuery(hHeadset, tx, out rx, 62, 120) && rx[0] == 0x66 && rx[1] == 0x89)
                        {
                            int pct = (rx[4] > 0 && rx[4] <= 100) ? (int)rx[4] : ((rx[3] > 0 && rx[3] <= 100) ? (int)rx[3] : 0);
                            if (pct > 0)
                            {
                                bool isCharging = false;
                                byte[] txChg = new byte[62];
                                txChg[0] = 0x66; txChg[1] = 0x8A;
                                byte[] rxChg;
                                if (OverlappedHidQuery(hHeadset, txChg, out rxChg, 62, 100) && rxChg[0] == 0x66 && rxChg[1] == 0x8A)
                                {
                                    if (rxChg[4] == 1 || rxChg[3] == 1) isCharging = true;
                                }
                                results.Add(new WirelessDeviceReading
                                {
                                    IdKey = c.Path,
                                    Icon = "🎧",
                                    Name = cleanName,
                                    Detail = isCharging ? (isEn ? "Headset • ⚡ Charging" : "Kulaklık • ⚡ Şarjda") : (isEn ? "Headset • 2.4GHz" : "Kulaklık • 2.4GHz"),
                                    PctText = isEn ? (pct + "%") : ("%" + pct),
                                    BarPct = pct,
                                    IsCharging = isCharging
                                });
                                addedNames.Add(cleanName);
                            }
                        }
                    }
                    finally { CloseHandle(hHeadset); }
                }
            }

            // 1E. Logitech HID++ 2.0 / 1.0 (VID 046D — Unifying, Bolt, LIGHTSPEED, Wired/BT, G Headsets)
            try { PollLogitechHidpp(colls, isEn, claimedVidPids, claimDev, lastKnownPcts, addedNames, results); } catch { }

            // 1F. Razer Wireless (VID 1532 — 90-byte Feature Report HyperSpeed/Mice/Keyboards + BlackShark V2 HyperSpeed)
            try { PollRazerHid(colls, isEn, claimedVidPids, claimDev, lastKnownPcts, addedNames, results); } catch { }

            // 1G. Corsair Wireless (VID 1B1C — Void/HS70/HS80, CompX Sabre V2, Bragi/Slipstream)
            try { PollCorsairHid(colls, isEn, claimedVidPids, claimDev, addedNames, results); } catch { }

            // 2. Scan Windows Native PnP / Bluetooth / BLE / Dongle Battery Devices (generic.cpp PollBluetooth logic)
            try
            {
                Dictionary<string, bool> btConnByMac;
                Dictionary<string, bool> btConnByName;
                QueryClassicBluetoothStates(out btConnByMac, out btConnByName);

                IntPtr hPnp = SetupDiGetClassDevsAll(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0x02 | 0x04);
                if (hPnp != IntPtr.Zero && hPnp != new IntPtr(-1))
                {
                    try
                    {
                        SP_DEVINFO_DATA devInfo = new SP_DEVINFO_DATA();
                        devInfo.cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVINFO_DATA));
                        DEVPROPKEY batKey = new DEVPROPKEY { fmtid = new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"), pid = 2 };
                        DEVPROPKEY isConnKey = new DEVPROPKEY { fmtid = new Guid("83DA6326-97A6-4088-9453-A1923F573B29"), pid = 15 };
                        DEVPROPKEY containerKey = new DEVPROPKEY { fmtid = new Guid("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C"), pid = 2 };
                        DEVPROPKEY hwIdsKey = new DEVPROPKEY { fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), pid = 3 };
                        DEVPROPKEY codKey = new DEVPROPKEY { fmtid = new Guid("2BD67D8B-8BEB-48D5-87E0-6CDA3428040A"), pid = 10 };

                        Dictionary<string, PnpBatteryCandidate> byContainer = new Dictionary<string, PnpBatteryCandidate>(StringComparer.OrdinalIgnoreCase);
                        Dictionary<Guid, bool> containerConn = new Dictionary<Guid, bool>();
                        Dictionary<Guid, string> containerBaseNames = new Dictionary<Guid, string>();
                        Dictionary<Guid, uint> containerCod = new Dictionary<Guid, uint>();

                        byte[] propBuf = new byte[1024];
                        StringBuilder sbInst = new StringBuilder(512);
                        StringBuilder sbName = new StringBuilder(256);
                        StringBuilder sbClass = new StringBuilder(128);
                        StringBuilder sbEnum = new StringBuilder(128);

                        uint idx = 0;
                        while (SetupDiEnumDeviceInfo(hPnp, idx, ref devInfo))
                        {
                            idx++;
                            uint propType, reqSize;

                            sbInst.Clear();
                            int instReq;
                            SetupDiGetDeviceInstanceIdW(hPnp, ref devInfo, sbInst, sbInst.Capacity, out instReq);
                            string instId = sbInst.ToString().Trim();

                            Guid containerId = Guid.Empty;
                            if (SetupDiGetDevicePropertyW(hPnp, ref devInfo, ref containerKey, out propType, propBuf, 16, out reqSize, 0) && reqSize >= 16)
                            {
                                byte[] gBytes = new byte[16];
                                Array.Copy(propBuf, 0, gBytes, 0, 16);
                                containerId = new Guid(gBytes);
                            }

                            sbName.Clear();
                            uint regType, regReq;
                            if (!SetupDiGetDeviceRegistryProperty(hPnp, ref devInfo, 0x0C, out regType, sbName, (uint)sbName.Capacity, out regReq) || sbName.Length == 0)
                            {
                                sbName.Clear();
                                SetupDiGetDeviceRegistryProperty(hPnp, ref devInfo, 0x00, out regType, sbName, (uint)sbName.Capacity, out regReq);
                            }
                            string devName = sbName.ToString().Trim();

                            bool hasConnProp = false;
                            bool connVal = true; // default true if property missing, matching generic.cpp
                            if (SetupDiGetDevicePropertyW(hPnp, ref devInfo, ref isConnKey, out propType, propBuf, 16, out reqSize, 0) && reqSize >= 1)
                            {
                                hasConnProp = true;
                                connVal = (propBuf[0] != 0);
                            }

                            if (containerId != Guid.Empty &&
                                (instId.StartsWith(@"BTHENUM\DEV_", StringComparison.OrdinalIgnoreCase) ||
                                 instId.StartsWith(@"BTHLE\DEV_", StringComparison.OrdinalIgnoreCase)))
                            {
                                if (!string.IsNullOrEmpty(devName) && !IsGenericBluetoothNodeName(devName) && !containerBaseNames.ContainsKey(containerId))
                                {
                                    string baseClean = StripAudioProfileSuffixes(devName);
                                    if (!string.IsNullOrEmpty(baseClean)) containerBaseNames[containerId] = baseClean;
                                }
                                if (SetupDiGetDevicePropertyW(hPnp, ref devInfo, ref codKey, out propType, propBuf, 16, out reqSize, 0) && reqSize >= 4)
                                {
                                    containerCod[containerId] = BitConverter.ToUInt32(propBuf, 0);
                                }
                                if (hasConnProp)
                                {
                                    bool prevC = false;
                                    containerConn.TryGetValue(containerId, out prevC);
                                    containerConn[containerId] = prevC || connVal;
                                }
                            }

                            if (SetupDiGetDevicePropertyW(hPnp, ref devInfo, ref batKey, out propType, propBuf, 16, out reqSize, 0) && reqSize >= 1)
                            {
                                int pct = propBuf[0];
                                if (pct > 0 && pct <= 100)
                                {
                                    uint cmStatus = 0, cmProblem = 0;
                                    bool cmOk = false;
                                    try { cmOk = (CM_Get_DevNode_Status(out cmStatus, out cmProblem, devInfo.DevInst, 0) == 0); } catch { }
                                    if (cmOk && (cmStatus & 0x00000400u) != 0) continue; // DN_HAS_PROBLEM

                                    sbClass.Clear();
                                    SetupDiGetDeviceRegistryProperty(hPnp, ref devInfo, 0x07, out regType, sbClass, (uint)sbClass.Capacity, out regReq);
                                    sbEnum.Clear();
                                    SetupDiGetDeviceRegistryProperty(hPnp, ref devInfo, 0x16, out regType, sbEnum, (uint)sbEnum.Capacity, out regReq);

                                    string hwIds = "";
                                    if (SetupDiGetDevicePropertyW(hPnp, ref devInfo, ref hwIdsKey, out propType, propBuf, (uint)propBuf.Length, out reqSize, 0) && reqSize > 0)
                                    {
                                        int byteLen = (int)Math.Min(reqSize, (uint)propBuf.Length);
                                        hwIds = Encoding.Unicode.GetString(propBuf, 0, byteLen).Replace('\0', ' ').Trim();
                                    }

                                    string cKey = (containerId != Guid.Empty) ? containerId.ToString("N") : instId;
                                    PnpBatteryCandidate prev;
                                    if (byContainer.TryGetValue(cKey, out prev) && prev.IsConnected && !connVal) continue;

                                    byContainer[cKey] = new PnpBatteryCandidate
                                    {
                                        RawName = devName,
                                        DevClass = sbClass.ToString().Trim(),
                                        InstanceId = instId,
                                        Enumerator = sbEnum.ToString().Trim(),
                                        HardwareIds = hwIds,
                                        ContainerId = containerId,
                                        Pct = pct,
                                        IsConnected = connVal
                                    };
                                }
                            }
                        }

                        foreach (PnpBatteryCandidate c in byContainer.Values)
                        {
                            if (c.ContainerId != Guid.Empty && claimedContainers.Contains(c.ContainerId)) continue;

                            bool isBt = IsBluetoothTransport(c.Enumerator, c.InstanceId, c.HardwareIds);
                            bool mainConnected = false;
                            if (c.ContainerId != Guid.Empty && containerConn.TryGetValue(c.ContainerId, out mainConnected))
                            {
                                if (!mainConnected) continue;
                            }
                            else
                            {
                                if (!c.IsConnected) continue;
                                if (isBt)
                                {
                                    string mac = ExtractBtMacFromInstanceId(c.InstanceId);
                                    bool macConn;
                                    if (!string.IsNullOrEmpty(mac) && btConnByMac.TryGetValue(mac, out macConn) && !macConn) continue;
                                }
                            }

                            string baseName = (c.ContainerId != Guid.Empty && containerBaseNames.ContainsKey(c.ContainerId))
                                ? containerBaseNames[c.ContainerId]
                                : "";
                            string cleanName = CleanWirelessDeviceName(c.RawName, baseName);
                            if (string.IsNullOrEmpty(cleanName) || addedNames.Contains(cleanName)) continue;

                            string icon, typeLabel;
                            uint cod;
                            if (c.ContainerId != Guid.Empty && containerCod.TryGetValue(c.ContainerId, out cod) &&
                                KindFromClassOfDevice(cod, isEn, out icon, out typeLabel))
                            {
                            }
                            else
                            {
                                ClassifyDeviceTypeAndIcon(cleanName, c.DevClass, isEn, out icon, out typeLabel);
                            }

                            string protoSuffix = "";
                            if (isBt && !c.InstanceId.StartsWith(@"HID\VID_", StringComparison.OrdinalIgnoreCase) && !c.InstanceId.StartsWith(@"USB\VID_", StringComparison.OrdinalIgnoreCase))
                            {
                                protoSuffix = " • Bluetooth";
                            }
                            else if (c.Enumerator.Equals("HID", StringComparison.OrdinalIgnoreCase) ||
                                     c.Enumerator.Equals("USB", StringComparison.OrdinalIgnoreCase) ||
                                     c.InstanceId.StartsWith(@"HID\", StringComparison.OrdinalIgnoreCase) ||
                                     c.InstanceId.StartsWith(@"USB\", StringComparison.OrdinalIgnoreCase))
                            {
                                protoSuffix = " • 2.4GHz";
                            }

                            string idKey = "PNP_" + (c.ContainerId != Guid.Empty ? c.ContainerId.ToString("N") : cleanName.ToLowerInvariant());
                            results.Add(new WirelessDeviceReading
                            {
                                IdKey = idKey,
                                Icon = icon,
                                Name = cleanName,
                                Detail = typeLabel + protoSuffix,
                                PctText = isEn ? (c.Pct + "%") : ("%" + c.Pct),
                                BarPct = c.Pct,
                                IsCharging = false
                            });
                            addedNames.Add(cleanName);
                            if (c.ContainerId != Guid.Empty) claimedContainers.Add(c.ContainerId);
                        }
                    }
                    finally { SetupDiDestroyDeviceInfoList(hPnp); }
                }
            }
            catch { }

            // 3. Generic USB HID Battery Fallback (UsagePage 0x06/0x20 or 0x85/0x65)
            try
            {
                HashSet<uint> digitizers = new HashSet<uint>();
                foreach (HidCollectionInfo c in colls)
                {
                    if (c.UsagePage == 0x0D) digitizers.Add(vpKey(c.Vid, c.Pid));
                }

                foreach (HidCollectionInfo c in colls)
                {
                    uint k = vpKey(c.Vid, c.Pid);
                    if (claimedVidPids.Contains(k) || digitizers.Contains(k)) continue;
                    if (c.ContainerId != Guid.Empty && claimedContainers.Contains(c.ContainerId)) continue;
                    if (c.UsagePage == 0x84 || c.UsagePage == 0x85) continue; // Skip UPS / power supplies
                    if (c.NumFeatureValueCaps == 0 && c.NumInputValueCaps == 0) continue;

                    IntPtr h = CreateFile(c.Path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                    if (h == IntPtr.Zero || h == new IntPtr(-1)) continue;
                    try
                    {
                        IntPtr pp;
                        if (!HidD_GetPreparsedData(h, out pp) || pp == IntPtr.Zero) continue;
                        try
                        {
                            int pct = -1;
                            bool charging = false;
                            int[] reportTypes = { 2, 0 }; // HidP_Feature = 2, HidP_Input = 0
                            foreach (int rType in reportTypes)
                            {
                                ushort nCaps = (rType == 2) ? c.NumFeatureValueCaps : c.NumInputValueCaps;
                                ushort rLen = (rType == 2) ? c.FeatLen : c.InLen;
                                if (nCaps == 0 || rLen == 0) continue;

                                HIDP_VALUE_CAPS[] vc = new HIDP_VALUE_CAPS[nCaps];
                                if (HidP_GetValueCaps(rType, vc, ref nCaps, pp) != 0x00110000) continue;

                                for (int i = 0; i < nCaps && pct < 0; i++)
                                {
                                    ushort u = (vc[i].IsRange != 0) ? vc[i].UsageMin : vc[i].UsageMin;
                                    if (!IsGenericHidBatteryUsage(vc[i].UsagePage, u)) continue;

                                    byte[] buf = new byte[rLen];
                                    buf[0] = vc[i].ReportID;
                                    bool got = (rType == 2) ? HidD_GetFeature(h, buf, rLen) : HidD_GetInputReport(h, buf, rLen);
                                    if (!got) continue;

                                    uint rawVal;
                                    if (HidP_GetUsageValue(rType, vc[i].UsagePage, vc[i].LinkCollection, u, out rawVal, pp, buf, rLen) != 0x00110000) continue;

                                    if (c.Vid == 0x05AC && rawVal <= 100)
                                    {
                                        pct = (int)rawVal;
                                    }
                                    else if (vc[i].LogicalMax > vc[i].LogicalMin)
                                    {
                                        double scaled = (((int)rawVal - vc[i].LogicalMin) * 100.0) / (vc[i].LogicalMax - vc[i].LogicalMin);
                                        pct = Math.Max(0, Math.Min(100, (int)Math.Round(scaled)));
                                    }
                                    else if (rawVal <= 100)
                                    {
                                        pct = (int)rawVal;
                                    }

                                    if (pct >= 0)
                                    {
                                        bool chg = false, full = false, ac = false, dis = false;
                                        uint maxU = HidP_MaxUsageListLength(rType, 0x85, pp);
                                        if (maxU > 0)
                                        {
                                            ushort[] on = new ushort[maxU];
                                            uint uLen = maxU;
                                            if (HidP_GetUsages(rType, 0x85, 0, on, ref uLen, pp, buf, rLen) == 0x00110000)
                                            {
                                                for (int ui = 0; ui < uLen; ui++)
                                                {
                                                    if (on[ui] == 0x44) chg = true;
                                                    else if (on[ui] == 0x45) dis = true;
                                                    else if (on[ui] == 0x46) full = true;
                                                    else if (on[ui] == 0xD0) ac = true;
                                                }
                                            }
                                        }
                                        for (int vi = 0; vi < nCaps; vi++)
                                        {
                                            if (vc[vi].UsagePage != 0x85) continue;
                                            ushort vu = vc[vi].UsageMin;
                                            if (vu != 0x44 && vu != 0x45 && vu != 0x46 && vu != 0xD0) continue;
                                            uint vVal;
                                            if (HidP_GetUsageValue(rType, 0x85, vc[vi].LinkCollection, vu, out vVal, pp, buf, rLen) == 0x00110000 && vVal != 0)
                                            {
                                                if (vu == 0x44) chg = true;
                                                else if (vu == 0x45) dis = true;
                                                else if (vu == 0x46) full = true;
                                                else if (vu == 0xD0) ac = true;
                                            }
                                        }
                                        charging = chg || full || (ac && !dis);
                                    }
                                }
                                if (pct >= 0) break;
                            }

                            if (pct > 0 && pct <= 100)
                            {
                                claimDev(c.Vid, c.Pid, c.ContainerId);
                                string rawName = ReadUsbProductName(c.Path, isEn ? "Wireless Device" : "Kablosuz Cihaz");
                                string cleanName = StripAudioProfileSuffixes(rawName);
                                if (string.IsNullOrEmpty(cleanName)) cleanName = isEn ? "Wireless Device" : "Kablosuz Cihaz";
                                if (!addedNames.Contains(cleanName))
                                {
                                    string icon, typeLabel;
                                    ClassifyDeviceTypeAndIcon(cleanName, "", isEn, out icon, out typeLabel);
                                    results.Add(new WirelessDeviceReading
                                    {
                                        IdKey = string.Format("HID_{0:X4}_{1:X4}", c.Vid, c.Pid),
                                        Icon = icon,
                                        Name = cleanName,
                                        Detail = charging ? (typeLabel + " • " + (isEn ? "⚡ Charging" : "⚡ Şarjda")) : (typeLabel + " • 2.4GHz"),
                                        PctText = isEn ? (pct + "%") : ("%" + pct),
                                        BarPct = pct,
                                        IsCharging = charging
                                    });
                                    addedNames.Add(cleanName);
                                }
                            }
                        }
                        finally { HidD_FreePreparsedData(pp); }
                    }
                    finally { CloseHandle(h); }
                }
            }
            catch { }

            // 4. Scan XInput & DirectInput Wireless Controllers (Gamepads)
            bool hasControllerAlready = false;
            foreach (WirelessDeviceReading r in results)
            {
                if (r.Icon == "🎮") { hasControllerAlready = true; break; }
            }

            if (!hasControllerAlready)
            {
                bool padConnected = false;
                bool padCharging = false;
                string padDetail = "";
                string padPctText = "";
                int padBarPct = 0;

                try
                {
                    for (uint i = 0; i < 4; i++)
                    {
                        XINPUT_STATE st;
                        if (XInputGetState(i, out st) == 0)
                        {
                            padConnected = true;
                            XINPUT_BATTERY_INFORMATION bat;
                            if (XInputGetBatteryInformation(i, 0, out bat) == 0)
                            {
                                if (bat.BatteryType == 1)
                                {
                                    padBarPct = 100;
                                    padCharging = true;
                                    padPctText = isEn ? "100%" : "%100";
                                    padDetail = isEn ? "Controller • Wired" : "Kumanda • Kablolu";
                                }
                                else if (bat.BatteryType == 2 || bat.BatteryType == 3)
                                {
                                    if (bat.BatteryLevel == 3) { padBarPct = 100; padPctText = isEn ? "100%" : "%100"; padDetail = isEn ? "Controller • Full" : "Kumanda • Tam Dolu"; }
                                    else if (bat.BatteryLevel == 2) { padBarPct = 65; padPctText = isEn ? "65%" : "%65"; padDetail = isEn ? "Controller • Good" : "Kumanda • İyi Seviye"; }
                                    else if (bat.BatteryLevel == 1) { padBarPct = 30; padPctText = isEn ? "30%" : "%30"; padDetail = isEn ? "Controller • Low" : "Kumanda • Düşük Pil"; }
                                    else { padBarPct = 10; padPctText = isEn ? "10%" : "%10"; padDetail = isEn ? "Controller • Critical" : "Kumanda • Çok Düşük!"; }
                                }
                                else
                                {
                                    padBarPct = 100;
                                    padPctText = isEn ? "Active" : "Açık";
                                    padDetail = isEn ? "Controller • Connected" : "Kumanda • Bağlı";
                                }
                            }
                            break;
                        }
                    }
                }
                catch { }

                if (!padConnected)
                {
                    try
                    {
                        int numDevs = Math.Min(16, joyGetNumDevs());
                        for (int i = 0; i < numDevs; i++)
                        {
                            JOYINFOEX ji = new JOYINFOEX();
                            ji.dwSize = Marshal.SizeOf(typeof(JOYINFOEX));
                            ji.dwFlags = 0xFF;
                            if (joyGetPosEx(i, ref ji) == 0)
                            {
                                padConnected = true;
                                padBarPct = 100;
                                padPctText = isEn ? "Active" : "Açık";
                                padDetail = isEn ? "Controller • Wireless" : "Kumanda • Kablosuz";
                                break;
                            }
                        }
                    }
                    catch { }
                }

                if (padConnected && padBarPct > 0)
                {
                    results.Add(new WirelessDeviceReading
                    {
                        IdKey = "GAMEPAD_0",
                        Icon = "🎮",
                        Name = isEn ? "Wireless Controller" : "Kablosuz Kumanda",
                        Detail = padDetail,
                        PctText = padPctText,
                        BarPct = padBarPct,
                        IsCharging = padCharging
                    });
                }
            }

            return results;
        }
    }

    public class ClipboardEntry
    {
        public string Text { get; set; }
        public string Time { get; set; }
    }

    public class GeoLookupResult
    {
        public bool Success;
        public string DisplayName;
        public double Latitude;
        public double Longitude;
    }

    public static class LocationHelper
    {
        public static GeoLookupResult ResolveAutoLocation(string lang = "TR")
        {
            bool isEn = string.Equals(lang, "EN", StringComparison.OrdinalIgnoreCase);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // 1. Try Windows Native GeoCoordinateWatcher (GPS / Wi-Fi Location Services) briefly
            try
            {
                using (GeoCoordinateWatcher watcher = new GeoCoordinateWatcher(GeoPositionAccuracy.Default))
                {
                    watcher.TryStart(false, TimeSpan.FromMilliseconds(1200));
                    DateTime waitUntil = DateTime.Now.AddMilliseconds(1200);
                    while (watcher.Position.Location.IsUnknown && DateTime.Now < waitUntil)
                    {
                        Thread.Sleep(100);
                    }
                    if (!watcher.Position.Location.IsUnknown)
                    {
                        double lat = watcher.Position.Location.Latitude;
                        double lon = watcher.Position.Location.Longitude;
                        watcher.Stop();
                        return new GeoLookupResult
                        {
                            Success = true,
                            DisplayName = isEn ? "GPS Location" : "GPS Konumu",
                            Latitude = lat,
                            Longitude = lon
                        };
                    }
                    watcher.Stop();
                }
            }
            catch { }

            // 2. Fallback to fast IP Geolocation
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://ip-api.com/json/?fields=status,city,regionName,lat,lon");
                req.Timeout = 4500;
                req.UserAgent = "OpenWidgetSetup/1.0";
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                {
                    string json = reader.ReadToEnd();
                    if (json.Contains("\"status\":\"success\""))
                    {
                        string city = ExtractString(json, "city", isEn ? "Auto Location" : "Otomatik Konum");
                        double lat = ExtractDouble(json, "lat", 39.9208);
                        double lon = ExtractDouble(json, "lon", 32.8541);
                        return new GeoLookupResult
                        {
                            Success = true,
                            DisplayName = city,
                            Latitude = lat,
                            Longitude = lon
                        };
                    }
                }
            }
            catch { }

            return new GeoLookupResult { Success = false, DisplayName = isEn ? "Istanbul" : "İstanbul", Latitude = 41.0082, Longitude = 28.9784 };
        }

        public static GeoLookupResult SearchCityByName(string query, string lang = "TR")
        {
            if (string.IsNullOrWhiteSpace(query))
                return new GeoLookupResult { Success = false };

            bool isEn = string.Equals(lang, "EN", StringComparison.OrdinalIgnoreCase);

            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                string cleanQuery = query.Trim();
                // If user typed "İl / İlçe" like "Kütahya / Merkez" or "İstanbul / Kadıköy", search the most specific part or city
                string searchPart = cleanQuery;
                if (cleanQuery.Contains("/"))
                {
                    string[] split = cleanQuery.Split('/');
                    string p0 = split[0].Trim();
                    string p1 = split[1].Trim();
                    searchPart = (p1.Equals("Merkez", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(p1)) ? p0 : p1;
                }

                string apiLang = isEn ? "en" : "tr";
                string url = "https://geocoding-api.open-meteo.com/v1/search?name=" + Uri.EscapeDataString(searchPart) + "&count=1&language=" + apiLang + "&format=json";
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = 5000;
                req.UserAgent = "OpenWidgetSetup/1.0";

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                {
                    string json = reader.ReadToEnd();
                    if (json.Contains("\"results\":"))
                    {
                        string name = ExtractString(json, "name", searchPart);
                        string admin1 = ExtractString(json, "admin1", "");
                        double lat = ExtractDouble(json, "latitude", 41.0082);
                        double lon = ExtractDouble(json, "longitude", 28.9784);

                        string label = cleanQuery.Contains("/")
                            ? cleanQuery
                            : (!string.IsNullOrEmpty(admin1) && !admin1.Equals(name, StringComparison.OrdinalIgnoreCase) ? admin1 + " / " + name : name);

                        return new GeoLookupResult
                        {
                            Success = true,
                            DisplayName = label,
                            Latitude = lat,
                            Longitude = lon
                        };
                    }
                }
            }
            catch { }

            return new GeoLookupResult { Success = false };
        }

        private static string ExtractString(string json, string key, string fallback)
        {
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : fallback;
        }

        private static double ExtractDouble(string json, string key, double fallback)
        {
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(-?\\d+(\\.\\d+)?)");
            if (m.Success)
            {
                double v;
                if (double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out v))
                    return v;
            }
            return fallback;
        }
    }

    public class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(int value);

        private static ImageSource cachedWindowIcon;
        public static void TryApplyWindowIcon(Window w)
        {
            try
            {
                if (cachedWindowIcon == null)
                {
                    string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    using (System.Drawing.Icon ico = System.Drawing.Icon.ExtractAssociatedIcon(exePath))
                    {
                        if (ico != null)
                        {
                            ImageSource img = Imaging.CreateBitmapSourceFromHIcon(ico.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                            img.Freeze();
                            cachedWindowIcon = img;
                        }
                    }
                }
                if (cachedWindowIcon != null) w.Icon = cachedWindowIcon;
            }
            catch { }
        }

        public static void ApplyThinScrollBarStyle(ScrollViewer sv, string accentHex)
        {
            if (sv == null) return;
            try
            {
                string thumbHex = string.IsNullOrEmpty(accentHex) ? "#E2925C" : accentHex;
                string xaml = string.Format(@"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
       TargetType='{{x:Type ScrollBar}}'>
    <Setter Property='Width' Value='5'/>
    <Setter Property='MinWidth' Value='5'/>
    <Setter Property='Margin' Value='2,2,0,2'/>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='Template'>
        <Setter.Value>
            <ControlTemplate TargetType='{{x:Type ScrollBar}}'>
                <Border Background='#261A1411' CornerRadius='2.5' Width='5'>
                    <Track x:Name='PART_Track' IsDirectionReversed='True'>
                        <Track.DecreaseRepeatButton>
                            <RepeatButton Command='ScrollBar.PageUpCommand' Opacity='0' Focusable='False'/>
                        </Track.DecreaseRepeatButton>
                        <Track.IncreaseRepeatButton>
                            <RepeatButton Command='ScrollBar.PageDownCommand' Opacity='0' Focusable='False'/>
                        </Track.IncreaseRepeatButton>
                        <Track.Thumb>
                            <Thumb Cursor='Hand'>
                                <Thumb.Template>
                                    <ControlTemplate TargetType='{{x:Type Thumb}}'>
                                        <Border CornerRadius='2.5' Background='{0}' Opacity='0.85'/>
                                    </ControlTemplate>
                                </Thumb.Template>
                            </Thumb>
                        </Track.Thumb>
                    </Track>
                </Border>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>", thumbHex);
                Style sbStyle = (Style)System.Windows.Markup.XamlReader.Parse(xaml);
                sv.Resources[typeof(ScrollBar)] = sbStyle;
            }
            catch { }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            // Multi-core background JIT profile for faster cold-boot launches
            try
            {
                System.Runtime.ProfileOptimization.SetProfileRoot(AppDomain.CurrentDomain.BaseDirectory);
                System.Runtime.ProfileOptimization.StartProfile("OpenWidget.jit.profile");
            }
            catch { }

            try { SetProcessDpiAwareness(2); }
            catch { try { SetProcessDPIAware(); } catch { } }

            bool forceSetup = false;
            if (args != null)
            {
                foreach (string a in args)
                {
                    if (a.Equals("--setup", StringComparison.OrdinalIgnoreCase) || a.Equals("/setup", StringComparison.OrdinalIgnoreCase))
                        forceSetup = true;
                }
            }

            string cfgPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.cfg");
            Application app = new Application();

            if (forceSetup || !File.Exists(cfgPath))
            {
                SetupWizardWindow wizard = new SetupWizardWindow(cfgPath);
                bool? res = wizard.ShowDialog();
                if (res != true && !File.Exists(cfgPath))
                {
                    return;
                }
            }

            bool createdNew;
            using (Mutex mutex = new Mutex(true, "OpenWidget_Public_Edition_Mutex", out createdNew))
            {
                if (!createdNew)
                {
                    return;
                }
                MainWindow win = new MainWindow();
                app.Run(win);
            }
        }
    }

    // ========================================================
    // INTERACTIVE SETUP WIZARD (KURULUM YARDIMCISI - TR / EN)
    // ========================================================
    public class SetupWizardWindow : Window
    {
        private string configFilePath;
        private string selectedLanguage = "TR";
        private Button btnLangTr;
        private Button btnLangEn;
        private TextBlock txtWizTitle;
        private TextBlock txtWizSubtitle;
        private TextBlock txtStepLang;
        private TextBlock txtStep1;
        private TextBlock txtStep2;
        private TextBlock txtStep3;
        private TextBlock txtStep4;
        private RadioButton rbAutoGps;
        private RadioButton rbManualCity;
        private TextBox txtCityInput;
        private Button btnTestCity;
        private TextBlock txtStatusFeedback;
        private Button btnCustomWizardColor;
        private CheckBox chkWizardCalendar;
        private CheckBox chkWizardTimer;
        private CheckBox chkDesktopShortcut;
        private CheckBox chkStartupRun;
        private string selectedAccent = "#E2925C";
        private GeoLookupResult resolvedLocation;
        private Button btnFinish;

        public SetupWizardWindow(string cfgPath)
        {
            this.configFilePath = cfgPath;
            this.Title = "OpenWidget Setup / Kurulum Yardımcısı";
            this.Width = 480;
            this.Height = Math.Max(480, Math.Min(650, SystemParameters.WorkArea.Height - 40));
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.ResizeMode = ResizeMode.NoResize;
            this.UseLayoutRounding = true;
            this.SnapsToDevicePixels = true;
            this.FontFamily = new FontFamily("Segoe UI");
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
            Program.TryApplyWindowIcon(this);

            BuildWizardUi();
        }

        private bool IsEn
        {
            get { return string.Equals(selectedLanguage, "EN", StringComparison.OrdinalIgnoreCase); }
        }

        private Brush HexBrush(string hex)
        {
            return (SolidColorBrush)new BrushConverter().ConvertFromString(hex);
        }

        private void BuildWizardUi()
        {
            Border card = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = HexBrush("#F71B1714"),
                BorderBrush = HexBrush("#E2925C"),
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(22, 16, 22, 18),
                Margin = new Thickness(10)
            };
            card.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 18, Opacity = 0.7 };

            Grid rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Scrollable steps
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Finish footer

            // Row 0: Draggable Header
            Grid hdr = new Grid { Margin = new Thickness(0, 0, 0, 10), Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
            hdr.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    try { this.DragMove(); } catch { }
                }
            };
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titleStack = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
            txtWizTitle = new TextBlock { Text = "✨ OpenWidget Kurulum Yardımcısı", Foreground = HexBrush("#FAF6F2"), FontSize = 16.5, FontWeight = FontWeights.Bold };
            txtWizSubtitle = new TextBlock { Text = "Masaüstü widget'ınızı kullanmaya başlamadan önce tercihlerinizi seçin.", Foreground = HexBrush("#B8A99E"), FontSize = 11.5, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap };
            titleStack.Children.Add(txtWizTitle);
            titleStack.Children.Add(txtWizSubtitle);
            Grid.SetColumn(titleStack, 0);
            hdr.Children.Add(titleStack);

            Button btnClose = new Button
            {
                Content = "✕",
                Foreground = HexBrush("#B8A99E"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Top
            };
            btnClose.Click += (s, e) => { this.DialogResult = false; this.Close(); };
            Grid.SetColumn(btnClose, 1);
            hdr.Children.Add(btnClose);
            Grid.SetRow(hdr, 0);
            rootGrid.Children.Add(hdr);

            // Row 1: Smooth ScrollViewer containing Setup Steps
            ScrollViewer wizScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                PanningMode = PanningMode.VerticalOnly,
                Margin = new Thickness(0, 0, 0, 12)
            };
            Program.ApplyThinScrollBarStyle(wizScroll, selectedAccent);
            StackPanel root = new StackPanel { Margin = new Thickness(0, 0, 4, 0) };
            wizScroll.Content = root;
            Grid.SetRow(wizScroll, 1);
            rootGrid.Children.Add(wizScroll);

            root.Children.Add(new Border { Height = 1, Background = HexBrush("#362C26"), Margin = new Thickness(0, 0, 0, 12) });

            // Language Selection Row (TR / EN)
            Grid langRow = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            langRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            langRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            langRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            txtStepLang = new TextBlock
            {
                Text = "🌐 Arayüz Dili / Language",
                Foreground = HexBrush("#E2925C"),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(txtStepLang, 0);
            langRow.Children.Add(txtStepLang);

            btnLangTr = new Button
            {
                Content = "🇹🇷 Türkçe",
                Background = HexBrush("#38281E"),
                Foreground = HexBrush("#FAF6F2"),
                BorderBrush = HexBrush("#E2925C"),
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(12, 5, 12, 5),
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold
            };
            btnLangTr.Click += (s, e) => SetWizardLanguage("TR");
            Grid.SetColumn(btnLangTr, 1);
            langRow.Children.Add(btnLangTr);

            btnLangEn = new Button
            {
                Content = "🇬🇧 English",
                Background = HexBrush("#26201C"),
                Foreground = HexBrush("#B8A99E"),
                BorderBrush = HexBrush("#4A3C34"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 5, 12, 5),
                Cursor = Cursors.Hand,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold
            };
            btnLangEn.Click += (s, e) => SetWizardLanguage("EN");
            Grid.SetColumn(btnLangEn, 2);
            langRow.Children.Add(btnLangEn);
            root.Children.Add(langRow);

            // Step 1: Weather Location Choice
            txtStep1 = new TextBlock { Text = "1. Hava Durumu Konumu Nasıl Belirlensin?", Foreground = HexBrush("#E2925C"), FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
            root.Children.Add(txtStep1);

            Border locBox = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = HexBrush("#26201C"),
                BorderBrush = HexBrush("#3D322C"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 12)
            };
            StackPanel locStack = new StackPanel();

            rbAutoGps = new RadioButton
            {
                Content = "📍 Otomatik Algıla (GPS / Konum Servisi ile bul)",
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                IsChecked = true,
                Margin = new Thickness(0, 2, 0, 8),
                Cursor = Cursors.Hand
            };

            rbManualCity = new RadioButton
            {
                Content = "🏙 Sabit İl / İlçe Yazarak Belirle (Örn: İstanbul, Ankara, Kütahya / Merkez)",
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = Cursors.Hand
            };

            Grid manualInputGrid = new Grid { Margin = new Thickness(20, 0, 0, 4), IsEnabled = false, Opacity = 0.5 };
            manualInputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            manualInputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            txtCityInput = new TextBox
            {
                Text = "İstanbul / Kadıköy",
                Background = HexBrush("#1C1714"),
                Foreground = HexBrush("#FAF6F2"),
                BorderBrush = HexBrush("#4A3C34"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 5, 8, 5),
                FontSize = 12
            };
            Grid.SetColumn(txtCityInput, 0);
            manualInputGrid.Children.Add(txtCityInput);

            btnTestCity = new Button
            {
                Content = "Doğrula",
                Background = HexBrush("#38281E"),
                Foreground = HexBrush("#FAF6F2"),
                BorderBrush = HexBrush("#E2925C"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(6, 0, 0, 0),
                Cursor = Cursors.Hand,
                FontSize = 11.5
            };
            btnTestCity.Click += (s, e) => VerifyLocationAsync();
            Grid.SetColumn(btnTestCity, 1);
            manualInputGrid.Children.Add(btnTestCity);

            rbAutoGps.Checked += (s, e) =>
            {
                if (manualInputGrid != null) { manualInputGrid.IsEnabled = false; manualInputGrid.Opacity = 0.5; }
                VerifyLocationAsync();
            };
            rbManualCity.Checked += (s, e) =>
            {
                if (manualInputGrid != null) { manualInputGrid.IsEnabled = true; manualInputGrid.Opacity = 1.0; txtCityInput.Focus(); }
            };

            txtStatusFeedback = new TextBlock
            {
                Text = "Konum algılanıyor...",
                Foreground = HexBrush("#34D399"),
                FontSize = 11,
                Margin = new Thickness(2, 4, 0, 0)
            };

            locStack.Children.Add(rbAutoGps);
            locStack.Children.Add(rbManualCity);
            locStack.Children.Add(manualInputGrid);
            locStack.Children.Add(txtStatusFeedback);
            locBox.Child = locStack;
            root.Children.Add(locBox);

            // Step 2: Theme Color
            txtStep2 = new TextBlock { Text = "2. Başlangıç Tema Rengi", Foreground = HexBrush("#E2925C"), FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
            root.Children.Add(txtStep2);
            string[] presets = { "#E2925C", "#D97706", "#10B981", "#38BDF8", "#A78BFA", "#F43F5E" };
            Grid themeSelectRow = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            themeSelectRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            themeSelectRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            UniformGrid swatchGrid = new UniformGrid { Columns = 6 };
            List<Border> swatchBorders = new List<Border>();
            for (int i = 0; i < presets.Length; i++)
            {
                string hex = presets[i];
                Border sw = new Border
                {
                    Width = 30,
                    Height = 30,
                    CornerRadius = new CornerRadius(15),
                    Background = HexBrush(hex),
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(i == 0 ? 2.5 : 0.5),
                    Cursor = Cursors.Hand
                };
                swatchBorders.Add(sw);
                sw.MouseLeftButtonDown += (s, e) =>
                {
                    selectedAccent = hex;
                    foreach (Border b in swatchBorders) b.BorderThickness = new Thickness(0.5);
                    sw.BorderThickness = new Thickness(2.5);
                    card.BorderBrush = HexBrush(hex);
                    if (btnFinish != null) btnFinish.Background = HexBrush(hex);
                    Program.ApplyThinScrollBarStyle(wizScroll, selectedAccent);
                };
                swatchGrid.Children.Add(sw);
            }
            Grid.SetColumn(swatchGrid, 0);
            themeSelectRow.Children.Add(swatchGrid);

            btnCustomWizardColor = new Button
            {
                Content = "🎨 Özel Renk Seç...",
                Background = HexBrush("#2A221E"),
                Foreground = HexBrush("#FAF6F2"),
                BorderBrush = HexBrush("#4A3C34"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(8, 0, 0, 0),
                Cursor = Cursors.Hand,
                FontSize = 11.5
            };
            btnCustomWizardColor.Click += (s, e) =>
            {
                Color cur = (Color)ColorConverter.ConvertFromString(selectedAccent);
                using (var dlg = new System.Windows.Forms.ColorDialog())
                {
                    dlg.FullOpen = true;
                    dlg.AnyColor = true;
                    dlg.Color = System.Drawing.Color.FromArgb(cur.R, cur.G, cur.B);
                    if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        selectedAccent = string.Format("#{0:X2}{1:X2}{2:X2}", dlg.Color.R, dlg.Color.G, dlg.Color.B);
                        foreach (Border b in swatchBorders) b.BorderThickness = new Thickness(0.5);
                        card.BorderBrush = HexBrush(selectedAccent);
                        if (btnFinish != null) btnFinish.Background = HexBrush(selectedAccent);
                        Program.ApplyThinScrollBarStyle(wizScroll, selectedAccent);
                    }
                }
            };
            Grid.SetColumn(btnCustomWizardColor, 1);
            themeSelectRow.Children.Add(btnCustomWizardColor);
            root.Children.Add(themeSelectRow);

            // Step 3: Companion Desktop Widgets (Optional, Default Off)
            txtStep3 = new TextBlock { Text = "3. Ek Masaüstü Widget'ları (İsteğe Bağlı)", Foreground = HexBrush("#E2925C"), FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
            root.Children.Add(txtStep3);

            Border compBox = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = HexBrush("#26201C"),
                BorderBrush = HexBrush("#3D322C"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 0, 0, 12)
            };
            StackPanel compStack = new StackPanel();
            chkWizardCalendar = new CheckBox
            {
                Content = "📅 Google Takvim Widget'ını Aç (Aylık takvim, iCal & etkinlikler)",
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 11.5,
                IsChecked = false,
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand
            };
            chkWizardTimer = new CheckBox
            {
                Content = "⏱ Timer Widgeti'ni Aç (Zamanlayıcı, Oyun Modu, Pomodoro & Yıllık Aktivite)",
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 11.5,
                IsChecked = false,
                Cursor = Cursors.Hand
            };
            compStack.Children.Add(chkWizardCalendar);
            compStack.Children.Add(chkWizardTimer);
            compBox.Child = compStack;
            root.Children.Add(compBox);

            // Step 4: System Integration Options
            txtStep4 = new TextBlock { Text = "4. Sistem ve Kısayol Seçenekleri", Foreground = HexBrush("#E2925C"), FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
            root.Children.Add(txtStep4);

            chkDesktopShortcut = new CheckBox
            {
                Content = "Masaüstüne OpenWidget kısayolu oluştur",
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 12,
                IsChecked = true,
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand
            };
            chkStartupRun = new CheckBox
            {
                Content = "Bilgisayar açıldığında otomatik olarak başlat (Önerilen)",
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 12,
                IsChecked = true,
                Margin = new Thickness(0, 0, 0, 4),
                Cursor = Cursors.Hand
            };
            root.Children.Add(chkDesktopShortcut);
            root.Children.Add(chkStartupRun);

            // Row 2: Sticky Finish Button Footer
            btnFinish = new Button
            {
                Content = "🚀 Kurulumu Tamamla ve OpenWidget'ı Başlat",
                Background = HexBrush("#E2925C"),
                Foreground = HexBrush("#181310"),
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Padding = new Thickness(14, 10, 14, 10),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnFinish.Click += BtnFinish_Click;
            Grid.SetRow(btnFinish, 2);
            rootGrid.Children.Add(btnFinish);

            card.Child = rootGrid;
            this.Content = card;

            VerifyLocationAsync();
        }

        private void SetWizardLanguage(string lang)
        {
            selectedLanguage = lang;
            bool en = IsEn;

            btnLangTr.Background = HexBrush(en ? "#26201C" : "#38281E");
            btnLangTr.Foreground = HexBrush(en ? "#B8A99E" : "#FAF6F2");
            btnLangTr.BorderBrush = HexBrush(en ? "#4A3C34" : "#E2925C");
            btnLangTr.BorderThickness = new Thickness(en ? 1 : 1.5);

            btnLangEn.Background = HexBrush(en ? "#38281E" : "#26201C");
            btnLangEn.Foreground = HexBrush(en ? "#FAF6F2" : "#B8A99E");
            btnLangEn.BorderBrush = HexBrush(en ? "#E2925C" : "#4A3C34");
            btnLangEn.BorderThickness = new Thickness(en ? 1.5 : 1);

            txtWizTitle.Text = en ? "✨ OpenWidget Setup Wizard" : "✨ OpenWidget Kurulum Yardımcısı";
            txtWizSubtitle.Text = en
                ? "Choose your preferences before launching your desktop widget."
                : "Masaüstü widget'ınızı kullanmaya başlamadan önce tercihlerinizi seçin.";
            txtStepLang.Text = en ? "🌐 Interface Language / Dil" : "🌐 Arayüz Dili / Language";
            txtStep1.Text = en ? "1. How Should Weather Location Be Determined?" : "1. Hava Durumu Konumu Nasıl Belirlensin?";
            rbAutoGps.Content = en
                ? "📍 Auto-Detect (Find via GPS / Location Services)"
                : "📍 Otomatik Algıla (GPS / Konum Servisi ile bul)";
            rbManualCity.Content = en
                ? "🏙 Enter City / District Manually (e.g., London, New York, Istanbul)"
                : "🏙 Sabit İl / İlçe Yazarak Belirle (Örn: İstanbul, Ankara, Kütahya / Merkez)";
            btnTestCity.Content = en ? "Verify" : "Doğrula";
            txtStep2.Text = en ? "2. Initial Theme Accent Color" : "2. Başlangıç Tema Rengi";
            btnCustomWizardColor.Content = en ? "🎨 Custom Color..." : "🎨 Özel Renk Seç...";
            txtStep3.Text = en ? "3. Companion Desktop Widgets (Optional)" : "3. Ek Masaüstü Widget'ları (İsteğe Bağlı)";
            chkWizardCalendar.Content = en
                ? "📅 Enable Calendar Widget (Monthly view, iCal sync & events)"
                : "📅 Google Takvim Widget'ını Aç (Aylık takvim, iCal & etkinlikler)";
            chkWizardTimer.Content = en
                ? "⏱ Enable Timer Widget (Stopwatch, Auto Game Mode, Pomodoro & Heatmap)"
                : "⏱ Timer Widgeti'ni Aç (Zamanlayıcı, Oyun Modu, Pomodoro & Yıllık Aktivite)";
            txtStep4.Text = en ? "4. System & Shortcut Options" : "4. Sistem ve Kısayol Seçenekleri";
            chkDesktopShortcut.Content = en
                ? "Create OpenWidget shortcut on Desktop"
                : "Masaüstüne OpenWidget kısayolu oluştur";
            chkStartupRun.Content = en
                ? "Launch automatically when Windows starts (Recommended)"
                : "Bilgisayar açıldığında otomatik olarak başlat (Önerilen)";
            btnFinish.Content = en
                ? "🚀 Complete Setup & Launch OpenWidget"
                : "🚀 Kurulumu Tamamla ve OpenWidget'ı Başlat";

            if (resolvedLocation != null && resolvedLocation.Success)
            {
                txtStatusFeedback.Text = en
                    ? string.Format("✓ Selected Location: {0} ({1:0.00}, {2:0.00})", resolvedLocation.DisplayName, resolvedLocation.Latitude, resolvedLocation.Longitude)
                    : string.Format("✓ Seçilen Konum: {0} ({1:0.00}, {2:0.00})", resolvedLocation.DisplayName, resolvedLocation.Latitude, resolvedLocation.Longitude);
            }
        }

        private void VerifyLocationAsync()
        {
            bool isAuto = rbAutoGps.IsChecked == true;
            string manualQuery = txtCityInput.Text.Trim();
            bool en = IsEn;
            txtStatusFeedback.Text = en ? "⏳ Checking location coordinates..." : "⏳ Konum koordinatları kontrol ediliyor...";
            txtStatusFeedback.Foreground = HexBrush("#FBBF24");

            Task.Run(() =>
            {
                GeoLookupResult res = isAuto
                    ? LocationHelper.ResolveAutoLocation(selectedLanguage)
                    : LocationHelper.SearchCityByName(manualQuery, selectedLanguage);

                Dispatcher.Invoke(() =>
                {
                    if (res.Success)
                    {
                        resolvedLocation = res;
                        txtStatusFeedback.Text = IsEn
                            ? string.Format("✓ Selected Location: {0} ({1:0.00}, {2:0.00})", res.DisplayName, res.Latitude, res.Longitude)
                            : string.Format("✓ Seçilen Konum: {0} ({1:0.00}, {2:0.00})", res.DisplayName, res.Latitude, res.Longitude);
                        txtStatusFeedback.Foreground = HexBrush("#34D399");
                    }
                    else
                    {
                        txtStatusFeedback.Text = IsEn
                            ? "⚠ City not found, please enter a valid city or district name."
                            : "⚠ Şehir bulunamadı, lütfen geçerli bir il/ilçe adı yazın.";
                        txtStatusFeedback.Foreground = HexBrush("#FB7185");
                    }
                });
            });
        }

        private void BtnFinish_Click(object sender, RoutedEventArgs e)
        {
            btnFinish.IsEnabled = false;
            btnFinish.Content = IsEn ? "Preparing..." : "Hazırlanıyor...";

            bool isAuto = rbAutoGps.IsChecked == true;
            string manualQuery = txtCityInput.Text.Trim();
            bool enableCalendar = chkWizardCalendar != null && chkWizardCalendar.IsChecked == true;
            bool enableTimer = chkWizardTimer != null && chkWizardTimer.IsChecked == true;
            bool createDesktop = chkDesktopShortcut.IsChecked == true;
            bool enableStartup = chkStartupRun.IsChecked == true;
            string langToSave = selectedLanguage;

            Task.Run(() =>
            {
                GeoLookupResult finalLoc = resolvedLocation;
                if (!isAuto)
                {
                    GeoLookupResult manualRes = LocationHelper.SearchCityByName(manualQuery, langToSave);
                    if (manualRes.Success) finalLoc = manualRes;
                }
                else if (finalLoc == null || !finalLoc.Success)
                {
                    finalLoc = LocationHelper.ResolveAutoLocation(langToSave);
                }

                if (finalLoc == null || !finalLoc.Success)
                {
                    finalLoc = new GeoLookupResult { Success = true, DisplayName = IsEn ? "Istanbul" : "İstanbul", Latitude = 41.0082, Longitude = 28.9784 };
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Language=" + langToSave);
                sb.AppendLine("Accent=" + selectedAccent);
                sb.AppendLine("Opacity=88");
                sb.AppendLine("LocationMode=" + (isAuto ? "Auto" : "Manual"));
                sb.AppendLine("LocationName=" + finalLoc.DisplayName);
                sb.AppendLine("Latitude=" + finalLoc.Latitude.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("Longitude=" + finalLoc.Longitude.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("ShowCalendarWidget=" + (enableCalendar ? "1" : "0"));
                sb.AppendLine("ShowTimerWidget=" + (enableTimer ? "1" : "0"));
                MainWindow.WriteAllTextAtomic(configFilePath, sb.ToString(), new UTF8Encoding(false));

                // Create shortcuts if requested
                try
                {
                    string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (key != null)
                        {
                            if (enableStartup) key.SetValue("OpenWidgetPublic", "\"" + exePath + "\"");
                            else key.DeleteValue("OpenWidgetPublic", false);
                        }
                    }
                    if (enableStartup)
                    {
                        using (RegistryKey serKey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Serialize"))
                        {
                            if (serKey != null)
                            {
                                serKey.SetValue("StartupDelayInMSec", 0, RegistryValueKind.DWord);
                                serKey.SetValue("WaitForIdleState", 0, RegistryValueKind.DWord);
                            }
                        }
                    }

                    if (createDesktop)
                    {
                        string deskDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        CreateShortcut(System.IO.Path.Combine(deskDir, "OpenWidget.lnk"), exePath);
                    }
                }
                catch { }

                Dispatcher.Invoke(() =>
                {
                    this.DialogResult = true;
                    this.Close();
                });
            });
        }

        private static void CreateShortcut(string shortcutPath, string targetExe)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(t);
                object shortcut = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                Type st = shortcut.GetType();
                st.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { targetExe });
                st.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { System.IO.Path.GetDirectoryName(targetExe) });
                st.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { "OpenWidget Desktop Widget" });
                st.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
            }
            catch { }
        }
    }

    // ========================================================
    // MONITOR DDC/CI & WINDOWS SDR WHITE LEVEL NATIVE ENGINE
    // ========================================================
    internal static class MonitorDdcNative
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left, top, right, bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szPhysicalMonitorDescription;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_SOURCE_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_TARGET_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint outputTechnology;
            public uint rotation;
            public uint scaling;
            public uint refreshRateNumerator;
            public uint refreshRateDenominator;
            public uint scanLineOrdering;
            public int targetAvailable;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_INFO
        {
            public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
            public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_MODE_INFO
        {
            public uint infoType;
            public uint id;
            public LUID adapterId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
            public byte[] data;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_DEVICE_INFO_HEADER
        {
            public int type;
            public uint size;
            public LUID adapterId;
            public uint id;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string viewGdiDeviceName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint flags;
            public uint outputTechnology;
            public ushort edidManufactureId;
            public ushort edidProductCodeId;
            public uint connectorInstance;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string monitorFriendlyDeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string monitorDevicePath;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_SDR_WHITE_LEVEL
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint SDRWhiteLevel;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_SET_SDR_WHITE_LEVEL
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint SDRWhiteLevel;
            public byte finalValue;
            public byte pad1;
            public byte pad2;
            public byte pad3;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint value;
            public int colorEncoding;
            public uint bitsPerColorChannel;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint value;
            public int colorEncoding;
            public uint bitsPerColorChannel;
            public int activeColorMode; // 0 = SDR, 1 = WCG, 2 = HDR
        }

        public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("user32.dll")]
        public static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        public static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathArray, ref uint numModeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);

        [DllImport("user32.dll")]
        public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME requestPacket);

        [DllImport("user32.dll")]
        public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SDR_WHITE_LEVEL requestPacket);

        [DllImport("user32.dll")]
        public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO requestPacket);

        [DllImport("user32.dll")]
        public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2 requestPacket);

        [DllImport("user32.dll")]
        public static extern int DisplayConfigSetDeviceInfo(ref DISPLAYCONFIG_SET_SDR_WHITE_LEVEL setPacket);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint pdwNumberOfPhysicalMonitors);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetMonitorBrightness(IntPtr hMonitor, out uint pdwMinimumBrightness, out uint pdwCurrentBrightness, out uint pdwMaximumBrightness);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool SetMonitorBrightness(IntPtr hMonitor, uint dwNewBrightness);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr hMonitor, byte bVCPCode, out uint pvct, out uint pdwCurrentValue, out uint pdwMaximumValue);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool SetVCPFeature(IntPtr hMonitor, byte bVCPCode, uint dwNewValue);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);

        public const byte VCP_BRIGHTNESS = 0x10;

        public class WinDisplayTargetInfo
        {
            public string GdiDeviceName;
            public string FriendlyName;
            public string MonitorDevicePath;
            public LUID AdapterId;
            public uint TargetId;
            public uint OutputTechnology;
            public bool IsInternalPanel;
            public bool HdrEnabled;
            public int SdrBrightnessPct;
        }

        public class WmiBrightnessInfo
        {
            public string InstanceName;
            public int CurrentBrightness;
        }

        public static string ExtractEdidIdFromDevicePath(string devPath)
        {
            if (string.IsNullOrWhiteSpace(devPath)) return "";
            string[] parts = devPath.Split('#');
            if (parts.Length >= 2) return parts[1].Trim();
            return "";
        }

        public static List<WmiBrightnessInfo> QueryWmiBrightnessDisplays()
        {
            List<WmiBrightnessInfo> list = new List<WmiBrightnessInfo>();
            try
            {
                ManagementScope scope = new ManagementScope(@"\\.\root\wmi");
                SelectQuery query = new SelectQuery("SELECT InstanceName, CurrentBrightness FROM WmiMonitorBrightness");
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementBaseObject obj in results)
                    {
                        using (obj)
                        {
                            string inst = obj["InstanceName"] as string ?? "";
                            int cur = 60;
                            if (obj["CurrentBrightness"] != null)
                            {
                                cur = Math.Max(0, Math.Min(100, Convert.ToInt32(obj["CurrentBrightness"])));
                            }
                            list.Add(new WmiBrightnessInfo { InstanceName = inst, CurrentBrightness = cur });
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        public static bool SetWmiBrightness(string instanceName, int targetPct)
        {
            int clamped = Math.Max(0, Math.Min(100, targetPct));
            try
            {
                ManagementScope scope = new ManagementScope(@"\\.\root\wmi");
                SelectQuery query = new SelectQuery("WmiMonitorBrightnessMethods");
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    ManagementObject fallbackObj = null;
                    try
                    {
                        foreach (ManagementObject mObj in results)
                        {
                            string inst = mObj["InstanceName"] as string ?? "";
                            if (string.IsNullOrEmpty(instanceName) || string.Equals(inst, instanceName, StringComparison.OrdinalIgnoreCase))
                            {
                                mObj.InvokeMethod("WmiSetBrightness", new object[] { (uint)0, (byte)clamped });
                                mObj.Dispose();
                                return true;
                            }
                            if (fallbackObj == null) fallbackObj = mObj;
                            else mObj.Dispose();
                        }
                        if (fallbackObj != null)
                        {
                            fallbackObj.InvokeMethod("WmiSetBrightness", new object[] { (uint)0, (byte)clamped });
                            return true;
                        }
                    }
                    finally
                    {
                        if (fallbackObj != null) fallbackObj.Dispose();
                    }
                }
            }
            catch { }
            return false;
        }

        public static List<WinDisplayTargetInfo> QueryActiveDisplayTargets()
        {
            List<WinDisplayTargetInfo> list = new List<WinDisplayTargetInfo>();
            try
            {
                uint np, nm;
                if (GetDisplayConfigBufferSizes(2, out np, out nm) != 0) return list;
                DISPLAYCONFIG_PATH_INFO[] paths = new DISPLAYCONFIG_PATH_INFO[np];
                DISPLAYCONFIG_MODE_INFO[] modes = new DISPLAYCONFIG_MODE_INFO[nm];
                if (QueryDisplayConfig(2, ref np, paths, ref nm, modes, IntPtr.Zero) != 0) return list;

                for (int i = 0; i < np; i++)
                {
                    var p = paths[i];
                    var srcName = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                    srcName.header.type = 1;
                    srcName.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME));
                    srcName.header.adapterId = p.sourceInfo.adapterId;
                    srcName.header.id = p.sourceInfo.id;
                    DisplayConfigGetDeviceInfo(ref srcName);

                    var tName = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
                    tName.header.type = 2;
                    tName.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_TARGET_DEVICE_NAME));
                    tName.header.adapterId = p.targetInfo.adapterId;
                    tName.header.id = p.targetInfo.id;
                    DisplayConfigGetDeviceInfo(ref tName);

                    bool hdrActive = false;
                    var adv2 = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2();
                    adv2.header.type = 15; // DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO_2 (Win11 24H2+)
                    adv2.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2));
                    adv2.header.adapterId = p.targetInfo.adapterId;
                    adv2.header.id = p.targetInfo.id;
                    if (DisplayConfigGetDeviceInfo(ref adv2) == 0)
                    {
                        hdrActive = (adv2.activeColorMode == 2);
                    }
                    else
                    {
                        var adv = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO();
                        adv.header.type = 9;
                        adv.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO));
                        adv.header.adapterId = p.targetInfo.adapterId;
                        adv.header.id = p.targetInfo.id;
                        if (DisplayConfigGetDeviceInfo(ref adv) == 0)
                        {
                            // Bit 1 (0x2) = advancedColorEnabled, Bit 2 (0x4) = wideColorEnforced (ACM/WCG), Bit 3 (0x8) = forceDisabled
                            hdrActive = ((adv.value & 0x2) != 0) && ((adv.value & 0x4) == 0) && ((adv.value & 0x8) == 0);
                        }
                    }

                    int pct = 60;
                    if (hdrActive)
                    {
                        var sdr = new DISPLAYCONFIG_SDR_WHITE_LEVEL();
                        sdr.header.type = 11;
                        sdr.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_SDR_WHITE_LEVEL));
                        sdr.header.adapterId = p.targetInfo.adapterId;
                        sdr.header.id = p.targetInfo.id;
                        if (DisplayConfigGetDeviceInfo(ref sdr) == 0 && sdr.SDRWhiteLevel >= 1000)
                        {
                            pct = (int)Math.Round((sdr.SDRWhiteLevel - 1000) / 50.0);
                            pct = Math.Max(0, Math.Min(100, pct));
                        }
                    }

                    uint outTech = p.targetInfo.outputTechnology;
                    bool isInternal = (outTech & 0x80000000u) != 0 || outTech == 6u || outTech == 11u || outTech == 13u;

                    list.Add(new WinDisplayTargetInfo
                    {
                        GdiDeviceName = srcName.viewGdiDeviceName,
                        FriendlyName = tName.monitorFriendlyDeviceName,
                        MonitorDevicePath = tName.monitorDevicePath,
                        AdapterId = p.targetInfo.adapterId,
                        TargetId = p.targetInfo.id,
                        OutputTechnology = outTech,
                        IsInternalPanel = isInternal,
                        HdrEnabled = hdrActive,
                        SdrBrightnessPct = pct
                    });
                }
            }
            catch { }
            return list;
        }

        public static bool SetSdrWhiteLevelPercent(LUID adapterId, uint targetId, int pct)
        {
            try
            {
                int clamped = Math.Max(0, Math.Min(100, pct));
                uint rawWhiteLevel = (uint)(1000 + clamped * 50);

                var setPkt = new DISPLAYCONFIG_SET_SDR_WHITE_LEVEL();
                setPkt.header.type = -18;
                setPkt.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_SET_SDR_WHITE_LEVEL));
                setPkt.header.adapterId = adapterId;
                setPkt.header.id = targetId;
                setPkt.SDRWhiteLevel = rawWhiteLevel;
                setPkt.finalValue = 1;
                return DisplayConfigSetDeviceInfo(ref setPkt) == 0;
            }
            catch
            {
                return false;
            }
        }
    }

    internal class PhysicalMonitorSlot
    {
        public IntPtr Handle = IntPtr.Zero;
        public string GdiDeviceName = "";
        public int PhysicalIndex = 0;
        public string Name = "Monitor";
        public uint MinVal = 0;
        public uint MaxVal = 100;
        public int CurrentBrightness = 60;
        public int PendingBrightness = -1;
        public int WorkerBusy = 0;
        public bool UseWindowsSdrWhiteLevel = false;
        public bool UseWmiBrightness = false;
        public string WmiInstanceName = "";
        public bool PreferHighLevelDdc = false;
        public MonitorDdcNative.LUID AdapterId;
        public uint TargetId = 0;
    }

    // ========================================================
    // MAIN WIDGET WINDOW (PUBLIC EDITION - NO BULB BLOAT)
    // ========================================================
    public class MainWindow : Window
    {
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out MonitorDdcNative.RECT lpRect);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        private const int SW_HIDE = 0;
        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();
        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const uint STILL_ACTIVE = 259;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX() { this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); }
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        private const int GWL_EXSTYLE = -20;
        private const int GWLP_HWNDPARENT = -8;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int HOTKEY_ID_MIC = 9001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint VK_M = 0x4D;
        private const uint WM_NCLBUTTONDOWN = 0x00A1;
        private const int HT_CAPTION = 0x02;
        private const int WM_CLIPBOARDUPDATE = 0x031D;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const int WS_EX_TOPMOST = 0x00000008;
        private const uint GW_HWNDPREV = 3;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int SW_SHOWNOACTIVATE = 4;

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
        }

        private bool userHidden = false;
        private bool isShowDesktopActive = false;
        private IntPtr cachedMainHwnd = IntPtr.Zero;
        private IntPtr cachedCalHwnd = IntPtr.Zero;
        private IntPtr cachedTimerHwnd = IntPtr.Zero;
        private IntPtr lastVerifiedFgHwnd = IntPtr.Zero;
        private readonly StringBuilder fgClassBuilder = new StringBuilder(64);
        private DispatcherTimer saveSettingsDebounceTimer;

        // Theme, Language, Location & Window State
        private string settingsFilePath;
        private string appLanguage = "TR";
        private string accentColorHex = "#E2925C";
        private int bgOpacityPercent = 88;
        private string locationMode = "Auto";
        private string locationName = "İstanbul";
        private double locationLat = 41.0082;
        private double locationLon = 28.9784;

        private bool IsEn
        {
            get { return string.Equals(appLanguage, "EN", StringComparison.OrdinalIgnoreCase); }
        }

        private string L(string tr, string en)
        {
            return IsEn ? en : tr;
        }

        private bool isDraggable = false;
        private double savedFreeWidth = 370;
        private double savedFreeHeight = 620;
        private double savedLeft = double.NaN;
        private double savedTop = double.NaN;
        private bool hasSavedDimensions = false;
        private bool isWindowInitialized = false;
        private bool isCalWindowInitialized = false;
        private bool isTimerWindowInitialized = false;

        // Audio / Tray State (Cached Single COM Endpoint)
        private bool isMicMuted = false;
        private IAudioEndpointVolume cachedMicVolumeConsole;
        private IAudioEndpointVolume cachedMicVolumeComm;
        private System.Windows.Forms.NotifyIcon trayIcon;
        private System.Drawing.Icon iconMicUnmuted;
        private System.Drawing.Icon iconMicMuted;
        private System.Windows.Forms.ToolStripMenuItem trayMuteItem;
        private System.Windows.Forms.ToolStripMenuItem traySettingsItem;
        private System.Windows.Forms.ToolStripMenuItem trayLockItem;
        private System.Windows.Forms.ToolStripMenuItem trayFitHeightItem;
        private System.Windows.Forms.ToolStripMenuItem trayDockRightItem;
        private System.Windows.Forms.ToolStripMenuItem trayDockLeftItem;
        private System.Windows.Forms.ToolStripMenuItem trayToggleVisItem;
        private System.Windows.Forms.ToolStripMenuItem trayAutoStartItem;
        private System.Windows.Forms.ToolStripMenuItem trayExitItem;
        private Window settingsWindow;

        // Telemetry Counters & Direct NVML Handle (Dynamic Hardware Detection for Any User's PC)
        private PerformanceCounter cpuCounter;
        private bool nvmlInitialized = false;
        private IntPtr nvmlDeviceHandle = IntPtr.Zero;
        private Ping sharedPing;
        private MEMORYSTATUSEX sharedMemStatus = new MEMORYSTATUSEX();
        private List<DriveInfo> fixedDrives = new List<DriveInfo>();

        private List<double> cpuHistory = new List<double>(20);
        private List<double> gpuHistory = new List<double>(20);
        private long lastBytesRecv = 0;
        private long lastBytesSent = 0;
        private DateTime lastNetTime = DateTime.Now;
        private double lastPingMs = 10;
        private double cpuTemp = 46;
        private string cpuShortName = "CPU";
        private string gpuModelName = "GPU";
        private double gpuTemp = 45;
        private double gpuLoad = 0;
        private double vramUsedGb = 1.5;
        private double vramTotalGb = 8.0;

        // Clipboard History
        private List<ClipboardEntry> clipboardHistory = new List<ClipboardEntry>();
        private string lastClipboardText = "";

        // Dynamic Theme UI References
        private Border outerBorder;
        private List<Border> glassCards = new List<Border>();
        private List<Border> subBoxes = new List<Border>();
        private List<TextBlock> accentTextBlocks = new List<TextBlock>();
        private List<Polyline> accentSparklines = new List<Polyline>();
        private List<Border> accentBarFills = new List<Border>();
        private Border weatherIconBox;
        private Border resizeGripIndicator;
        private WindowChrome windowChrome;

        // Specific UI Elements
        private TextBlock txtClockAndUptime;
        private Button btnLockDrag;
        private Button btnSettingsHeader;
        private Button btnMicHeader;
        private TextBlock txtMicHeaderIcon;

        // Weather UI & Unit State
        private bool useFahrenheit = false;
        private double lastWeatherTempC = 14;
        private double lastWeatherFeelsC = 13;
        private double[] lastWeatherMinC = new double[] { 8, 6, 8 };
        private double[] lastWeatherMaxC = new double[] { 16, 14, 16 };
        private TextBlock txtWeatherSectionTitle;
        private TextBlock txtLocationBadge;
        private TextBlock txtWeatherTemp;
        private TextBlock txtWeatherDesc;
        private TextBlock txtWeatherFeels;
        private TextBlock txtWeatherMainIcon;
        private TextBlock txtHumidityLabel;
        private TextBlock txtWindLabel;
        private TextBlock txtRainLabel;
        private TextBlock txtWeatherHumidity;
        private TextBlock txtWeatherWind;
        private TextBlock txtWeatherRain;
        private TextBlock[] txtDayNames = new TextBlock[3];
        private TextBlock[] txtDayIcons = new TextBlock[3];
        private TextBlock[] txtDayTemps = new TextBlock[3];

        // Wireless Peripherals Battery UI & State (Auto-Detects Mouse, Keyboard, Headset, Controller)
        private class WirelessRowUi
        {
            public Grid Row;
            public TextBlock Label;
            public TextBlock Detail;
            public Border Fill;
            public TextBlock Pct;
            public bool IsCharging;
            public int BarPct;
        }
        private Grid wirelessHeaderGrid;
        private TextBlock txtWirelessSectionTitle;
        private Button btnRefreshWireless;
        private StackPanel panelWirelessContent;
        private Dictionary<string, WirelessRowUi> wirelessRowCache = new Dictionary<string, WirelessRowUi>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, int> wirelessLastPcts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> wirelessLastHz = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private int wirelessPollingBusy = 0;
        private int lastWirelessCount = 0;
        private bool notifyLowBattery = true;
        private readonly HashSet<string> lowBatteryNotifiedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Monitor Brightness UI & State (Auto-Detects Connected Displays, HDR SDRWhiteLevel + DDC/CI)
        private readonly List<PhysicalMonitorSlot> monitorSlots = new List<PhysicalMonitorSlot>();
        private readonly object monitorHandleLock = new object();
        private readonly object monitorRefreshLock = new object();
        private CancellationTokenSource monitorRefreshCts;
        private EventHandler displaySettingsChangedHandler;
        private PowerModeChangedEventHandler powerModeChangedHandler;
        private bool monitorLinkSync = false;
        private bool isUpdatingMonitorUi = false;
        private Grid monHeaderGrid;
        private TextBlock txtMonitorSectionTitle;
        private Button btnMonitorLink;
        private Button btnRefreshMonitors;
        private Border monitorCard;
        private StackPanel monRowsHostStack;
        private List<TextBlock> txtMonitorNames = new List<TextBlock>();
        private List<TextBlock> txtMonitorPercents = new List<TextBlock>();
        private List<Slider> sliderMonitors = new List<Slider>();
        private List<Border> monitorSliderContainers = new List<Border>();

        // Layout Order & Visibility State
        private List<string> sectionOrder = new List<string> { "weather", "monitors", "wireless", "telemetry", "clipboard" };
        private bool showSecWeather = true;
        private bool showSecMonitors = true;
        private bool showSecWireless = true;
        private bool showSecTelemetry = true;
        private bool showSecClipboard = true;
        private bool showTeleCpu = true;
        private bool showTeleRam = true;
        private bool showTeleGpu = true;
        private bool showTeleVram = true;
        private bool showTeleNet = true;
        private bool showTeleDisks = true;
        private int currentSettingsTab = 0;

        // Section Root Containers for Dynamic Reordering
        private StackPanel modulesHostPanel;
        private StackPanel secContainerWeather;
        private StackPanel secContainerMonitors;
        private StackPanel secContainerWireless;
        private StackPanel secContainerTelemetry;
        private StackPanel secContainerClipboard;

        // Telemetry UI
        private TextBlock txtTelemetrySectionTitle;
        private StackPanel panelTelemetryContent;
        private Grid rowTeleCpu;
        private Grid rowTeleRam;
        private Grid rowTeleGpu;
        private Grid rowTeleVram;
        private Grid rowTeleNet;
        private List<Grid> rowsTeleDisks = new List<Grid>();
        private TextBlock txtCpuRowLabel;
        private TextBlock txtCpuDetail;
        private TextBlock txtCpuPct;
        private Polyline sparkCpu;
        private TextBlock txtRamRowLabel;
        private TextBlock txtRamDetail;
        private TextBlock txtRamPct;
        private Border barRamFill;
        private TextBlock txtGpuRowLabel;
        private TextBlock txtGpuDetail;
        private TextBlock txtGpuPct;
        private Polyline sparkGpu;
        private TextBlock txtVramRowLabel;
        private TextBlock txtVramDetail;
        private TextBlock txtVramPct;
        private Border barVramFill;
        private TextBlock txtNetRowLabel;
        private TextBlock txtNetSpeed;
        private TextBlock txtNetBadge;
        private TextBlock txtNetPing;
        private List<TextBlock> diskRowLabels = new List<TextBlock>();
        private List<TextBlock> diskDetailBlocks = new List<TextBlock>();
        private List<Border> diskFillBars = new List<Border>();
        private List<TextBlock> diskPctBlocks = new List<TextBlock>();

        // Clipboard UI
        private TextBlock txtClipboardSectionTitle;
        private Button btnClearClip;
        private TextBlock txtClipCount;
        private StackPanel panelClipItems;
        private Popup toastPopup;
        private Border toastBorder;
        private TextBlock txtToastMessage;
        private DispatcherTimer toastTimer;

        // Standalone Google Calendar Desktop Widget (Default Off for Public)
        private bool showCalendarWidget = false;
        private double savedCalWidth = 468;
        private double savedCalHeight = 258;
        private double savedCalLeft = double.NaN;
        private double savedCalTop = double.NaN;
        private string calendarSecretIcalUrl = "";
        private Window calendarWindow;
        private Border calOuterBorder;
        private WindowChrome calWindowChrome;
        private Border calResizeGrip;
        private Button btnCalLockDrag;
        private TextBlock txtCalDayOfWeekHeader;
        private TextBlock txtCalBigDayNum;
        private TextBlock txtCalMonthYearLabel;
        private StackPanel calMiniMonthGrid;
        private TextBlock txtCalRightScheduleTitle;
        private ScrollViewer calEventsScroll;
        private StackPanel calEventsListPanel;
        private Border calQuickAddPanel;
        private TextBox txtCalQuickTitle;
        private TextBox txtCalQuickTime;
        private TextBox txtCalQuickLoc;
        private DateTime calDisplayedMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        private DateTime calSelectedDate = DateTime.Today;
        private readonly List<CalendarEventItem> allCalendarEvents = new List<CalendarEventItem>();
        private Dictionary<DateTime, byte> calEventDotsByDate = new Dictionary<DateTime, byte>();
        private int calEventsVersion = 0;
        private string lastLeftColumnSignature = "";
        private string lastRightScheduleSignature = "";
        private DateTime lastCalDatWriteUtc = DateTime.MinValue;
        private DateTime lastCustomDatWriteUtc = DateTime.MinValue;
        private readonly object calEventsLock = new object();
        private System.Windows.Forms.ToolStripMenuItem trayCalendarItem;

        // Standalone Timer Widget (Modes, Auto Game Detection, Pomodoro & Yearly Heatmap - Default Off for Public)
        private bool showTimerWidget = false;
        private double savedTimerWidth = 540;
        private double savedTimerHeight = 310;
        private double savedTimerLeft = double.NaN;
        private double savedTimerTop = double.NaN;
        private int timerCurrentMode = 0;
        private bool defaultTimerIsRunning = false;
        private int defaultTimerSeconds = 0;
        private bool gameTimerIsRunning = false;
        private int pomoFocusMinutes = 25;
        private int pomoBreakMinutes = 5;
        private bool pomoIsRunning = false;
        private bool pomoIsBreakPhase = false;
        private int pomoRemainingSeconds = 25 * 60;
        private int timerGameSessionSeconds = 0;
        private int timerSaveTickCounter = 0;
        private DateTime activeTrackingDate = DateTime.Today;
        private string activityDataFilePath;
        private string lastSavedActivityPayload = null;
        private static readonly object atomicFileIoLock = new object();
        private Window timerWindow;
        private Border timerOuterBorder;
        private WindowChrome timerWindowChrome;
        private Border timerResizeGrip;
        private Button btnTimerLockDrag;
        private Button[] btnTimerModes = new Button[3];
        private Border timerPomoSettingsPopup;
        private TextBlock txtTimerDialPhase;
        private TextBlock txtTimerDialCenter;
        private TextBlock txtTimerDialSub;
        private TextBlock txtPomoFocusSettingVal;
        private TextBlock txtPomoBreakSettingVal;
        private Border timerDialInnerCircle;
        private Line[] timerDialTicks = new Line[24];
        private Button btnPomoPlayPause;
        private TextBlock txtPomoPlayPauseIcon;
        private Button btnPomoReset;
        private TextBlock txtTodayStudyVal;
        private TextBlock txtTodayGameVal;
        private TextBlock txtTodayPcVal;
        private Grid timerHeatmapHost;
        private Canvas timerHeatmapCanvas;
        private int lastHeatmapLayoutWidth = 0;
        private int lastHeatmapLayoutHeight = 0;
        private readonly List<Border> heatmapCellPool = new List<Border>(400);
        private readonly List<TextBlock> heatmapMonthPool = new List<TextBlock>(16);
        private string lastPlayBtnBrushKey = "";
        private Brush cachedPlayBtnBg = null;
        private Brush cachedPlayBtnBd = null;
        private int lastRenderedLitTicks = -1;
        private string lastRenderedTickHex = "";
        private Border todayHeatmapCell;
        private Brush[] cachedHeatmapTierBrushes = new Brush[6];
        private string cachedHeatmapAccentHex = "";
        private DispatcherTimer timerWidgetTickTimer;
        private System.Windows.Forms.ToolStripMenuItem trayTimerItem;
        private readonly Dictionary<DateTime, DailyActivityRecord> activityByDate = new Dictionary<DateTime, DailyActivityRecord>();
        private uint activeGamePid = 0;
        private string activeGameName = "";
        private uint lastCheckedNonGamePid = 0;
        private bool lastCheckedWasJava = false;
        private int autoGameCheckTick = 0;
        private readonly StringBuilder procPathSb = new StringBuilder(512);
        private readonly StringBuilder windowTitleSb = new StringBuilder(256);

        // Taskbar Wireless Devices Battery Overlay (Default Off for Public)
        private bool showTaskbarDevices = false;
        private Window taskbarDevicesWindow;
        private IntPtr cachedTaskbarOverlayHwnd = IntPtr.Zero;
        private StackPanel taskbarDevicesPanel;
        private System.Windows.Forms.ToolStripMenuItem trayTaskbarDevicesItem;
        private string lastTaskbarDevicesSignature = "";
        private int lastTaskbarOverlayX = int.MinValue;
        private int lastTaskbarOverlayY = int.MinValue;
        private int lastTaskbarOverlayW = int.MinValue;
        private bool taskbarOverlayHiddenByFullscreen = false;
        private struct TaskbarDeviceItem { public string Kind; public string Name; public int Pct; public string Detail; public bool IsCharging; }
        private readonly List<TaskbarDeviceItem> latestTaskbarDevices = new List<TaskbarDeviceItem>();

        // Custom Timer Mode Colors & Settings / Detailed View Windows
        private string timerStudyColorHex = "#2EA043";
        private string timerGameColorHex = "#A78BFA";
        private Border timerStudyStatBar;
        private Border timerGameStatBar;
        private Border timerPcStatBar;
        private Window timerSettingsWindow;
        private Window timerDetailedWindow;
        private Action refreshTimerSettingsPreviewAction;
        private Action refreshTimerDetailedViewAction;

        private static readonly HashSet<string> ExcludedNonGameExes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "explorer", "searchhost", "searchapp", "shellexperiencehost", "startmenuexperiencehost",
            "textinputhost", "applicationframehost", "systemsettings", "taskmgr", "dwm", "csrss", "svchost",
            "lockapp", "snippingtool", "screenclippinghost", "mspaint", "notepad", "calc", "calculatorapp",
            "cmd", "powershell", "pwsh", "windowsterminal", "conhost", "regedit", "mmc", "control", "msiexec", "rundll32",
            "chrome", "msedge", "firefox", "opera", "opera_gx", "brave", "vivaldi",
            "discord", "discordptb", "discordcanary", "spotify", "vlc", "mpv", "potplayermini64",
            "telegram", "whatsapp", "teams", "ms-teams", "slack", "zoom",
            "code", "devenv", "rider64", "idea64", "pycharm64", "clion64", "sublime_text", "notepad++",
            "openwidget", "wallpaper32", "wallpaper64", "ui32", "obs64", "obs32", "sharex", "nvidia overlay", "geforceexperience",
            "steam", "steamwebhelper", "steamservice", "gameoverlayui", "epicgameslauncher", "epicwebhelper",
            "eosoverlayrenderer-win64-shipping", "riotclientservices", "riotclientux", "riotclientuxrender", "riotclientelectron",
            "battle.net", "agent", "eaapp", "eadesktop", "eabackgroundservice", "ealocalhostsvc", "origin",
            "ubisoftconnect", "upc", "ubisoftgamelauncher", "galaxyclient", "xboxpcapp", "gamingservices",
            "curseforge", "overwolf", "overwolfbrowser", "modrinth app", "prismlauncher", "multimc", "tlauncher",
            "vgc", "vgtray", "easyanticheat", "easyanticheat_eos"
        };

        private static readonly Dictionary<string, string> KnownGameFriendlyNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "cs2", "Counter-Strike 2" },
            { "csgo", "CS:GO" },
            { "valorant", "VALORANT" },
            { "valorant-win64-shipping", "VALORANT" },
            { "league of legends", "League of Legends" },
            { "r5apex", "Apex Legends" },
            { "r5apex_dx12", "Apex Legends" },
            { "eldenring", "ELDEN RING" },
            { "gta5", "GTA V" },
            { "gta5_enhanced", "GTA V" },
            { "playgtav", "GTA V" },
            { "rdr2", "Red Dead Redemption 2" },
            { "cyberpunk2077", "Cyberpunk 2077" },
            { "witcher3", "The Witcher 3" },
            { "fortniteclient-win64-shipping", "Fortnite" },
            { "overwatch", "Overwatch 2" },
            { "dota2", "Dota 2" },
            { "rocketleague", "Rocket League" },
            { "tslgame", "PUBG" },
            { "bg3", "Baldur's Gate 3" },
            { "bg3_dx11", "Baldur's Gate 3" },
            { "fc24", "EA FC 24" },
            { "fc25", "EA FC 25" },
            { "forzahorizon5", "Forza Horizon 5" },
            { "eurotrucks2", "Euro Truck Sim 2" },
            { "amtrucks", "American Truck Sim" },
            { "rustclient", "Rust" },
            { "deadbydaylight-win64-shipping", "Dead by Daylight" },
            { "genshinimpact", "Genshin Impact" },
            { "starrail", "Honkai: Star Rail" },
            { "palworld-win64-shipping", "Palworld" },
            { "helldivers2", "HELLDIVERS 2" },
            { "marvel-win64-shipping", "Marvel Rivals" },
            { "marvelrivals-win64-shipping", "Marvel Rivals" },
            { "b1-win64-shipping", "Black Myth: Wukong" },
            { "pathofexile", "Path of Exile" },
            { "pathofexile_x64", "Path of Exile" },
            { "pathofexile2", "Path of Exile 2" },
            { "pathofexile2steam", "Path of Exile 2" },
            { "escapefromtarkov", "Escape from Tarkov" },
            { "rainbowsix", "Rainbow Six Siege" },
            { "rainbowsix_vulkan", "Rainbow Six Siege" },
            { "rainbowsix_be", "Rainbow Six Siege" },
            { "robloxplayerbeta", "Roblox" },
            { "minecraft.windows", "Minecraft Bedrock" },
            { "stardew valley", "Stardew Valley" },
            { "hades", "Hades" },
            { "hades2", "Hades II" },
            { "hollow_knight", "Hollow Knight" },
            { "terraria", "Terraria" },
            { "factorio", "Factorio" },
            { "factorygame-win64-shipping", "Satisfactory" },
            { "factorygamesteam-win64-shipping", "Satisfactory" },
            { "valheim", "Valheim" },
            { "sekiro", "Sekiro" },
            { "darksoulsiii", "Dark Souls III" },
            { "l4d2", "Left 4 Dead 2" },
            { "left4dead2", "Left 4 Dead 2" },
            { "tf_win64", "Team Fortress 2" },
            { "brawlhalla", "Brawlhalla" },
            { "osu!", "osu!" },
            { "xerogame", "Xero" }
        };

        private class DailyActivityRecord
        {
            public DateTime Date;
            public int StudySeconds;
            public int GameSeconds;
            public int PcSeconds;
            public Dictionary<string, int> GameBreakdown = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        private class CalendarEventItem
        {
            public DateTime Start;
            public DateTime End;
            public bool AllDay;
            public string Source;
            public string ColorHex;
            public string Title;
            public string Location;
            public string Description;
            public string HtmlLink;
            public string RawCustomLine;
        }

        public MainWindow()
        {
            settingsFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.cfg");
            LoadSettings();
            DetectUserHardwareNames();

            for (int i = 0; i < 20; i++)
            {
                cpuHistory.Add(6);
                gpuHistory.Add(2);
            }

            try { sharedPing = new Ping(); } catch { }
            try
            {
                foreach (DriveInfo d in DriveInfo.GetDrives())
                {
                    if (d.DriveType == DriveType.Fixed && d.IsReady)
                    {
                        fixedDrives.Add(d);
                        if (fixedDrives.Count >= 3) break;
                    }
                }
            }
            catch { }

            InitializeWindowProps();
            BuildUserInterface();
            ApplyTheme(accentColorHex, bgOpacityPercent);
            InitializeTrayAndAudio();
            ApplyLanguage();
            InitializeTimers();
        }

        private void DetectUserHardwareNames()
        {
            // Detect CPU friendly model name from Registry (instant, zero WMI overhead)
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", false))
                {
                    if (key != null)
                    {
                        string rawCpu = (key.GetValue("ProcessorNameString") ?? "").ToString().Trim();
                        rawCpu = Regex.Replace(rawCpu, @"\b(AMD|Intel\(R\)|Core\(TM\)|Processor|\d+-Core|\d+(st|nd|rd|th)\s+Gen)\b", "", RegexOptions.IgnoreCase);
                        rawCpu = Regex.Replace(rawCpu, @"\s+with\s+Radeon.*$", "", RegexOptions.IgnoreCase);
                        rawCpu = Regex.Replace(rawCpu, @"\s+", " ").Trim();
                        if (!string.IsNullOrEmpty(rawCpu)) cpuShortName = rawCpu;
                    }
                }
            }
            catch { }
        }

        internal static bool IsValidTextFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                FileInfo fi = new FileInfo(path);
                if (fi.Length <= 0) return false;
                int checkLen = (int)Math.Min(fi.Length, 512);
                byte[] buf = new byte[checkLen];
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int read = fs.Read(buf, 0, checkLen);
                    if (read <= 0) return false;
                    bool hasNonWhitespace = false;
                    for (int i = 0; i < read; i++)
                    {
                        byte b = buf[i];
                        if (b == 0) return false; // NUL byte indicates NTFS crash truncation / corruption
                        if (b != (byte)' ' && b != (byte)'\t' && b != (byte)'\r' && b != (byte)'\n')
                            hasNonWhitespace = true;
                    }
                    return hasNonWhitespace;
                }
            }
            catch { return false; }
        }

        internal static void WriteAllTextAtomic(string path, string content, Encoding encoding)
        {
            if (string.IsNullOrEmpty(path)) return;
            lock (atomicFileIoLock)
            {
                string tmpPath = path + ".tmp";
                string bakPath = path + ".bak";
                byte[] data = (encoding ?? new UTF8Encoding(false)).GetBytes(content ?? "");

                // 1. Write to temp file with WriteThrough + Flush(true) so bytes reach physical disk before rename
                using (FileStream fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    if (data.Length > 0) fs.Write(data, 0, data.Length);
                    fs.Flush(true);
                }

                // 2. Atomically replace target while preserving previous healthy version as .bak
                bool targetExists = File.Exists(path);
                if (targetExists)
                {
                    bool targetHealthy = IsValidTextFile(path);
                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        try
                        {
                            if (targetHealthy)
                            {
                                // ReplaceFileW atomically rotates path -> bakPath and tmpPath -> path
                                File.Replace(tmpPath, path, bakPath, true);
                            }
                            else
                            {
                                // Do not overwrite a potentially healthy .bak with a corrupted target
                                File.Copy(tmpPath, path, true);
                                try { File.Delete(tmpPath); } catch { }
                            }
                            return;
                        }
                        catch (IOException)
                        {
                            if (attempt < 2) System.Threading.Thread.Sleep(25);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            if (attempt < 2) System.Threading.Thread.Sleep(25);
                        }
                    }

                    // Fallback if File.Replace is blocked by AV/lock
                    if (targetHealthy)
                    {
                        try { File.Copy(path, bakPath, true); } catch { }
                    }
                    File.Copy(tmpPath, path, true);
                    try { File.Delete(tmpPath); } catch { }
                }
                else
                {
                    File.Move(tmpPath, path);
                    try
                    {
                        if (!File.Exists(bakPath)) File.Copy(path, bakPath, false);
                    }
                    catch { }
                }
            }
        }

        internal static string[] ReadAllLinesCrashSafe(string path, Encoding encoding)
        {
            if (string.IsNullOrEmpty(path)) return null;
            Encoding enc = encoding ?? Encoding.UTF8;
            string[] candidates = new string[] { path, path + ".bak", path + ".tmp" };
            for (int i = 0; i < candidates.Length; i++)
            {
                string c = candidates[i];
                if (!IsValidTextFile(c)) continue;
                try
                {
                    string[] lines = File.ReadAllLines(c, enc);
                    if (lines != null && lines.Length > 0)
                    {
                        // If recovered from .bak or .tmp because primary was missing/corrupted, self-heal primary
                        if (i > 0)
                        {
                            try { WriteAllTextAtomic(path, string.Join(Environment.NewLine, lines) + Environment.NewLine, new UTF8Encoding(false)); } catch { }
                        }
                        return lines;
                    }
                }
                catch { }
            }
            return null;
        }

        private void LoadSettings()
        {
            try
            {
                string[] settingsLines = ReadAllLinesCrashSafe(settingsFilePath, Encoding.UTF8);
                if (settingsLines != null)
                {
                    foreach (string line in settingsLines)
                    {
                        int idx = line.IndexOf('=');
                        if (idx > 0)
                        {
                            string k = line.Substring(0, idx).Trim();
                            string v = line.Substring(idx + 1).Trim();
                            if (k == "Language" && (v.Equals("EN", StringComparison.OrdinalIgnoreCase) || v.Equals("TR", StringComparison.OrdinalIgnoreCase)))
                                appLanguage = v.ToUpperInvariant();
                            else if (k == "TempUnit")
                                useFahrenheit = (v.Equals("F", StringComparison.OrdinalIgnoreCase) || v.Equals("Fahrenheit", StringComparison.OrdinalIgnoreCase));
                            else if (k == "Accent" && v.StartsWith("#") && (v.Length == 7 || v.Length == 9))
                                accentColorHex = v;
                            else if (k == "Opacity")
                            {
                                int op;
                                if (int.TryParse(v, out op)) bgOpacityPercent = Math.Max(30, Math.Min(100, op));
                            }
                            else if (k == "LocationMode")
                                locationMode = v;
                            else if (k == "LocationName" && !string.IsNullOrEmpty(v))
                                locationName = v;
                            else if (k == "Latitude")
                                double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out locationLat);
                            else if (k == "Longitude")
                                double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out locationLon);
                            else if (k == "FreeWidth")
                            {
                                double fw;
                                if (double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out fw))
                                {
                                    savedFreeWidth = Math.Max(330, Math.Min(700, fw));
                                    hasSavedDimensions = true;
                                }
                            }
                            else if (k == "FreeHeight")
                            {
                                double fh;
                                if (double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out fh))
                                {
                                    savedFreeHeight = Math.Max(380, Math.Min(1400, fh));
                                    hasSavedDimensions = true;
                                }
                            }
                            else if (k == "Left")
                            {
                                double l;
                                if (double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out l))
                                    savedLeft = l;
                            }
                            else if (k == "Top")
                            {
                                double t;
                                if (double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out t))
                                    savedTop = t;
                            }
                            else if (k == "MonitorLink")
                                monitorLinkSync = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                            else if (k == "ShowWeather")
                                showSecWeather = (v != "0" && !v.Equals("false", StringComparison.OrdinalIgnoreCase));
                            else if (k == "ShowMonitors")
                                showSecMonitors = (v != "0" && !v.Equals("false", StringComparison.OrdinalIgnoreCase));
                            else if (k == "ShowWireless")
                                showSecWireless = (v != "0" && !v.Equals("false", StringComparison.OrdinalIgnoreCase));
                            else if (k == "ShowTelemetry")
                                showSecTelemetry = (v != "0" && !v.Equals("false", StringComparison.OrdinalIgnoreCase));
                            else if (k == "ShowClipboard")
                                showSecClipboard = (v != "0" && !v.Equals("false", StringComparison.OrdinalIgnoreCase));
                            else if (k == "ShowTeleCpu")
                                showTeleCpu = (v != "0" && !v.Equals("false", StringComparison.OrdinalIgnoreCase));
                            else if (k == "ShowTeleRam")
                                showTeleRam = (v != "0" && !v.Equals("false", StringComparison.OrdinalIgnoreCase));
                            else if (k == "ShowTeleGpu")
                                showTeleGpu = (v != "0" && !v.Equals("false", StringComparison.OrdinalIgnoreCase));
                            else if (k == "ShowTeleVram")
                                showTeleVram = (v != "0" && !v.Equals("false", StringComparison.OrdinalIgnoreCase));
                            else if (k == "ShowTeleNet")
                                showTeleNet = (v != "0" && !v.Equals("false", StringComparison.OrdinalIgnoreCase));
                            else if (k == "ShowTeleDisks")
                                showTeleDisks = (v != "0" && !v.Equals("false", StringComparison.OrdinalIgnoreCase));
                            else if (k == "SectionOrder" && !string.IsNullOrEmpty(v))
                            {
                                string[] parts = v.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                List<string> parsed = new List<string>();
                                string[] validKeys = { "weather", "monitors", "wireless", "telemetry", "clipboard" };
                                foreach (string p in parts)
                                {
                                    string clean = p.Trim().ToLowerInvariant();
                                    if (Array.IndexOf(validKeys, clean) >= 0 && !parsed.Contains(clean))
                                        parsed.Add(clean);
                                }
                                foreach (string vk in validKeys)
                                {
                                    if (!parsed.Contains(vk)) parsed.Add(vk);
                                }
                                sectionOrder = parsed;
                            }
                            else if (k == "ShowCalendarWidget") showCalendarWidget = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                            else if (k == "CalWidth")
                            {
                                double cw;
                                if (double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out cw))
                                    savedCalWidth = Math.Max(390, Math.Min(900, cw));
                            }
                            else if (k == "CalHeight")
                            {
                                double ch;
                                if (double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out ch))
                                    savedCalHeight = Math.Max(215, Math.Min(900, ch));
                            }
                            else if (k == "CalLeft")
                            {
                                double cl;
                                if (double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out cl))
                                    savedCalLeft = cl;
                            }
                            else if (k == "CalTop")
                            {
                                double ct;
                                if (double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out ct))
                                    savedCalTop = ct;
                            }
                            else if (k == "CalendarIcalUrl")
                            {
                                calendarSecretIcalUrl = v.Trim();
                            }
                            else if (k == "ShowTimerWidget") showTimerWidget = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                            else if (k == "TimerWidth")
                            {
                                double tw;
                                if (double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out tw))
                                    savedTimerWidth = Math.Max(460, Math.Min(950, tw));
                            }
                            else if (k == "TimerHeight")
                            {
                                double th;
                                if (double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out th))
                                    savedTimerHeight = Math.Max(275, Math.Min(650, th));
                            }
                            else if (k == "TimerLeft")
                            {
                                double tl;
                                if (double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out tl))
                                    savedTimerLeft = tl;
                            }
                            else if (k == "TimerTop")
                            {
                                double tt;
                                if (double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out tt))
                                    savedTimerTop = tt;
                            }
                            else if (k == "PomoFocusMin")
                            {
                                int pf;
                                if (int.TryParse(v, out pf))
                                {
                                    pomoFocusMinutes = Math.Max(1, Math.Min(180, pf));
                                    pomoRemainingSeconds = pomoFocusMinutes * 60;
                                }
                            }
                            else if (k == "PomoBreakMin")
                            {
                                int pb;
                                if (int.TryParse(v, out pb))
                                    pomoBreakMinutes = Math.Max(1, Math.Min(60, pb));
                            }
                            else if (k == "ShowTaskbarDevices")
                            {
                                showTaskbarDevices = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                            }
                            else if (k == "NotifyLowBattery")
                            {
                                notifyLowBattery = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                            }
                            else if (k == "TimerStudyColor" && v.StartsWith("#") && (v.Length == 7 || v.Length == 9))
                            {
                                timerStudyColorHex = v;
                            }
                            else if (k == "TimerGameColor" && v.StartsWith("#") && (v.Length == 7 || v.Length == 9))
                            {
                                timerGameColorHex = v;
                            }
                        }
                    }
                }
            }
            catch { }
            if (IsAutoStartEnabled()) EnsureAutoStart(true);
        }

        private void SaveSettings()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Language=" + appLanguage);
                sb.AppendLine("TempUnit=" + (useFahrenheit ? "F" : "C"));
                sb.AppendLine("Accent=" + accentColorHex);
                sb.AppendLine("Opacity=" + bgOpacityPercent);
                sb.AppendLine("LocationMode=" + locationMode);
                sb.AppendLine("LocationName=" + locationName);
                sb.AppendLine("Latitude=" + locationLat.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("Longitude=" + locationLon.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("FreeWidth=" + savedFreeWidth.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("FreeHeight=" + savedFreeHeight.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                if (!double.IsNaN(savedLeft))
                    sb.AppendLine("Left=" + savedLeft.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                if (!double.IsNaN(savedTop))
                    sb.AppendLine("Top=" + savedTop.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("MonitorLink=" + (monitorLinkSync ? "1" : "0"));
                sb.AppendLine("SectionOrder=" + string.Join(",", sectionOrder.ToArray()));
                sb.AppendLine("ShowWeather=" + (showSecWeather ? "1" : "0"));
                sb.AppendLine("ShowMonitors=" + (showSecMonitors ? "1" : "0"));
                sb.AppendLine("ShowWireless=" + (showSecWireless ? "1" : "0"));
                sb.AppendLine("ShowTelemetry=" + (showSecTelemetry ? "1" : "0"));
                sb.AppendLine("ShowClipboard=" + (showSecClipboard ? "1" : "0"));
                sb.AppendLine("ShowTeleCpu=" + (showTeleCpu ? "1" : "0"));
                sb.AppendLine("ShowTeleRam=" + (showTeleRam ? "1" : "0"));
                sb.AppendLine("ShowTeleGpu=" + (showTeleGpu ? "1" : "0"));
                sb.AppendLine("ShowTeleVram=" + (showTeleVram ? "1" : "0"));
                sb.AppendLine("ShowTeleNet=" + (showTeleNet ? "1" : "0"));
                sb.AppendLine("ShowTeleDisks=" + (showTeleDisks ? "1" : "0"));
                sb.AppendLine("ShowCalendarWidget=" + (showCalendarWidget ? "1" : "0"));
                sb.AppendLine("CalWidth=" + savedCalWidth.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("CalHeight=" + savedCalHeight.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                if (!double.IsNaN(savedCalLeft))
                    sb.AppendLine("CalLeft=" + savedCalLeft.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                if (!double.IsNaN(savedCalTop))
                    sb.AppendLine("CalTop=" + savedCalTop.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(calendarSecretIcalUrl))
                    sb.AppendLine("CalendarIcalUrl=" + calendarSecretIcalUrl);
                sb.AppendLine("ShowTimerWidget=" + (showTimerWidget ? "1" : "0"));
                sb.AppendLine("TimerWidth=" + savedTimerWidth.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("TimerHeight=" + savedTimerHeight.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                if (!double.IsNaN(savedTimerLeft))
                    sb.AppendLine("TimerLeft=" + savedTimerLeft.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                if (!double.IsNaN(savedTimerTop))
                    sb.AppendLine("TimerTop=" + savedTimerTop.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("PomoFocusMin=" + pomoFocusMinutes);
                sb.AppendLine("PomoBreakMin=" + pomoBreakMinutes);
                sb.AppendLine("ShowTaskbarDevices=" + (showTaskbarDevices ? "1" : "0"));
                sb.AppendLine("NotifyLowBattery=" + (notifyLowBattery ? "1" : "0"));
                sb.AppendLine("TimerStudyColor=" + timerStudyColorHex);
                sb.AppendLine("TimerGameColor=" + timerGameColorHex);
                WriteAllTextAtomic(settingsFilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }

        private void SaveSettingsDebounced()
        {
            if (!isWindowInitialized) return;
            if (saveSettingsDebounceTimer == null)
            {
                saveSettingsDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
                saveSettingsDebounceTimer.Tick += (s, e) =>
                {
                    saveSettingsDebounceTimer.Stop();
                    SaveSettings();
                    TrimProcessMemory();
                };
            }
            saveSettingsDebounceTimer.Stop();
            saveSettingsDebounceTimer.Start();
        }

        private bool IsAutoStartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    if (key != null) return key.GetValue("OpenWidgetPublic") != null;
                }
            }
            catch { }
            return false;
        }

        private void EnsureAutoStart(bool enable)
        {
            try
            {
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        if (enable) key.SetValue("OpenWidgetPublic", "\"" + exePath + "\"");
                        else key.DeleteValue("OpenWidgetPublic", false);
                    }
                }
                if (enable)
                {
                    using (RegistryKey serKey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Serialize"))
                    {
                        if (serKey != null)
                        {
                            serKey.SetValue("StartupDelayInMSec", 0, RegistryValueKind.DWord);
                            serKey.SetValue("WaitForIdleState", 0, RegistryValueKind.DWord);
                        }
                    }
                }
            }
            catch { }
        }

        private bool IsPositionWithinVirtualDesktop(double left, double top, double width, double height)
        {
            if (double.IsNaN(left) || double.IsNaN(top) || double.IsInfinity(left) || double.IsInfinity(top))
                return false;
            if (left <= -10000 || top <= -10000 || left >= 20000 || top >= 20000)
                return false;

            double vLeft = SystemParameters.VirtualScreenLeft;
            double vTop = SystemParameters.VirtualScreenTop;
            double vRight = vLeft + SystemParameters.VirtualScreenWidth;
            double vBottom = vTop + SystemParameters.VirtualScreenHeight;

            try
            {
                foreach (var scr in System.Windows.Forms.Screen.AllScreens)
                {
                    if (scr.Bounds.Left < vLeft) vLeft = scr.Bounds.Left;
                    if (scr.Bounds.Top < vTop) vTop = scr.Bounds.Top;
                    if (scr.Bounds.Right > vRight) vRight = scr.Bounds.Right;
                    if (scr.Bounds.Bottom > vBottom) vBottom = scr.Bounds.Bottom;
                }
            }
            catch { }

            double w = Math.Max(200, width);
            double h = Math.Max(150, height);
            bool horizOk = (left + w >= vLeft + 60) && (left <= vRight - 60);
            bool vertOk = (top + h >= vTop + 40) && (top <= vBottom - 60);
            return horizOk && vertOk;
        }

        private Rect GetWindowMonitorWorkArea(Window win)
        {
            try
            {
                if (win != null)
                {
                    IntPtr hwnd = new WindowInteropHelper(win).Handle;
                    System.Windows.Forms.Screen scr = hwnd != IntPtr.Zero
                        ? System.Windows.Forms.Screen.FromHandle(hwnd)
                        : System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(
                            (int)Math.Round(double.IsNaN(win.Left) ? 0 : win.Left + (win.Width / 2)),
                            (int)Math.Round(double.IsNaN(win.Top) ? 0 : win.Top + (win.Height / 2))));
                    if (scr != null)
                    {
                        double scaleX = 1.0, scaleY = 1.0;
                        PresentationSource src = PresentationSource.FromVisual(win);
                        if (src != null && src.CompositionTarget != null)
                        {
                            scaleX = src.CompositionTarget.TransformToDevice.M11;
                            scaleY = src.CompositionTarget.TransformToDevice.M22;
                        }
                        if (scaleX <= 0.1) scaleX = 1.0;
                        if (scaleY <= 0.1) scaleY = 1.0;
                        var wa = scr.WorkingArea;
                        return new Rect(wa.Left / scaleX, wa.Top / scaleY, wa.Width / scaleX, wa.Height / scaleY);
                    }
                }
            }
            catch { }
            return SystemParameters.WorkArea;
        }

        private void InitializeWindowProps()
        {
            this.Title = "OpenWidget Desktop";
            Program.TryApplyWindowIcon(this);
            this.MinWidth = 335;
            this.MinHeight = 380;
            this.Width = savedFreeWidth;
            this.Height = savedFreeHeight;

            var workArea = SystemParameters.WorkArea;
            if (IsPositionWithinVirtualDesktop(savedLeft, savedTop, this.Width, this.Height))
            {
                this.Left = savedLeft;
                this.Top = savedTop;
            }
            else
            {
                this.Left = workArea.Right - this.Width - 14;
                this.Top = workArea.Top + 14;
                savedLeft = this.Left;
                savedTop = this.Top;
            }

            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.ResizeMode = ResizeMode.NoResize;
            this.ShowInTaskbar = false;
            this.Topmost = false;

            this.UseLayoutRounding = true;
            this.SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
            RenderOptions.SetClearTypeHint(this, ClearTypeHint.Enabled);
            this.FontFamily = new FontFamily("Segoe UI");

            windowChrome = new WindowChrome
            {
                CaptionHeight = 0,
                CornerRadius = new CornerRadius(0),
                GlassFrameThickness = new Thickness(0),
                NonClientFrameEdges = NonClientFrameEdges.None,
                ResizeBorderThickness = new Thickness(0)
            };
            WindowChrome.SetWindowChrome(this, windowChrome);

            this.SizeChanged += (s, e) =>
            {
                if (this.ActualWidth >= 335 && this.ActualHeight >= 380)
                {
                    savedFreeWidth = this.ActualWidth;
                    savedFreeHeight = this.ActualHeight;
                    if (isWindowInitialized) SaveSettings();
                }
            };

            this.LocationChanged += (s, e) =>
            {
                if (isWindowInitialized && !double.IsNaN(this.Left) && !double.IsNaN(this.Top) && this.Left > -10000 && this.Top > -10000)
                {
                    savedLeft = this.Left;
                    savedTop = this.Top;
                    SaveSettings();
                }
            };

            this.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (!isDraggable) return;
                DependencyObject dep = e.OriginalSource as DependencyObject;
                if (IsInteractiveControl(dep)) return;

                try
                {
                    IntPtr hwnd = new WindowInteropHelper(this).Handle;
                    ReleaseCapture();
                    SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)HT_CAPTION, IntPtr.Zero);
                    savedLeft = this.Left;
                    savedTop = this.Top;
                    SaveSettings();
                    e.Handled = true;
                }
                catch { }
            };

            this.Loaded += (s, e) =>
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                cachedMainHwnd = hwnd;
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);

                AttachToDesktopShell(hwnd);
                try { AddClipboardFormatListener(hwnd); } catch { }

                RegisterHotKey(hwnd, HOTKEY_ID_MIC, MOD_CONTROL | MOD_SHIFT, VK_M);
                HwndSource source = HwndSource.FromHwnd(hwnd);
                source.AddHook(WndProc);

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!hasSavedDimensions)
                    {
                        FitHeightToContentIfCompact();
                    }
                    isWindowInitialized = true;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (showCalendarWidget) InitializeCalendarWidget();
                        if (showTimerWidget) InitializeTimerWidget();
                        if (showTaskbarDevices) InitializeTaskbarDevicesOverlay();
                    }), DispatcherPriority.ApplicationIdle);
                    Task.Run(async () =>
                    {
                        await Task.Delay(2500);
                        TrimProcessMemory();
                    });
                }), DispatcherPriority.Loaded);

                if (displaySettingsChangedHandler == null)
                {
                    displaySettingsChangedHandler = (ds, de) => ScheduleMonitorRediscovery(1500);
                    SystemEvents.DisplaySettingsChanged += displaySettingsChangedHandler;
                }
                if (powerModeChangedHandler == null)
                {
                    powerModeChangedHandler = (ps, pe) =>
                    {
                        if (pe.Mode == PowerModes.Resume) ScheduleMonitorRediscovery(1800);
                    };
                    SystemEvents.PowerModeChanged += powerModeChangedHandler;
                }
            };

            this.StateChanged += (s, e) =>
            {
                if (this.WindowState == WindowState.Minimized)
                {
                    this.WindowState = WindowState.Normal;
                }
            };

            this.Closed += (s, e) =>
            {
                if (isWindowInitialized && IsPositionWithinVirtualDesktop(this.Left, this.Top, this.ActualWidth, this.ActualHeight))
                {
                    savedLeft = this.Left;
                    savedTop = this.Top;
                }
                if (calendarWindow != null && isCalWindowInitialized && IsPositionWithinVirtualDesktop(calendarWindow.Left, calendarWindow.Top, calendarWindow.ActualWidth, calendarWindow.ActualHeight))
                {
                    savedCalLeft = calendarWindow.Left;
                    savedCalTop = calendarWindow.Top;
                }
                if (timerWindow != null && isTimerWindowInitialized && IsPositionWithinVirtualDesktop(timerWindow.Left, timerWindow.Top, timerWindow.ActualWidth, timerWindow.ActualHeight))
                {
                    savedTimerLeft = timerWindow.Left;
                    savedTimerTop = timerWindow.Top;
                }
                SaveTimerActivityData();
                SaveSettings();
                try
                {
                    if (calendarWindow != null)
                    {
                        calendarWindow.Close();
                        calendarWindow = null;
                    }
                    if (timerWindow != null)
                    {
                        timerWindow.Close();
                        timerWindow = null;
                    }
                    if (taskbarDevicesWindow != null)
                    {
                        taskbarDevicesWindow.Close();
                        taskbarDevicesWindow = null;
                    }
                    if (timerSettingsWindow != null)
                    {
                        timerSettingsWindow.Close();
                        timerSettingsWindow = null;
                    }
                    if (timerDetailedWindow != null)
                    {
                        timerDetailedWindow.Close();
                        timerDetailedWindow = null;
                    }
                }
                catch { }
                try
                {
                    IntPtr hwnd = new WindowInteropHelper(this).Handle;
                    RemoveClipboardFormatListener(hwnd);
                    UnregisterHotKey(hwnd, HOTKEY_ID_MIC);
                }
                catch { }
                try { if (nvmlInitialized) NvmlNative.nvmlShutdown(); } catch { }
                try
                {
                    if (displaySettingsChangedHandler != null)
                    {
                        SystemEvents.DisplaySettingsChanged -= displaySettingsChangedHandler;
                        displaySettingsChangedHandler = null;
                    }
                    if (powerModeChangedHandler != null)
                    {
                        SystemEvents.PowerModeChanged -= powerModeChangedHandler;
                        powerModeChangedHandler = null;
                    }
                    lock (monitorRefreshLock)
                    {
                        if (monitorRefreshCts != null) { monitorRefreshCts.Cancel(); monitorRefreshCts.Dispose(); monitorRefreshCts = null; }
                    }
                }
                catch { }
                CleanupMonitorHandles();
                try { if (sharedPing != null) sharedPing.Dispose(); } catch { }
                if (trayIcon != null)
                {
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                }
            };
        }

        private void AttachToDesktopShell(IntPtr hwnd)
        {
            try
            {
                // Place widget on the desktop layer (bottom of normal windows) without binding GWLP_HWNDPARENT to WorkerW
                SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
            catch { }
        }

        private bool IsWindowAbove(IntPtr hwnd, IntPtr targetHwnd)
        {
            if (hwnd == IntPtr.Zero || targetHwnd == IntPtr.Zero || hwnd == targetHwnd) return false;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOPMOST) != 0) return true;
            IntPtr cur = GetWindow(targetHwnd, GW_HWNDPREV);
            int steps = 0;
            while (cur != IntPtr.Zero && steps < 64)
            {
                if (cur == hwnd) return true;
                cur = GetWindow(cur, GW_HWNDPREV);
                steps++;
            }
            return false;
        }

        private bool IsWindowStrictlyAbove(IntPtr candidate, IntPtr reference)
        {
            if (candidate == IntPtr.Zero || reference == IntPtr.Zero || candidate == reference) return false;
            IntPtr cur = GetWindow(reference, GW_HWNDPREV);
            int steps = 0;
            while (cur != IntPtr.Zero && steps < 96)
            {
                if (cur == candidate) return true;
                cur = GetWindow(cur, GW_HWNDPREV);
                steps++;
            }
            return false;
        }

        private IntPtr TaskbarOverlayWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_MOUSEACTIVATE = 0x0021;
            const int MA_NOACTIVATE = 3;
            if (msg == WM_MOUSEACTIVATE)
            {
                handled = true;
                return new IntPtr(MA_NOACTIVATE);
            }
            return IntPtr.Zero;
        }

        private void SendWidgetToDesktopBottom(IntPtr hwnd)
        {
            SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        private void CheckAndEnforceShowDesktopImmunity()
        {
            UpdateTaskbarDevicesOverlayState();
            if (userHidden) return;
            try
            {
                IntPtr hwnd = cachedMainHwnd != IntPtr.Zero ? cachedMainHwnd : (cachedMainHwnd = new WindowInteropHelper(this).Handle);
                if (hwnd == IntPtr.Zero) return;

                IntPtr calHwnd = (calendarWindow != null && showCalendarWidget)
                    ? (cachedCalHwnd != IntPtr.Zero ? cachedCalHwnd : (cachedCalHwnd = new WindowInteropHelper(calendarWindow).Handle))
                    : IntPtr.Zero;

                IntPtr timerHwnd = (timerWindow != null && showTimerWidget)
                    ? (cachedTimerHwnd != IntPtr.Zero ? cachedTimerHwnd : (cachedTimerHwnd = new WindowInteropHelper(timerWindow).Handle))
                    : IntPtr.Zero;

                IntPtr fg = GetForegroundWindow();
                if (fg != IntPtr.Zero)
                {
                    if (fg == lastVerifiedFgHwnd && !isShowDesktopActive)
                        return;

                    fgClassBuilder.Length = 0;
                    GetClassName(fg, fgClassBuilder, 64);
                    string cls = fgClassBuilder.ToString();
                    if (cls == "WorkerW" || cls == "Progman")
                    {
                        lastVerifiedFgHwnd = IntPtr.Zero;
                        if (!isShowDesktopActive)
                        {
                            isShowDesktopActive = true;
                            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                            if (calHwnd != IntPtr.Zero)
                                SetWindowPos(calHwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                            if (timerHwnd != IntPtr.Zero)
                                SetWindowPos(timerHwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                        }
                    }
                    else if (fg != hwnd && fg != calHwnd && fg != timerHwnd && cls != "Shell_TrayWnd" && cls != "NotifyIconOverflowWindow")
                    {
                        if (isShowDesktopActive)
                        {
                            isShowDesktopActive = false;
                            SendWidgetToDesktopBottom(hwnd);
                            if (calHwnd != IntPtr.Zero) SendWidgetToDesktopBottom(calHwnd);
                            if (timerHwnd != IntPtr.Zero) SendWidgetToDesktopBottom(timerHwnd);
                        }
                        else
                        {
                            if (IsWindowAbove(hwnd, fg)) SendWidgetToDesktopBottom(hwnd);
                            if (calHwnd != IntPtr.Zero && IsWindowAbove(calHwnd, fg)) SendWidgetToDesktopBottom(calHwnd);
                            if (timerHwnd != IntPtr.Zero && IsWindowAbove(timerHwnd, fg)) SendWidgetToDesktopBottom(timerHwnd);
                        }
                        lastVerifiedFgHwnd = fg;
                    }
                }
            }
            catch { }
        }

        private static void TrimProcessMemory()
        {
            try
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false);
                GC.WaitForPendingFinalizers();
                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1));
            }
            catch { }
        }

        private bool IsInteractiveControl(DependencyObject dep)
        {
            while (dep != null && dep != this)
            {
                if (dep is ButtonBase || dep is Slider || dep is Thumb || dep is TextBox)
                    return true;
                if (dep is FrameworkElement)
                {
                    FrameworkElement fe = (FrameworkElement)dep;
                    if (fe.Tag != null && fe.Tag.ToString() == "Interactive")
                        return true;
                }
                dep = VisualTreeHelper.GetParent(dep);
            }
            return false;
        }

        private void FitHeightToContentIfCompact()
        {
            if (outerBorder != null && outerBorder.Child is Grid)
            {
                Grid g = (Grid)outerBorder.Child;
                if (g.Children.Count > 0 && g.Children[0] is ScrollViewer)
                {
                    ScrollViewer sv = (ScrollViewer)g.Children[0];
                    if (sv.Content is FrameworkElement)
                    {
                        FrameworkElement content = (FrameworkElement)sv.Content;
                        content.Measure(new Size(this.ActualWidth - 36, double.PositiveInfinity));
                        double desiredH = content.DesiredSize.Height + 42;
                        Rect workArea = GetWindowMonitorWorkArea(this);
                        double targetH = Math.Min(workArea.Height - 28, Math.Max(390, desiredH));
                        this.Height = targetH;
                        savedFreeHeight = targetH;
                    }
                }
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_MOUSEACTIVATE = 0x0021;
            const int MA_NOACTIVATE = 3;
            const int WM_HOTKEY = 0x0312;
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_MINIMIZE = 0xF020;
            const int WM_WINDOWPOSCHANGING = 0x0046;
            const uint SWP_HIDEWINDOW = 0x0080;

            if (msg == WM_MOUSEACTIVATE)
            {
                handled = true;
                return new IntPtr(MA_NOACTIVATE);
            }
            else if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID_MIC)
            {
                ToggleMicrophoneMute();
                handled = true;
            }
            else if (msg == WM_CLIPBOARDUPDATE)
            {
                CheckClipboardChanges();
            }
            else if (msg == WM_SYSCOMMAND && (wParam.ToInt32() & 0xFFF0) == SC_MINIMIZE)
            {
                handled = true;
            }
            else if (msg == WM_WINDOWPOSCHANGING && !userHidden && lParam != IntPtr.Zero)
            {
                try
                {
                    WINDOWPOS wp = (WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(WINDOWPOS));
                    bool modified = false;
                    if ((wp.flags & SWP_HIDEWINDOW) != 0)
                    {
                        wp.flags &= ~SWP_HIDEWINDOW;
                        wp.flags |= SWP_SHOWWINDOW;
                        modified = true;
                    }
                    // Prevent mouse clicks on the widget from raising it above normal windows (browsers, apps)
                    if (!isShowDesktopActive && (wp.flags & SWP_NOZORDER) == 0 && wp.hwndInsertAfter == IntPtr.Zero)
                    {
                        wp.hwndInsertAfter = HWND_BOTTOM;
                        modified = true;
                    }
                    if (modified)
                    {
                        Marshal.StructureToPtr(wp, lParam, true);
                    }
                }
                catch { }
            }
            return IntPtr.Zero;
        }

        private static readonly Dictionary<string, SolidColorBrush> brushCache = new Dictionary<string, SolidColorBrush>(StringComparer.OrdinalIgnoreCase);

        private Brush HexBrush(string hex)
        {
            SolidColorBrush b;
            if (!brushCache.TryGetValue(hex, out b))
            {
                b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex);
                if (b.CanFreeze) b.Freeze();
                brushCache[hex] = b;
            }
            return b;
        }

        private Color ParseColor(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }

        private Color BlendColors(Color baseColor, Color tint, double tintRatio, byte alpha)
        {
            byte r = (byte)Math.Max(0, Math.Min(255, baseColor.R * (1 - tintRatio) + tint.R * tintRatio));
            byte g = (byte)Math.Max(0, Math.Min(255, baseColor.G * (1 - tintRatio) + tint.G * tintRatio));
            byte b = (byte)Math.Max(0, Math.Min(255, baseColor.B * (1 - tintRatio) + tint.B * tintRatio));
            return Color.FromArgb(alpha, r, g, b);
        }

        public void ApplyTheme(string newAccentHex, int newOpacityPct)
        {
            try
            {
                Color accent = ParseColor(newAccentHex);
                accentColorHex = newAccentHex;
                bgOpacityPercent = Math.Max(30, Math.Min(100, newOpacityPct));

                byte mainAlpha = (byte)Math.Round((bgOpacityPercent / 100.0) * 255.0);
                byte cardAlpha = (byte)Math.Round((Math.Min(100, bgOpacityPercent * 0.65) / 100.0) * 255.0);
                byte subAlpha = (byte)Math.Round((Math.Min(100, bgOpacityPercent * 0.55) / 100.0) * 255.0);

                Color darkTop = BlendColors(Color.FromRgb(22, 19, 17), accent, 0.08, mainAlpha);
                Color darkBottom = BlendColors(Color.FromRgb(28, 24, 21), accent, 0.12, mainAlpha);
                Color outerBorderCol = BlendColors(Color.FromRgb(56, 48, 42), accent, 0.35, 220);

                Color cardBgCol = BlendColors(Color.FromRgb(34, 29, 26), accent, 0.10, cardAlpha);
                Color cardBorderCol = BlendColors(Color.FromRgb(64, 54, 48), accent, 0.28, 190);

                Color subBgCol = BlendColors(Color.FromRgb(42, 35, 31), accent, 0.12, subAlpha);
                Color subBorderCol = BlendColors(Color.FromRgb(74, 62, 55), accent, 0.30, 180);

                SolidColorBrush accentBrush = new SolidColorBrush(accent); accentBrush.Freeze();
                SolidColorBrush cardBgBrush = new SolidColorBrush(cardBgCol); cardBgBrush.Freeze();
                SolidColorBrush cardBorderBrush = new SolidColorBrush(cardBorderCol); cardBorderBrush.Freeze();
                SolidColorBrush subBgBrush = new SolidColorBrush(subBgCol); subBgBrush.Freeze();
                SolidColorBrush subBorderBrush = new SolidColorBrush(subBorderCol); subBorderBrush.Freeze();

                if (outerBorder != null)
                {
                    LinearGradientBrush grad = new LinearGradientBrush(darkTop, darkBottom, new Point(0, 0), new Point(0, 1));
                    grad.Freeze();
                    outerBorder.Background = grad;
                    SolidColorBrush ob = new SolidColorBrush(outerBorderCol); ob.Freeze();
                    outerBorder.BorderBrush = ob;
                }

                foreach (Border card in glassCards)
                {
                    card.Background = cardBgBrush;
                    card.BorderBrush = cardBorderBrush;
                }

                foreach (Border box in subBoxes)
                {
                    box.Background = subBgBrush;
                    box.BorderBrush = subBorderBrush;
                }

                if (weatherIconBox != null)
                {
                    weatherIconBox.Background = new SolidColorBrush(BlendColors(Color.FromRgb(38, 32, 28), accent, 0.22, 210));
                    weatherIconBox.BorderBrush = new SolidColorBrush(BlendColors(Color.FromRgb(60, 50, 44), accent, 0.45, 220));
                }

                foreach (TextBlock tb in accentTextBlocks)
                {
                    tb.Foreground = accentBrush;
                }

                foreach (Polyline poly in accentSparklines)
                {
                    poly.Stroke = accentBrush;
                }

                foreach (Border fill in accentBarFills)
                {
                    fill.Background = accentBrush;
                }

                foreach (WirelessRowUi wui in wirelessRowCache.Values)
                {
                    Brush sb = GetWirelessStatusBrush(wui.BarPct, wui.IsCharging);
                    wui.Pct.Foreground = sb;
                    wui.Fill.Background = sb;
                }

                for (int i = 0; i < monitorSliderContainers.Count && i < sliderMonitors.Count; i++)
                {
                    if (monitorSliderContainers[i] != null && sliderMonitors[i] != null)
                    {
                        int idx = i;
                        double curVal = sliderMonitors[i].Value;
                        Slider ms = CreateModernSlider(accentColorHex, curVal, 0, "6,0,8,0");
                        ms.ValueChanged += (s, e) => OnMonitorSliderValueChanged(idx, e.NewValue);
                        AttachMonitorSliderWheel(ms, idx);
                        sliderMonitors[i] = ms;
                        monitorSliderContainers[i].Child = ms;
                    }
                }

                UpdateMonitorLinkButtonUi();
                UpdateDraggableButtonUi();
                ApplyCalendarWidgetTheme();
                ApplyTimerWidgetTheme();

                if (toastBorder != null)
                {
                    toastBorder.Background = new SolidColorBrush(BlendColors(Color.FromRgb(30, 26, 24), accent, 0.75, 240));
                }

                SaveSettings();
            }
            catch { }
        }

        private void BuildUserInterface()
        {
            outerBorder = new Border
            {
                CornerRadius = new CornerRadius(16),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(4)
            };
            DropShadowEffect shadow = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 14,
                ShadowDepth = 2,
                Opacity = 0.60
            };
            shadow.Freeze();
            outerBorder.Effect = shadow;

            Grid outerGrid = new Grid();

            ScrollViewer scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(14, 12, 14, 14)
            };

            StackPanel rootStack = new StackPanel();

            // 1. TOP HEADER (Clock + Date + Uptime + Icon-Only Mic + Icon-Only Lock + Settings)
            Grid headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            txtClockAndUptime = new TextBlock
            {
                Text = "05:50 · 6 Kas Cum  Açık: 2s 15dk",
                Foreground = HexBrush("#F5EFEA"),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(txtClockAndUptime, 0);
            headerGrid.Children.Add(txtClockAndUptime);

            btnMicHeader = CreateStyledIconButton("#26201D", "Transparent", L("Mikrofonu Sustur / Aç (Ctrl+Shift+M)", "Mute / Unmute Microphone (Ctrl+Shift+M)"));
            btnMicHeader.Margin = new Thickness(4, 0, 4, 0);
            btnMicHeader.Padding = new Thickness(7, 3, 7, 3);
            txtMicHeaderIcon = new TextBlock { Text = "🎙", FontSize = 11.5, Foreground = HexBrush("#FAF6F2"), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            btnMicHeader.Content = txtMicHeaderIcon;
            btnMicHeader.Click += (s, e) => ToggleMicrophoneMute();
            Grid.SetColumn(btnMicHeader, 1);
            headerGrid.Children.Add(btnMicHeader);

            btnLockDrag = CreateStyledIconButton("#26201D", "Transparent", L("Sabit Mod (Taşınabilir ve Boyutlandırılabilir Modu Açmak İçin Tıklayın)", "Locked Mode (Click to Enable Draggable & Resizable Mode)"));
            btnLockDrag.Padding = new Thickness(7, 3, 7, 3);
            btnLockDrag.Margin = new Thickness(0, 0, 4, 0);
            btnLockDrag.Content = new TextBlock { Text = "📌", FontSize = 11.5, Foreground = HexBrush("#C7B9AE"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            btnLockDrag.Click += (s, e) => ToggleDraggableMode();
            Grid.SetColumn(btnLockDrag, 2);
            headerGrid.Children.Add(btnLockDrag);

            btnSettingsHeader = CreateStyledIconButton("#26201D", "Transparent", L("Widget Ayarları (Dil, Konum, Tema Rengi & Opaklık)", "Widget Settings (Language, Location, Theme & Opacity)"));
            btnSettingsHeader.Padding = new Thickness(7, 3, 7, 3);
            btnSettingsHeader.Content = new TextBlock { Text = "⚙", FontSize = 11.5, Foreground = HexBrush("#C7B9AE") };
            btnSettingsHeader.Click += (s, e) => OpenSettingsWindow();
            Grid.SetColumn(btnSettingsHeader, 3);
            headerGrid.Children.Add(btnSettingsHeader);

            rootStack.Children.Add(headerGrid);

            modulesHostPanel = new StackPanel();
            rootStack.Children.Add(modulesHostPanel);

            // 2. WEATHER SECTION
            secContainerWeather = new StackPanel();
            Grid weatherTitleGrid = new Grid { Margin = new Thickness(0, 2, 0, 6) };
            weatherTitleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            weatherTitleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel weatherTitleLeft = new StackPanel { Orientation = Orientation.Horizontal };
            weatherTitleLeft.Children.Add(new TextBlock { Text = "☀ ", Foreground = HexBrush("#C7B9AE"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            txtWeatherSectionTitle = new TextBlock { Text = L("Hava Durumu", "Weather"), Foreground = HexBrush("#E6DDD6"), FontSize = 12, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center };
            weatherTitleLeft.Children.Add(txtWeatherSectionTitle);
            Grid.SetColumn(weatherTitleLeft, 0);
            weatherTitleGrid.Children.Add(weatherTitleLeft);

            txtLocationBadge = new TextBlock
            {
                Text = locationName,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                Tag = "Interactive",
                ToolTip = L("Hava durumunu yenilemek için sol tıklayın, konumu değiştirmek için Ayarlar (⚙) menüsünü açın", "Left-click to refresh weather, open Settings (⚙) to change location")
            };
            accentTextBlocks.Add(txtLocationBadge);
            txtLocationBadge.MouseLeftButtonDown += (s, e) => Task.Run(() => FetchWeatherAsync());
            Grid.SetColumn(txtLocationBadge, 1);
            weatherTitleGrid.Children.Add(txtLocationBadge);
            secContainerWeather.Children.Add(weatherTitleGrid);

            Border weatherCard = CreateGlassCard();
            StackPanel weatherStack = new StackPanel();

            Grid mainWeatherRow = new Grid { Margin = new Thickness(2, 0, 2, 10) };
            mainWeatherRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            mainWeatherRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainWeatherRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            txtWeatherTemp = new TextBlock
            {
                Text = "14°",
                Foreground = Brushes.White,
                FontSize = 30,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            Grid.SetColumn(txtWeatherTemp, 0);
            mainWeatherRow.Children.Add(txtWeatherTemp);

            StackPanel descStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            txtWeatherDesc = new TextBlock { Text = L("Parçalı Bulutlu", "Partly Cloudy"), Foreground = HexBrush("#FAF6F2"), FontSize = 13.5, FontWeight = FontWeights.SemiBold };
            txtWeatherFeels = new TextBlock { Text = L("Hissedilen 13°C", "Feels like 13°C"), Foreground = HexBrush("#B8A99E"), FontSize = 11.5, Margin = new Thickness(0, 2, 0, 0) };
            descStack.Children.Add(txtWeatherDesc);
            descStack.Children.Add(txtWeatherFeels);
            Grid.SetColumn(descStack, 1);
            mainWeatherRow.Children.Add(descStack);

            weatherIconBox = new Border
            {
                Width = 38,
                Height = 38,
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1)
            };
            txtWeatherMainIcon = new TextBlock
            {
                Text = "☀",
                FontSize = 19,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            accentTextBlocks.Add(txtWeatherMainIcon);
            weatherIconBox.Child = txtWeatherMainIcon;
            Grid.SetColumn(weatherIconBox, 2);
            mainWeatherRow.Children.Add(weatherIconBox);
            weatherStack.Children.Add(mainWeatherRow);

            UniformGrid metricsGrid = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 10) };
            metricsGrid.Children.Add(CreateWeatherMetricBox(L("💧 Nem", "💧 Humidity"), "%75", out txtHumidityLabel, out txtWeatherHumidity, new Thickness(0, 0, 4, 0)));
            metricsGrid.Children.Add(CreateWeatherMetricBox(L("💨 Rüzgar", "💨 Wind"), L("10 km/s", "10 km/h"), out txtWindLabel, out txtWeatherWind, new Thickness(2, 0, 2, 0)));
            metricsGrid.Children.Add(CreateWeatherMetricBox(L("🌧 Yağış", "🌧 Rain"), "0 mm", out txtRainLabel, out txtWeatherRain, new Thickness(4, 0, 0, 0)));
            weatherStack.Children.Add(metricsGrid);

            weatherStack.Children.Add(new Border { Height = 1, Background = HexBrush("#3D322C"), Margin = new Thickness(0, 0, 0, 8) });

            UniformGrid forecastGrid = new UniformGrid { Columns = 3 };
            string[] defaultDays = { L("Bugün", "Today"), L("Yarın", "Tomorrow"), L("Pazar", "Sun") };
            for (int i = 0; i < 3; i++)
            {
                StackPanel dayBox = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                txtDayNames[i] = new TextBlock { Text = defaultDays[i], Foreground = HexBrush("#B8A99E"), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center };
                txtDayIcons[i] = new TextBlock { Text = "⛅", FontSize = 13, Margin = new Thickness(0, 3, 0, 3), HorizontalAlignment = HorizontalAlignment.Center };
                accentTextBlocks.Add(txtDayIcons[i]);
                txtDayTemps[i] = new TextBlock { Text = "8° / 16°", Foreground = HexBrush("#FAF6F2"), FontSize = 11.5, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
                dayBox.Children.Add(txtDayNames[i]);
                dayBox.Children.Add(txtDayIcons[i]);
                dayBox.Children.Add(txtDayTemps[i]);
                forecastGrid.Children.Add(dayBox);
            }
            weatherStack.Children.Add(forecastGrid);
            weatherCard.Child = weatherStack;
            secContainerWeather.Children.Add(weatherCard);
            RefreshWeatherTemperatureUi();

            // 2B. MONITOR BRIGHTNESS SECTION (Auto-Detects Monitors, HDR SDRWhiteLevel + DDC/CI)
            secContainerMonitors = new StackPanel { Visibility = Visibility.Collapsed };
            monHeaderGrid = new Grid { Margin = new Thickness(0, 12, 0, 6), Cursor = Cursors.Hand, Tag = "Interactive" };
            monHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            monHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            monHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            monHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel monHeaderLeft = new StackPanel { Orientation = Orientation.Horizontal };
            monHeaderLeft.Children.Add(new TextBlock { Text = "🖥 ", Foreground = HexBrush("#B8A99E"), FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });
            txtMonitorSectionTitle = new TextBlock { Text = L("Monitör Parlaklığı", "Monitor Brightness"), Foreground = HexBrush("#E6DDD6"), FontSize = 12.5, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center };
            monHeaderLeft.Children.Add(txtMonitorSectionTitle);
            Grid.SetColumn(monHeaderLeft, 0);
            monHeaderGrid.Children.Add(monHeaderLeft);

            btnMonitorLink = CreateStyledIconButton("#26201D", "#443831", L("Monitörlerin parlaklığını birlikte (senkron) değiştir", "Sync brightness across all monitors"));
            btnMonitorLink.Padding = new Thickness(6, 2, 6, 2);
            btnMonitorLink.Margin = new Thickness(0, 0, 4, 0);
            btnMonitorLink.Click += (s, e) =>
            {
                e.Handled = true;
                monitorLinkSync = !monitorLinkSync;
                UpdateMonitorLinkButtonUi();
                SaveSettings();
                if (monitorLinkSync && sliderMonitors.Count > 0 && sliderMonitors[0] != null)
                {
                    SetMonitorBrightnessFromUi(0, (int)Math.Round(sliderMonitors[0].Value), true);
                }
            };
            Grid.SetColumn(btnMonitorLink, 1);
            monHeaderGrid.Children.Add(btnMonitorLink);

            btnRefreshMonitors = CreateStyledIconButton("Transparent", "Transparent", L("Monitör parlaklıklarını yeniden oku", "Re-read monitor brightness levels"));
            btnRefreshMonitors.Padding = new Thickness(5, 1, 5, 1);
            btnRefreshMonitors.Margin = new Thickness(0, 0, 4, 0);
            btnRefreshMonitors.Content = new TextBlock { Text = "↻", Foreground = HexBrush("#B8A99E"), FontSize = 12, FontWeight = FontWeights.Bold };
            btnRefreshMonitors.Click += (s, e) =>
            {
                e.Handled = true;
                Task.Run(() => DiscoverAndSyncMonitors(true));
                ShowToast(L("Monitör parlaklıkları okunuyor...", "Reading monitor brightness..."));
            };
            Grid.SetColumn(btnRefreshMonitors, 2);
            monHeaderGrid.Children.Add(btnRefreshMonitors);

            TextBlock monChevron = new TextBlock { Text = "⌄", Foreground = HexBrush("#B8A99E"), FontSize = 13, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(monChevron, 3);
            monHeaderGrid.Children.Add(monChevron);

            monitorCard = CreateGlassCard();
            monHeaderGrid.MouseLeftButtonDown += (s, e) =>
            {
                if (monitorCard.Visibility == Visibility.Visible)
                {
                    monitorCard.Visibility = Visibility.Collapsed;
                    monChevron.Text = "›";
                }
                else
                {
                    monitorCard.Visibility = Visibility.Visible;
                    monChevron.Text = "⌄";
                }
            };
            secContainerMonitors.Children.Add(monHeaderGrid);

            StackPanel monCardStack = new StackPanel();
            monRowsHostStack = new StackPanel();
            monCardStack.Children.Add(monRowsHostStack);

            UniformGrid monPresetGrid = new UniformGrid { Columns = 4, Margin = new Thickness(0, 4, 0, 0) };
            int[] monPresetVals = { 15, 40, 75, 100 };
            string[] monPresetIcons = { "🌙 ", "🔉 ", "🔆 ", "☀ " };
            for (int i = 0; i < 4; i++)
            {
                int targetVal = monPresetVals[i];
                string iconPrefix = monPresetIcons[i];
                Button b = CreateStyledIconButton("#26201D", "#443831", L("Tüm monitörleri %", "Set all monitors to ") + targetVal + (IsEn ? "%" : " yap"));
                b.Margin = new Thickness(i == 0 ? 0 : 2, 0, i == 3 ? 0 : 2, 0);
                b.Padding = new Thickness(2, 3, 2, 3);
                b.Content = new TextBlock { Text = iconPrefix + (IsEn ? (targetVal + "%") : ("%" + targetVal)), Foreground = HexBrush("#E6DDD6"), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center };
                b.Click += (s, e) => SetMonitorBrightnessFromUi(0, targetVal, true);
                monPresetGrid.Children.Add(b);
            }
            monCardStack.Children.Add(monPresetGrid);
            monitorCard.Child = monCardStack;
            secContainerMonitors.Children.Add(monitorCard);
            UpdateMonitorLinkButtonUi();

            // 3. WIRELESS DEVICES SECTION (Auto-Discovers Connected Mouse, Keyboard, Headset, Controller)
            secContainerWireless = new StackPanel { Visibility = Visibility.Collapsed };
            wirelessHeaderGrid = new Grid
            {
                Margin = new Thickness(0, 12, 0, 4),
                Cursor = Cursors.Hand,
                Tag = "Interactive"
            };
            wirelessHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            wirelessHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            wirelessHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel wirelessTitleLeft = new StackPanel { Orientation = Orientation.Horizontal };
            wirelessTitleLeft.Children.Add(new TextBlock { Text = "🔋 ", Foreground = HexBrush("#B8A99E"), FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });
            txtWirelessSectionTitle = new TextBlock { Text = L("Kablosuz Cihazlar", "Wireless Devices"), Foreground = HexBrush("#E6DDD6"), FontSize = 12.5, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center };
            wirelessTitleLeft.Children.Add(txtWirelessSectionTitle);
            Grid.SetColumn(wirelessTitleLeft, 0);
            wirelessHeaderGrid.Children.Add(wirelessTitleLeft);

            btnRefreshWireless = CreateStyledIconButton("Transparent", "Transparent", L("Kablosuz cihaz şarjlarını şimdi yenile", "Refresh wireless device battery levels now"));
            btnRefreshWireless.Padding = new Thickness(5, 1, 5, 1);
            btnRefreshWireless.Margin = new Thickness(0, 0, 4, 0);
            btnRefreshWireless.Content = new TextBlock { Text = "↻", Foreground = HexBrush("#B8A99E"), FontSize = 12, FontWeight = FontWeights.Bold };
            btnRefreshWireless.Click += (s, e) =>
            {
                e.Handled = true;
                Task.Run(() => PollWirelessDevicesAsync());
                ShowToast(L("Kablosuz cihazlar taranıyor...", "Scanning wireless devices..."));
            };
            Grid.SetColumn(btnRefreshWireless, 1);
            wirelessHeaderGrid.Children.Add(btnRefreshWireless);

            TextBlock wirelessChevron = new TextBlock { Text = "⌄", Foreground = HexBrush("#B8A99E"), FontSize = 13, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(wirelessChevron, 2);
            wirelessHeaderGrid.Children.Add(wirelessChevron);

            panelWirelessContent = new StackPanel();
            wirelessHeaderGrid.MouseLeftButtonDown += (s, e) =>
            {
                if (panelWirelessContent.Visibility == Visibility.Visible)
                {
                    panelWirelessContent.Visibility = Visibility.Collapsed;
                    wirelessChevron.Text = "›";
                }
                else
                {
                    panelWirelessContent.Visibility = Visibility.Visible;
                    wirelessChevron.Text = "⌄";
                    Task.Run(() => PollWirelessDevicesAsync());
                }
            };
            secContainerWireless.Children.Add(wirelessHeaderGrid);
            secContainerWireless.Children.Add(panelWirelessContent);

            // 4. TELEMETRY SECTION
            secContainerTelemetry = new StackPanel();
            Grid teleHeader = new Grid { Margin = new Thickness(0, 12, 0, 4), Cursor = Cursors.Hand, Tag = "Interactive" };
            teleHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            teleHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            txtTelemetrySectionTitle = new TextBlock { Text = L("Telemetri", "Telemetry"), Foreground = HexBrush("#E6DDD6"), FontSize = 12.5, FontWeight = FontWeights.Medium };
            teleHeader.Children.Add(txtTelemetrySectionTitle);
            TextBlock teleChevron = new TextBlock { Text = "⌄", Foreground = HexBrush("#B8A99E"), FontSize = 13, FontWeight = FontWeights.Bold };
            Grid.SetColumn(teleChevron, 1);
            teleHeader.Children.Add(teleChevron);

            panelTelemetryContent = new StackPanel();
            teleHeader.MouseLeftButtonDown += (s, e) =>
            {
                if (panelTelemetryContent.Visibility == Visibility.Visible)
                {
                    panelTelemetryContent.Visibility = Visibility.Collapsed;
                    teleChevron.Text = "›";
                }
                else
                {
                    panelTelemetryContent.Visibility = Visibility.Visible;
                    teleChevron.Text = "⌄";
                }
            };
            secContainerTelemetry.Children.Add(teleHeader);

            rowTeleCpu = CreateTelemetrySparkRow("⚙", L("İşlemci (CPU)", "Processor (CPU)"), "46°C • " + cpuShortName, true, out txtCpuRowLabel, out txtCpuDetail, out sparkCpu, out txtCpuPct);
            panelTelemetryContent.Children.Add(rowTeleCpu);

            rowTeleRam = CreateTelemetryBarRow("📟", L("Bellek (RAM)", "Memory (RAM)"), "8.0 / 16 GB", true, out txtRamRowLabel, out txtRamDetail, out barRamFill, out txtRamPct);
            panelTelemetryContent.Children.Add(rowTeleRam);

            rowTeleGpu = CreateTelemetrySparkRow("🎮", L("Ekran Kartı (GPU)", "Graphics (GPU)"), "45°C • " + gpuModelName, true, out txtGpuRowLabel, out txtGpuDetail, out sparkGpu, out txtGpuPct);
            panelTelemetryContent.Children.Add(rowTeleGpu);

            rowTeleVram = CreateTelemetryBarRow("🎞", L("Video Belleği (VRAM)", "Video Memory (VRAM)"), "1.5 / 8 GB", true, out txtVramRowLabel, out txtVramDetail, out barVramFill, out txtVramPct);
            panelTelemetryContent.Children.Add(rowTeleVram);

            // Network Row
            rowTeleNet = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            rowTeleNet.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowTeleNet.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowTeleNet.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowTeleNet.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel netLeft = new StackPanel { Orientation = Orientation.Horizontal };
            netLeft.Children.Add(CreateMiniIconBadge("⇄"));
            txtNetRowLabel = new TextBlock { Text = L("Ağ Trafiği", "Network"), Foreground = HexBrush("#F5EFEA"), FontSize = 12, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center };
            netLeft.Children.Add(txtNetRowLabel);
            Grid.SetColumn(netLeft, 0);
            rowTeleNet.Children.Add(netLeft);

            txtNetSpeed = new TextBlock
            {
                Text = "↓ 0 Kbps ↑ 0 Kbps",
                Foreground = HexBrush("#B8A99E"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(4, 0, 6, 0)
            };
            Grid.SetColumn(txtNetSpeed, 1);
            rowTeleNet.Children.Add(txtNetSpeed);

            Border netBadgeBorder = new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = HexBrush("#2A211C"),
                BorderBrush = HexBrush("#4F3B30"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5, 1, 5, 1),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            subBoxes.Add(netBadgeBorder);
            txtNetBadge = new TextBlock { Text = L("Harika", "Great"), FontSize = 10, FontWeight = FontWeights.SemiBold };
            accentTextBlocks.Add(txtNetBadge);
            netBadgeBorder.Child = txtNetBadge;
            Grid.SetColumn(netBadgeBorder, 2);
            rowTeleNet.Children.Add(netBadgeBorder);

            txtNetPing = new TextBlock
            {
                Text = "10 ms",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Width = 48,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            accentTextBlocks.Add(txtNetPing);
            Grid.SetColumn(txtNetPing, 3);
            rowTeleNet.Children.Add(txtNetPing);
            panelTelemetryContent.Children.Add(rowTeleNet);

            // Dynamic Fixed Drives (Automatically adapts to whatever disks C:, D:, E: the user has!)
            for (int i = 0; i < fixedDrives.Count; i++)
            {
                DriveInfo d = fixedDrives[i];
                string dName = d.Name.TrimEnd('\\');
                TextBlock rowLbl;
                TextBlock det;
                Border fill;
                TextBlock pct;
                Grid dRow = CreateTelemetryBarRow(i == 0 ? "💾" : "💿", dName + L(" Diski", " Drive"), L("... GB Boş", "... GB Free"), true, out rowLbl, out det, out fill, out pct);
                panelTelemetryContent.Children.Add(dRow);
                rowsTeleDisks.Add(dRow);
                diskRowLabels.Add(rowLbl);
                diskDetailBlocks.Add(det);
                diskFillBars.Add(fill);
                diskPctBlocks.Add(pct);
            }

            secContainerTelemetry.Children.Add(panelTelemetryContent);

            // 5. CLIPBOARD HISTORY SECTION
            secContainerClipboard = new StackPanel();
            Grid clipHeader = new Grid { Margin = new Thickness(0, 12, 0, 6) };
            clipHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            clipHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            clipHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel clipTitleLeft = new StackPanel { Orientation = Orientation.Horizontal };
            clipTitleLeft.Children.Add(new TextBlock { Text = "📋 ", Foreground = HexBrush("#B8A99E"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            txtClipboardSectionTitle = new TextBlock { Text = L("Pano Geçmişi", "Clipboard History"), Foreground = HexBrush("#E6DDD6"), FontSize = 12.5, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center };
            clipTitleLeft.Children.Add(txtClipboardSectionTitle);
            Grid.SetColumn(clipTitleLeft, 0);
            clipHeader.Children.Add(clipTitleLeft);

            btnClearClip = CreateStyledIconButton("Transparent", "Transparent", L("Pano geçmişini temizle", "Clear clipboard history"));
            btnClearClip.Padding = new Thickness(5, 2, 5, 2);
            btnClearClip.Margin = new Thickness(0, 0, 6, 0);
            btnClearClip.Content = new TextBlock { Text = "🗑", Foreground = HexBrush("#B8A99E"), FontSize = 12 };
            btnClearClip.Click += (s, e) =>
            {
                clipboardHistory.Clear();
                RefreshClipboardUi();
                ShowToast(L("Pano geçmişi temizlendi", "Clipboard history cleared"));
            };
            Grid.SetColumn(btnClearClip, 1);
            clipHeader.Children.Add(btnClearClip);

            Border clipCountBadge = new Border
            {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(7, 2, 7, 2)
            };
            subBoxes.Add(clipCountBadge);
            txtClipCount = new TextBlock { Text = L("0 öğe", "0 items"), Foreground = HexBrush("#E6DDD6"), FontSize = 11 };
            clipCountBadge.Child = txtClipCount;
            Grid.SetColumn(clipCountBadge, 2);
            clipHeader.Children.Add(clipCountBadge);
            secContainerClipboard.Children.Add(clipHeader);

            panelClipItems = new StackPanel();
            secContainerClipboard.Children.Add(panelClipItems);

            ApplyLayoutAndVisibility();

            resizeGripIndicator = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 6, 4),
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            resizeGripIndicator.Child = new TextBlock
            {
                Text = "◢",
                FontSize = 11,
                Foreground = HexBrush("#E2925C"),
                Opacity = 0.75
            };

            toastPopup = new Popup
            {
                PlacementTarget = this,
                Placement = PlacementMode.Bottom,
                VerticalOffset = -45,
                HorizontalOffset = 80,
                AllowsTransparency = true,
                StaysOpen = true
            };
            toastBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = HexBrush("#E6E2925C"),
                Padding = new Thickness(12, 6, 12, 6)
            };
            txtToastMessage = new TextBlock { Text = L("Panoya kopyalandı", "Copied to clipboard"), Foreground = Brushes.White, FontSize = 11.5, FontWeight = FontWeights.SemiBold };
            toastBorder.Child = txtToastMessage;
            toastPopup.Child = toastBorder;

            toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1600) };
            toastTimer.Tick += (s, e) => { toastPopup.IsOpen = false; toastTimer.Stop(); };

            scroll.Content = rootStack;
            outerGrid.Children.Add(scroll);
            outerGrid.Children.Add(resizeGripIndicator);
            outerBorder.Child = outerGrid;
            this.Content = outerBorder;
        }

        private Border CreateGlassCard()
        {
            Border b = new Border
            {
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 10, 12, 10)
            };
            glassCards.Add(b);
            return b;
        }

        private Slider CreateModernSlider(string accentHex, double initialVal)
        {
            return CreateModernSlider(accentHex, initialVal, 30, "0,2,0,10");
        }

        private Slider CreateModernSlider(string accentHex, double initialVal, double minVal, string marginStr)
        {
            string xaml = string.Format(@"
            <Slider xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                    Minimum='{2}' Maximum='100' Value='{1}' IsMoveToPointEnabled='True' Cursor='Hand' Margin='{3}'>
                <Slider.Template>
                    <ControlTemplate TargetType='Slider'>
                        <Grid VerticalAlignment='Center' Height='18'>
                            <Border Height='4' CornerRadius='2' Background='#2A211C' VerticalAlignment='Center'/>
                            <Track x:Name='PART_Track'>
                                <Track.DecreaseRepeatButton>
                                    <RepeatButton Command='Slider.DecreaseLarge'>
                                        <RepeatButton.Template>
                                            <ControlTemplate TargetType='RepeatButton'>
                                                <Border Height='4' CornerRadius='2' Background='{0}' VerticalAlignment='Center'/>
                                            </ControlTemplate>
                                        </RepeatButton.Template>
                                    </RepeatButton>
                                </Track.DecreaseRepeatButton>
                                <Track.IncreaseRepeatButton>
                                    <RepeatButton Command='Slider.IncreaseLarge'>
                                        <RepeatButton.Template>
                                            <ControlTemplate TargetType='RepeatButton'>
                                                <Border Height='4' CornerRadius='2' Background='Transparent' VerticalAlignment='Center'/>
                                            </ControlTemplate>
                                        </RepeatButton.Template>
                                    </RepeatButton>
                                </Track.IncreaseRepeatButton>
                                <Track.Thumb>
                                    <Thumb>
                                        <Thumb.Template>
                                            <ControlTemplate TargetType='Thumb'>
                                                <Border Width='14' Height='14' CornerRadius='7' Background='{0}' BorderBrush='#FAF6F2' BorderThickness='2'/>
                                            </ControlTemplate>
                                        </Thumb.Template>
                                    </Thumb>
                                </Track.Thumb>
                            </Track>
                        </Grid>
                    </ControlTemplate>
                </Slider.Template>
            </Slider>", accentHex, (int)Math.Round(initialVal), (int)Math.Round(minVal), marginStr);
            return (Slider)System.Windows.Markup.XamlReader.Parse(xaml);
        }

        private void ApplyLayoutAndVisibility()
        {
            if (modulesHostPanel == null) return;

            if (secContainerWeather != null) secContainerWeather.Visibility = showSecWeather ? Visibility.Visible : Visibility.Collapsed;
            if (secContainerMonitors != null) secContainerMonitors.Visibility = (showSecMonitors && monitorSlots.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
            if (secContainerWireless != null) secContainerWireless.Visibility = (showSecWireless && lastWirelessCount > 0) ? Visibility.Visible : Visibility.Collapsed;
            if (secContainerTelemetry != null) secContainerTelemetry.Visibility = showSecTelemetry ? Visibility.Visible : Visibility.Collapsed;
            if (secContainerClipboard != null) secContainerClipboard.Visibility = showSecClipboard ? Visibility.Visible : Visibility.Collapsed;

            if (rowTeleCpu != null) rowTeleCpu.Visibility = showTeleCpu ? Visibility.Visible : Visibility.Collapsed;
            if (rowTeleRam != null) rowTeleRam.Visibility = showTeleRam ? Visibility.Visible : Visibility.Collapsed;
            if (rowTeleGpu != null) rowTeleGpu.Visibility = showTeleGpu ? Visibility.Visible : Visibility.Collapsed;
            if (rowTeleVram != null) rowTeleVram.Visibility = showTeleVram ? Visibility.Visible : Visibility.Collapsed;
            if (rowTeleNet != null) rowTeleNet.Visibility = showTeleNet ? Visibility.Visible : Visibility.Collapsed;
            for (int i = 0; i < rowsTeleDisks.Count; i++)
            {
                if (rowsTeleDisks[i] != null)
                {
                    rowsTeleDisks[i].Visibility = showTeleDisks ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            modulesHostPanel.Children.Clear();
            bool isFirstVisible = true;
            foreach (string key in sectionOrder)
            {
                StackPanel container = null;
                if (key == "weather") container = secContainerWeather;
                else if (key == "monitors") container = secContainerMonitors;
                else if (key == "wireless") container = secContainerWireless;
                else if (key == "telemetry") container = secContainerTelemetry;
                else if (key == "clipboard") container = secContainerClipboard;

                if (container != null)
                {
                    if (container.Children.Count > 0 && container.Children[0] is FrameworkElement)
                    {
                        FrameworkElement hdr = (FrameworkElement)container.Children[0];
                        if (container.Visibility == Visibility.Visible)
                        {
                            hdr.Margin = isFirstVisible ? new Thickness(0, 2, 0, 6) : new Thickness(0, 12, 0, 6);
                            isFirstVisible = false;
                        }
                    }
                    modulesHostPanel.Children.Add(container);
                }
            }
        }

        private Border CreateWeatherMetricBox(string label, string initialValue, out TextBlock labelBlock, out TextBlock valueBlock, Thickness margin)
        {
            Border b = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 6, 6, 6),
                Margin = margin
            };
            subBoxes.Add(b);
            StackPanel sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            labelBlock = new TextBlock { Text = label, Foreground = HexBrush("#B8A99E"), FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(labelBlock);
            valueBlock = new TextBlock
            {
                Text = initialValue,
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 2, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            sp.Children.Add(valueBlock);
            b.Child = sp;
            return b;
        }

        private Border CreateMiniIconBadge(string iconText)
        {
            Border b = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(5),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            subBoxes.Add(b);
            b.Child = new TextBlock
            {
                Text = iconText,
                Foreground = HexBrush("#C7B9AE"),
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            return b;
        }

        private Grid CreateTelemetrySparkRow(string icon, string label, string detail, bool useThemeAccent, out TextBlock labelBlock, out TextBlock detailBlock, out Polyline sparkline, out TextBlock pctBlock)
        {
            Grid g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel left = new StackPanel { Orientation = Orientation.Horizontal };
            left.Children.Add(CreateMiniIconBadge(icon));
            labelBlock = new TextBlock { Text = label, Foreground = HexBrush("#F5EFEA"), FontSize = 12, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(labelBlock);
            Grid.SetColumn(left, 0);
            g.Children.Add(left);

            detailBlock = new TextBlock
            {
                Text = detail,
                Foreground = HexBrush("#B8A99E"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(4, 0, 8, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(detailBlock, 1);
            g.Children.Add(detailBlock);

            Border sparkBox = new Border
            {
                Width = 46,
                Height = 16,
                CornerRadius = new CornerRadius(3),
                Background = HexBrush("#1F1916"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                ClipToBounds = true
            };
            Canvas c = new Canvas { Width = 46, Height = 16 };
            sparkline = new Polyline { StrokeThickness = 1.6 };
            if (useThemeAccent) accentSparklines.Add(sparkline);
            c.Children.Add(sparkline);
            sparkBox.Child = c;
            Grid.SetColumn(sparkBox, 2);
            g.Children.Add(sparkBox);

            pctBlock = new TextBlock
            {
                Text = "%6",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Width = 46,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (useThemeAccent) accentTextBlocks.Add(pctBlock);
            Grid.SetColumn(pctBlock, 3);
            g.Children.Add(pctBlock);

            return g;
        }

        private Grid CreateTelemetryBarRow(string icon, string label, string detail, bool useThemeAccent, out TextBlock labelBlock, out TextBlock detailBlock, out Border barFill, out TextBlock pctBlock)
        {
            Grid g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MaxWidth = 148 });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 0 });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid left = new Grid { VerticalAlignment = VerticalAlignment.Center };
            left.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            left.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 0 });
            FrameworkElement badge = CreateMiniIconBadge(icon);
            Grid.SetColumn(badge, 0);
            left.Children.Add(badge);
            labelBlock = new TextBlock
            {
                Text = label,
                Foreground = HexBrush("#F5EFEA"),
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            };
            Grid.SetColumn(labelBlock, 1);
            left.Children.Add(labelBlock);
            Grid.SetColumn(left, 0);
            g.Children.Add(left);

            detailBlock = new TextBlock
            {
                Text = detail,
                Foreground = HexBrush("#B8A99E"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(4, 0, 8, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            };
            Grid.SetColumn(detailBlock, 1);
            g.Children.Add(detailBlock);

            Border track = new Border
            {
                Width = 42,
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Background = HexBrush("#2E2520"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            barFill = new Border
            {
                Width = 20,
                Height = 4,
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            if (useThemeAccent) accentBarFills.Add(barFill);
            track.Child = barFill;
            Grid.SetColumn(track, 2);
            g.Children.Add(track);

            pctBlock = new TextBlock
            {
                Text = "%40",
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                MinWidth = 48,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (useThemeAccent) accentTextBlocks.Add(pctBlock);
            Grid.SetColumn(pctBlock, 3);
            g.Children.Add(pctBlock);

            return g;
        }

        private Grid CreateWirelessDeviceRow(string icon, string label, string detail, bool useThemeAccent, out TextBlock labelBlock, out TextBlock detailBlock, out Border barFill, out TextBlock pctBlock)
        {
            Grid g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 0 });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            FrameworkElement badge = CreateMiniIconBadge(icon);
            badge.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(badge, 0);
            g.Children.Add(badge);

            Grid infoGrid = new Grid { Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            labelBlock = new TextBlock
            {
                Text = label,
                Foreground = HexBrush("#F5EFEA"),
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                ToolTip = label
            };
            Grid.SetRow(labelBlock, 0);
            infoGrid.Children.Add(labelBlock);

            detailBlock = new TextBlock
            {
                Text = detail,
                Foreground = HexBrush("#B8A99E"),
                FontSize = 10,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                ToolTip = detail
            };
            Grid.SetRow(detailBlock, 1);
            infoGrid.Children.Add(detailBlock);

            Grid.SetColumn(infoGrid, 1);
            g.Children.Add(infoGrid);

            Border track = new Border
            {
                Width = 42,
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Background = HexBrush("#2E2520"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            barFill = new Border
            {
                Width = 20,
                Height = 4,
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            if (useThemeAccent) accentBarFills.Add(barFill);
            track.Child = barFill;
            Grid.SetColumn(track, 2);
            g.Children.Add(track);

            pctBlock = new TextBlock
            {
                Text = "%40",
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                MinWidth = 38,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (useThemeAccent) accentTextBlocks.Add(pctBlock);
            Grid.SetColumn(pctBlock, 3);
            g.Children.Add(pctBlock);

            return g;
        }

        private Button CreateStyledIconButton(string bgHex, string borderHex, string tooltip)
        {
            Button btn = new Button
            {
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Background = bgHex == "Transparent" ? Brushes.Transparent : HexBrush(bgHex),
                BorderBrush = borderHex == "Transparent" ? Brushes.Transparent : HexBrush(borderHex),
                BorderThickness = new Thickness(1)
            };

            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);

            template.VisualTree = border;
            btn.Template = template;
            return btn;
        }

        private Button CreateStretchSettingsButton(string bgHex, string borderHex, string tooltip)
        {
            Button btn = new Button
            {
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Background = bgHex == "Transparent" ? Brushes.Transparent : HexBrush(bgHex),
                BorderBrush = borderHex == "Transparent" ? Brushes.Transparent : HexBrush(borderHex),
                BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);

            template.VisualTree = border;
            btn.Template = template;
            return btn;
        }

        // ==========================================
        // SYSTEM TRAY & CACHED COREAUDIO CONTROLLER
        // ==========================================
        private void InitializeTrayAndAudio()
        {
            iconMicUnmuted = CreateModernMicTrayIcon(false);
            iconMicMuted = CreateModernMicTrayIcon(true);

            trayIcon = new System.Windows.Forms.NotifyIcon();
            trayIcon.Icon = iconMicUnmuted;
            trayIcon.Visible = true;

            // Cache CoreAudio endpoints off the UI thread so startup never stalls on audio service/COM init
            Task.Run(() =>
            {
                try
                {
                    var enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                    IMMDevice devConsole;
                    if (enumerator.GetDefaultAudioEndpoint(1, 0, out devConsole) == 0 && devConsole != null)
                    {
                        Guid iid = typeof(IAudioEndpointVolume).GUID;
                        object obj;
                        devConsole.Activate(ref iid, 23, IntPtr.Zero, out obj);
                        cachedMicVolumeConsole = (IAudioEndpointVolume)obj;
                    }
                    IMMDevice devComm;
                    if (enumerator.GetDefaultAudioEndpoint(1, 2, out devComm) == 0 && devComm != null)
                    {
                        Guid iid = typeof(IAudioEndpointVolume).GUID;
                        object obj;
                        devComm.Activate(ref iid, 23, IntPtr.Zero, out obj);
                        cachedMicVolumeComm = (IAudioEndpointVolume)obj;
                    }
                }
                catch { }
                try { Dispatcher.BeginInvoke(new Action(() => SyncMicrophoneStateFromOs(true))); } catch { }
            });

            var menu = new System.Windows.Forms.ContextMenuStrip();
            trayMuteItem = new System.Windows.Forms.ToolStripMenuItem(L("🎙 Mikrofonu Sustur / Aç (Sol Tık)", "🎙 Mute / Unmute Microphone (Left Click)"));
            trayMuteItem.Click += (s, e) => ToggleMicrophoneMute();
            menu.Items.Add(trayMuteItem);

            traySettingsItem = new System.Windows.Forms.ToolStripMenuItem(L("⚙ Widget Ayarları (Dil, Konum & Tema)...", "⚙ Widget Settings (Language, Location & Theme)..."));
            traySettingsItem.Font = new System.Drawing.Font(traySettingsItem.Font, System.Drawing.FontStyle.Bold);
            traySettingsItem.Click += (s, e) => OpenSettingsWindow();
            menu.Items.Add(traySettingsItem);

            trayCalendarItem = new System.Windows.Forms.ToolStripMenuItem(showCalendarWidget
                ? L("📅 Takvim Widget'ını Gizle", "📅 Hide Calendar Widget")
                : L("📅 Takvim Widget'ını Göster", "📅 Show Calendar Widget"));
            trayCalendarItem.Click += (s, e) => SetCalendarWidgetVisible(!showCalendarWidget);
            menu.Items.Add(trayCalendarItem);

            trayTimerItem = new System.Windows.Forms.ToolStripMenuItem(showTimerWidget
                ? L("⏱ Timer Widgeti'ni Gizle", "⏱ Hide Timer Widget")
                : L("⏱ Timer Widgeti'ni Göster", "⏱ Show Timer Widget"));
            trayTimerItem.Click += (s, e) => SetTimerWidgetVisible(!showTimerWidget);
            menu.Items.Add(trayTimerItem);

            trayTaskbarDevicesItem = new System.Windows.Forms.ToolStripMenuItem(showTaskbarDevices
                ? L("🔋 Görev Çubuğu Cihaz Şarjlarını Gizle", "🔋 Hide Taskbar Device Batteries")
                : L("🔋 Görev Çubuğu Cihaz Şarjlarını Göster", "🔋 Show Taskbar Device Batteries"));
            trayTaskbarDevicesItem.Click += (s, e) => SetTaskbarDevicesVisible(!showTaskbarDevices);
            menu.Items.Add(trayTaskbarDevicesItem);

            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            trayLockItem = new System.Windows.Forms.ToolStripMenuItem(L("🔓 Taşınabilir & Boyutlandırılabilir Modu Aç", "🔓 Enable Draggable & Resizable Mode"));
            trayLockItem.Click += (s, e) => ToggleDraggableMode();
            menu.Items.Add(trayLockItem);

            trayFitHeightItem = new System.Windows.Forms.ToolStripMenuItem(L("📐 Yüksekliği İçeriğe Göre Otomatik Sığdır", "📐 Auto-Fit Height to Content"));
            trayFitHeightItem.Click += (s, e) => FitHeightToContentIfCompact();
            menu.Items.Add(trayFitHeightItem);

            trayDockRightItem = new System.Windows.Forms.ToolStripMenuItem(L("➡ Sağ Kenara Hizala", "➡ Dock to Right Edge"));
            trayDockRightItem.Click += (s, e) => DockWindow("Right");
            menu.Items.Add(trayDockRightItem);

            trayDockLeftItem = new System.Windows.Forms.ToolStripMenuItem(L("⬅ Sol Kenara Hizala", "⬅ Dock to Left Edge"));
            trayDockLeftItem.Click += (s, e) => DockWindow("Left");
            menu.Items.Add(trayDockLeftItem);

            trayToggleVisItem = new System.Windows.Forms.ToolStripMenuItem(L("👁 Widget'ı Göster / Gizle", "👁 Show / Hide Widget"));
            trayToggleVisItem.Click += (s, e) =>
            {
                if (this.Visibility == Visibility.Visible && !userHidden)
                {
                    userHidden = true;
                    this.Visibility = Visibility.Hidden;
                }
                else
                {
                    userHidden = false;
                    this.Visibility = Visibility.Visible;
                }
            };
            menu.Items.Add(trayToggleVisItem);

            trayAutoStartItem = new System.Windows.Forms.ToolStripMenuItem(L("🚀 Windows Başlangıcında Otomatik Aç", "🚀 Launch Automatically on Windows Startup"))
            {
                Checked = IsAutoStartEnabled(),
                CheckOnClick = true
            };
            trayAutoStartItem.Click += (s, e) =>
            {
                EnsureAutoStart(trayAutoStartItem.Checked);
                ShowToast(trayAutoStartItem.Checked
                    ? L("Başlangıçta otomatik açılma aktif edildi", "Startup auto-launch enabled")
                    : L("Başlangıçta otomatik açılma kapatıldı", "Startup auto-launch disabled"));
            };
            menu.Items.Add(trayAutoStartItem);

            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            trayExitItem = new System.Windows.Forms.ToolStripMenuItem(L("✖ Çıkış", "✖ Exit"));
            trayExitItem.Click += (s, e) => Application.Current.Shutdown();
            menu.Items.Add(trayExitItem);

            trayIcon.ContextMenuStrip = menu;

            trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    ToggleMicrophoneMute();
                }
            };
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        private System.Drawing.Icon CreateModernMicTrayIcon(bool muted)
        {
            using (var bmp = new System.Drawing.Bitmap(32, 32))
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.Clear(System.Drawing.Color.Transparent);

                if (!muted)
                {
                    using (var whiteBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 250, 250, 252)))
                    using (var whitePen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(255, 250, 250, 252), 2.8f))
                    {
                        whitePen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                        whitePen.EndCap = System.Drawing.Drawing2D.LineCap.Round;

                        g.FillRectangle(whiteBrush, 11, 6, 10, 11);
                        g.FillEllipse(whiteBrush, 11, 2, 10, 8);
                        g.FillEllipse(whiteBrush, 11, 13, 10, 8);

                        g.DrawArc(whitePen, 7, 8, 18, 15, 0, 180);
                        g.DrawLine(whitePen, 16, 23, 16, 28);
                        g.DrawLine(whitePen, 10, 28, 22, 28);
                    }
                }
                else
                {
                    using (var dimWhiteBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(215, 240, 240, 240)))
                    using (var dimWhitePen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(215, 240, 240, 240), 2.6f))
                    {
                        dimWhitePen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                        dimWhitePen.EndCap = System.Drawing.Drawing2D.LineCap.Round;

                        g.FillRectangle(dimWhiteBrush, 11, 6, 10, 11);
                        g.FillEllipse(dimWhiteBrush, 11, 2, 10, 8);
                        g.FillEllipse(dimWhiteBrush, 11, 13, 10, 8);

                        g.DrawArc(dimWhitePen, 7, 8, 18, 15, 0, 180);
                        g.DrawLine(dimWhitePen, 16, 23, 16, 28);
                        g.DrawLine(dimWhitePen, 10, 28, 22, 28);
                    }

                    using (var shadowPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(220, 20, 20, 20), 5.5f))
                    using (var redSlashPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(255, 244, 63, 94), 3.4f))
                    {
                        shadowPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                        shadowPen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                        redSlashPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                        redSlashPen.EndCap = System.Drawing.Drawing2D.LineCap.Round;

                        g.DrawLine(shadowPen, 5, 5, 27, 27);
                        g.DrawLine(redSlashPen, 5, 5, 27, 27);
                    }
                }

                IntPtr hIcon = bmp.GetHicon();
                System.Drawing.Icon cloned = (System.Drawing.Icon)System.Drawing.Icon.FromHandle(hIcon).Clone();
                DestroyIcon(hIcon);
                return cloned;
            }
        }

        private void SyncMicrophoneStateFromOs(bool forceUiUpdate)
        {
            bool prev = isMicMuted;
            try
            {
                if (cachedMicVolumeConsole != null)
                {
                    bool muted;
                    cachedMicVolumeConsole.GetMute(out muted);
                    isMicMuted = muted;
                }
            }
            catch { }
            if (forceUiUpdate || prev != isMicMuted)
            {
                UpdateMicrophoneUi();
            }
        }

        private void ToggleMicrophoneMute()
        {
            bool targetMute = !isMicMuted;
            try
            {
                Guid ctx = Guid.Empty;
                if (cachedMicVolumeConsole != null) cachedMicVolumeConsole.SetMute(targetMute, ref ctx);
                if (cachedMicVolumeComm != null) cachedMicVolumeComm.SetMute(targetMute, ref ctx);
                isMicMuted = targetMute;
            }
            catch { }

            UpdateMicrophoneUi();
            ShowToast(isMicMuted
                ? L("🔇 Mikrofon Susturuldu (Muted)", "🔇 Microphone Muted")
                : L("🎙 Mikrofon Açıldı (Canlı)", "🎙 Microphone Unmuted (Live)"));
        }

        private void UpdateMicrophoneUi()
        {
            Dispatcher.Invoke(() =>
            {
                if (trayIcon != null)
                {
                    trayIcon.Icon = isMicMuted ? iconMicMuted : iconMicUnmuted;
                    trayIcon.Text = isMicMuted
                        ? L("Mikrofon: KAPALI (Susturuldu) - Açmak için tıkla", "Microphone: MUTED - Click to unmute")
                        : L("Mikrofon: AÇIK (Canlı) - Susturmak için tıkla", "Microphone: LIVE - Click to mute");
                }
                if (txtMicHeaderIcon != null && btnMicHeader != null)
                {
                    if (isMicMuted)
                    {
                        txtMicHeaderIcon.Text = "🔇";
                        txtMicHeaderIcon.Foreground = HexBrush("#FB7185");
                        btnMicHeader.Background = HexBrush("#3B1622");
                        btnMicHeader.BorderBrush = HexBrush("#E11D48");
                        btnMicHeader.ToolTip = L("Mikrofon Sessiz (Açmak için tıklayın veya Ctrl+Shift+M)", "Microphone Muted (Click to unmute or Ctrl+Shift+M)");
                    }
                    else
                    {
                        txtMicHeaderIcon.Text = "🎙";
                        txtMicHeaderIcon.Foreground = HexBrush("#FAF6F2");
                        btnMicHeader.Background = HexBrush("#26201D");
                        btnMicHeader.BorderBrush = Brushes.Transparent;
                        btnMicHeader.ToolTip = L("Mikrofon Açık (Susturmak için tıklayın veya Ctrl+Shift+M)", "Microphone Live (Click to mute or Ctrl+Shift+M)");
                    }
                }
            });
        }

        public void ApplyLanguage()
        {
            try
            {
                if (txtWeatherSectionTitle != null) txtWeatherSectionTitle.Text = L("Hava Durumu", "Weather");
                if (txtLocationBadge != null) txtLocationBadge.ToolTip = L("Hava durumunu yenilemek için sol tıklayın, konumu değiştirmek için Ayarlar (⚙) menüsünü açın", "Left-click to refresh weather, open Settings (⚙) to change location");
                if (txtHumidityLabel != null) txtHumidityLabel.Text = L("💧 Nem", "💧 Humidity");
                if (txtWindLabel != null) txtWindLabel.Text = L("💨 Rüzgar", "💨 Wind");
                if (txtRainLabel != null) txtRainLabel.Text = L("🌧 Yağış", "🌧 Rain");
                if (txtMonitorSectionTitle != null) txtMonitorSectionTitle.Text = L("Monitör Parlaklığı", "Monitor Brightness");
                if (btnRefreshMonitors != null) btnRefreshMonitors.ToolTip = L("Monitörleri ve parlaklık seviyelerini yeniden algıla", "Re-detect monitors and brightness levels");
                UpdateMonitorLinkButtonUi();
                RebuildMonitorSlidersUi();
                if (txtWirelessSectionTitle != null) txtWirelessSectionTitle.Text = L("Kablosuz Cihazlar", "Wireless Devices");
                if (btnRefreshWireless != null) btnRefreshWireless.ToolTip = L("Kablosuz cihaz şarjlarını şimdi yenile", "Refresh wireless device battery levels now");
                if (txtTelemetrySectionTitle != null) txtTelemetrySectionTitle.Text = L("Telemetri", "Telemetry");
                if (txtCpuRowLabel != null) txtCpuRowLabel.Text = L("İşlemci (CPU)", "Processor (CPU)");
                if (txtRamRowLabel != null) txtRamRowLabel.Text = L("Bellek (RAM)", "Memory (RAM)");
                if (txtGpuRowLabel != null) txtGpuRowLabel.Text = L("Ekran Kartı (GPU)", "Graphics (GPU)");
                if (txtVramRowLabel != null) txtVramRowLabel.Text = L("Video Belleği (VRAM)", "Video Memory (VRAM)");
                if (txtNetRowLabel != null) txtNetRowLabel.Text = L("Ağ Trafiği", "Network");

                for (int i = 0; i < fixedDrives.Count && i < diskRowLabels.Count; i++)
                {
                    string dName = fixedDrives[i].Name.TrimEnd('\\');
                    diskRowLabels[i].Text = dName + L(" Diski", " Drive");
                }

                if (txtClipboardSectionTitle != null) txtClipboardSectionTitle.Text = L("Pano Geçmişi", "Clipboard History");
                if (btnClearClip != null) btnClearClip.ToolTip = L("Pano geçmişini temizle", "Clear clipboard history");
                if (btnSettingsHeader != null) btnSettingsHeader.ToolTip = L("Widget Ayarları (Dil, Konum, Tema & Düzen)", "Widget Settings (Language, Location, Theme & Layout)");

                if (trayMuteItem != null) trayMuteItem.Text = L("🎙 Mikrofonu Sustur / Aç (Sol Tık)", "🎙 Mute / Unmute Microphone (Left Click)");
                if (traySettingsItem != null) traySettingsItem.Text = L("⚙ Widget Ayarları (Dil, Konum, Tema & Düzen)...", "⚙ Widget Settings (Language, Location, Theme & Layout)...");
                if (trayCalendarItem != null) trayCalendarItem.Text = showCalendarWidget ? L("📅 Takvim Widget'ını Gizle", "📅 Hide Calendar Widget") : L("📅 Takvim Widget'ını Göster", "📅 Show Calendar Widget");
                if (trayTimerItem != null) trayTimerItem.Text = showTimerWidget ? L("⏱ Timer Widgeti'ni Gizle", "⏱ Hide Timer Widget") : L("⏱ Timer Widgeti'ni Göster", "⏱ Show Timer Widget");
                if (trayTaskbarDevicesItem != null) trayTaskbarDevicesItem.Text = showTaskbarDevices ? L("🔋 Görev Çubuğu Cihaz Şarjlarını Gizle", "🔋 Hide Taskbar Device Batteries") : L("🔋 Görev Çubuğu Cihaz Şarjlarını Göster", "🔋 Show Taskbar Device Batteries");
                if (trayLockItem != null)
                {
                    trayLockItem.Text = isDraggable
                        ? L("📌 Konumu ve Boyutu Kilitle (Sabit Mod)", "📌 Lock Position & Size (Fixed Mode)")
                        : L("🔓 Taşınabilir & Boyutlandırılabilir Modu Aç", "🔓 Enable Draggable & Resizable Mode");
                }
                if (trayFitHeightItem != null) trayFitHeightItem.Text = L("📐 Yüksekliği İçeriğe Göre Otomatik Sığdır", "📐 Auto-Fit Height to Content");
                if (trayDockRightItem != null) trayDockRightItem.Text = L("➡ Sağ Kenara Hizala", "➡ Dock to Right Edge");
                if (trayDockLeftItem != null) trayDockLeftItem.Text = L("⬅ Sol Kenara Hizala", "⬅ Dock to Left Edge");
                if (trayToggleVisItem != null) trayToggleVisItem.Text = L("👁 Widget'ı Göster / Gizle", "👁 Show / Hide Widget");
                if (trayAutoStartItem != null) trayAutoStartItem.Text = L("🚀 Windows Başlangıcında Otomatik Aç", "🚀 Launch Automatically on Windows Startup");
                if (trayExitItem != null) trayExitItem.Text = L("✖ Çıkış", "✖ Exit");

                UpdateMicrophoneUi();
                UpdateDraggableButtonUi();
                UpdateClockAndUptime();
                UpdateTelemetryFast();
                RefreshClipboardUi();
                if (calendarWindow != null)
                {
                    lastRightScheduleSignature = "";
                    lastLeftColumnSignature = "";
                    BuildCalendarWidgetUi();
                    ApplyCalendarWidgetTheme();
                }
                if (timerWindow != null)
                {
                    cachedHeatmapAccentHex = "";
                    lastPlayBtnBrushKey = "";
                    lastRenderedLitTicks = -1;
                    heatmapCellPool.Clear();
                    heatmapMonthPool.Clear();
                    BuildTimerWidgetUi();
                    ApplyTimerWidgetTheme();
                }
                if (refreshTimerDetailedViewAction != null) refreshTimerDetailedViewAction();
                Task.Run(() =>
                {
                    FetchWeatherAsync();
                    PollWirelessDevicesAsync();
                });
            }
            catch { }
        }

        // ==========================================
        // DRAGGABLE & RESIZABLE MODE + SETTINGS WINDOW
        // ==========================================
        private void ToggleDraggableMode()
        {
            isDraggable = !isDraggable;
            if (isDraggable)
            {
                this.ResizeMode = ResizeMode.CanResize;
                if (windowChrome != null) windowChrome.ResizeBorderThickness = new Thickness(8);
                if (resizeGripIndicator != null) resizeGripIndicator.Visibility = Visibility.Visible;
                if (calendarWindow != null)
                {
                    calendarWindow.ResizeMode = ResizeMode.CanResize;
                    if (calWindowChrome != null) calWindowChrome.ResizeBorderThickness = new Thickness(8);
                    if (calResizeGrip != null) calResizeGrip.Visibility = Visibility.Visible;
                }
                if (timerWindow != null)
                {
                    timerWindow.ResizeMode = ResizeMode.CanResize;
                    if (timerWindowChrome != null) timerWindowChrome.ResizeBorderThickness = new Thickness(8);
                    if (timerResizeGrip != null) timerResizeGrip.Visibility = Visibility.Visible;
                }
                if (trayLockItem != null) trayLockItem.Text = L("📌 Konumu ve Boyutu Kilitle (Sabit Mod)", "📌 Lock Position & Size (Fixed Mode)");
                ShowToast(L("Taşınabilir Mod Açık: Sürükleyebilir veya kenarlardan boyutlandırabilirsiniz", "Unlocked Mode: Drag anywhere or resize from edges"));
            }
            else
            {
                this.ResizeMode = ResizeMode.NoResize;
                if (windowChrome != null) windowChrome.ResizeBorderThickness = new Thickness(0);
                if (resizeGripIndicator != null) resizeGripIndicator.Visibility = Visibility.Collapsed;
                if (calendarWindow != null)
                {
                    calendarWindow.ResizeMode = ResizeMode.NoResize;
                    if (calWindowChrome != null) calWindowChrome.ResizeBorderThickness = new Thickness(0);
                    if (calResizeGrip != null) calResizeGrip.Visibility = Visibility.Collapsed;
                }
                if (timerWindow != null)
                {
                    timerWindow.ResizeMode = ResizeMode.NoResize;
                    if (timerWindowChrome != null) timerWindowChrome.ResizeBorderThickness = new Thickness(0);
                    if (timerResizeGrip != null) timerResizeGrip.Visibility = Visibility.Collapsed;
                }
                if (trayLockItem != null) trayLockItem.Text = L("🔓 Taşınabilir & Boyutlandırılabilir Modu Aç", "🔓 Enable Draggable & Resizable Mode");
                SaveSettings();
                ShowToast(L("Konum ve boyut kilitlendi", "Position and size locked"));
            }
            UpdateDraggableButtonUi();
        }

        private void UpdateDraggableButtonUi()
        {
            if (btnLockDrag != null)
            {
                if (isDraggable)
                {
                    btnLockDrag.Content = new TextBlock { Text = "🔓", FontSize = 11.5, Foreground = HexBrush(accentColorHex), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    btnLockDrag.Background = HexBrush("#33251D");
                    btnLockDrag.BorderBrush = HexBrush(accentColorHex);
                    btnLockDrag.ToolTip = L("Taşınabilir & Boyutlandırılabilir Mod Açık (Sabitlemek için tıklayın)", "Draggable & Resizable Mode Active (Click to lock)");
                }
                else
                {
                    btnLockDrag.Content = new TextBlock { Text = "📌", FontSize = 11.5, Foreground = HexBrush("#C7B9AE"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    btnLockDrag.Background = HexBrush("#26201D");
                    btnLockDrag.BorderBrush = Brushes.Transparent;
                    btnLockDrag.ToolTip = L("Sabit Mod (Taşınabilir ve Boyutlandırılabilir Modu Açmak İçin Tıklayın)", "Locked Mode (Click to Enable Draggable & Resizable Mode)");
                }
            }
            if (btnCalLockDrag != null)
            {
                if (isDraggable)
                {
                    btnCalLockDrag.Content = new TextBlock { Text = "🔓", FontSize = 10.5, Foreground = HexBrush(accentColorHex), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    btnCalLockDrag.Background = HexBrush("#33251D");
                    btnCalLockDrag.BorderBrush = HexBrush(accentColorHex);
                }
                else
                {
                    btnCalLockDrag.Content = new TextBlock { Text = "📌", FontSize = 10.5, Foreground = HexBrush("#C7B9AE"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    btnCalLockDrag.Background = HexBrush("#26201D");
                    btnCalLockDrag.BorderBrush = Brushes.Transparent;
                }
            }
            if (btnTimerLockDrag != null)
            {
                if (isDraggable)
                {
                    btnTimerLockDrag.Content = new TextBlock { Text = "🔓", FontSize = 10.5, Foreground = HexBrush(accentColorHex), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    btnTimerLockDrag.Background = HexBrush("#33251D");
                    btnTimerLockDrag.BorderBrush = HexBrush(accentColorHex);
                }
                else
                {
                    btnTimerLockDrag.Content = new TextBlock { Text = "📌", FontSize = 10.5, Foreground = HexBrush("#C7B9AE"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    btnTimerLockDrag.Background = HexBrush("#26201D");
                    btnTimerLockDrag.BorderBrush = Brushes.Transparent;
                }
            }
        }

        private void DockWindow(string side)
        {
            Rect workArea = GetWindowMonitorWorkArea(this);
            this.Top = workArea.Top + 14;
            if (side == "Left")
            {
                this.Left = workArea.Left + 14;
            }
            else
            {
                this.Left = workArea.Right - this.ActualWidth - 14;
            }
            savedLeft = this.Left;
            savedTop = this.Top;
            SaveSettings();
        }

        private string GetSectionDisplayName(string key)
        {
            if (key == "weather") return L("☀  Hava Durumu", "☀  Weather");
            if (key == "monitors") return L("🖥  Monitör Parlaklığı", "🖥  Monitor Brightness");
            if (key == "wireless") return L("🔋  Kablosuz Cihazlar", "🔋  Wireless Devices");
            if (key == "telemetry") return L("⚙  Telemetri", "⚙  Telemetry");
            if (key == "clipboard") return L("📋  Pano Geçmişi", "📋  Clipboard History");
            return key;
        }

        private bool IsSectionEnabled(string key)
        {
            if (key == "weather") return showSecWeather;
            if (key == "monitors") return showSecMonitors;
            if (key == "wireless") return showSecWireless;
            if (key == "telemetry") return showSecTelemetry;
            if (key == "clipboard") return showSecClipboard;
            return true;
        }

        private void SetSectionEnabled(string key, bool val)
        {
            if (key == "weather") showSecWeather = val;
            else if (key == "monitors") showSecMonitors = val;
            else if (key == "wireless") showSecWireless = val;
            else if (key == "telemetry") showSecTelemetry = val;
            else if (key == "clipboard") showSecClipboard = val;
        }

        private Border CreateSettingsSectionCard(string sectionTitle, string subtitle)
        {
            Border secCard = new Border
            {
                CornerRadius = new CornerRadius(11),
                Background = HexBrush("#251F1B"),
                BorderBrush = HexBrush("#3B302A"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(13, 11, 13, 11),
                Margin = new Thickness(0, 0, 0, 10)
            };
            StackPanel sp = new StackPanel();
            if (!string.IsNullOrEmpty(sectionTitle))
            {
                sp.Children.Add(new TextBlock
                {
                    Text = sectionTitle,
                    Foreground = HexBrush("#FAF6F2"),
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, string.IsNullOrEmpty(subtitle) ? 8 : 2)
                });
            }
            if (!string.IsNullOrEmpty(subtitle))
            {
                sp.Children.Add(new TextBlock
                {
                    Text = subtitle,
                    Foreground = HexBrush("#9E8E82"),
                    FontSize = 10.5,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 8)
                });
            }
            secCard.Child = sp;
            return secCard;
        }

        private void OpenSettingsWindow()
        {
            if (settingsWindow != null && settingsWindow.IsVisible)
            {
                settingsWindow.Activate();
                return;
            }

            if (currentSettingsTab < 0 || currentSettingsTab > 2) currentSettingsTab = 0;

            double maxH = Math.Min(650, SystemParameters.WorkArea.Height - 40);
            settingsWindow = new Window
            {
                Title = L("OpenWidget Ayarları", "OpenWidget Settings"),
                Width = 450,
                Height = Math.Max(460, maxH),
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize,
                Topmost = true,
                UseLayoutRounding = true,
                SnapsToDevicePixels = true,
                FontFamily = new FontFamily("Segoe UI")
            };
            TextOptions.SetTextFormattingMode(settingsWindow, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(settingsWindow, TextRenderingMode.ClearType);
            Program.TryApplyWindowIcon(settingsWindow);

            Border card = new Border
            {
                CornerRadius = new CornerRadius(15),
                Background = HexBrush("#F71B1613"),
                BorderBrush = HexBrush(accentColorHex),
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(8)
            };
            card.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 16, Opacity = 0.68 };

            // 3-Row Grid: Fixed Sticky Header + Segmented Tab Bar + Smooth ScrollViewer Content
            Grid rootLayout = new Grid();
            rootLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Row 0: Sticky Draggable Header
            Grid hdr = new Grid { Margin = new Thickness(0, 0, 0, 11), Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
            hdr.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    try { settingsWindow.DragMove(); } catch { }
                }
            };
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel hdrTitleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            hdrTitleStack.Children.Add(new TextBlock
            {
                Text = L("⚙ OpenWidget Ayarları", "⚙ OpenWidget Settings"),
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 15,
                FontWeight = FontWeights.Bold
            });
            hdrTitleStack.Children.Add(new TextBlock
            {
                Text = L("Görünüm, konum, masaüstü eklentileri ve modül düzeni", "Appearance, location, companion widgets & module layout"),
                Foreground = HexBrush("#9E8E82"),
                FontSize = 10.5,
                Margin = new Thickness(0, 1, 0, 0)
            });
            Grid.SetColumn(hdrTitleStack, 0);
            hdr.Children.Add(hdrTitleStack);

            Button btnClose = CreateStyledIconButton("#2E2520", "#4A3C34", L("Kapat", "Close"));
            btnClose.Padding = new Thickness(9, 4, 9, 4);
            btnClose.VerticalAlignment = VerticalAlignment.Center;
            btnClose.Content = new TextBlock { Text = "✕", Foreground = HexBrush("#C7B9AE"), FontSize = 11.5 };
            btnClose.Click += (s, e) => settingsWindow.Close();
            Grid.SetColumn(btnClose, 1);
            hdr.Children.Add(btnClose);
            Grid.SetRow(hdr, 0);
            rootLayout.Children.Add(hdr);

            // Row 1: Modern 3-Tab Segmented Pill Bar
            Border tabShell = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = HexBrush("#14100E"),
                BorderBrush = HexBrush("#332924"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(3),
                Margin = new Thickness(0, 0, 0, 10)
            };
            UniformGrid tabBar = new UniformGrid { Columns = 3 };
            Button[] tabBtns = new Button[3];
            TextBlock[] tabLbls = new TextBlock[3];
            string[] tabTitles = {
                L("🎨 Görünüm", "🎨 Appearance"),
                L("✨ Ek Widget'lar", "✨ Widgets"),
                L("🧩 Modül Düzeni", "🧩 Modules")
            };
            string[] tabTips = {
                L("Dil, hava durumu konumu, tema rengi ve saydamlık", "Language, weather location, theme color & opacity"),
                L("Takvim Widget ve Timer Widget eklentileri", "Calendar Widget & Timer Widget companions"),
                L("Ana widget modül sıralaması ve telemetri öğeleri", "Main widget module order & telemetry metrics")
            };
            for (int ti = 0; ti < 3; ti++)
            {
                Button tb = CreateStyledIconButton("Transparent", "Transparent", tabTips[ti]);
                tb.Padding = new Thickness(6, 6, 6, 6);
                tb.Margin = new Thickness(ti == 0 ? 0 : 2, 0, ti == 2 ? 0 : 2, 0);
                TextBlock lbl = new TextBlock
                {
                    Text = tabTitles[ti],
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                tb.Content = lbl;
                tabBtns[ti] = tb;
                tabLbls[ti] = lbl;
                tabBar.Children.Add(tb);
            }
            tabShell.Child = tabBar;
            Grid.SetRow(tabShell, 1);
            rootLayout.Children.Add(tabShell);

            // Row 2: Smooth ScrollViewer containing Tab Panels
            ScrollViewer settingsScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                PanningMode = PanningMode.VerticalOnly,
                Padding = new Thickness(0, 0, 4, 0)
            };
            ApplyThinDarkScrollBarStyle(settingsScroll);
            Grid.SetRow(settingsScroll, 2);
            rootLayout.Children.Add(settingsScroll);

            StackPanel scrollContentHost = new StackPanel { Margin = new Thickness(0, 0, 2, 4) };
            settingsScroll.Content = scrollContentHost;

            StackPanel[] tabPanels = new StackPanel[] { new StackPanel(), new StackPanel(), new StackPanel() };
            scrollContentHost.Children.Add(tabPanels[0]);
            scrollContentHost.Children.Add(tabPanels[1]);
            scrollContentHost.Children.Add(tabPanels[2]);

            Action syncAccentControls = null;
            Action syncTabsUi = () =>
            {
                for (int ti = 0; ti < 3; ti++)
                {
                    bool active = (currentSettingsTab == ti);
                    tabPanels[ti].Visibility = active ? Visibility.Visible : Visibility.Collapsed;
                    tabBtns[ti].Background = HexBrush(active ? "#38281E" : "Transparent");
                    tabBtns[ti].BorderBrush = HexBrush(active ? accentColorHex : "Transparent");
                    tabLbls[ti].Foreground = HexBrush(active ? "#FAF6F2" : "#9E8E82");
                }
                if (syncAccentControls != null) syncAccentControls();
            };
            for (int ti = 0; ti < 3; ti++)
            {
                int capturedTi = ti;
                tabBtns[ti].Click += (s, e) =>
                {
                    currentSettingsTab = capturedTi;
                    syncTabsUi();
                    settingsScroll.ScrollToTop();
                };
            }

            // =========================================================
            // TAB 0: APPEARANCE, LANGUAGE & WEATHER LOCATION
            // =========================================================
            Border langLocCard = CreateSettingsSectionCard(L("🌐 Dil & Hava Durumu", "🌐 Language & Weather"), L("Arayüz dili, sıcaklık birimi ve şehir konumu", "Interface language, temperature unit & city location"));
            StackPanel langLocStack = (StackPanel)langLocCard.Child;

            Grid langRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            langRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            langRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            langRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            langRow.Children.Add(new TextBlock
            {
                Text = L("Arayüz Dili", "Interface Language"),
                Foreground = HexBrush("#C7B9AE"),
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 10, 0)
            });

            Button btnSetTr = CreateStyledIconButton(IsEn ? "#1D1815" : "#38281E", IsEn ? "#4A3C34" : accentColorHex, "Türkçe");
            btnSetTr.Margin = new Thickness(0, 0, 6, 0);
            btnSetTr.Padding = new Thickness(10, 4, 10, 4);
            btnSetTr.Content = new TextBlock { Text = "🇹🇷 Türkçe", Foreground = HexBrush("#FAF6F2"), FontSize = 11, FontWeight = FontWeights.SemiBold };
            btnSetTr.Click += (s, e) =>
            {
                if (appLanguage != "TR")
                {
                    appLanguage = "TR";
                    SaveSettings();
                    ApplyLanguage();
                    settingsWindow.Close();
                    OpenSettingsWindow();
                }
            };
            Grid.SetColumn(btnSetTr, 1);
            langRow.Children.Add(btnSetTr);

            Button btnSetEn = CreateStyledIconButton(IsEn ? "#38281E" : "#1D1815", IsEn ? accentColorHex : "#4A3C34", "English");
            btnSetEn.Padding = new Thickness(10, 4, 10, 4);
            btnSetEn.Content = new TextBlock { Text = "🇬🇧 English", Foreground = HexBrush("#FAF6F2"), FontSize = 11, FontWeight = FontWeights.SemiBold };
            btnSetEn.Click += (s, e) =>
            {
                if (appLanguage != "EN")
                {
                    appLanguage = "EN";
                    SaveSettings();
                    ApplyLanguage();
                    settingsWindow.Close();
                    OpenSettingsWindow();
                }
            };
            Grid.SetColumn(btnSetEn, 2);
            langRow.Children.Add(btnSetEn);
            langLocStack.Children.Add(langRow);

            Grid tempUnitRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            tempUnitRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tempUnitRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            tempUnitRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            tempUnitRow.Children.Add(new TextBlock
            {
                Text = L("Sıcaklık Birimi", "Temperature Unit"),
                Foreground = HexBrush("#C7B9AE"),
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 10, 0)
            });

            Button btnSetCelsius = CreateStyledIconButton(!useFahrenheit ? "#38281E" : "#1D1815", !useFahrenheit ? accentColorHex : "#4A3C34", "Celsius (°C)");
            btnSetCelsius.Margin = new Thickness(0, 0, 6, 0);
            btnSetCelsius.Padding = new Thickness(10, 4, 10, 4);
            btnSetCelsius.Content = new TextBlock { Text = "°C", Foreground = HexBrush("#FAF6F2"), FontSize = 11, FontWeight = FontWeights.SemiBold };

            Button btnSetFahrenheit = CreateStyledIconButton(useFahrenheit ? "#38281E" : "#1D1815", useFahrenheit ? accentColorHex : "#4A3C34", "Fahrenheit (°F)");
            btnSetFahrenheit.Padding = new Thickness(10, 4, 10, 4);
            btnSetFahrenheit.Content = new TextBlock { Text = "°F", Foreground = HexBrush("#FAF6F2"), FontSize = 11, FontWeight = FontWeights.SemiBold };

            Action syncTempUnitButtons = () =>
            {
                btnSetCelsius.Background = HexBrush(!useFahrenheit ? "#38281E" : "#1D1815");
                btnSetCelsius.BorderBrush = HexBrush(!useFahrenheit ? accentColorHex : "#4A3C34");
                btnSetFahrenheit.Background = HexBrush(useFahrenheit ? "#38281E" : "#1D1815");
                btnSetFahrenheit.BorderBrush = HexBrush(useFahrenheit ? accentColorHex : "#4A3C34");
            };

            btnSetCelsius.Click += (s, e) =>
            {
                if (useFahrenheit)
                {
                    useFahrenheit = false;
                    syncTempUnitButtons();
                    SaveSettings();
                    RefreshWeatherTemperatureUi();
                    ShowToast(L("Sıcaklık birimi: °C (Celsius)", "Temperature unit: °C (Celsius)"));
                }
            };
            Grid.SetColumn(btnSetCelsius, 1);
            tempUnitRow.Children.Add(btnSetCelsius);

            btnSetFahrenheit.Click += (s, e) =>
            {
                if (!useFahrenheit)
                {
                    useFahrenheit = true;
                    syncTempUnitButtons();
                    SaveSettings();
                    RefreshWeatherTemperatureUi();
                    ShowToast(L("Sıcaklık birimi: °F (Fahrenheit)", "Temperature unit: °F (Fahrenheit)"));
                }
            };
            Grid.SetColumn(btnSetFahrenheit, 2);
            tempUnitRow.Children.Add(btnSetFahrenheit);
            langLocStack.Children.Add(tempUnitRow);

            langLocStack.Children.Add(new TextBlock { Text = L("Hava Durumu Konumu (GPS veya Şehir/İlçe Adı)", "Weather Location (GPS or City Name)"), Foreground = HexBrush("#B8A99E"), FontSize = 11, Margin = new Thickness(0, 0, 0, 5) });
            Grid locRow = new Grid();
            locRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            locRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            locRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBox txtCity = new TextBox
            {
                Text = locationName,
                Background = HexBrush("#1C1714"),
                Foreground = HexBrush("#FAF6F2"),
                BorderBrush = HexBrush("#4A3C34"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 5, 8, 5),
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(txtCity, 0);
            locRow.Children.Add(txtCity);

            Button btnSaveCity = CreateStyledIconButton("#38281E", accentColorHex, L("Yazılan şehri kaydet", "Save entered city"));
            btnSaveCity.Margin = new Thickness(6, 0, 0, 0);
            btnSaveCity.Padding = new Thickness(10, 5, 10, 5);
            btnSaveCity.Content = new TextBlock { Text = L("Şehir Ara", "Search"), Foreground = HexBrush("#FAF6F2"), FontSize = 11, FontWeight = FontWeights.SemiBold };
            btnSaveCity.Click += (s, e) =>
            {
                string q = txtCity.Text.Trim();
                Task.Run(() =>
                {
                    GeoLookupResult r = LocationHelper.SearchCityByName(q, appLanguage);
                    if (r.Success)
                    {
                        locationMode = "Manual";
                        locationName = r.DisplayName;
                        locationLat = r.Latitude;
                        locationLon = r.Longitude;
                        Dispatcher.Invoke(() =>
                        {
                            txtLocationBadge.Text = locationName;
                            txtCity.Text = locationName;
                        });
                        SaveSettings();
                        FetchWeatherAsync();
                        ShowToast(L("Konum güncellendi: ", "Location updated: ") + locationName);
                    }
                    else
                    {
                        ShowToast(L("Şehir bulunamadı!", "City not found!"));
                    }
                });
            };
            Grid.SetColumn(btnSaveCity, 1);
            locRow.Children.Add(btnSaveCity);

            Button btnAutoGps = CreateStyledIconButton("#2A221E", "#4A3C34", L("GPS / Konum Servisi ile otomatik bul", "Auto-detect via GPS / Location Services"));
            btnAutoGps.Margin = new Thickness(6, 0, 0, 0);
            btnAutoGps.Padding = new Thickness(9, 5, 9, 5);
            btnAutoGps.Content = new TextBlock { Text = "📍 GPS", Foreground = HexBrush("#FAF6F2"), FontSize = 11 };
            btnAutoGps.Click += (s, e) =>
            {
                Task.Run(() =>
                {
                    GeoLookupResult r = LocationHelper.ResolveAutoLocation(appLanguage);
                    if (r.Success)
                    {
                        locationMode = "Auto";
                        locationName = r.DisplayName;
                        locationLat = r.Latitude;
                        locationLon = r.Longitude;
                        Dispatcher.Invoke(() =>
                        {
                            txtLocationBadge.Text = locationName;
                            txtCity.Text = locationName;
                        });
                        SaveSettings();
                        FetchWeatherAsync();
                        ShowToast(L("Otomatik konum: ", "Auto location: ") + locationName);
                    }
                });
            };
            Grid.SetColumn(btnAutoGps, 2);
            locRow.Children.Add(btnAutoGps);
            langLocStack.Children.Add(locRow);
            tabPanels[0].Children.Add(langLocCard);

            // Color Picker Card
            Border colorSecCard = CreateSettingsSectionCard(L("🎨 Ana Tema Rengi", "🎨 Theme Accent Color"), L("Tüm widget çerçeveleri ve grafikler seçilen renge uyarlanır", "All widget borders, charts & activity heatmap adapt to this color"));
            StackPanel colorSecStack = (StackPanel)colorSecCard.Child;

            Border previewSwatch = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(7),
                Background = HexBrush(accentColorHex),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1.5),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBox txtHexInput = new TextBox
            {
                Text = accentColorHex,
                Background = HexBrush("#1C1714"),
                Foreground = HexBrush("#FAF6F2"),
                BorderBrush = HexBrush("#4A3C34"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 5, 8, 5),
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            FrameworkElement spectrumPicker = CreateInteractiveSpectrumPicker(accentColorHex, (pickedHex) =>
            {
                txtHexInput.Text = pickedHex;
                previewSwatch.Background = HexBrush(pickedHex);
                ApplyTheme(pickedHex, bgOpacityPercent);
                card.BorderBrush = HexBrush(pickedHex);
                syncTabsUi();
            });
            spectrumPicker.HorizontalAlignment = HorizontalAlignment.Center;
            colorSecStack.Children.Add(spectrumPicker);

            Grid colorCustomRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            colorCustomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            colorCustomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            colorCustomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            colorCustomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(previewSwatch, 0);
            colorCustomRow.Children.Add(previewSwatch);
            Grid.SetColumn(txtHexInput, 1);
            colorCustomRow.Children.Add(txtHexInput);

            Button btnApplyHex = CreateStyledIconButton("#38281E", accentColorHex, L("Kodu Uygula", "Apply Hex Code"));
            btnApplyHex.Margin = new Thickness(6, 0, 0, 0);
            btnApplyHex.Padding = new Thickness(10, 5, 10, 5);
            btnApplyHex.Content = new TextBlock { Text = L("Uygula", "Apply"), Foreground = HexBrush("#FAF6F2"), FontSize = 11.5, FontWeight = FontWeights.SemiBold };
            btnApplyHex.Click += (s, e) =>
            {
                string val = txtHexInput.Text.Trim();
                if (!val.StartsWith("#")) val = "#" + val;
                try
                {
                    ParseColor(val);
                    previewSwatch.Background = HexBrush(val);
                    ApplyTheme(val, bgOpacityPercent);
                    card.BorderBrush = HexBrush(val);
                    syncTabsUi();
                }
                catch
                {
                    ShowToast(L("Geçersiz renk kodu! Örn: #E2925C", "Invalid color code! e.g. #E2925C"));
                }
            };
            Grid.SetColumn(btnApplyHex, 2);
            colorCustomRow.Children.Add(btnApplyHex);

            Button btnWinPalette = CreateStyledIconButton("#2A221E", "#4A3C34", L("Gelişmiş Renk Seçici Penceresini Aç", "Open Advanced Color Picker Dialog"));
            btnWinPalette.Margin = new Thickness(6, 0, 0, 0);
            btnWinPalette.Padding = new Thickness(9, 5, 9, 5);
            btnWinPalette.Content = new TextBlock { Text = L("🎨 Palet...", "🎨 Palette..."), Foreground = HexBrush("#FAF6F2"), FontSize = 11.5 };
            btnWinPalette.Click += (s, e) =>
            {
                Color cur = ParseColor(accentColorHex);
                using (var dlg = new System.Windows.Forms.ColorDialog())
                {
                    dlg.FullOpen = true;
                    dlg.AnyColor = true;
                    dlg.Color = System.Drawing.Color.FromArgb(cur.R, cur.G, cur.B);
                    if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        string hex = string.Format("#{0:X2}{1:X2}{2:X2}", dlg.Color.R, dlg.Color.G, dlg.Color.B);
                        txtHexInput.Text = hex;
                        previewSwatch.Background = HexBrush(hex);
                        ApplyTheme(hex, bgOpacityPercent);
                        card.BorderBrush = HexBrush(hex);
                        syncTabsUi();
                    }
                }
            };
            Grid.SetColumn(btnWinPalette, 3);
            colorCustomRow.Children.Add(btnWinPalette);
            colorSecStack.Children.Add(colorCustomRow);

            string[] presets = { "#E2925C", "#D97706", "#C08457", "#E07A5F", "#10B981", "#38BDF8", "#A78BFA", "#F43F5E" };
            string[] presetNames = IsEn
                ? new string[] { "Default (#E2925C)", "Amber", "Bronze Brown", "Coral", "Emerald", "Sky Blue", "Lavender", "Ruby" }
                : new string[] { "Varsayılan (#E2925C)", "Kehribar", "Bronz Kahve", "Mercan", "Zümrüt", "Gökyüzü", "Lavanta", "Yakut" };
            UniformGrid swatchGrid = new UniformGrid { Columns = 8 };
            for (int i = 0; i < presets.Length; i++)
            {
                string hex = presets[i];
                Border sw = new Border
                {
                    Width = 28,
                    Height = 28,
                    CornerRadius = new CornerRadius(14),
                    Background = HexBrush(hex),
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(hex.Equals(accentColorHex, StringComparison.OrdinalIgnoreCase) ? 2 : 0.5),
                    Cursor = Cursors.Hand,
                    ToolTip = presetNames[i]
                };
                sw.MouseLeftButtonDown += (s, e) =>
                {
                    txtHexInput.Text = hex;
                    previewSwatch.Background = HexBrush(hex);
                    ApplyTheme(hex, bgOpacityPercent);
                    card.BorderBrush = HexBrush(hex);
                    syncTabsUi();
                };
                swatchGrid.Children.Add(sw);
            }
            colorSecStack.Children.Add(swatchGrid);
            tabPanels[0].Children.Add(colorSecCard);

            // Opacity & Fit Card
            Border opSecCard = CreateSettingsSectionCard(L("🪟 Saydamlık & Pencere", "🪟 Opacity & Window"), L("Arka plan saydamlığını ve widget boyutunu ayarlayın", "Adjust background transparency & main widget height"));
            StackPanel opSecStack = (StackPanel)opSecCard.Child;

            Grid opRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            opRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            opRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            opRow.Children.Add(new TextBlock { Text = L("Arka Plan Opaklığı", "Background Opacity"), Foreground = HexBrush("#C7B9AE"), FontSize = 11.5 });
            TextBlock txtOpVal = new TextBlock { Text = IsEn ? (bgOpacityPercent + "%") : ("%" + bgOpacityPercent), Foreground = HexBrush("#FAF6F2"), FontSize = 12, FontWeight = FontWeights.Bold };
            Grid.SetColumn(txtOpVal, 1);
            opRow.Children.Add(txtOpVal);
            opSecStack.Children.Add(opRow);

            Border opSliderHost = new Border();
            Action rebuildOpacitySlider = () =>
            {
                Slider slOpacity = CreateModernSlider(accentColorHex, bgOpacityPercent);
                slOpacity.Minimum = 30;
                slOpacity.Maximum = 100;
                slOpacity.Value = bgOpacityPercent;
                slOpacity.ValueChanged += (s, e) =>
                {
                    int v = (int)Math.Round(e.NewValue);
                    txtOpVal.Text = IsEn ? (v + "%") : ("%" + v);
                    ApplyTheme(accentColorHex, v);
                };
                opSliderHost.Child = slOpacity;
            };
            string lastSyncedAccent = accentColorHex;
            syncAccentControls = () =>
            {
                if (!string.Equals(lastSyncedAccent, accentColorHex, StringComparison.OrdinalIgnoreCase))
                {
                    lastSyncedAccent = accentColorHex;
                    ApplyThinDarkScrollBarStyle(settingsScroll);
                    btnApplyHex.BorderBrush = HexBrush(accentColorHex);
                    rebuildOpacitySlider();
                }
            };
            rebuildOpacitySlider();
            opSecStack.Children.Add(opSliderHost);

            Button btnFit = CreateStyledIconButton("#2E241E", "#4A3C34", L("Widget boyunu içerik kadar küçült/sığdır", "Auto-fit widget height to content"));
            btnFit.Margin = new Thickness(0, 4, 0, 0);
            btnFit.Padding = new Thickness(10, 7, 10, 7);
            btnFit.Content = new TextBlock { Text = L("📐 Widget Yüksekliğini İçeriğe Sığdır", "📐 Auto-Fit Widget Height to Content"), Foreground = HexBrush("#FAF6F2"), FontSize = 11.5, FontWeight = FontWeights.Medium };
            btnFit.Click += (s, e) =>
            {
                FitHeightToContentIfCompact();
                ShowToast(L("Yükseklik içeriğe göre optimize edildi", "Height optimized to fit content"));
            };
            opSecStack.Children.Add(btnFit);
            tabPanels[0].Children.Add(opSecCard);

            // =========================================================
            // TAB 1: COMPANION WIDGETS (CALENDAR & TIMER)
            // =========================================================
            Border calCompanionCard = new Border
            {
                CornerRadius = new CornerRadius(11),
                Background = HexBrush(showCalendarWidget ? "#2C221C" : "#221C18"),
                BorderBrush = HexBrush(showCalendarWidget ? accentColorHex : "#3B302A"),
                BorderThickness = new Thickness(1.2),
                Padding = new Thickness(13, 11, 13, 11),
                Margin = new Thickness(0, 0, 0, 10)
            };
            StackPanel calCompanionStack = new StackPanel();
            Grid calCompanionTop = new Grid();
            calCompanionTop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            calCompanionTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel calCompanionInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            calCompanionInfo.Children.Add(new TextBlock
            {
                Text = L("📅 Takvim Widget'ı (iCal Destekli)", "📅 Calendar Widget (iCal Sync)"),
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap
            });
            calCompanionInfo.Children.Add(new TextBlock
            {
                Text = L("Aylık takvim, iCal (.ics) senkronizasyonu ve hızlı etkinlik ekleme", "Monthly desktop calendar with iCal (.ics) sync & quick event capture"),
                Foreground = HexBrush("#9E8E82"),
                FontSize = 10.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(calCompanionInfo, 0);
            calCompanionTop.Children.Add(calCompanionInfo);

            Button btnToggleCalWidget = CreateStyledIconButton(
                showCalendarWidget ? "#38281E" : "#1D1815",
                showCalendarWidget ? accentColorHex : "#3E322B",
                L("Takvim masaüstü widget'ını aç veya kapat", "Enable or disable the Calendar desktop widget"));
            btnToggleCalWidget.Padding = new Thickness(11, 5, 11, 5);
            btnToggleCalWidget.MinWidth = 72;
            btnToggleCalWidget.VerticalAlignment = VerticalAlignment.Center;
            TextBlock txtToggleCalWidget = new TextBlock
            {
                Text = showCalendarWidget ? L("✓ Açık", "✓ On") : L("Kapalı", "Off"),
                Foreground = HexBrush(showCalendarWidget ? "#FAF6F2" : "#8E7F74"),
                FontSize = 11,
                FontWeight = FontWeights.Bold
            };
            btnToggleCalWidget.Content = txtToggleCalWidget;
            btnToggleCalWidget.Click += (s, e) =>
            {
                SetCalendarWidgetVisible(!showCalendarWidget);
                calCompanionCard.Background = HexBrush(showCalendarWidget ? "#2C221C" : "#221C18");
                calCompanionCard.BorderBrush = HexBrush(showCalendarWidget ? accentColorHex : "#3B302A");
                btnToggleCalWidget.Background = HexBrush(showCalendarWidget ? "#38281E" : "#1D1815");
                btnToggleCalWidget.BorderBrush = HexBrush(showCalendarWidget ? accentColorHex : "#3E322B");
                txtToggleCalWidget.Text = showCalendarWidget ? L("✓ Açık", "✓ On") : L("Kapalı", "Off");
                txtToggleCalWidget.Foreground = HexBrush(showCalendarWidget ? "#FAF6F2" : "#8E7F74");
            };
            Grid.SetColumn(btnToggleCalWidget, 1);
            calCompanionTop.Children.Add(btnToggleCalWidget);
            calCompanionStack.Children.Add(calCompanionTop);

            calCompanionStack.Children.Add(new TextBlock
            {
                Text = L("Google Takvim Gizli iCal (.ics) Adresi (İsteğe Bağlı)", "Google Calendar Secret iCal (.ics) URL (Optional)"),
                Foreground = HexBrush("#B8A99E"),
                FontSize = 10.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 10, 0, 4)
            });
            Grid calIcalRow = new Grid();
            calIcalRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            calIcalRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBox txtIcalUrl = new TextBox
            {
                Text = string.IsNullOrEmpty(calendarSecretIcalUrl) ? "" : calendarSecretIcalUrl,
                Background = HexBrush("#1C1714"),
                Foreground = HexBrush("#E6DDD6"),
                BorderBrush = HexBrush("#3E322B"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(7, 4, 7, 4),
                FontSize = 10.5,
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = L("Google Takvim Ayarları -> 'iCal biçimindeki gizli adres' (.ics) linkini buraya yapıştırabilirsiniz", "Paste your Google Calendar 'Secret address in iCal format' (.ics) URL here")
            };
            Grid.SetColumn(txtIcalUrl, 0);
            calIcalRow.Children.Add(txtIcalUrl);

            Button btnSaveIcal = CreateStyledIconButton("#2E241E", "#4A3C34", L("iCal adresini kaydet ve senkronize et", "Save & sync iCal feed"));
            btnSaveIcal.Margin = new Thickness(6, 0, 0, 0);
            btnSaveIcal.Padding = new Thickness(10, 4, 10, 4);
            btnSaveIcal.Content = new TextBlock { Text = L("🔗 Kaydet", "🔗 Save"), Foreground = HexBrush("#E6DDD6"), FontSize = 10.5, FontWeight = FontWeights.SemiBold };
            btnSaveIcal.Click += (s, e) =>
            {
                calendarSecretIcalUrl = txtIcalUrl.Text.Trim();
                SaveSettings();
                if (showCalendarWidget) Task.Run(() => LoadAndSyncCalendarEventsAsync(true));
                ShowToast(L("Takvim iCal bağlantısı kaydedildi", "Calendar iCal URL saved"));
            };
            Grid.SetColumn(btnSaveIcal, 1);
            calIcalRow.Children.Add(btnSaveIcal);
            calCompanionStack.Children.Add(calIcalRow);

            calCompanionCard.Child = calCompanionStack;
            tabPanels[1].Children.Add(calCompanionCard);

            // Standalone Timer Widget Companion Card
            Border timerCompanionCard = new Border
            {
                CornerRadius = new CornerRadius(11),
                Background = HexBrush(showTimerWidget ? "#2C221C" : "#221C18"),
                BorderBrush = HexBrush(showTimerWidget ? accentColorHex : "#3B302A"),
                BorderThickness = new Thickness(1.2),
                Padding = new Thickness(13, 11, 13, 11),
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid timerCompanionTop = new Grid();
            timerCompanionTop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            timerCompanionTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel timerCompanionInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            timerCompanionInfo.Children.Add(new TextBlock
            {
                Text = L("⏱ Timer Widgeti", "⏱ Timer Widget"),
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap
            });
            timerCompanionInfo.Children.Add(new TextBlock
            {
                Text = L("Normal zamanlayıcı, otomatik oyun algılayan Eğlence Modu, Pomodoro ve yıllık aktivite ısı haritası", "Stopwatch, auto game-detecting Gaming mode, Study Pomodoro & yearly activity heatmap"),
                Foreground = HexBrush("#9E8E82"),
                FontSize = 10.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(timerCompanionInfo, 0);
            timerCompanionTop.Children.Add(timerCompanionInfo);

            Button btnToggleTimerWidget = CreateStyledIconButton(
                showTimerWidget ? "#38281E" : "#1D1815",
                showTimerWidget ? accentColorHex : "#3E322B",
                L("Timer masaüstü widget'ını aç veya kapat", "Enable or disable the Timer desktop widget"));
            btnToggleTimerWidget.Padding = new Thickness(11, 5, 11, 5);
            btnToggleTimerWidget.MinWidth = 72;
            btnToggleTimerWidget.VerticalAlignment = VerticalAlignment.Center;
            TextBlock txtToggleTimerWidget = new TextBlock
            {
                Text = showTimerWidget ? L("✓ Açık", "✓ On") : L("Kapalı", "Off"),
                Foreground = HexBrush(showTimerWidget ? "#FAF6F2" : "#8E7F74"),
                FontSize = 11,
                FontWeight = FontWeights.Bold
            };
            btnToggleTimerWidget.Content = txtToggleTimerWidget;
            btnToggleTimerWidget.Click += (s, e) =>
            {
                SetTimerWidgetVisible(!showTimerWidget);
                timerCompanionCard.Background = HexBrush(showTimerWidget ? "#2C221C" : "#221C18");
                timerCompanionCard.BorderBrush = HexBrush(showTimerWidget ? accentColorHex : "#3B302A");
                btnToggleTimerWidget.Background = HexBrush(showTimerWidget ? "#38281E" : "#1D1815");
                btnToggleTimerWidget.BorderBrush = HexBrush(showTimerWidget ? accentColorHex : "#3E322B");
                txtToggleTimerWidget.Text = showTimerWidget ? L("✓ Açık", "✓ On") : L("Kapalı", "Off");
                txtToggleTimerWidget.Foreground = HexBrush(showTimerWidget ? "#FAF6F2" : "#8E7F74");
            };
            Grid.SetColumn(btnToggleTimerWidget, 1);
            timerCompanionTop.Children.Add(btnToggleTimerWidget);
            timerCompanionCard.Child = timerCompanionTop;
            tabPanels[1].Children.Add(timerCompanionCard);

            // Taskbar Wireless Battery Overlay Card
            Border taskbarDevCard = new Border
            {
                CornerRadius = new CornerRadius(11),
                Background = HexBrush(showTaskbarDevices ? "#2C221C" : "#221C18"),
                BorderBrush = HexBrush(showTaskbarDevices ? accentColorHex : "#3B302A"),
                BorderThickness = new Thickness(1.2),
                Padding = new Thickness(13, 11, 13, 11),
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid tbDevTop = new Grid();
            tbDevTop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tbDevTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel tbDevInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            tbDevInfo.Children.Add(new TextBlock
            {
                Text = L("🔋 Görev Çubuğu Cihaz Şarjları", "🔋 Taskbar Device Batteries"),
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap
            });
            tbDevInfo.Children.Add(new TextBlock
            {
                Text = L("Tespit edilen kablosuz kulaklık, fare, klavye ve kumanda şarj yüzdelerini görev çubuğunda gösterir", "Displays active wireless headset, mouse, keyboard & gamepad battery levels on your taskbar"),
                Foreground = HexBrush("#9E8E82"),
                FontSize = 10.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(tbDevInfo, 0);
            tbDevTop.Children.Add(tbDevInfo);

            Button btnToggleTbDev = CreateStyledIconButton(
                showTaskbarDevices ? "#38281E" : "#1D1815",
                showTaskbarDevices ? accentColorHex : "#3E322B",
                L("Görev çubuğunda cihaz şarj yüzdelerini aç veya kapat", "Enable or disable taskbar wireless device battery overlay"));
            btnToggleTbDev.Padding = new Thickness(11, 5, 11, 5);
            btnToggleTbDev.MinWidth = 72;
            btnToggleTbDev.VerticalAlignment = VerticalAlignment.Center;
            TextBlock txtToggleTbDev = new TextBlock
            {
                Text = showTaskbarDevices ? L("✓ Açık", "✓ On") : L("Kapalı", "Off"),
                Foreground = HexBrush(showTaskbarDevices ? "#FAF6F2" : "#8E7F74"),
                FontSize = 11,
                FontWeight = FontWeights.Bold
            };
            btnToggleTbDev.Content = txtToggleTbDev;
            btnToggleTbDev.Click += (s, e) =>
            {
                SetTaskbarDevicesVisible(!showTaskbarDevices);
                taskbarDevCard.Background = HexBrush(showTaskbarDevices ? "#2C221C" : "#221C18");
                taskbarDevCard.BorderBrush = HexBrush(showTaskbarDevices ? accentColorHex : "#3B302A");
                btnToggleTbDev.Background = HexBrush(showTaskbarDevices ? "#38281E" : "#1D1815");
                btnToggleTbDev.BorderBrush = HexBrush(showTaskbarDevices ? accentColorHex : "#3E322B");
                txtToggleTbDev.Text = showTaskbarDevices ? L("✓ Açık", "✓ On") : L("Kapalı", "Off");
                txtToggleTbDev.Foreground = HexBrush(showTaskbarDevices ? "#FAF6F2" : "#8E7F74");
            };
            Grid.SetColumn(btnToggleTbDev, 1);
            tbDevTop.Children.Add(btnToggleTbDev);
            taskbarDevCard.Child = tbDevTop;
            tabPanels[1].Children.Add(taskbarDevCard);

            // Low Battery Notification Card (%10 Alert)
            Border lowBatCard = new Border
            {
                CornerRadius = new CornerRadius(11),
                Background = HexBrush(notifyLowBattery ? "#2C221C" : "#221C18"),
                BorderBrush = HexBrush(notifyLowBattery ? accentColorHex : "#3B302A"),
                BorderThickness = new Thickness(1.2),
                Padding = new Thickness(13, 11, 13, 11),
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid lowBatTop = new Grid();
            lowBatTop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            lowBatTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel lowBatInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            lowBatInfo.Children.Add(new TextBlock
            {
                Text = L("🪫 Düşük Pil Bildirimi (%10)", "🪫 Low Battery Alert (10%)"),
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap
            });
            lowBatInfo.Children.Add(new TextBlock
            {
                Text = L("Kablosuz cihazların şarjı %10 veya altına düştüğünde Windows bildirimi gönderir", "Sends a Windows notification when any wireless device battery drops to 10% or below"),
                Foreground = HexBrush("#9E8E82"),
                FontSize = 10.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(lowBatInfo, 0);
            lowBatTop.Children.Add(lowBatInfo);

            Button btnToggleLowBat = CreateStyledIconButton(
                notifyLowBattery ? "#38281E" : "#1D1815",
                notifyLowBattery ? accentColorHex : "#3E322B",
                L("Düşük pil bildirimini aç veya kapat", "Enable or disable low battery notifications"));
            btnToggleLowBat.Padding = new Thickness(11, 5, 11, 5);
            btnToggleLowBat.MinWidth = 72;
            btnToggleLowBat.VerticalAlignment = VerticalAlignment.Center;
            TextBlock txtToggleLowBat = new TextBlock
            {
                Text = notifyLowBattery ? L("✓ Açık", "✓ On") : L("Kapalı", "Off"),
                Foreground = HexBrush(notifyLowBattery ? "#FAF6F2" : "#8E7F74"),
                FontSize = 11,
                FontWeight = FontWeights.Bold
            };
            btnToggleLowBat.Content = txtToggleLowBat;
            btnToggleLowBat.Click += (s, e) =>
            {
                notifyLowBattery = !notifyLowBattery;
                if (!notifyLowBattery) lowBatteryNotifiedKeys.Clear();
                SaveSettings();
                lowBatCard.Background = HexBrush(notifyLowBattery ? "#2C221C" : "#221C18");
                lowBatCard.BorderBrush = HexBrush(notifyLowBattery ? accentColorHex : "#3B302A");
                btnToggleLowBat.Background = HexBrush(notifyLowBattery ? "#38281E" : "#1D1815");
                btnToggleLowBat.BorderBrush = HexBrush(notifyLowBattery ? accentColorHex : "#3E322B");
                txtToggleLowBat.Text = notifyLowBattery ? L("✓ Açık", "✓ On") : L("Kapalı", "Off");
                txtToggleLowBat.Foreground = HexBrush(notifyLowBattery ? "#FAF6F2" : "#8E7F74");
            };
            Grid.SetColumn(btnToggleLowBat, 1);
            lowBatTop.Children.Add(btnToggleLowBat);
            lowBatCard.Child = lowBatTop;
            tabPanels[1].Children.Add(lowBatCard);

            // =========================================================
            // TAB 2: MODULE ORDER, VISIBILITY & TELEMETRY SUB-TOGGLES
            // =========================================================
            Border modulesOrderCard = CreateSettingsSectionCard(L("🧩 Ana Widget Modülleri", "🧩 Main Widget Modules"), L("Tut-sürükle veya ▲/▼ ile sıralayın, sağdaki butonla gizleyip açın", "Drag or use ▲/▼ to reorder, toggle visibility on the right"));
            StackPanel modulesOrderCardStack = (StackPanel)modulesOrderCard.Child;

            StackPanel modulesOrderList = new StackPanel();
            int draggingModuleIdx = -1;
            Action rebuildModulesOrderUi = null;

            rebuildModulesOrderUi = () =>
            {
                modulesOrderList.Children.Clear();
                for (int i = 0; i < sectionOrder.Count; i++)
                {
                    int idx = i;
                    string key = sectionOrder[idx];
                    bool enabled = IsSectionEnabled(key);
                    bool isDraggingThis = (draggingModuleIdx == idx);

                    Border rowBorder = new Border
                    {
                        CornerRadius = new CornerRadius(8),
                        Background = HexBrush(isDraggingThis ? "#3A2B21" : (enabled ? "#1E1916" : "#181412")),
                        BorderBrush = HexBrush(isDraggingThis ? accentColorHex : (enabled ? "#4A3C34" : "#2E2520")),
                        BorderThickness = new Thickness(isDraggingThis ? 1.5 : 1),
                        Padding = new Thickness(10, 6, 8, 6),
                        Margin = new Thickness(0, 0, 0, 5),
                        Cursor = Cursors.SizeNS,
                        ToolTip = L("Sıralamayı değiştirmek için basılı tutup yukarı/aşağı sürükleyin", "Hold and drag up/down to reorder module")
                    };

                    Grid rg = new Grid();
                    rg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    rg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    rg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    rg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    TextBlock grip = new TextBlock
                    {
                        Text = "⠿",
                        Foreground = HexBrush(isDraggingThis ? accentColorHex : "#8E7F74"),
                        FontSize = 13.5,
                        Margin = new Thickness(0, 0, 8, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(grip, 0);
                    rg.Children.Add(grip);

                    TextBlock title = new TextBlock
                    {
                        Text = (idx + 1) + ". " + GetSectionDisplayName(key),
                        Foreground = HexBrush(enabled ? "#FAF6F2" : "#7E7067"),
                        FontSize = 11.5,
                        FontWeight = FontWeights.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        Margin = new Thickness(0, 0, 8, 0)
                    };
                    Grid.SetColumn(title, 1);
                    rg.Children.Add(title);

                    Button btnUp = CreateStyledIconButton("#26201C", "#3E322B", L("Yukarı Taşı", "Move Up"));
                    btnUp.Padding = new Thickness(6, 3, 6, 3);
                    btnUp.Margin = new Thickness(0, 0, 3, 0);
                    btnUp.IsEnabled = (idx > 0);
                    btnUp.Opacity = (idx > 0) ? 1.0 : 0.35;
                    btnUp.Content = new TextBlock { Text = "▲", Foreground = HexBrush("#C7B9AE"), FontSize = 9.5 };
                    btnUp.Click += (s, e) =>
                    {
                        if (idx > 0)
                        {
                            string tmp = sectionOrder[idx - 1];
                            sectionOrder[idx - 1] = sectionOrder[idx];
                            sectionOrder[idx] = tmp;
                            ApplyLayoutAndVisibility();
                            SaveSettings();
                            rebuildModulesOrderUi();
                        }
                        e.Handled = true;
                    };
                    Grid.SetColumn(btnUp, 2);
                    rg.Children.Add(btnUp);

                    Button btnDown = CreateStyledIconButton("#26201C", "#3E322B", L("Aşağı Taşı", "Move Down"));
                    btnDown.Padding = new Thickness(6, 3, 6, 3);
                    btnDown.Margin = new Thickness(0, 0, 6, 0);
                    btnDown.IsEnabled = (idx < sectionOrder.Count - 1);
                    btnDown.Opacity = (idx < sectionOrder.Count - 1) ? 1.0 : 0.35;
                    btnDown.Content = new TextBlock { Text = "▼", Foreground = HexBrush("#C7B9AE"), FontSize = 9.5 };
                    btnDown.Click += (s, e) =>
                    {
                        if (idx < sectionOrder.Count - 1)
                        {
                            string tmp = sectionOrder[idx + 1];
                            sectionOrder[idx + 1] = sectionOrder[idx];
                            sectionOrder[idx] = tmp;
                            ApplyLayoutAndVisibility();
                            SaveSettings();
                            rebuildModulesOrderUi();
                        }
                        e.Handled = true;
                    };
                    Grid.SetColumn(btnDown, 3);
                    rg.Children.Add(btnDown);

                    Button btnToggle = CreateStyledIconButton(
                        enabled ? "#38281E" : "#1D1815",
                        enabled ? accentColorHex : "#3E322B",
                        L("Bu modülü göster veya gizle", "Show or hide this module"));
                    btnToggle.Padding = new Thickness(9, 3, 9, 3);
                    btnToggle.MinWidth = 60;
                    btnToggle.Content = new TextBlock
                    {
                        Text = enabled ? L("✓ Açık", "✓ On") : L("Kapalı", "Off"),
                        Foreground = HexBrush(enabled ? "#FAF6F2" : "#8E7F74"),
                        FontSize = 10.5,
                        FontWeight = FontWeights.SemiBold
                    };
                    btnToggle.Click += (s, e) =>
                    {
                        bool nextState = !IsSectionEnabled(key);
                        SetSectionEnabled(key, nextState);
                        if (key == "monitors" && nextState && monitorSlots.Count == 0)
                        {
                            Task.Run(() => DiscoverAndSyncMonitors(true));
                        }
                        ApplyLayoutAndVisibility();
                        FitHeightToContentIfCompact();
                        SaveSettings();
                        rebuildModulesOrderUi();
                        e.Handled = true;
                    };
                    Grid.SetColumn(btnToggle, 4);
                    rg.Children.Add(btnToggle);

                    rowBorder.Child = rg;

                    rowBorder.MouseLeftButtonDown += (s, e) =>
                    {
                        draggingModuleIdx = idx;
                        modulesOrderList.CaptureMouse();
                        rebuildModulesOrderUi();
                        e.Handled = true;
                    };

                    modulesOrderList.Children.Add(rowBorder);
                }
            };

            modulesOrderList.MouseMove += (s, e) =>
            {
                if (draggingModuleIdx >= 0 && e.LeftButton == MouseButtonState.Pressed)
                {
                    Point pt = e.GetPosition(modulesOrderList);
                    double rowH = Math.Max(32.0, modulesOrderList.ActualHeight / Math.Max(1, sectionOrder.Count));
                    int targetIdx = (int)(pt.Y / rowH);
                    if (targetIdx < 0) targetIdx = 0;
                    if (targetIdx >= sectionOrder.Count) targetIdx = sectionOrder.Count - 1;

                    if (targetIdx != draggingModuleIdx)
                    {
                        string item = sectionOrder[draggingModuleIdx];
                        sectionOrder.RemoveAt(draggingModuleIdx);
                        sectionOrder.Insert(targetIdx, item);
                        draggingModuleIdx = targetIdx;
                        ApplyLayoutAndVisibility();
                        SaveSettings();
                        rebuildModulesOrderUi();
                    }
                }
            };

            modulesOrderList.MouseLeftButtonUp += (s, e) =>
            {
                if (draggingModuleIdx >= 0)
                {
                    draggingModuleIdx = -1;
                    if (modulesOrderList.IsMouseCaptured) modulesOrderList.ReleaseMouseCapture();
                    rebuildModulesOrderUi();
                }
            };

            rebuildModulesOrderUi();
            modulesOrderCardStack.Children.Add(modulesOrderList);
            tabPanels[2].Children.Add(modulesOrderCard);

            // Telemetry Sub-Items Visibility Section
            Border teleSubCard = CreateSettingsSectionCard(L("📊 Telemetri İçeriği", "📊 Telemetry Metrics"), L("Ana widget içindeki donanım sensörlerini seçin", "Choose which hardware stats appear inside the main widget"));
            StackPanel teleSubStack = (StackPanel)teleSubCard.Child;

            UniformGrid teleSubGrid = new UniformGrid { Columns = 2 };
            Action rebuildTeleSubGrid = null;
            rebuildTeleSubGrid = () =>
            {
                teleSubGrid.Children.Clear();
                string[] tKeys = { "cpu", "ram", "gpu", "vram", "net", "disks" };
                string[] tLabels = {
                    L("⚙ İşlemci (CPU)", "⚙ Processor (CPU)"),
                    L("▦ Bellek (RAM)", "▦ Memory (RAM)"),
                    L("▣ Ekran Kartı (GPU)", "▣ Graphics (GPU)"),
                    L("▤ Video Belleği (VRAM)", "▤ Video Memory (VRAM)"),
                    L("⇄ Ağ Trafiği & Ping", "⇄ Network & Ping"),
                    L("⛁ Diskler (C:, D:...)", "⛁ Drives (C:, D:...)")
                };
                bool[] tStates = { showTeleCpu, showTeleRam, showTeleGpu, showTeleVram, showTeleNet, showTeleDisks };

                for (int i = 0; i < tKeys.Length; i++)
                {
                    int idx = i;
                    string tk = tKeys[idx];
                    bool on = tStates[idx];

                    Button bSub = CreateStretchSettingsButton(on ? "#38281E" : "#1D1815", on ? accentColorHex : "#3A2F29", tLabels[idx]);
                    bSub.Margin = new Thickness(idx % 2 == 0 ? 0 : 3, 0, idx % 2 == 0 ? 3 : 0, 6);
                    bSub.Padding = new Thickness(9, 6, 9, 6);

                    Grid bg = new Grid();
                    bg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    bg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    TextBlock lbl = new TextBlock
                    {
                        Text = tLabels[idx],
                        Foreground = HexBrush(on ? "#FAF6F2" : "#8E7F74"),
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        Margin = new Thickness(0, 0, 8, 0)
                    };
                    Grid.SetColumn(lbl, 0);
                    bg.Children.Add(lbl);

                    TextBlock stateBadge = new TextBlock
                    {
                        Text = on ? "✓" : "✕",
                        Foreground = HexBrush(on ? accentColorHex : "#6E5E55"),
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(stateBadge, 1);
                    bg.Children.Add(stateBadge);

                    bSub.Content = bg;
                    bSub.Click += (s, e) =>
                    {
                        if (tk == "cpu") showTeleCpu = !showTeleCpu;
                        else if (tk == "ram") showTeleRam = !showTeleRam;
                        else if (tk == "gpu") showTeleGpu = !showTeleGpu;
                        else if (tk == "vram") showTeleVram = !showTeleVram;
                        else if (tk == "net") showTeleNet = !showTeleNet;
                        else if (tk == "disks") showTeleDisks = !showTeleDisks;

                        ApplyLayoutAndVisibility();
                        FitHeightToContentIfCompact();
                        SaveSettings();
                        rebuildTeleSubGrid();
                    };
                    teleSubGrid.Children.Add(bSub);
                }
            };
            rebuildTeleSubGrid();
            teleSubStack.Children.Add(teleSubGrid);

            Button btnFitLayout = CreateStyledIconButton("#2E241E", "#4A3C34", L("Widget boyunu seçili modüllere göre otomatik sığdır", "Auto-fit widget height to active modules"));
            btnFitLayout.Margin = new Thickness(0, 4, 0, 0);
            btnFitLayout.Padding = new Thickness(10, 7, 10, 7);
            btnFitLayout.Content = new TextBlock { Text = L("📐 Widget Yüksekliğini Modüllere Göre Sığdır", "📐 Auto-Fit Height to Selected Modules"), Foreground = HexBrush("#FAF6F2"), FontSize = 11.5, FontWeight = FontWeights.Medium };
            btnFitLayout.Click += (s, e) =>
            {
                FitHeightToContentIfCompact();
                ShowToast(L("Yükseklik aktif modüllere göre optimize edildi", "Height optimized for active modules"));
            };
            teleSubStack.Children.Add(btnFitLayout);
            tabPanels[2].Children.Add(teleSubCard);

            syncTabsUi();
            card.Child = rootLayout;
            settingsWindow.Content = card;
            settingsWindow.Show();
        }

        private FrameworkElement CreateInteractiveSpectrumPicker(string initialHex, Action<string> onColorPicked)
        {
            double currentH = 24, currentS = 0.70, currentV = 0.89;
            try
            {
                Color c = ParseColor(initialHex);
                RgbToHsv(c.R, c.G, c.B, out currentH, out currentS, out currentV);
            }
            catch { }

            StackPanel container = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

            double svWidth = 330;
            double svHeight = 96;

            Border svBorder = new Border
            {
                Width = svWidth,
                Height = svHeight,
                CornerRadius = new CornerRadius(8),
                BorderBrush = HexBrush("#4A3C34"),
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                Cursor = Cursors.Cross,
                Margin = new Thickness(0, 0, 0, 8)
            };

            Grid svGrid = new Grid();
            Border hueBaseLayer = new Border { Background = new SolidColorBrush(HsvToRgb(currentH, 1.0, 1.0)) };

            LinearGradientBrush whiteHoriz = new LinearGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255), new Point(0, 0), new Point(1, 0));
            whiteHoriz.Freeze();
            Border satLayer = new Border { Background = whiteHoriz };

            LinearGradientBrush blackVert = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, new Point(0, 0), new Point(0, 1));
            blackVert.Freeze();
            Border valLayer = new Border { Background = blackVert };

            Canvas svCanvas = new Canvas { Width = svWidth, Height = svHeight };
            Border svThumb = new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(7),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(2),
                Background = Brushes.Transparent,
                IsHitTestVisible = false
            };
            svThumb.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 0, Opacity = 0.9 };
            svCanvas.Children.Add(svThumb);

            svGrid.Children.Add(hueBaseLayer);
            svGrid.Children.Add(satLayer);
            svGrid.Children.Add(valLayer);
            svGrid.Children.Add(svCanvas);
            svBorder.Child = svGrid;

            Border hueBarBorder = new Border
            {
                Width = svWidth,
                Height = 16,
                CornerRadius = new CornerRadius(8),
                BorderBrush = HexBrush("#4A3C34"),
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                Cursor = Cursors.Hand
            };

            LinearGradientBrush rainbow = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            rainbow.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 0), 0.00));
            rainbow.GradientStops.Add(new GradientStop(Color.FromRgb(255, 255, 0), 0.17));
            rainbow.GradientStops.Add(new GradientStop(Color.FromRgb(0, 255, 0), 0.33));
            rainbow.GradientStops.Add(new GradientStop(Color.FromRgb(0, 255, 255), 0.50));
            rainbow.GradientStops.Add(new GradientStop(Color.FromRgb(0, 0, 255), 0.67));
            rainbow.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 255), 0.83));
            rainbow.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 0), 1.00));
            rainbow.Freeze();

            Canvas hueCanvas = new Canvas { Width = svWidth, Height = 16, Background = rainbow };
            Border hueThumb = new Border
            {
                Width = 8,
                Height = 16,
                CornerRadius = new CornerRadius(3),
                Background = Brushes.White,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                IsHitTestVisible = false
            };
            hueCanvas.Children.Add(hueThumb);
            hueBarBorder.Child = hueCanvas;

            Action updateThumbsAndNotify = () =>
            {
                Canvas.SetLeft(svThumb, Math.Max(0, Math.Min(svWidth - 14, currentS * svWidth - 7)));
                Canvas.SetTop(svThumb, Math.Max(0, Math.Min(svHeight - 14, (1.0 - currentV) * svHeight - 7)));
                Canvas.SetLeft(hueThumb, Math.Max(0, Math.Min(svWidth - 8, (currentH / 360.0) * svWidth - 4)));

                hueBaseLayer.Background = new SolidColorBrush(HsvToRgb(currentH, 1.0, 1.0));
                Color picked = HsvToRgb(currentH, currentS, currentV);
                string hex = string.Format("#{0:X2}{1:X2}{2:X2}", picked.R, picked.G, picked.B);
                if (onColorPicked != null) onColorPicked(hex);
            };

            Canvas.SetLeft(svThumb, Math.Max(0, Math.Min(svWidth - 14, currentS * svWidth - 7)));
            Canvas.SetTop(svThumb, Math.Max(0, Math.Min(svHeight - 14, (1.0 - currentV) * svHeight - 7)));
            Canvas.SetLeft(hueThumb, Math.Max(0, Math.Min(svWidth - 8, (currentH / 360.0) * svWidth - 4)));

            Action<Point> handleSvMouse = (pt) =>
            {
                currentS = Math.Max(0.0, Math.Min(1.0, pt.X / svWidth));
                currentV = Math.Max(0.05, Math.Min(1.0, 1.0 - (pt.Y / svHeight)));
                updateThumbsAndNotify();
            };

            svBorder.MouseLeftButtonDown += (s, e) =>
            {
                svBorder.CaptureMouse();
                handleSvMouse(e.GetPosition(svCanvas));
                e.Handled = true;
            };
            svBorder.MouseMove += (s, e) =>
            {
                if (svBorder.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
                {
                    handleSvMouse(e.GetPosition(svCanvas));
                    e.Handled = true;
                }
            };
            svBorder.MouseLeftButtonUp += (s, e) =>
            {
                if (svBorder.IsMouseCaptured) svBorder.ReleaseMouseCapture();
                e.Handled = true;
            };

            Action<Point> handleHueMouse = (pt) =>
            {
                currentH = Math.Max(0.0, Math.Min(360.0, (pt.X / svWidth) * 360.0));
                updateThumbsAndNotify();
            };

            hueBarBorder.MouseLeftButtonDown += (s, e) =>
            {
                hueBarBorder.CaptureMouse();
                handleHueMouse(e.GetPosition(hueCanvas));
                e.Handled = true;
            };
            hueBarBorder.MouseMove += (s, e) =>
            {
                if (hueBarBorder.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
                {
                    handleHueMouse(e.GetPosition(hueCanvas));
                    e.Handled = true;
                }
            };
            hueBarBorder.MouseLeftButtonUp += (s, e) =>
            {
                if (hueBarBorder.IsMouseCaptured) hueBarBorder.ReleaseMouseCapture();
                e.Handled = true;
            };

            container.Children.Add(svBorder);
            container.Children.Add(hueBarBorder);
            return container;
        }

        private static Color HsvToRgb(double h, double s, double v)
        {
            double c = v * s;
            double x = c * (1.0 - Math.Abs(((h / 60.0) % 2.0) - 1.0));
            double m = v - c;
            double r1 = 0, g1 = 0, b1 = 0;

            if (h < 60) { r1 = c; g1 = x; b1 = 0; }
            else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
            else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
            else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
            else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
            else { r1 = c; g1 = 0; b1 = x; }

            byte r = (byte)Math.Round(Math.Max(0, Math.Min(255, (r1 + m) * 255.0)));
            byte g = (byte)Math.Round(Math.Max(0, Math.Min(255, (g1 + m) * 255.0)));
            byte b = (byte)Math.Round(Math.Max(0, Math.Min(255, (b1 + m) * 255.0)));
            return Color.FromRgb(r, g, b);
        }

        private static void RgbToHsv(byte r, byte g, byte b, out double h, out double s, out double v)
        {
            double rd = r / 255.0;
            double gd = g / 255.0;
            double bd = b / 255.0;
            double max = Math.Max(rd, Math.Max(gd, bd));
            double min = Math.Min(rd, Math.Min(gd, bd));
            double delta = max - min;

            h = 0;
            if (delta > 0.0001)
            {
                if (max == rd) h = 60.0 * (((gd - bd) / delta) % 6.0);
                else if (max == gd) h = 60.0 * (((bd - rd) / delta) + 2.0);
                else h = 60.0 * (((rd - gd) / delta) + 4.0);
                if (h < 0) h += 360.0;
            }
            s = max <= 0.0001 ? 0 : delta / max;
            v = max;
        }

        // ==========================================
        // MONITOR SDR / DDC BRIGHTNESS ENGINE (ZERO-OVERHEAD)
        // ==========================================
        private void UpdateMonitorLinkButtonUi()
        {
            if (btnMonitorLink == null) return;
            btnMonitorLink.Background = HexBrush(monitorLinkSync ? "#3A2B21" : "#221C19");
            btnMonitorLink.BorderBrush = HexBrush(monitorLinkSync ? accentColorHex : "#3A2E28");
            btnMonitorLink.ToolTip = monitorLinkSync
                ? L("Senkronize Mod Açık: Slider tüm monitörleri birlikte değiştirir (Ayırmak için tıkla)", "Sync Mode Active: Slider adjusts all monitors together (Click to separate)")
                : L("Ayrı Mod: Her monitör bağımsız kontrol edilir (Birleştirmek için tıkla)", "Separate Mode: Each monitor is controlled independently (Click to sync)");
            btnMonitorLink.Content = new TextBlock
            {
                Text = monitorLinkSync ? L("🔗 Birlikte", "🔗 Linked") : L("⛓ Ayrı", "⛓ Separate"),
                Foreground = HexBrush(monitorLinkSync ? accentColorHex : "#B8A99E"),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            };
        }

        private void AttachMonitorSliderWheel(Slider sl, int monitorIndex)
        {
            sl.PreviewMouseWheel += (s, e) =>
            {
                int delta = e.Delta > 0 ? 5 : -5;
                int cur = (monitorIndex >= 0 && monitorIndex < monitorSlots.Count) ? monitorSlots[monitorIndex].CurrentBrightness : (int)Math.Round(sl.Value);
                int next = Math.Max(0, Math.Min(100, cur + delta));
                SetMonitorBrightnessFromUi(monitorIndex, next, monitorLinkSync);
                e.Handled = true;
            };
        }

        private void OnMonitorSliderValueChanged(int monitorIndex, double newValue)
        {
            if (isUpdatingMonitorUi) return;
            int target = Math.Max(0, Math.Min(100, (int)Math.Round(newValue)));
            SetMonitorBrightnessFromUi(monitorIndex, target, monitorLinkSync);
        }

        private void SetMonitorBrightnessFromUi(int sourceIndex, int targetPct, bool syncAll)
        {
            targetPct = Math.Max(0, Math.Min(100, targetPct));
            isUpdatingMonitorUi = true;
            try
            {
                if (syncAll)
                {
                    for (int i = 0; i < monitorSlots.Count; i++)
                    {
                        monitorSlots[i].CurrentBrightness = targetPct;
                        if (i < txtMonitorPercents.Count) txtMonitorPercents[i].Text = IsEn ? (targetPct + "%") : ("%" + targetPct);
                        if (i < sliderMonitors.Count && Math.Abs(sliderMonitors[i].Value - targetPct) >= 0.5)
                        {
                            sliderMonitors[i].Value = targetPct;
                        }
                        QueueMonitorBrightness(i, targetPct);
                    }
                }
                else
                {
                    if (sourceIndex >= 0 && sourceIndex < monitorSlots.Count)
                    {
                        monitorSlots[sourceIndex].CurrentBrightness = targetPct;
                        if (sourceIndex < txtMonitorPercents.Count) txtMonitorPercents[sourceIndex].Text = IsEn ? (targetPct + "%") : ("%" + targetPct);
                        if (sourceIndex < sliderMonitors.Count && Math.Abs(sliderMonitors[sourceIndex].Value - targetPct) >= 0.5)
                        {
                            sliderMonitors[sourceIndex].Value = targetPct;
                        }
                        QueueMonitorBrightness(sourceIndex, targetPct);
                    }
                }
            }
            finally
            {
                isUpdatingMonitorUi = false;
            }
        }

        private void QueueMonitorBrightness(int index, int pct)
        {
            if (index < 0 || index >= monitorSlots.Count) return;
            PhysicalMonitorSlot slot = monitorSlots[index];
            Interlocked.Exchange(ref slot.PendingBrightness, pct);

            if (Interlocked.CompareExchange(ref slot.WorkerBusy, 1, 0) == 0)
            {
                ThreadPool.QueueUserWorkItem(_ => RunMonitorDdcWorker(slot));
            }
        }

        private void ScheduleMonitorRediscovery(int delayMs)
        {
            try
            {
                CancellationTokenSource cts;
                lock (monitorRefreshLock)
                {
                    if (monitorRefreshCts != null)
                    {
                        try { monitorRefreshCts.Cancel(); monitorRefreshCts.Dispose(); } catch { }
                    }
                    monitorRefreshCts = new CancellationTokenSource();
                    cts = monitorRefreshCts;
                }
                Task.Delay(delayMs, cts.Token).ContinueWith(t =>
                {
                    if (!t.IsCanceled)
                    {
                        DiscoverAndSyncMonitors(true);
                    }
                }, TaskScheduler.Default);
            }
            catch { }
        }

        private bool ReacquirePhysicalMonitorHandle(PhysicalMonitorSlot slot)
        {
            if (slot == null || string.IsNullOrEmpty(slot.GdiDeviceName)) return false;
            try
            {
                List<IntPtr> matchingHmons = new List<IntPtr>();
                MonitorDdcNative.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref MonitorDdcNative.RECT r, IntPtr d) =>
                {
                    MonitorDdcNative.MONITORINFOEX mi = new MonitorDdcNative.MONITORINFOEX();
                    mi.cbSize = Marshal.SizeOf(typeof(MonitorDdcNative.MONITORINFOEX));
                    if (MonitorDdcNative.GetMonitorInfo(hMon, ref mi) &&
                        string.Equals(mi.szDevice, slot.GdiDeviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        matchingHmons.Add(hMon);
                    }
                    return true;
                }, IntPtr.Zero);

                for (int h = 0; h < matchingHmons.Count; h++)
                {
                    uint count = 0;
                    if (!MonitorDdcNative.GetNumberOfPhysicalMonitorsFromHMONITOR(matchingHmons[h], out count) || count == 0)
                        continue;
                    MonitorDdcNative.PHYSICAL_MONITOR[] phys = new MonitorDdcNative.PHYSICAL_MONITOR[count];
                    if (!MonitorDdcNative.GetPhysicalMonitorsFromHMONITOR(matchingHmons[h], count, phys))
                        continue;

                    int pickIdx = (slot.PhysicalIndex >= 0 && slot.PhysicalIndex < count) ? slot.PhysicalIndex : 0;
                    IntPtr newHandle = phys[pickIdx].hPhysicalMonitor;
                    for (int i = 0; i < count; i++)
                    {
                        if (i != pickIdx && phys[i].hPhysicalMonitor != IntPtr.Zero)
                        {
                            try { MonitorDdcNative.DestroyPhysicalMonitor(phys[i].hPhysicalMonitor); } catch { }
                        }
                    }

                    if (newHandle != IntPtr.Zero)
                    {
                        if (slot.Handle != IntPtr.Zero)
                        {
                            try { MonitorDdcNative.DestroyPhysicalMonitor(slot.Handle); } catch { }
                        }
                        slot.Handle = newHandle;
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private bool TrySetPhysicalDdcBrightness(PhysicalMonitorSlot slot, uint rawVal)
        {
            if (slot.Handle == IntPtr.Zero) return false;
            if (slot.PreferHighLevelDdc)
            {
                if (MonitorDdcNative.SetMonitorBrightness(slot.Handle, rawVal)) return true;
                if (MonitorDdcNative.SetVCPFeature(slot.Handle, MonitorDdcNative.VCP_BRIGHTNESS, rawVal))
                {
                    slot.PreferHighLevelDdc = false;
                    return true;
                }
            }
            else
            {
                if (MonitorDdcNative.SetVCPFeature(slot.Handle, MonitorDdcNative.VCP_BRIGHTNESS, rawVal)) return true;
                if (MonitorDdcNative.SetMonitorBrightness(slot.Handle, rawVal))
                {
                    slot.PreferHighLevelDdc = true;
                    return true;
                }
            }
            return false;
        }

        private void RunMonitorDdcWorker(PhysicalMonitorSlot slot)
        {
            try
            {
                while (true)
                {
                    int target = Interlocked.Exchange(ref slot.PendingBrightness, -1);
                    if (target < 0) break;

                    if (slot.UseWmiBrightness)
                    {
                        MonitorDdcNative.SetWmiBrightness(slot.WmiInstanceName, target);
                        Thread.Sleep(12);
                    }
                    else if (slot.UseWindowsSdrWhiteLevel && slot.TargetId != 0)
                    {
                        MonitorDdcNative.SetSdrWhiteLevelPercent(slot.AdapterId, slot.TargetId, target);
                        Thread.Sleep(8);
                    }
                    else
                    {
                        uint span = (slot.MaxVal > slot.MinVal) ? (slot.MaxVal - slot.MinVal) : 100u;
                        uint rawVal = slot.MinVal + (uint)Math.Round((target / 100.0) * span);

                        lock (monitorHandleLock)
                        {
                            bool ok = TrySetPhysicalDdcBrightness(slot, rawVal);
                            if (!ok && ReacquirePhysicalMonitorHandle(slot))
                            {
                                span = (slot.MaxVal > slot.MinVal) ? (slot.MaxVal - slot.MinVal) : 100u;
                                rawVal = slot.MinVal + (uint)Math.Round((target / 100.0) * span);
                                TrySetPhysicalDdcBrightness(slot, rawVal);
                            }
                        }
                        Thread.Sleep(28);
                    }
                }
            }
            catch { }
            finally
            {
                Interlocked.Exchange(ref slot.WorkerBusy, 0);
                if (Interlocked.CompareExchange(ref slot.PendingBrightness, -1, -1) >= 0)
                {
                    if (Interlocked.CompareExchange(ref slot.WorkerBusy, 1, 0) == 0)
                    {
                        ThreadPool.QueueUserWorkItem(_ => RunMonitorDdcWorker(slot));
                    }
                }
            }
        }

        private string FormatFriendlyMonitorName(string rawName, int idx, bool isInternalPanel)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return isInternalPanel ? L("Dahili Ekran", "Built-in Display") : (L("Monitör ", "Monitor ") + (idx + 1));
            string n = rawName.Trim();
            if (n.IndexOf("Generic PnP", StringComparison.OrdinalIgnoreCase) >= 0)
                return isInternalPanel ? L("Dahili Ekran", "Built-in Display") : (L("Monitör ", "Monitor ") + (idx + 1));
            return n;
        }

        private void DiscoverAndSyncMonitors(bool isInitial)
        {
            try
            {
                List<MonitorDdcNative.WinDisplayTargetInfo> winTargets = MonitorDdcNative.QueryActiveDisplayTargets();
                List<MonitorDdcNative.WmiBrightnessInfo> wmiDisplays = MonitorDdcNative.QueryWmiBrightnessDisplays();
                HashSet<string> usedWmiInstances = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                List<IntPtr> hMonitors = new List<IntPtr>();
                MonitorDdcNative.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref MonitorDdcNative.RECT r, IntPtr d) =>
                {
                    hMonitors.Add(hMon);
                    return true;
                }, IntPtr.Zero);

                List<PhysicalMonitorSlot> discovered = new List<PhysicalMonitorSlot>();

                for (int m = 0; m < hMonitors.Count; m++)
                {
                    string gdiDevName = "";
                    MonitorDdcNative.MONITORINFOEX mi = new MonitorDdcNative.MONITORINFOEX();
                    mi.cbSize = Marshal.SizeOf(typeof(MonitorDdcNative.MONITORINFOEX));
                    if (MonitorDdcNative.GetMonitorInfo(hMonitors[m], ref mi))
                    {
                        gdiDevName = mi.szDevice ?? "";
                    }

                    MonitorDdcNative.WinDisplayTargetInfo matchedTarget = null;
                    if (!string.IsNullOrEmpty(gdiDevName))
                    {
                        for (int t = 0; t < winTargets.Count; t++)
                        {
                            if (string.Equals(winTargets[t].GdiDeviceName, gdiDevName, StringComparison.OrdinalIgnoreCase))
                            {
                                matchedTarget = winTargets[t];
                                break;
                            }
                        }
                    }
                    if (matchedTarget == null && m < winTargets.Count)
                    {
                        matchedTarget = winTargets[m];
                    }

                    MonitorDdcNative.WmiBrightnessInfo matchedWmi = null;
                    bool exactWmiEdidMatch = false;
                    if (wmiDisplays.Count > 0)
                    {
                        string edidId = matchedTarget != null ? MonitorDdcNative.ExtractEdidIdFromDevicePath(matchedTarget.MonitorDevicePath) : "";
                        if (!string.IsNullOrEmpty(edidId))
                        {
                            for (int w = 0; w < wmiDisplays.Count; w++)
                            {
                                if (!usedWmiInstances.Contains(wmiDisplays[w].InstanceName) &&
                                    wmiDisplays[w].InstanceName.IndexOf(edidId, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    matchedWmi = wmiDisplays[w];
                                    exactWmiEdidMatch = true;
                                    break;
                                }
                            }
                        }
                        if (matchedWmi == null && matchedTarget != null && matchedTarget.IsInternalPanel)
                        {
                            for (int w = 0; w < wmiDisplays.Count; w++)
                            {
                                if (!usedWmiInstances.Contains(wmiDisplays[w].InstanceName))
                                {
                                    matchedWmi = wmiDisplays[w];
                                    break;
                                }
                            }
                        }
                    }

                    uint count = 0;
                    bool hasPhys = MonitorDdcNative.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitors[m], out count) && count > 0;
                    if (!hasPhys)
                    {
                        if (matchedWmi != null)
                        {
                            usedWmiInstances.Add(matchedWmi.InstanceName);
                            bool hdrOn = (matchedTarget != null && matchedTarget.HdrEnabled);
                            discovered.Add(new PhysicalMonitorSlot
                            {
                                Handle = IntPtr.Zero,
                                GdiDeviceName = gdiDevName,
                                PhysicalIndex = 0,
                                Name = FormatFriendlyMonitorName(matchedTarget != null ? matchedTarget.FriendlyName : "", discovered.Count, true),
                                MinVal = 0,
                                MaxVal = 100,
                                CurrentBrightness = hdrOn ? matchedTarget.SdrBrightnessPct : matchedWmi.CurrentBrightness,
                                PendingBrightness = -1,
                                WorkerBusy = 0,
                                UseWindowsSdrWhiteLevel = hdrOn,
                                UseWmiBrightness = !hdrOn,
                                WmiInstanceName = matchedWmi.InstanceName,
                                AdapterId = matchedTarget != null ? matchedTarget.AdapterId : default(MonitorDdcNative.LUID),
                                TargetId = matchedTarget != null ? matchedTarget.TargetId : 0u
                            });
                        }
                        continue;
                    }

                    MonitorDdcNative.PHYSICAL_MONITOR[] phys = new MonitorDdcNative.PHYSICAL_MONITOR[count];
                    if (!MonitorDdcNative.GetPhysicalMonitorsFromHMONITOR(hMonitors[m], count, phys))
                        continue;

                    for (int i = 0; i < count; i++)
                    {
                        IntPtr hPhys = phys[i].hPhysicalMonitor;
                        string rawDesc = phys[i].szPhysicalMonitorDescription;
                        if (matchedTarget != null && !string.IsNullOrWhiteSpace(matchedTarget.FriendlyName))
                        {
                            rawDesc = matchedTarget.FriendlyName;
                        }

                        bool hdrActive = (matchedTarget != null && matchedTarget.HdrEnabled);
                        bool useSdr = false;
                        bool useWmi = false;
                        bool preferHighLevel = false;
                        string wmiInst = "";
                        uint minVal = 0, maxVal = 100, curVal = 50;
                        int pct = 50;

                        if (hdrActive)
                        {
                            useSdr = true;
                            pct = matchedTarget.SdrBrightnessPct;
                        }
                        else if (matchedWmi != null && (exactWmiEdidMatch || (matchedTarget != null && matchedTarget.IsInternalPanel)))
                        {
                            useWmi = true;
                            wmiInst = matchedWmi.InstanceName;
                            pct = matchedWmi.CurrentBrightness;
                            usedWmiInstances.Add(wmiInst);
                            if (hPhys != IntPtr.Zero)
                            {
                                try { MonitorDdcNative.DestroyPhysicalMonitor(hPhys); } catch { }
                                hPhys = IntPtr.Zero;
                            }
                        }
                        else
                        {
                            bool ddcOk = false;
                            for (int attempt = 0; attempt < 2 && !ddcOk; attempt++)
                            {
                                if (attempt > 0) Thread.Sleep(20);
                                uint hlMin, hlCur, hlMax;
                                if (MonitorDdcNative.GetMonitorBrightness(hPhys, out hlMin, out hlCur, out hlMax) && hlMax > hlMin)
                                {
                                    minVal = hlMin;
                                    curVal = hlCur;
                                    maxVal = hlMax;
                                    preferHighLevel = true;
                                    ddcOk = true;
                                    break;
                                }
                                uint pdwType, vcpCur, vcpMax;
                                if (MonitorDdcNative.GetVCPFeatureAndVCPFeatureReply(hPhys, MonitorDdcNative.VCP_BRIGHTNESS, out pdwType, out vcpCur, out vcpMax) && vcpMax > 0)
                                {
                                    minVal = 0;
                                    curVal = vcpCur;
                                    maxVal = vcpMax;
                                    preferHighLevel = false;
                                    ddcOk = true;
                                    break;
                                }
                            }

                            if (ddcOk && maxVal > minVal)
                            {
                                double ratio = (curVal - minVal) / (double)(maxVal - minVal);
                                pct = Math.Max(0, Math.Min(100, (int)Math.Round(ratio * 100.0)));
                            }
                            else
                            {
                                // Fallback for built-in laptop panels routed via GPU mux without internal OutputTechnology flag
                                MonitorDdcNative.WmiBrightnessInfo fallbackWmi = null;
                                for (int w = 0; w < wmiDisplays.Count; w++)
                                {
                                    if (!usedWmiInstances.Contains(wmiDisplays[w].InstanceName))
                                    {
                                        fallbackWmi = wmiDisplays[w];
                                        break;
                                    }
                                }
                                if (fallbackWmi != null)
                                {
                                    useWmi = true;
                                    wmiInst = fallbackWmi.InstanceName;
                                    pct = fallbackWmi.CurrentBrightness;
                                    usedWmiInstances.Add(wmiInst);
                                    if (hPhys != IntPtr.Zero)
                                    {
                                        try { MonitorDdcNative.DestroyPhysicalMonitor(hPhys); } catch { }
                                        hPhys = IntPtr.Zero;
                                    }
                                }
                            }
                        }

                        bool isInternalSlot = useWmi || (matchedTarget != null && matchedTarget.IsInternalPanel);
                        PhysicalMonitorSlot slot = new PhysicalMonitorSlot
                        {
                            Handle = hPhys,
                            GdiDeviceName = gdiDevName,
                            PhysicalIndex = i,
                            Name = FormatFriendlyMonitorName(rawDesc, discovered.Count, isInternalSlot),
                            MinVal = minVal,
                            MaxVal = (maxVal > minVal) ? maxVal : 100u,
                            CurrentBrightness = pct,
                            PendingBrightness = -1,
                            WorkerBusy = 0,
                            UseWindowsSdrWhiteLevel = useSdr,
                            UseWmiBrightness = useWmi,
                            WmiInstanceName = wmiInst,
                            PreferHighLevelDdc = preferHighLevel,
                            AdapterId = matchedTarget != null ? matchedTarget.AdapterId : default(MonitorDdcNative.LUID),
                            TargetId = matchedTarget != null ? matchedTarget.TargetId : 0u
                        };
                        discovered.Add(slot);
                    }
                }

                if (discovered.Count > 0)
                {
                    lock (monitorHandleLock)
                    {
                        CleanupMonitorHandles();
                        monitorSlots.Clear();
                        monitorSlots.AddRange(discovered);
                    }

                    Dispatcher.Invoke(() =>
                    {
                        RebuildMonitorSlidersUi();
                        ApplyLayoutAndVisibility();
                        if (isWindowInitialized) FitHeightToContentIfCompact();
                    });
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        ApplyLayoutAndVisibility();
                    });
                }
            }
            catch { }
        }

        private void RebuildMonitorSlidersUi()
        {
            if (monRowsHostStack == null) return;
            isUpdatingMonitorUi = true;
            try
            {
                monRowsHostStack.Children.Clear();
                txtMonitorNames.Clear();
                txtMonitorPercents.Clear();
                sliderMonitors.Clear();
                monitorSliderContainers.Clear();

                for (int i = 0; i < monitorSlots.Count; i++)
                {
                    int idx = i;
                    PhysicalMonitorSlot slot = monitorSlots[i];

                    Grid row = new Grid { Margin = new Thickness(0, i == 0 ? 0 : 5, 0, 2) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });

                    Grid nameGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
                    nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    TextBlock tIcon = new TextBlock { Text = "🖥", Foreground = HexBrush("#B8A99E"), FontSize = 11, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(tIcon, 0);
                    nameGrid.Children.Add(tIcon);

                    TextBlock tName = new TextBlock
                    {
                        Text = slot.Name + (slot.UseWindowsSdrWhiteLevel ? " (HDR)" : ""),
                        Foreground = HexBrush("#F5EFEA"),
                        FontSize = 11.5,
                        FontWeight = FontWeights.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        TextWrapping = TextWrapping.NoWrap,
                        ToolTip = slot.UseWindowsSdrWhiteLevel
                            ? (slot.Name + L(" • HDR Açık: Windows SDR İçerik Parlaklığı", " • HDR Active: Windows SDR Content Brightness"))
                            : (slot.UseWmiBrightness
                                ? (slot.Name + L(" • Dahili Panel (WMI Parlaklık)", " • Built-in Display (WMI Brightness)"))
                                : (slot.Name + L(" • Donanımsal DDC/CI Parlaklık", " • Hardware DDC/CI Brightness")))
                    };
                    Grid.SetColumn(tName, 1);
                    nameGrid.Children.Add(tName);
                    txtMonitorNames.Add(tName);
                    Grid.SetColumn(nameGrid, 0);
                    row.Children.Add(nameGrid);

                    Border sHost = new Border { VerticalAlignment = VerticalAlignment.Center };
                    Slider sl = CreateModernSlider(accentColorHex, slot.CurrentBrightness, 0, "6,0,8,0");
                    sl.ValueChanged += (s, e) => OnMonitorSliderValueChanged(idx, e.NewValue);
                    AttachMonitorSliderWheel(sl, idx);
                    sHost.Child = sl;
                    monitorSliderContainers.Add(sHost);
                    sliderMonitors.Add(sl);
                    Grid.SetColumn(sHost, 1);
                    row.Children.Add(sHost);

                    TextBlock tPct = new TextBlock
                    {
                        Text = IsEn ? (slot.CurrentBrightness + "%") : ("%" + slot.CurrentBrightness),
                        Foreground = HexBrush(accentColorHex),
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    accentTextBlocks.Add(tPct);
                    txtMonitorPercents.Add(tPct);
                    Grid.SetColumn(tPct, 2);
                    row.Children.Add(tPct);

                    monRowsHostStack.Children.Add(row);
                }
            }
            finally
            {
                isUpdatingMonitorUi = false;
            }
        }

        private void CleanupMonitorHandles()
        {
            lock (monitorHandleLock)
            {
                foreach (var slot in monitorSlots)
                {
                    if (slot.Handle != IntPtr.Zero)
                    {
                        try { MonitorDdcNative.DestroyPhysicalMonitor(slot.Handle); } catch { }
                        slot.Handle = IntPtr.Zero;
                    }
                }
            }
        }

        // ==========================================
        // ULTRA-LIGHT TIMERS, NVML GPU & TELEMETRY
        // ==========================================
        private void InitializeTimers()
        {
            Task.Run(() =>
            {
                // Initialize Direct NVML C-API off UI thread so cold boot is never stalled by GPU driver init
                try
                {
                    if (NvmlNative.nvmlInit_v2() == 0)
                    {
                        if (NvmlNative.nvmlDeviceGetHandleByIndex_v2(0, out nvmlDeviceHandle) == 0)
                        {
                            nvmlInitialized = true;
                            StringBuilder sb = new StringBuilder(96);
                            if (NvmlNative.nvmlDeviceGetName(nvmlDeviceHandle, sb, 96) == 0)
                            {
                                string rawGpu = sb.ToString().Replace("NVIDIA", "").Replace("GeForce", "").Trim();
                                if (!string.IsNullOrEmpty(rawGpu)) gpuModelName = rawGpu;
                            }
                        }
                    }
                }
                catch { }
                try
                {
                    cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    cpuCounter.NextValue();
                }
                catch { }
                DiscoverAndSyncMonitors(true);
                FetchWeatherAsync();
                UpdatePingAsync();
                PollWirelessDevicesAsync();
            });

            clipboardHistory.Add(new ClipboardEntry { Text = L("OpenWidget Masaüstü Hazır", "OpenWidget Desktop Ready"), Time = DateTime.Now.ToString("HH:mm") });
            RefreshClipboardUi();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        string initText = Clipboard.GetText().Trim();
                        if (!string.IsNullOrEmpty(initText))
                        {
                            clipboardHistory.Clear();
                            lastClipboardText = initText;
                            clipboardHistory.Add(new ClipboardEntry { Text = initText, Time = DateTime.Now.ToString("HH:mm") });
                            RefreshClipboardUi();
                        }
                    }
                }
                catch { }
            }), DispatcherPriority.ApplicationIdle);

            DispatcherTimer desktopGuardTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            desktopGuardTimer.Tick += (s, e) => CheckAndEnforceShowDesktopImmunity();
            desktopGuardTimer.Start();

            DispatcherTimer fastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            fastTimer.Tick += (s, e) =>
            {
                UpdateClockAndUptime();
                UpdateTelemetryFast();
                SyncMicrophoneStateFromOs(false);
            };
            fastTimer.Start();

            int mediumTickCount = 0;
            DispatcherTimer mediumTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            mediumTimer.Tick += (s, e) =>
            {
                mediumTickCount++;
                bool pollWirelessNow = (mediumTickCount % 3 == 0);
                bool trimNow = (mediumTickCount % 20 == 0);
                Task.Run(() =>
                {
                    UpdatePingAsync();
                    if (pollWirelessNow)
                    {
                        PollWirelessDevicesAsync();
                    }
                    if (trimNow)
                    {
                        TrimProcessMemory();
                    }
                });
            };
            mediumTimer.Start();

            DispatcherTimer weatherTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
            weatherTimer.Tick += (s, e) =>
            {
                Task.Run(() => FetchWeatherAsync());
                TrimProcessMemory();
            };
            weatherTimer.Start();

            UpdateClockAndUptime();
            Dispatcher.BeginInvoke(new Action(() => UpdateTelemetryFast()), DispatcherPriority.Background);
        }

        private void UpdateClockAndUptime()
        {
            DateTime now = DateTime.Now;
            ulong tickMs = GetTickCount64();
            TimeSpan up = TimeSpan.FromMilliseconds(tickMs);
            int totalHours = (int)up.TotalHours;

            if (IsEn)
            {
                string[] monthsEn = { "", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
                string[] daysEn = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
                txtClockAndUptime.Text = string.Format("{0:HH:mm} · {1} {2} {3}  Up: {4}h {5}m",
                    now, now.Day, monthsEn[now.Month], daysEn[(int)now.DayOfWeek], totalHours, up.Minutes);
            }
            else
            {
                string[] aylar = { "", "Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara" };
                string[] gunler = { "Pazar", "Pzt", "Salı", "Çar", "Per", "Cuma", "Cmt" };
                txtClockAndUptime.Text = string.Format("{0:HH:mm} · {1} {2} {3}  Açık: {4}s {5}dk",
                    now, now.Day, aylar[now.Month], gunler[(int)now.DayOfWeek], totalHours, up.Minutes);
            }
        }

        private void PollGpuDirectNvml()
        {
            if (!nvmlInitialized || nvmlDeviceHandle == IntPtr.Zero) return;
            try
            {
                uint temp;
                if (NvmlNative.nvmlDeviceGetTemperature(nvmlDeviceHandle, 0, out temp) == 0)
                {
                    gpuTemp = temp;
                }
                NvmlNative.nvmlUtilization_t util;
                if (NvmlNative.nvmlDeviceGetUtilizationRates(nvmlDeviceHandle, out util) == 0)
                {
                    gpuLoad = util.gpu;
                    gpuHistory.Add(gpuLoad);
                    if (gpuHistory.Count > 20) gpuHistory.RemoveAt(0);
                }
                NvmlNative.nvmlMemory_t mem;
                if (NvmlNative.nvmlDeviceGetMemoryInfo(nvmlDeviceHandle, out mem) == 0)
                {
                    vramUsedGb = mem.used / (1024.0 * 1024.0 * 1024.0);
                    vramTotalGb = mem.total / (1024.0 * 1024.0 * 1024.0);
                }
            }
            catch { }
        }

        private double ReadCpuTemperature(double currentCpuLoad)
        {
            double baseAmbient = Math.Max(38.0, gpuTemp - 4.0);
            double targetTemp = baseAmbient + (currentCpuLoad * 0.34);
            cpuTemp = (cpuTemp * 0.70) + (targetTemp * 0.30);
            return Math.Round(Math.Max(36, Math.Min(95, cpuTemp)));
        }

        private void UpdateTelemetryFast()
        {
            PollGpuDirectNvml();

            double cpuVal = 6;
            try
            {
                if (cpuCounter != null) cpuVal = Math.Round(cpuCounter.NextValue());
            }
            catch { }
            cpuVal = Math.Max(1, Math.Min(100, cpuVal));
            cpuHistory.Add(cpuVal);
            if (cpuHistory.Count > 20) cpuHistory.RemoveAt(0);

            double cTemp = ReadCpuTemperature(cpuVal);
            txtCpuDetail.Text = string.Format("{0:0}°C • {1}", cTemp, cpuShortName);
            txtCpuPct.Text = IsEn ? ((int)cpuVal + "%") : ("%" + (int)cpuVal);
            DrawSparkline(sparkCpu, cpuHistory, 46, 16);

            try
            {
                if (GlobalMemoryStatusEx(sharedMemStatus))
                {
                    double totalGb = sharedMemStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                    double usedGb = (sharedMemStatus.ullTotalPhys - sharedMemStatus.ullAvailPhys) / (1024.0 * 1024.0 * 1024.0);
                    int pct = (int)sharedMemStatus.dwMemoryLoad;
                    txtRamDetail.Text = string.Format("{0:0.0} / {1:0} GB", usedGb, Math.Round(totalGb));
                    txtRamPct.Text = IsEn ? (pct + "%") : ("%" + pct);
                    barRamFill.Width = Math.Max(3, (42.0 * pct) / 100.0);
                }
            }
            catch { }

            txtGpuDetail.Text = string.Format("{0:0}°C • {1}", gpuTemp, gpuModelName);
            txtGpuPct.Text = IsEn ? ((int)gpuLoad + "%") : ("%" + (int)gpuLoad);
            DrawSparkline(sparkGpu, gpuHistory, 46, 16);

            int vramPct = vramTotalGb > 0 ? (int)Math.Round((vramUsedGb / vramTotalGb) * 100.0) : 0;
            txtVramDetail.Text = string.Format("{0:0.0} / {1:0} GB", vramUsedGb, vramTotalGb);
            txtVramPct.Text = IsEn ? (vramPct + "%") : ("%" + vramPct);
            barVramFill.Width = Math.Max(3, (42.0 * vramPct) / 100.0);

            try
            {
                long totalRecv = 0;
                long totalSent = 0;
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        var stats = ni.GetIPv4Statistics();
                        totalRecv += stats.BytesReceived;
                        totalSent += stats.BytesSent;
                    }
                }

                DateTime now = DateTime.Now;
                double elapsedSec = (now - lastNetTime).TotalSeconds;
                if (lastBytesRecv > 0 && elapsedSec > 0.2)
                {
                    double downKbps = ((totalRecv - lastBytesRecv) * 8.0 / 1000.0) / elapsedSec;
                    double upKbps = ((totalSent - lastBytesSent) * 8.0 / 1000.0) / elapsedSec;
                    txtNetSpeed.Text = string.Format("↓ {0} ↑ {1}", FormatBitrate(downKbps), FormatBitrate(upKbps));
                }
                lastBytesRecv = totalRecv;
                lastBytesSent = totalSent;
                lastNetTime = now;
            }
            catch { }

            txtNetPing.Text = string.Format("{0:0} ms", lastPingMs);
            if (IsEn)
                txtNetBadge.Text = lastPingMs < 40 ? "Great" : (lastPingMs < 90 ? "Good" : "Fair");
            else
                txtNetBadge.Text = lastPingMs < 40 ? "Harika" : (lastPingMs < 90 ? "İyi" : "Orta");

            for (int i = 0; i < fixedDrives.Count; i++)
            {
                UpdateDiskRow(fixedDrives[i], diskDetailBlocks[i], diskFillBars[i], diskPctBlocks[i]);
            }
        }

        private string FormatBitrate(double kbps)
        {
            if (kbps >= 1000.0)
                return string.Format("{0:0.0} Mbps", kbps / 1000.0);
            return string.Format("{0:0} Kbps", Math.Max(0, kbps));
        }

        private void UpdateDiskRow(DriveInfo d, TextBlock detail, Border fill, TextBlock pctBlock)
        {
            try
            {
                if (d != null && d.IsReady)
                {
                    double freeGb = d.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    double totalGb = d.TotalSize / (1024.0 * 1024.0 * 1024.0);
                    int usedPct = (int)Math.Round(((totalGb - freeGb) / totalGb) * 100.0);
                    detail.Text = IsEn
                        ? string.Format("{0:0} GB Free / {1:0} GB", freeGb, totalGb)
                        : string.Format("{0:0} GB Boş / {1:0} GB", freeGb, totalGb);
                    pctBlock.Text = IsEn ? (usedPct + "% Used") : ("%" + usedPct + " Dolu");
                    fill.Width = Math.Max(3, (42.0 * usedPct) / 100.0);
                }
            }
            catch { }
        }

        private void DrawSparkline(Polyline poly, List<double> values, double width, double height)
        {
            if (values == null || values.Count < 2) return;
            PointCollection pts = new PointCollection(values.Count);
            double step = width / (values.Count - 1);
            for (int i = 0; i < values.Count; i++)
            {
                double v = Math.Max(0, Math.Min(100, values[i]));
                double x = i * step;
                double y = height - ((v / 100.0) * (height - 4)) - 2;
                pts.Add(new Point(x, y));
            }
            pts.Freeze();
            poly.Points = pts;
        }

        private void UpdatePingAsync()
        {
            try
            {
                if (sharedPing != null)
                {
                    PingReply reply = sharedPing.Send("1.1.1.1", 1000);
                    if (reply != null && reply.Status == IPStatus.Success)
                    {
                        lastPingMs = reply.RoundtripTime;
                    }
                }
            }
            catch { }
        }

        // ==========================================
        // UNIVERSAL WIRELESS PERIPHERALS BATTERY SCAN
        // ==========================================
        private Brush GetWirelessStatusBrush(int pct, bool isCharging)
        {
            if (isCharging) return HexBrush("#22C55E");
            if (pct > 0 && pct <= 10) return HexBrush("#EF4444");
            return HexBrush(accentColorHex);
        }

        private void CheckAndNotifyLowBattery(string key, string name, int pct, bool isCharging, bool isAsleep)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!notifyLowBattery)
            {
                lowBatteryNotifiedKeys.Remove(key);
                return;
            }
            if (isAsleep || pct <= 0 || pct > 100) return;
            if (isCharging || pct > 20)
            {
                lowBatteryNotifiedKeys.Remove(key);
                return;
            }
            if (pct <= 10 && !lowBatteryNotifiedKeys.Contains(key))
            {
                lowBatteryNotifiedKeys.Add(key);
                try
                {
                    if (trayIcon != null)
                    {
                        trayIcon.ShowBalloonTip(
                            6000,
                            L("🪫 Düşük Pil Uyarısı", "🪫 Low Battery Alert"),
                            string.Format(L("{0} şarjı %{1} seviyesine düştü!", "{0} battery is down to {1}%!"), name, pct),
                            System.Windows.Forms.ToolTipIcon.Warning);
                    }
                }
                catch { }
            }
        }

        private void PollWirelessDevicesAsync()
        {
            if (Interlocked.CompareExchange(ref wirelessPollingBusy, 1, 0) != 0) return;
            try
            {
                bool en = IsEn;
                List<WirelessDeviceReading> activeList = WirelessBatteryNative.ScanActiveWirelessDevices(en, wirelessLastPcts, wirelessLastHz);

                Dispatcher.Invoke(() =>
                {
                    if (panelWirelessContent == null || wirelessHeaderGrid == null) return;

                    bool countChanged = (activeList.Count != lastWirelessCount);
                    lastWirelessCount = activeList.Count;

                    foreach (var kv in wirelessRowCache)
                    {
                        kv.Value.Row.Visibility = Visibility.Collapsed;
                    }

                    foreach (WirelessDeviceReading dev in activeList)
                    {
                        WirelessRowUi ui;
                        if (!wirelessRowCache.TryGetValue(dev.IdKey, out ui))
                        {
                            ui = new WirelessRowUi();
                            ui.Row = CreateWirelessDeviceRow(dev.Icon, dev.Name, dev.Detail, false, out ui.Label, out ui.Detail, out ui.Fill, out ui.Pct);
                            wirelessRowCache[dev.IdKey] = ui;
                            panelWirelessContent.Children.Add(ui.Row);
                        }
                        ui.Row.Visibility = Visibility.Visible;
                        ui.IsCharging = dev.IsCharging;
                        ui.BarPct = dev.BarPct;
                        Brush statusBrush = GetWirelessStatusBrush(dev.BarPct, dev.IsCharging);
                        ui.Label.Text = dev.Name;
                        ui.Label.ToolTip = dev.Name;
                        ui.Detail.Text = dev.Detail;
                        ui.Detail.ToolTip = dev.Detail;
                        ui.Pct.Text = dev.PctText;
                        ui.Pct.Foreground = statusBrush;
                        ui.Fill.Background = statusBrush;
                        ui.Fill.Width = dev.BarPct > 0 ? Math.Max(3, (42.0 * dev.BarPct) / 100.0) : 0;

                        CheckAndNotifyLowBattery(dev.IdKey, dev.Name, dev.BarPct, dev.IsCharging, dev.IsAsleep);
                    }

                    Visibility targetVis = (showSecWireless && activeList.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
                    if (secContainerWireless != null && secContainerWireless.Visibility != targetVis)
                    {
                        ApplyLayoutAndVisibility();
                        countChanged = true;
                    }

                    if (countChanged && isWindowInitialized)
                    {
                        FitHeightToContentIfCompact();
                    }

                    latestTaskbarDevices.Clear();
                    foreach (WirelessDeviceReading dev in activeList)
                    {
                        if (dev.BarPct <= 0) continue;
                        string kind = "Gamepad";
                        if (dev.Icon == "🎧") kind = "Headset";
                        else if (dev.Icon == "🖱") kind = "Mouse";
                        else if (dev.Icon == "⌨") kind = "Keyboard";
                        else if (dev.Name != null)
                        {
                            string ln = dev.Name.ToLowerInvariant();
                            if (ln.Contains("mouse") || ln.Contains("scyrox") || ln.Contains("viper") || ln.Contains("superlight")) kind = "Mouse";
                            else if (ln.Contains("head") || ln.Contains("audio") || ln.Contains("hyperx") || ln.Contains("buds") || ln.Contains("airpod")) kind = "Headset";
                            else if (ln.Contains("key")) kind = "Keyboard";
                        }
                        latestTaskbarDevices.Add(new TaskbarDeviceItem
                        {
                            Kind = kind,
                            Name = dev.Name,
                            Pct = dev.BarPct,
                            Detail = dev.Detail,
                            IsCharging = dev.IsCharging
                        });
                    }
                    RefreshTaskbarDevicesOverlayUi();
                });
            }
            catch { }
            finally
            {
                Interlocked.Exchange(ref wirelessPollingBusy, 0);
            }
        }

        public void SetTaskbarDevicesVisible(bool visible)
        {
            showTaskbarDevices = visible;
            SaveSettings();
            if (trayTaskbarDevicesItem != null)
            {
                trayTaskbarDevicesItem.Text = showTaskbarDevices
                    ? L("🔋 Görev Çubuğu Cihaz Şarjlarını Gizle", "🔋 Hide Taskbar Device Batteries")
                    : L("🔋 Görev Çubuğu Cihaz Şarjlarını Göster", "🔋 Show Taskbar Device Batteries");
            }
            if (!showTaskbarDevices)
            {
                if (cachedTaskbarOverlayHwnd != IntPtr.Zero) ShowWindow(cachedTaskbarOverlayHwnd, SW_HIDE);
                return;
            }
            if (taskbarDevicesWindow == null) InitializeTaskbarDevicesOverlay();
            else if (!taskbarDevicesWindow.IsVisible) taskbarDevicesWindow.Show();
            lastTaskbarDevicesSignature = "";
            lastTaskbarOverlayX = int.MinValue;
            lastTaskbarOverlayY = int.MinValue;
            lastTaskbarOverlayW = int.MinValue;
            RefreshTaskbarDevicesOverlayUi();
            Task.Run(() => PollWirelessDevicesAsync());
        }

        private void InitializeTaskbarDevicesOverlay()
        {
            if (taskbarDevicesWindow != null) return;
            taskbarDevicesWindow = new Window
            {
                Title = "OpenWidget Taskbar Devices",
                Width = 10,
                Height = 40,
                Left = -20000,
                Top = -20000,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Topmost = true,
                UseLayoutRounding = true,
                SnapsToDevicePixels = true,
                FontFamily = new FontFamily("Segoe UI")
            };
            TextOptions.SetTextFormattingMode(taskbarDevicesWindow, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(taskbarDevicesWindow, TextRenderingMode.ClearType);

            Grid host = new Grid
            {
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            taskbarDevicesPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 4, 0)
            };
            host.Children.Add(taskbarDevicesPanel);
            taskbarDevicesWindow.Content = host;

            taskbarDevicesWindow.Loaded += (s, e) =>
            {
                IntPtr h = new WindowInteropHelper(taskbarDevicesWindow).Handle;
                cachedTaskbarOverlayHwnd = h;
                int exStyle = GetWindowLong(h, GWL_EXSTYLE);
                SetWindowLong(h, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST);
                HwndSource src = HwndSource.FromHwnd(h);
                if (src != null) src.AddHook(TaskbarOverlayWndProc);
                lastTaskbarOverlayX = int.MinValue;
                lastTaskbarOverlayW = int.MinValue;
                UpdateTaskbarDevicesOverlayState();
            };

            taskbarDevicesWindow.Show();
            cachedTaskbarOverlayHwnd = new WindowInteropHelper(taskbarDevicesWindow).Handle;
            if (!showTaskbarDevices || latestTaskbarDevices.Count == 0)
            {
                if (cachedTaskbarOverlayHwnd != IntPtr.Zero) ShowWindow(cachedTaskbarOverlayHwnd, SW_HIDE);
            }
            RefreshTaskbarDevicesOverlayUi();
        }

        private bool IsWindowsTaskbarLightTheme()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize", false))
                {
                    if (key != null)
                    {
                        object v = key.GetValue("SystemUsesLightTheme");
                        if (v is int && (int)v == 1) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private UIElement CreateTaskbarDeviceVectorIcon(string kind, Brush strokeBrush)
        {
            Canvas c = new Canvas { Width = 15, Height = 15, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) };
            Action<string> addStrokePath = (geom) =>
            {
                System.Windows.Shapes.Path p = new System.Windows.Shapes.Path
                {
                    Data = Geometry.Parse(geom),
                    Stroke = strokeBrush,
                    StrokeThickness = 1.35,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeLineJoin = PenLineJoin.Round
                };
                c.Children.Add(p);
            };
            if (kind == "Headset")
            {
                addStrokePath("M 2.5,8.5 V 7 A 5,5 0 0 1 12.5,7 V 8.5");
                addStrokePath("M 2.2,8 H 4.2 A 1,1 0 0 1 5.2,9 V 12 A 1,1 0 0 1 4.2,13 H 2.7 A 1.2,1.2 0 0 1 1.5,11.8 V 9.2 A 1.2,1.2 0 0 1 2.2,8 Z");
                addStrokePath("M 10.8,8 H 12.8 A 1.2,1.2 0 0 1 13.5,9.2 V 11.8 A 1.2,1.2 0 0 1 12.3,13 H 10.8 A 1,1 0 0 1 9.8,12 V 9 A 1,1 0 0 1 10.8,8 Z");
            }
            else if (kind == "Mouse")
            {
                addStrokePath("M 7.5,1.6 C 5.1,1.6 3.5,3.2 3.5,5.5 V 9.8 C 3.5,12.1 5.1,13.6 7.5,13.6 C 9.9,13.6 11.5,12.1 11.5,9.8 V 5.5 C 11.5,3.2 9.9,1.6 7.5,1.6 Z");
                addStrokePath("M 7.5,4.1 V 6.6");
            }
            else if (kind == "Keyboard")
            {
                addStrokePath("M 1.6,4.2 H 13.4 A 1.1,1.1 0 0 1 14.5,5.3 V 10.4 A 1.1,1.1 0 0 1 13.4,11.5 H 1.6 A 1.1,1.1 0 0 1 0.5,10.4 V 5.3 A 1.1,1.1 0 0 1 1.6,4.2 Z");
                addStrokePath("M 3.2,6.6 H 4.6 M 6.4,6.6 H 7.8 M 9.6,6.6 H 11.6 M 4.2,9.1 H 10.6");
            }
            else
            {
                addStrokePath("M 4.1,4.6 H 10.9 C 12.4,4.6 13.6,6.0 13.8,8.1 L 14.1,10.6 C 14.2,11.6 13.4,12.3 12.4,12.1 C 11.7,12.0 11.2,11.4 10.7,10.6 H 4.3 C 3.8,11.4 3.3,12.0 2.6,12.1 C 1.6,12.3 0.8,11.6 0.9,10.6 L 1.2,8.1 C 1.4,6.0 2.6,4.6 4.1,4.6 Z");
                addStrokePath("M 3.7,7.6 H 5.5 M 4.6,6.7 V 8.5 M 9.7,7.1 H 10.1 M 11.0,8.1 H 11.4");
            }
            return c;
        }

        private void RefreshTaskbarDevicesOverlayUi()
        {
            if (taskbarDevicesWindow == null || taskbarDevicesPanel == null) return;
            bool lightTheme = IsWindowsTaskbarLightTheme();
            StringBuilder sigSb = new StringBuilder();
            sigSb.Append(lightTheme ? "L|" : "D|").Append(appLanguage).Append('|');
            for (int i = 0; i < latestTaskbarDevices.Count; i++)
            {
                sigSb.Append(latestTaskbarDevices[i].Kind).Append(':').Append(latestTaskbarDevices[i].Pct).Append(latestTaskbarDevices[i].IsCharging ? ":C:" : ":").Append(latestTaskbarDevices[i].Detail).Append('|');
            }
            string sig = sigSb.ToString();
            if (string.Equals(sig, lastTaskbarDevicesSignature, StringComparison.Ordinal))
            {
                UpdateTaskbarDevicesOverlayState();
                return;
            }
            lastTaskbarDevicesSignature = sig;

            taskbarDevicesPanel.Children.Clear();
            Brush normalFg = HexBrush(lightTheme ? "#1B1B1B" : "#F3F3F3");
            Brush chargingFg = HexBrush(lightTheme ? "#15803D" : "#4ADE80");
            Brush lowFg = HexBrush(lightTheme ? "#C42B1C" : "#FF5A5A");
            Brush hoverBg = HexBrush(lightTheme ? "#16000000" : "#1CFFFFFF");

            for (int i = 0; i < latestTaskbarDevices.Count; i++)
            {
                TaskbarDeviceItem d = latestTaskbarDevices[i];
                Brush fg = d.IsCharging ? chargingFg : (d.Pct <= 10 ? lowFg : normalFg);

                Border pill = new Border
                {
                    CornerRadius = new CornerRadius(5),
                    Background = HexBrush("#01000000"),
                    Padding = new Thickness(7, 4, 7, 4),
                    Margin = new Thickness(i == 0 ? 0 : 2, 0, 0, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = string.Format("{0}\n{1}% • {2}\n{3}", d.Name, d.Pct, d.Detail, L("(Yenile: Sol tık • Ayarlar: Sağ tık)", "(Refresh: Left click • Settings: Right click)"))
                };
                pill.MouseEnter += (s, e) => pill.Background = hoverBg;
                pill.MouseLeave += (s, e) => pill.Background = HexBrush("#01000000");
                pill.MouseLeftButtonDown += (s, e) =>
                {
                    Task.Run(() => PollWirelessDevicesAsync());
                    ShowToast(L("🔋 Cihaz şarjları yenileniyor...", "🔋 Refreshing wireless device batteries..."));
                    e.Handled = true;
                };
                pill.MouseRightButtonUp += (s, e) =>
                {
                    OpenSettingsWindow();
                    e.Handled = true;
                };

                StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                sp.Children.Add(CreateTaskbarDeviceVectorIcon(d.Kind, fg));
                sp.Children.Add(new TextBlock
                {
                    Text = d.Pct + "%",
                    Foreground = fg,
                    FontSize = 12.2,
                    FontWeight = FontWeights.Normal,
                    VerticalAlignment = VerticalAlignment.Center
                });
                pill.Child = sp;
                taskbarDevicesPanel.Children.Add(pill);
            }

            taskbarDevicesPanel.Measure(new Size(double.PositiveInfinity, 48));
            lastTaskbarOverlayX = int.MinValue;
            UpdateTaskbarDevicesOverlayState();
        }

        private bool IsForegroundFullscreenOnPrimary(IntPtr fg, IntPtr shellTray)
        {
            if (fg == IntPtr.Zero || fg == shellTray || fg == cachedMainHwnd || fg == cachedCalHwnd || fg == cachedTimerHwnd || fg == cachedTaskbarOverlayHwnd)
                return false;
            if (!IsWindowVisible(fg)) return false;

            fgClassBuilder.Length = 0;
            GetClassName(fg, fgClassBuilder, 64);
            string cls = fgClassBuilder.ToString();
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd" ||
                cls == "NotifyIconOverflowWindow" || cls == "TopLevelWindowForOverflowXamlIsland")
                return false;

            MonitorDdcNative.RECT r;
            if (!GetWindowRect(fg, out r)) return false;
            var pBounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            return r.left <= pBounds.Left + 2 && r.top <= pBounds.Top + 2 &&
                   r.right >= pBounds.Right - 2 && r.bottom >= pBounds.Bottom - 2;
        }

        private void UpdateTaskbarDevicesOverlayState()
        {
            if (taskbarDevicesWindow == null) return;
            IntPtr h = cachedTaskbarOverlayHwnd != IntPtr.Zero ? cachedTaskbarOverlayHwnd : new WindowInteropHelper(taskbarDevicesWindow).Handle;
            if (h == IntPtr.Zero) return;
            cachedTaskbarOverlayHwnd = h;

            if (!showTaskbarDevices || latestTaskbarDevices.Count == 0)
            {
                if (IsWindowVisible(h)) ShowWindow(h, SW_HIDE);
                return;
            }

            IntPtr shellTray = FindWindow("Shell_TrayWnd", null);
            if (shellTray == IntPtr.Zero || !IsWindowVisible(shellTray))
            {
                if (IsWindowVisible(h)) ShowWindow(h, SW_HIDE);
                return;
            }

            IntPtr fg = GetForegroundWindow();
            if (IsForegroundFullscreenOnPrimary(fg, shellTray))
            {
                taskbarOverlayHiddenByFullscreen = true;
                if (IsWindowVisible(h)) ShowWindow(h, SW_HIDE);
                return;
            }

            MonitorDdcNative.RECT trayRect;
            if (!GetWindowRect(shellTray, out trayRect) || (trayRect.bottom - trayRect.top) <= 8) return;

            double scaleX = 1.0, scaleY = 1.0;
            PresentationSource src = PresentationSource.FromVisual(taskbarDevicesWindow) ?? PresentationSource.FromVisual(this);
            if (src != null && src.CompositionTarget != null)
            {
                scaleX = src.CompositionTarget.TransformToDevice.M11;
                scaleY = src.CompositionTarget.TransformToDevice.M22;
            }
            if (scaleX <= 0.1) scaleX = 1.0;
            if (scaleY <= 0.1) scaleY = 1.0;

            if (!taskbarDevicesWindow.IsVisible)
                taskbarDevicesWindow.Show();

            double targetDipH = Math.Max(28.0, (trayRect.bottom - trayRect.top) / scaleY);
            if (taskbarDevicesPanel != null)
                taskbarDevicesPanel.Measure(new Size(double.PositiveInfinity, targetDipH));
            double desiredW = taskbarDevicesPanel != null ? taskbarDevicesPanel.DesiredSize.Width + 6.0 : 80.0;
            double targetDipW = Math.Max(28.0, Math.Ceiling(desiredW));
            if (Math.Abs(taskbarDevicesWindow.Width - targetDipW) > 0.5 || Math.Abs(taskbarDevicesWindow.Height - targetDipH) > 0.5)
            {
                taskbarDevicesWindow.Width = targetDipW;
                taskbarDevicesWindow.Height = targetDipH;
                taskbarDevicesWindow.UpdateLayout();
            }

            int overlayPhysW = Math.Max(28, (int)Math.Ceiling(targetDipW * scaleX));
            int overlayPhysH = Math.Max(24, trayRect.bottom - trayRect.top);

            int x;
            IntPtr notifyWnd = FindWindowEx(shellTray, IntPtr.Zero, "TrayNotifyWnd", null);
            MonitorDdcNative.RECT notifyRect;
            if (notifyWnd != IntPtr.Zero && GetWindowRect(notifyWnd, out notifyRect) &&
                notifyRect.right > notifyRect.left && notifyRect.left > trayRect.left + 80)
            {
                x = notifyRect.left - overlayPhysW - 4;
            }
            else
            {
                x = trayRect.right - overlayPhysW - (int)Math.Round(240 * scaleX);
            }
            if (x < trayRect.left + 8) x = trayRect.left + 8;
            if (x + overlayPhysW > trayRect.right - 8) x = Math.Max(trayRect.left + 8, trayRect.right - overlayPhysW - 8);
            int y = trayRect.top;

            bool needsReposition = taskbarOverlayHiddenByFullscreen || !IsWindowVisible(h) ||
                Math.Abs(overlayPhysW - lastTaskbarOverlayW) > 2 ||
                Math.Abs(x - lastTaskbarOverlayX) > 2 || Math.Abs(y - lastTaskbarOverlayY) > 2 ||
                IsWindowStrictlyAbove(shellTray, h);

            if (needsReposition)
            {
                taskbarOverlayHiddenByFullscreen = false;
                lastTaskbarOverlayX = x;
                lastTaskbarOverlayY = y;
                lastTaskbarOverlayW = overlayPhysW;
                SetWindowPos(h, HWND_TOPMOST, x, y, overlayPhysW, overlayPhysH, SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
        }

        // ==========================================
        // WEATHER FETCHER (DYNAMIC USER LOCATION)
        // ==========================================
        private double ToDisplayTemp(double tempC)
        {
            return useFahrenheit ? Math.Round((tempC * 9.0 / 5.0) + 32.0) : Math.Round(tempC);
        }

        private void RefreshWeatherTemperatureUi()
        {
            try
            {
                string unitSuffix = useFahrenheit ? "°F" : "°C";
                if (txtWeatherTemp != null)
                    txtWeatherTemp.Text = string.Format("{0:0}°", ToDisplayTemp(lastWeatherTempC));
                if (txtWeatherFeels != null)
                {
                    txtWeatherFeels.Text = IsEn
                        ? string.Format("Feels like {0:0}{1}", ToDisplayTemp(lastWeatherFeelsC), unitSuffix)
                        : string.Format("Hissedilen {0:0}{1}", ToDisplayTemp(lastWeatherFeelsC), unitSuffix);
                }
                for (int i = 0; i < 3; i++)
                {
                    if (txtDayTemps[i] != null && lastWeatherMinC != null && lastWeatherMaxC != null && lastWeatherMinC.Length > i && lastWeatherMaxC.Length > i)
                    {
                        txtDayTemps[i].Text = string.Format("{0:0}° / {1:0}°", ToDisplayTemp(lastWeatherMinC[i]), ToDisplayTemp(lastWeatherMaxC[i]));
                    }
                }
            }
            catch { }
        }

        private void FetchWeatherAsync()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                string latStr = locationLat.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
                string lonStr = locationLon.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
                string url = string.Format("https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}&current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=4", latStr, lonStr);

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = 6000;
                req.UserAgent = "OpenWidgetPublic/2.5";

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                {
                    string json = reader.ReadToEnd();

                    int currentIdx = json.IndexOf("\"current\":");
                    string currentSection = currentIdx >= 0 ? json.Substring(currentIdx) : json;

                    double temp = ExtractJsonDouble(currentSection, "temperature_2m", 14);
                    double feels = ExtractJsonDouble(currentSection, "apparent_temperature", 13);
                    double hum = ExtractJsonDouble(currentSection, "relative_humidity_2m", 75);
                    double wind = ExtractJsonDouble(currentSection, "wind_speed_10m", 12);
                    double precip = ExtractJsonDouble(currentSection, "precipitation", 0);
                    int wCode = (int)ExtractJsonDouble(currentSection, "weather_code", 1);

                    double[] minTemps = ExtractJsonArrayDoubles(json, "temperature_2m_min");
                    double[] maxTemps = ExtractJsonArrayDoubles(json, "temperature_2m_max");
                    double[] dayCodes = ExtractJsonArrayDoubles(json, "weather_code");

                    lastWeatherTempC = temp;
                    lastWeatherFeelsC = feels;
                    if (minTemps.Length >= 3) lastWeatherMinC = minTemps;
                    if (maxTemps.Length >= 3) lastWeatherMaxC = maxTemps;

                    Dispatcher.Invoke(() =>
                    {
                        txtLocationBadge.Text = locationName;
                        RefreshWeatherTemperatureUi();
                        txtWeatherDesc.Text = WeatherCodeToText(wCode);
                        txtWeatherMainIcon.Text = WeatherCodeToIcon(wCode);
                        txtWeatherHumidity.Text = IsEn ? (Math.Round(hum) + "%") : ("%" + Math.Round(hum));
                        txtWeatherWind.Text = Math.Round(wind) + L(" km/s", " km/h");
                        txtWeatherRain.Text = precip.ToString("0.#") + " mm";

                        string[] gunIsimleri = IsEn
                            ? new string[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" }
                            : new string[] { "Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi" };
                        for (int i = 0; i < 3; i++)
                        {
                            txtDayNames[i].Text = i == 0
                                ? L("Bugün", "Today")
                                : (i == 1 ? L("Yarın", "Tomorrow") : gunIsimleri[(int)DateTime.Now.AddDays(i).DayOfWeek]);
                            if (dayCodes.Length > i)
                            {
                                txtDayIcons[i].Text = WeatherCodeToIcon((int)dayCodes[i]);
                            }
                        }
                    });
                }
            }
            catch { }
        }

        private double ExtractJsonDouble(string json, string key, double fallback)
        {
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(-?\\d+(\\.\\d+)?)");
            if (m.Success)
            {
                double v;
                if (double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out v))
                    return v;
            }
            return fallback;
        }

        private double[] ExtractJsonArrayDoubles(string json, string key)
        {
            List<double> list = new List<double>();
            int dailyIdx = json.IndexOf("\"daily\":");
            string section = dailyIdx >= 0 ? json.Substring(dailyIdx) : json;
            Match m = Regex.Match(section, "\"" + key + "\"\\s*:\\s*\\[([^\\]]+)\\]");
            if (m.Success)
            {
                string[] parts = m.Groups[1].Value.Split(',');
                foreach (string p in parts)
                {
                    double v;
                    if (double.TryParse(p.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out v))
                        list.Add(v);
                }
            }
            return list.ToArray();
        }

        private string WeatherCodeToText(int code)
        {
            if (code == 0) return L("Açık", "Clear Sky");
            if (code == 1 || code == 2) return L("Parçalı Bulutlu", "Partly Cloudy");
            if (code == 3) return L("Çok Bulutlu", "Overcast");
            if (code == 45 || code == 48) return L("Sisli", "Foggy");
            if (code >= 51 && code <= 67) return L("Yağmurlu", "Rainy");
            if (code >= 71 && code <= 77) return L("Kar Yağışlı", "Snowy");
            if (code >= 80 && code <= 82) return L("Sağanak Yağışlı", "Rain Showers");
            if (code >= 95) return L("Fırtınalı", "Thunderstorm");
            return L("Parçalı Bulutlu", "Partly Cloudy");
        }

        private string WeatherCodeToIcon(int code)
        {
            if (code == 0) return "☀";
            if (code == 1 || code == 2) return "⛅";
            if (code == 3) return "☁";
            if (code == 45 || code == 48) return "🌫";
            if (code >= 51 && code <= 67) return "🌧";
            if (code >= 71 && code <= 77) return "❄";
            if (code >= 80 && code <= 82) return "💧";
            if (code >= 95) return "⚡";
            return "☀";
        }

        // ==========================================
        // EVENT-DRIVEN CLIPBOARD HISTORY MANAGER
        // ==========================================
        private void CheckClipboardChanges()
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText().Trim();
                    if (!string.IsNullOrEmpty(text) && text != lastClipboardText)
                    {
                        lastClipboardText = text;
                        clipboardHistory.RemoveAll(x => x.Text == text);
                        clipboardHistory.Insert(0, new ClipboardEntry
                        {
                            Text = text,
                            Time = DateTime.Now.ToString("HH:mm")
                        });
                        if (clipboardHistory.Count > 8)
                            clipboardHistory.RemoveAt(clipboardHistory.Count - 1);

                        RefreshClipboardUi();
                    }
                }
            }
            catch { }
        }

        private void RefreshClipboardUi()
        {
            panelClipItems.Children.Clear();
            txtClipCount.Text = clipboardHistory.Count + L(" öğe", " items");

            if (clipboardHistory.Count == 0)
            {
                Border emptyCard = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Background = HexBrush("#5526201D"),
                    BorderBrush = HexBrush("#3D322C"),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 8, 10, 8)
                };
                emptyCard.Child = new TextBlock
                {
                    Text = L("Henüz kopyalanan metin yok...", "No copied text yet..."),
                    Foreground = HexBrush("#8C7A6B"),
                    FontSize = 11.5,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                panelClipItems.Children.Add(emptyCard);
                return;
            }

            foreach (var item in clipboardHistory)
            {
                string copyText = item.Text;
                string singleLine = copyText.Replace("\r", " ").Replace("\n", " ");

                Border itemBorder = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Background = HexBrush("#7728211E"),
                    BorderBrush = HexBrush("#453831"),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 7, 10, 7),
                    Margin = new Thickness(0, 0, 0, 5),
                    Cursor = Cursors.Hand,
                    Tag = "Interactive",
                    ToolTip = L("Tekrar kopyalamak için tıklayın:\n", "Click to copy again:\n") + (copyText.Length > 200 ? copyText.Substring(0, 200) + "..." : copyText)
                };

                Grid row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                row.Children.Add(new TextBlock { Text = "📄", Foreground = HexBrush("#B8A99E"), FontSize = 11, Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center });

                TextBlock txtContent = new TextBlock
                {
                    Text = singleLine,
                    Foreground = HexBrush("#FAF6F2"),
                    FontSize = 11.5,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                Grid.SetColumn(txtContent, 1);
                row.Children.Add(txtContent);

                TextBlock txtTime = new TextBlock
                {
                    Text = item.Time,
                    Foreground = HexBrush("#9E8C7E"),
                    FontSize = 10.5,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(txtTime, 2);
                row.Children.Add(txtTime);

                itemBorder.Child = row;
                itemBorder.MouseEnter += (s, e) => itemBorder.Background = HexBrush("#AA382D27");
                itemBorder.MouseLeave += (s, e) => itemBorder.Background = HexBrush("#7728211E");
                itemBorder.MouseLeftButtonDown += (s, e) =>
                {
                    try
                    {
                        lastClipboardText = copyText;
                        Clipboard.SetText(copyText);
                        ShowToast(L("Panoya kopyalandı!", "Copied to clipboard!"));
                    }
                    catch { }
                };

                panelClipItems.Children.Add(itemBorder);
            }
        }

        private void ShowToast(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                txtToastMessage.Text = msg;
                toastPopup.IsOpen = true;
                toastTimer.Stop();
                toastTimer.Start();
            });
        }

        // =========================================================================
        // STANDALONE APPLE-STYLE GOOGLE CALENDAR DESKTOP WIDGET (SEPARATE WINDOW)
        // =========================================================================
        public void SetCalendarWidgetVisible(bool visible)
        {
            showCalendarWidget = visible;
            SaveSettings();
            if (trayCalendarItem != null)
            {
                trayCalendarItem.Text = showCalendarWidget
                    ? L("📅 Takvim Widget'ını Gizle", "📅 Hide Calendar Widget")
                    : L("📅 Takvim Widget'ını Göster", "📅 Show Calendar Widget");
            }
            if (calendarWindow == null && showCalendarWidget)
            {
                InitializeCalendarWidget();
                return;
            }
            if (calendarWindow != null)
            {
                if (showCalendarWidget)
                {
                    calendarWindow.Show();
                    IntPtr calHwnd = cachedCalHwnd != IntPtr.Zero ? cachedCalHwnd : new WindowInteropHelper(calendarWindow).Handle;
                    if (calHwnd != IntPtr.Zero) SendWidgetToDesktopBottom(calHwnd);
                    RefreshCalendarWidgetUi();
                }
                else
                {
                    calendarWindow.Hide();
                }
            }
        }

        private void InitializeCalendarWidget()
        {
            if (calendarWindow != null) return;

            calendarWindow = new Window
            {
                Title = L("OpenWidget • Takvim", "OpenWidget • Calendar"),
                MinWidth = 390,
                MinHeight = 215,
                Width = savedCalWidth,
                Height = savedCalHeight,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = isDraggable ? ResizeMode.CanResize : ResizeMode.NoResize,
                ShowInTaskbar = false,
                Topmost = false,
                UseLayoutRounding = true,
                SnapsToDevicePixels = true,
                FontFamily = new FontFamily("Segoe UI")
            };
            Program.TryApplyWindowIcon(calendarWindow);
            TextOptions.SetTextFormattingMode(calendarWindow, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(calendarWindow, TextRenderingMode.ClearType);
            RenderOptions.SetClearTypeHint(calendarWindow, ClearTypeHint.Enabled);

            if (IsPositionWithinVirtualDesktop(savedCalLeft, savedCalTop, savedCalWidth, savedCalHeight))
            {
                calendarWindow.Left = savedCalLeft;
                calendarWindow.Top = savedCalTop;
            }
            else
            {
                Rect workArea = GetWindowMonitorWorkArea(this);
                double defaultLeft = this.Left - savedCalWidth - 10;
                if (defaultLeft < workArea.Left + 10) defaultLeft = workArea.Left + 14;
                calendarWindow.Left = defaultLeft;
                calendarWindow.Top = this.Top;
                savedCalLeft = calendarWindow.Left;
                savedCalTop = calendarWindow.Top;
            }

            calWindowChrome = new WindowChrome
            {
                CaptionHeight = 0,
                CornerRadius = new CornerRadius(0),
                GlassFrameThickness = new Thickness(0),
                NonClientFrameEdges = NonClientFrameEdges.None,
                ResizeBorderThickness = isDraggable ? new Thickness(8) : new Thickness(0)
            };
            WindowChrome.SetWindowChrome(calendarWindow, calWindowChrome);

            calendarWindow.SizeChanged += (s, e) =>
            {
                if (e.HeightChanged && calendarWindow.ActualHeight >= 210)
                {
                    RebuildCalendarLeftColumn(calendarWindow.ActualHeight);
                }
                if (calendarWindow.ActualWidth >= 380 && calendarWindow.ActualHeight >= 210)
                {
                    savedCalWidth = calendarWindow.ActualWidth;
                    savedCalHeight = calendarWindow.ActualHeight;
                    SaveSettingsDebounced();
                }
            };

            calendarWindow.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (!isDraggable) return;
                DependencyObject dep = e.OriginalSource as DependencyObject;
                while (dep != null && dep != calendarWindow)
                {
                    if (dep is ButtonBase || dep is Slider || dep is Thumb || dep is TextBox || dep is ScrollBar)
                        return;
                    if (dep is FrameworkElement)
                    {
                        FrameworkElement fe = (FrameworkElement)dep;
                        if (fe.Tag != null && fe.Tag.ToString() == "Interactive") return;
                    }
                    dep = VisualTreeHelper.GetParent(dep);
                }
                try
                {
                    IntPtr calHwnd = cachedCalHwnd != IntPtr.Zero ? cachedCalHwnd : new WindowInteropHelper(calendarWindow).Handle;
                    ReleaseCapture();
                    SendMessage(calHwnd, WM_NCLBUTTONDOWN, (IntPtr)HT_CAPTION, IntPtr.Zero);
                    savedCalLeft = calendarWindow.Left;
                    savedCalTop = calendarWindow.Top;
                    SaveSettingsDebounced();
                    e.Handled = true;
                }
                catch { }
            };

            calendarWindow.LocationChanged += (s, e) =>
            {
                if (isCalWindowInitialized && !double.IsNaN(calendarWindow.Left) && !double.IsNaN(calendarWindow.Top) && calendarWindow.Left > -10000 && calendarWindow.Top > -10000)
                {
                    savedCalLeft = calendarWindow.Left;
                    savedCalTop = calendarWindow.Top;
                    SaveSettingsDebounced();
                }
            };

            calendarWindow.Loaded += (s, e) =>
            {
                IntPtr calHwnd = new WindowInteropHelper(calendarWindow).Handle;
                cachedCalHwnd = calHwnd;
                int exStyle = GetWindowLong(calHwnd, GWL_EXSTYLE);
                SetWindowLong(calHwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
                AttachToDesktopShell(calHwnd);
                HwndSource src = HwndSource.FromHwnd(calHwnd);
                if (src != null) src.AddHook(WndProc);
                isCalWindowInitialized = true;
            };

            calendarWindow.StateChanged += (s, e) =>
            {
                if (calendarWindow.WindowState == WindowState.Minimized)
                    calendarWindow.WindowState = WindowState.Normal;
            };

            BuildCalendarWidgetUi();
            ApplyCalendarWidgetTheme();

            if (showCalendarWidget)
            {
                calendarWindow.Show();
            }

            Task.Run(() => LoadAndSyncCalendarEventsAsync(false));

            DispatcherTimer calUiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(45) };
            int calTick = 0;
            calUiTimer.Tick += (s, e) =>
            {
                calTick++;
                if (showCalendarWidget) RefreshCalendarWidgetUi(false);
                if (calTick % 7 == 0 && showCalendarWidget)
                {
                    Task.Run(() => LoadAndSyncCalendarEventsAsync(false));
                }
            };
            calUiTimer.Start();
        }

        private void BuildCalendarWidgetUi()
        {
            calOuterBorder = new Border
            {
                CornerRadius = new CornerRadius(16),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(2),
                Padding = new Thickness(14, 12, 14, 12)
            };

            Grid rootGrid = new Grid();
            Grid twoPaneGrid = new Grid();
            twoPaneGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(182) });
            twoPaneGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            twoPaneGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // LEFT PANE
            Grid leftPane = new Grid();
            leftPane.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            leftPane.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            leftPane.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid dateTopRow = new Grid { Margin = new Thickness(2, 0, 2, 6) };
            dateTopRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dateTopRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel dateLeftStack = new StackPanel();
            txtCalDayOfWeekHeader = new TextBlock
            {
                Text = L("BUGÜN", "TODAY"),
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = HexBrush(accentColorHex)
            };
            dateLeftStack.Children.Add(txtCalDayOfWeekHeader);

            StackPanel bigNumRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
            txtCalBigDayNum = new TextBlock
            {
                Text = DateTime.Now.Day.ToString(),
                FontSize = 27,
                FontWeight = FontWeights.Bold,
                Foreground = HexBrush("#FAF6F2"),
                Margin = new Thickness(0, -3, 7, -2)
            };
            txtCalMonthYearLabel = new TextBlock
            {
                Text = "",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = HexBrush("#B8A99E"),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 3)
            };
            bigNumRow.Children.Add(txtCalBigDayNum);
            bigNumRow.Children.Add(txtCalMonthYearLabel);
            dateLeftStack.Children.Add(bigNumRow);
            Grid.SetColumn(dateLeftStack, 0);
            dateTopRow.Children.Add(dateLeftStack);

            StackPanel monthNavBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
            Button btnPrevMonth = CreateStyledIconButton("#26201D", "Transparent", L("Önceki Ay", "Previous Month"));
            btnPrevMonth.Width = 20; btnPrevMonth.Height = 20;
            btnPrevMonth.Content = new TextBlock { Text = "‹", Foreground = HexBrush("#C7B9AE"), FontSize = 13, FontWeight = FontWeights.Bold, Margin = new Thickness(0, -2, 0, 0) };
            btnPrevMonth.Click += (s, e) =>
            {
                calDisplayedMonth = calDisplayedMonth.AddMonths(-1);
                RefreshCalendarWidgetUi();
            };

            Button btnGoToday = CreateStyledIconButton("#26201D", "Transparent", L("Bugüne Dön", "Jump to Today"));
            btnGoToday.Width = 20; btnGoToday.Height = 20;
            btnGoToday.Margin = new Thickness(2, 0, 2, 0);
            btnGoToday.Content = new TextBlock { Text = "•", Foreground = HexBrush(accentColorHex), FontSize = 13, FontWeight = FontWeights.Bold, Margin = new Thickness(0, -2, 0, 0) };
            btnGoToday.Click += (s, e) =>
            {
                calSelectedDate = DateTime.Today;
                calDisplayedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                RefreshCalendarWidgetUi();
            };

            Button btnNextMonth = CreateStyledIconButton("#26201D", "Transparent", L("Sonraki Ay", "Next Month"));
            btnNextMonth.Width = 20; btnNextMonth.Height = 20;
            btnNextMonth.Content = new TextBlock { Text = "›", Foreground = HexBrush("#C7B9AE"), FontSize = 13, FontWeight = FontWeights.Bold, Margin = new Thickness(0, -2, 0, 0) };
            btnNextMonth.Click += (s, e) =>
            {
                calDisplayedMonth = calDisplayedMonth.AddMonths(1);
                RefreshCalendarWidgetUi();
            };

            monthNavBtns.Children.Add(btnPrevMonth);
            monthNavBtns.Children.Add(btnGoToday);
            monthNavBtns.Children.Add(btnNextMonth);
            Grid.SetColumn(monthNavBtns, 1);
            dateTopRow.Children.Add(monthNavBtns);
            Grid.SetRow(dateTopRow, 0);
            leftPane.Children.Add(dateTopRow);

            UniformGrid weekDayHdr = new UniformGrid { Columns = 7, Margin = new Thickness(0, 0, 0, 3) };
            string[] shortDays = IsEn
                ? new string[] { "M", "T", "W", "T", "F", "S", "S" }
                : new string[] { "P", "S", "Ç", "P", "C", "C", "P" };
            for (int i = 0; i < 7; i++)
            {
                weekDayHdr.Children.Add(new TextBlock
                {
                    Text = shortDays[i],
                    Foreground = HexBrush(i >= 5 ? "#8C7A6B" : "#A8988C"),
                    FontSize = 9.5,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
            }
            Grid.SetRow(weekDayHdr, 1);
            leftPane.Children.Add(weekDayHdr);

            calMiniMonthGrid = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Top,
                ClipToBounds = true
            };
            Grid.SetRow(calMiniMonthGrid, 2);
            leftPane.Children.Add(calMiniMonthGrid);

            Grid.SetColumn(leftPane, 0);
            twoPaneGrid.Children.Add(leftPane);

            Border vDivider = new Border
            {
                Width = 1,
                Background = HexBrush("#3B302A"),
                Margin = new Thickness(6, 2, 6, 2)
            };
            Grid.SetColumn(vDivider, 1);
            twoPaneGrid.Children.Add(vDivider);

            // RIGHT PANE
            Grid rightPane = new Grid();
            rightPane.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rightPane.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rightPane.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid rightHeader = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            rightHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rightHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            txtCalRightScheduleTitle = new TextBlock
            {
                Text = L("BUGÜN • ETKİNLİKLER", "TODAY • SCHEDULE"),
                Foreground = HexBrush("#C7B9AE"),
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(txtCalRightScheduleTitle, 0);
            rightHeader.Children.Add(txtCalRightScheduleTitle);

            StackPanel rightBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            Button btnQuickAdd = CreateStyledIconButton("#26201D", "Transparent", L("Hızlı Etkinlik / Hatırlatıcı Ekle", "Quick Add Event / Reminder"));
            btnQuickAdd.Width = 22; btnQuickAdd.Height = 22;
            btnQuickAdd.Margin = new Thickness(0, 0, 3, 0);
            btnQuickAdd.Content = new TextBlock { Text = "+", Foreground = HexBrush(accentColorHex), FontSize = 13, FontWeight = FontWeights.Bold, Margin = new Thickness(0, -2, 0, 0) };
            btnQuickAdd.Click += (s, e) =>
            {
                calQuickAddPanel.Visibility = (calQuickAddPanel.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible;
                FitCalendarHeightToContent();
            };
            rightBtns.Children.Add(btnQuickAdd);

            Button btnSyncCal = CreateStyledIconButton("#26201D", "Transparent", L("Takvimi Şimdi Senkronize Et", "Sync Calendar Now"));
            btnSyncCal.Width = 22; btnSyncCal.Height = 22;
            btnSyncCal.Margin = new Thickness(0, 0, 3, 0);
            btnSyncCal.Content = new TextBlock { Text = "↻", Foreground = HexBrush("#C7B9AE"), FontSize = 11.5, FontWeight = FontWeights.Bold };
            btnSyncCal.Click += (s, e) =>
            {
                Task.Run(() => LoadAndSyncCalendarEventsAsync(true));
            };
            rightBtns.Children.Add(btnSyncCal);

            Button btnOpenGCalWeb = CreateStyledIconButton("#26201D", "Transparent", L("Google Takvim'i Tarayıcıda Aç", "Open Google Calendar in Browser"));
            btnOpenGCalWeb.Width = 22; btnOpenGCalWeb.Height = 22;
            btnOpenGCalWeb.Margin = new Thickness(0, 0, 3, 0);
            btnOpenGCalWeb.Content = new TextBlock { Text = "📅", Foreground = HexBrush("#C7B9AE"), FontSize = 10.5 };
            btnOpenGCalWeb.Click += (s, e) =>
            {
                try { Process.Start("https://calendar.google.com/calendar/r"); } catch { }
            };
            rightBtns.Children.Add(btnOpenGCalWeb);

            btnCalLockDrag = CreateStyledIconButton("#26201D", "Transparent", L("Taşıma ve Boyutlandırma Kilidi", "Drag & Resize Lock"));
            btnCalLockDrag.Width = 22; btnCalLockDrag.Height = 22;
            btnCalLockDrag.Click += (s, e) => ToggleDraggableMode();
            rightBtns.Children.Add(btnCalLockDrag);

            Grid.SetColumn(rightBtns, 1);
            rightHeader.Children.Add(rightBtns);
            Grid.SetRow(rightHeader, 0);
            rightPane.Children.Add(rightHeader);

            calQuickAddPanel = new Border
            {
                Visibility = Visibility.Collapsed,
                CornerRadius = new CornerRadius(8),
                Background = HexBrush("#261E1A"),
                BorderBrush = HexBrush("#4A3C34"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(7, 5, 7, 5),
                Margin = new Thickness(0, 0, 0, 6),
                Tag = "Interactive"
            };
            Grid qaGrid = new Grid();
            qaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(68) });
            qaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            qaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            txtCalQuickTime = new TextBox
            {
                Text = "14:00",
                Background = HexBrush("#1C1613"),
                Foreground = HexBrush("#FAF6F2"),
                BorderBrush = HexBrush("#3E322B"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4, 2, 4, 2),
                FontSize = 10.5,
                Margin = new Thickness(0, 0, 4, 0),
                ToolTip = L("Saat (Örn: 14:00 veya 09:30-11:00)", "Time (e.g., 14:00 or 09:30-11:00)")
            };
            Grid.SetColumn(txtCalQuickTime, 0);
            qaGrid.Children.Add(txtCalQuickTime);

            txtCalQuickTitle = new TextBox
            {
                Text = "",
                Background = HexBrush("#1C1613"),
                Foreground = HexBrush("#FAF6F2"),
                BorderBrush = HexBrush("#3E322B"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5, 2, 5, 2),
                FontSize = 10.5,
                Margin = new Thickness(0, 0, 4, 0),
                ToolTip = L("Etkinlik başlığı yazın", "Enter event title")
            };
            txtCalQuickLoc = new TextBox { Text = "" };
            Grid.SetColumn(txtCalQuickTitle, 1);
            qaGrid.Children.Add(txtCalQuickTitle);

            Button btnSaveQuickEvent = CreateStyledIconButton("#38281E", accentColorHex, L("Seçili güne etkinlik ekle", "Add event to selected day"));
            btnSaveQuickEvent.Padding = new Thickness(8, 2, 8, 2);
            btnSaveQuickEvent.Content = new TextBlock { Text = L("Ekle", "Add"), Foreground = HexBrush("#FAF6F2"), FontSize = 10, FontWeight = FontWeights.Bold };
            btnSaveQuickEvent.Click += (s, e) => AddCustomCalendarEventFromUi();
            Grid.SetColumn(btnSaveQuickEvent, 2);
            qaGrid.Children.Add(btnSaveQuickEvent);

            calQuickAddPanel.Child = qaGrid;
            Grid.SetRow(calQuickAddPanel, 1);
            rightPane.Children.Add(calQuickAddPanel);

            calEventsScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                PanningMode = PanningMode.VerticalOnly
            };
            ApplyThinDarkScrollBarStyle(calEventsScroll);
            calEventsListPanel = new StackPanel { Margin = new Thickness(0, 0, 3, 0) };
            calEventsScroll.Content = calEventsListPanel;
            Grid.SetRow(calEventsScroll, 2);
            rightPane.Children.Add(calEventsScroll);

            Grid.SetColumn(rightPane, 2);
            twoPaneGrid.Children.Add(rightPane);

            rootGrid.Children.Add(twoPaneGrid);

            calResizeGrip = new Border
            {
                Width = 12,
                Height = 12,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, -8, -6),
                Visibility = isDraggable ? Visibility.Visible : Visibility.Collapsed,
                Child = new TextBlock { Text = "◢", FontSize = 9, Foreground = HexBrush("#8C7A6B") }
            };
            rootGrid.Children.Add(calResizeGrip);

            calOuterBorder.Child = rootGrid;
            calendarWindow.Content = calOuterBorder;
            UpdateDraggableButtonUi();
        }

        private void ApplyThinDarkScrollBarStyle(ScrollViewer sv)
        {
            Program.ApplyThinScrollBarStyle(sv, accentColorHex);
        }

        private void ApplyCalendarWidgetTheme()
        {
            if (calOuterBorder == null) return;
            try
            {
                Color accent = ParseColor(accentColorHex);
                byte mainAlpha = (byte)Math.Round((bgOpacityPercent / 100.0) * 255.0);
                Color darkTop = BlendColors(Color.FromRgb(22, 19, 17), accent, 0.08, mainAlpha);
                Color darkBottom = BlendColors(Color.FromRgb(28, 24, 21), accent, 0.12, mainAlpha);
                Color outerBorderCol = BlendColors(Color.FromRgb(56, 48, 42), accent, 0.35, 220);

                LinearGradientBrush grad = new LinearGradientBrush(darkTop, darkBottom, new Point(0, 0), new Point(0, 1));
                grad.Freeze();
                calOuterBorder.Background = grad;

                SolidColorBrush ob = new SolidColorBrush(outerBorderCol);
                ob.Freeze();
                calOuterBorder.BorderBrush = ob;

                if (txtCalDayOfWeekHeader != null)
                    txtCalDayOfWeekHeader.Foreground = HexBrush(accentColorHex);
                if (calEventsScroll != null)
                    ApplyThinDarkScrollBarStyle(calEventsScroll);

                RefreshCalendarWidgetUi();
            }
            catch { }
        }

        private void RefreshCalendarWidgetUi(bool autoShrinkHeight = true)
        {
            if (calendarWindow == null || calMiniMonthGrid == null || calEventsListPanel == null) return;
            try
            {
                string[] dayNamesUpper = IsEn
                    ? new string[] { "SUNDAY", "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY" }
                    : new string[] { "PAZAR", "PAZARTESİ", "SALI", "ÇARŞAMBA", "PERŞEMBE", "CUMA", "CUMARTESİ" };
                string[] dayNamesShortUpper = IsEn
                    ? new string[] { "SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT" }
                    : new string[] { "PAZ", "PZT", "SAL", "ÇAR", "PER", "CUM", "CMT" };
                string[] monthNames = IsEn
                    ? new string[] { "", "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" }
                    : new string[] { "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };
                string[] monthNamesUpper = IsEn
                    ? new string[] { "", "JANUARY", "FEBRUARY", "MARCH", "APRIL", "MAY", "JUNE", "JULY", "AUGUST", "SEPTEMBER", "OCTOBER", "NOVEMBER", "DECEMBER" }
                    : new string[] { "", "OCAK", "ŞUBAT", "MART", "NİSAN", "MAYIS", "HAZİRAN", "TEMMUZ", "AĞUSTOS", "EYLÜL", "EKİM", "KASIM", "ARALIK" };

                DateTime today = DateTime.Today;
                DateTime headerDate = calSelectedDate.Date;

                txtCalDayOfWeekHeader.Text = dayNamesUpper[(int)headerDate.DayOfWeek];
                txtCalDayOfWeekHeader.Foreground = HexBrush(accentColorHex);
                txtCalBigDayNum.Text = headerDate.Day.ToString();
                txtCalMonthYearLabel.Text = monthNames[calDisplayedMonth.Month] + " " + calDisplayedMonth.Year;

                List<CalendarEventItem> snapshot;
                int evVer;
                lock (calEventsLock)
                {
                    snapshot = new List<CalendarEventItem>(allCalendarEvents);
                    evVer = calEventsVersion;
                }

                List<CalendarEventItem> dayEvents = GetEventsForDate(snapshot, calSelectedDate.Date);
                bool isViewingToday = (calSelectedDate.Date == today);
                DateTime now = DateTime.Now;

                int activeStateMask = 0;
                if (isViewingToday)
                {
                    for (int i = 0; i < dayEvents.Count; i++)
                    {
                        CalendarEventItem ev = dayEvents[i];
                        int st = (!ev.AllDay && now >= ev.Start && now <= ev.End) ? 2 : ((!ev.AllDay && now > ev.End) ? 1 : 0);
                        activeStateMask = (activeStateMask * 31) + st;
                    }
                }
                string rightSig = string.Format("{0:yyyyMMdd}|{1:yyyyMMdd}|{2}|{3}|{4}|{5}|{6}",
                    today, calSelectedDate.Date, evVer, accentColorHex, dayEvents.Count, activeStateMask, appLanguage);

                if (autoShrinkHeight || rightSig != lastRightScheduleSignature)
                {
                    lastRightScheduleSignature = rightSig;

                    calEventsListPanel.Children.Clear();

                    if (isViewingToday)
                    {
                        txtCalRightScheduleTitle.Text = string.Format("{0} • {1} {2} ({3})",
                            L("BUGÜN", "TODAY"),
                            today.Day,
                            monthNamesUpper[today.Month],
                            dayEvents.Count > 0 ? (dayEvents.Count + L(" ETKİNLİK", " EVENTS")) : L("BOŞ", "FREE"));
                    }
                    else
                    {
                        txtCalRightScheduleTitle.Text = string.Format("{0} {1} {2} • {3} {4}",
                            calSelectedDate.Day,
                            monthNamesUpper[calSelectedDate.Month],
                            dayNamesShortUpper[(int)calSelectedDate.DayOfWeek],
                            dayEvents.Count,
                            L("ETKİNLİK", "EVENTS"));
                    }

                    if (dayEvents.Count == 0)
                    {
                        Border emptyBox = new Border
                        {
                            CornerRadius = new CornerRadius(8),
                            Background = HexBrush("#4426201D"),
                            BorderBrush = HexBrush("#382E28"),
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(10, 7, 10, 7),
                            Margin = new Thickness(0, 0, 0, 6)
                        };
                        string emptyMsg = isViewingToday
                            ? (string.IsNullOrEmpty(calendarSecretIcalUrl)
                                ? L("Bugün etkinlik yok. Ayarlar > Ek Widget'lar'dan iCal linki ekleyebilir veya '+' ile etkinlik oluşturabilirsiniz.",
                                    "No events today. Add a Google iCal URL in Settings > Widgets or click '+' to add one.")
                                : L("Bugün için planlanmış etkinlik bulunmuyor.", "No events scheduled for today."))
                            : L("Bu tarihte kayıtlı etkinlik bulunmuyor.", "No events recorded for this date.");
                        emptyBox.Child = new TextBlock
                        {
                            Text = emptyMsg,
                            Foreground = HexBrush("#9E8C7E"),
                            FontSize = 10.5,
                            TextWrapping = TextWrapping.Wrap
                        };
                        calEventsListPanel.Children.Add(emptyBox);
                    }
                    else
                    {
                        foreach (CalendarEventItem ev in dayEvents)
                        {
                            calEventsListPanel.Children.Add(CreateAppleCalendarEventPill(ev, now, isViewingToday));
                        }
                    }

                    if (isViewingToday)
                    {
                        for (int ahead = 1; ahead <= 7; ahead++)
                        {
                            DateTime nextDate = today.AddDays(ahead);
                            List<CalendarEventItem> nextEvents = GetEventsForDate(snapshot, nextDate);
                            if (nextEvents.Count > 0)
                            {
                                string nextHdr = ahead == 1
                                    ? string.Format("{0} ({1} {2})", L("SIRADAKİ • YARIN", "UP NEXT • TOMORROW"), nextDate.Day, monthNamesUpper[nextDate.Month])
                                    : string.Format("{0} • {1} {2} {3}", L("SIRADAKİ", "UP NEXT"), nextDate.Day, monthNamesUpper[nextDate.Month], dayNamesUpper[(int)nextDate.DayOfWeek]);

                                calEventsListPanel.Children.Add(new TextBlock
                                {
                                    Text = nextHdr,
                                    Foreground = HexBrush("#A8988C"),
                                    FontSize = 9.5,
                                    FontWeight = FontWeights.Bold,
                                    Margin = new Thickness(2, 5, 0, 4)
                                });

                                int shownNext = 0;
                                foreach (CalendarEventItem nev in nextEvents)
                                {
                                    calEventsListPanel.Children.Add(CreateAppleCalendarEventPill(nev, now, false));
                                    shownNext++;
                                    if (shownNext >= 3) break;
                                }
                                break;
                            }
                        }
                    }

                    FitCalendarHeightToContent(autoShrinkHeight);
                }

                double effectiveH = (!double.IsNaN(calendarWindow.Height) && calendarWindow.Height > 200)
                    ? calendarWindow.Height
                    : (calendarWindow.ActualHeight > 200 ? calendarWindow.ActualHeight : savedCalHeight);
                RebuildCalendarLeftColumn(effectiveH);
            }
            catch { }
        }

        private void RebuildCalendarLeftColumn(double windowHeight)
        {
            if (calMiniMonthGrid == null) return;
            try
            {
                DateTime today = DateTime.Today;
                double availLeftH = Math.Max(138.0, windowHeight - 90.0);
                const double rowH = 22.0;
                const double subHdrH = 22.0;

                int quantizedSteps = (int)(availLeftH / 11.0);
                Dictionary<DateTime, byte> dotsLookup;
                int evVer;
                lock (calEventsLock)
                {
                    dotsLookup = calEventDotsByDate;
                    evVer = calEventsVersion;
                }

                string leftSig = string.Format("{0:yyyyMM}|{1:yyyyMMdd}|{2:yyyyMMdd}|{3}|{4}|{5}|{6}",
                    calDisplayedMonth, calSelectedDate.Date, today, evVer, accentColorHex, quantizedSteps, appLanguage);
                if (leftSig == lastLeftColumnSignature && calMiniMonthGrid.Children.Count > 0)
                    return;
                lastLeftColumnSignature = leftSig;

                string[] monthNamesUpper = IsEn
                    ? new string[] { "", "JANUARY", "FEBRUARY", "MARCH", "APRIL", "MAY", "JUNE", "JULY", "AUGUST", "SEPTEMBER", "OCTOBER", "NOVEMBER", "DECEMBER" }
                    : new string[] { "", "OCAK", "ŞUBAT", "MART", "NİSAN", "MAYIS", "HAZİRAN", "TEMMUZ", "AĞUSTOS", "EYLÜL", "EKİM", "KASIM", "ARALIK" };

                calMiniMonthGrid.Children.Clear();
                double usedH = 0.0;

                for (int m = 0; m < 12; m++)
                {
                    DateTime monthDate = new DateTime(calDisplayedMonth.Year, calDisplayedMonth.Month, 1).AddMonths(m);
                    int firstDayOffset = ((int)monthDate.DayOfWeek + 6) % 7;
                    int daysInMonth = DateTime.DaysInMonth(monthDate.Year, monthDate.Month);
                    int weekRows = (int)Math.Ceiling((firstDayOffset + daysInMonth) / 7.0);
                    DateTime gridStart = monthDate.AddDays(-firstDayOffset);

                    if (m > 0)
                    {
                        if (usedH + subHdrH + rowH > availLeftH)
                            break;

                        Border subMonthHdr = new Border
                        {
                            Height = subHdrH,
                            BorderBrush = HexBrush("#362C26"),
                            BorderThickness = new Thickness(0, 1, 0, 0),
                            Margin = new Thickness(0, 3, 0, 1),
                            Padding = new Thickness(2, 3, 2, 1)
                        };
                        subMonthHdr.Child = new TextBlock
                        {
                            Text = monthNamesUpper[monthDate.Month] + " " + monthDate.Year,
                            Foreground = HexBrush(accentColorHex),
                            FontSize = 9.5,
                            FontWeight = FontWeights.Bold,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        calMiniMonthGrid.Children.Add(subMonthHdr);
                        usedH += subHdrH + 4.0;
                    }

                    bool willFitNextMonth = (usedH + (weekRows * rowH) + subHdrH + rowH) <= availLeftH;

                    for (int w = 0; w < weekRows; w++)
                    {
                        if (m > 0 && usedH + rowH > availLeftH)
                            break;

                        UniformGrid weekRowGrid = new UniformGrid
                        {
                            Columns = 7,
                            Rows = 1,
                            Height = rowH,
                            VerticalAlignment = VerticalAlignment.Top
                        };

                        for (int d = 0; d < 7; d++)
                        {
                            int cell = w * 7 + d;
                            DateTime cellDate = gridStart.AddDays(cell).Date;
                            bool isCurrentMonth = (cellDate.Month == monthDate.Month);

                            if (!isCurrentMonth && (m > 0 || (willFitNextMonth && cellDate > monthDate)))
                            {
                                weekRowGrid.Children.Add(new Border { Width = 21, Height = 21 });
                                continue;
                            }

                            bool isToday = (cellDate == today);
                            bool isSelected = (cellDate == calSelectedDate.Date);

                            byte dotMask = 0;
                            if (dotsLookup != null) dotsLookup.TryGetValue(cellDate, out dotMask);
                            bool hasPrimaryEvent = (dotMask & 1) != 0;
                            bool hasHolidayEvent = (dotMask & 2) != 0;
                            bool hasExamEvent = (dotMask & 4) != 0;

                            Border dayCell = new Border
                            {
                                Width = 21,
                                Height = 21,
                                CornerRadius = new CornerRadius(10.5),
                                Background = isToday
                                    ? HexBrush(accentColorHex)
                                    : (isSelected ? HexBrush("#38281E") : Brushes.Transparent),
                                BorderBrush = (!isToday && isSelected) ? HexBrush(accentColorHex) : Brushes.Transparent,
                                BorderThickness = new Thickness(!isToday && isSelected ? 1.2 : 0),
                                Cursor = Cursors.Hand,
                                Tag = "Interactive",
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center
                            };

                            TextBlock txtNum = new TextBlock
                            {
                                Text = cellDate.Day.ToString(),
                                FontSize = 9.5,
                                FontWeight = (isToday || isSelected) ? FontWeights.Bold : FontWeights.Medium,
                                Foreground = isToday
                                    ? Brushes.White
                                    : (isCurrentMonth ? HexBrush("#FAF6F2") : HexBrush("#5E5047")),
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center,
                                Margin = new Thickness(0, -1, 0, 0)
                            };

                            if (hasPrimaryEvent || hasHolidayEvent || hasExamEvent)
                            {
                                StackPanel cellStack = new StackPanel
                                {
                                    VerticalAlignment = VerticalAlignment.Center,
                                    HorizontalAlignment = HorizontalAlignment.Center
                                };
                                cellStack.Children.Add(txtNum);

                                string dotHex = hasExamEvent ? "#C084FC" : (hasPrimaryEvent ? accentColorHex : "#9A9CFF");
                                Border dot = new Border
                                {
                                    Width = 3.5,
                                    Height = 3.5,
                                    CornerRadius = new CornerRadius(2),
                                    Background = isToday ? Brushes.White : HexBrush(dotHex),
                                    HorizontalAlignment = HorizontalAlignment.Center,
                                    Margin = new Thickness(0, 0.5, 0, 0)
                                };
                                cellStack.Children.Add(dot);
                                dayCell.Child = cellStack;
                            }
                            else
                            {
                                dayCell.Child = txtNum;
                            }
                            DateTime capturedDate = cellDate;
                            dayCell.MouseLeftButtonDown += (s, e) =>
                            {
                                calSelectedDate = capturedDate;
                                if (capturedDate.Month != calDisplayedMonth.Month || capturedDate.Year != calDisplayedMonth.Year)
                                {
                                    calDisplayedMonth = new DateTime(capturedDate.Year, capturedDate.Month, 1);
                                }
                                RefreshCalendarWidgetUi();
                                e.Handled = true;
                            };

                            weekRowGrid.Children.Add(dayCell);
                        }

                        calMiniMonthGrid.Children.Add(weekRowGrid);
                        usedH += rowH;
                    }

                    if (usedH + subHdrH + rowH > availLeftH)
                        break;
                }
            }
            catch { }
        }

        private void FitCalendarHeightToContent(bool allowShrink = true)
        {
            if (calendarWindow == null || calEventsListPanel == null) return;
            try
            {
                double winW = calendarWindow.ActualWidth > 300 ? calendarWindow.ActualWidth : savedCalWidth;
                double rightWidth = Math.Max(210, winW - 234);
                calEventsListPanel.Measure(new Size(rightWidth, double.PositiveInfinity));
                double eventsH = calEventsListPanel.DesiredSize.Height;
                double quickAddH = (calQuickAddPanel != null && calQuickAddPanel.Visibility == Visibility.Visible) ? 38 : 0;
                double neededRightH = eventsH + quickAddH + 68;
                double minLeftH = 246;
                Rect workArea = GetWindowMonitorWorkArea(calendarWindow);
                double targetH = Math.Min(workArea.Height - 40, Math.Max(minLeftH, Math.Ceiling(neededRightH)));
                if ((allowShrink && Math.Abs(calendarWindow.Height - targetH) > 1.0) || (!allowShrink && calendarWindow.Height < targetH - 1.0))
                {
                    calendarWindow.Height = targetH;
                    savedCalHeight = targetH;
                }
            }
            catch { }
        }

        private UIElement CreateAppleCalendarEventPill(CalendarEventItem ev, DateTime now, bool isTodayCard)
        {
            bool isOngoing = !ev.AllDay && isTodayCard && now >= ev.Start && now <= ev.End;
            bool isPastToday = !ev.AllDay && isTodayCard && now > ev.End;

            string customCol = !string.IsNullOrEmpty(ev.ColorHex) ? ev.ColorHex : "#10B981";
            string barHex = ev.Source == "holiday" ? "#9A9CFF" : (isOngoing ? accentColorHex : customCol);
            if (ev.Source == "custom") barHex = accentColorHex;

            Border card = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = HexBrush(isOngoing ? "#AA382920" : "#8828211E"),
                BorderBrush = HexBrush(isOngoing ? accentColorHex : "#443730"),
                BorderThickness = new Thickness(isOngoing ? 1.2 : 1),
                Padding = new Thickness(7, 5, 8, 5),
                Margin = new Thickness(0, 0, 0, 5),
                Opacity = isPastToday ? 0.62 : 1.0,
                Cursor = Cursors.Hand,
                Tag = "Interactive",
                ToolTip = ev.Title + (string.IsNullOrEmpty(ev.Location) ? "" : ("\n📍 " + ev.Location)) + (string.IsNullOrEmpty(ev.Description) ? "" : ("\n" + ev.Description))
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border colorBar = new Border
            {
                Width = 3.5,
                CornerRadius = new CornerRadius(2),
                Background = HexBrush(barHex),
                Margin = new Thickness(0, 1, 8, 1)
            };
            Grid.SetColumn(colorBar, 0);
            g.Children.Add(colorBar);

            StackPanel infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            Grid titleRow = new Grid();
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock txtTitle = new TextBlock
            {
                Text = ev.Title,
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 6, 0)
            };
            Grid.SetColumn(txtTitle, 0);
            titleRow.Children.Add(txtTitle);

            string timeBadgeText = ev.AllDay
                ? L("Tüm Gün", "All Day")
                : (ev.Start.ToString("HH:mm") + " – " + ev.End.ToString("HH:mm"));
            if (isOngoing) timeBadgeText = L("● ŞİMDİ (", "● NOW (") + ev.End.ToString("HH:mm") + L("'e kadar)", " end)");

            TextBlock txtTime = new TextBlock
            {
                Text = timeBadgeText,
                Foreground = HexBrush(isOngoing ? accentColorHex : "#C7B9AE"),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(txtTime, 1);
            titleRow.Children.Add(txtTime);
            infoStack.Children.Add(titleRow);

            string subLine = "";
            if (!string.IsNullOrEmpty(ev.Location)) subLine += "📍 " + ev.Location;
            if (!string.IsNullOrEmpty(ev.Description) && !ev.Description.StartsWith("Bunun gibi otomatik"))
            {
                if (subLine.Length > 0) subLine += "  •  ";
                subLine += ev.Description;
            }
            if (!string.IsNullOrEmpty(subLine))
            {
                infoStack.Children.Add(new TextBlock
                {
                    Text = subLine,
                    Foreground = HexBrush("#9E8E82"),
                    FontSize = 10,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 1.5, 0, 0)
                });
            }

            Grid.SetColumn(infoStack, 1);
            g.Children.Add(infoStack);

            if (ev.Source == "custom" && !string.IsNullOrEmpty(ev.RawCustomLine))
            {
                string rawToRemove = ev.RawCustomLine;
                Button btnDel = CreateStyledIconButton("Transparent", "Transparent", L("Bu özel etkinliği sil", "Delete custom event"));
                btnDel.Padding = new Thickness(4, 0, 2, 0);
                btnDel.Content = new TextBlock { Text = "✕", Foreground = HexBrush("#8C7A6B"), FontSize = 9.5 };
                btnDel.Click += (s, e) =>
                {
                    RemoveCustomCalendarEvent(rawToRemove);
                    e.Handled = true;
                };
                Grid.SetColumn(btnDel, 2);
                g.Children.Add(btnDel);
            }

            card.Child = g;
            card.MouseEnter += (s, e) => card.Background = HexBrush("#BB382D27");
            card.MouseLeave += (s, e) => card.Background = HexBrush(isOngoing ? "#AA382920" : "#8828211E");
            card.MouseLeftButtonDown += (s, e) =>
            {
                if (!string.IsNullOrEmpty(ev.HtmlLink))
                {
                    try { Process.Start(ev.HtmlLink); } catch { }
                }
                else
                {
                    ShowToast(ev.Title + (string.IsNullOrEmpty(ev.Location) ? "" : (" • " + ev.Location)));
                }
            };

            return card;
        }

        private bool DoesEventOccurOnDate(CalendarEventItem ev, DateTime date)
        {
            DateTime d = date.Date;
            if (ev.AllDay)
            {
                DateTime s = ev.Start.Date;
                DateTime e = ev.End.Date;
                if (e <= s) e = s.AddDays(1);
                return d >= s && d < e;
            }
            return ev.Start.Date == d;
        }

        private List<CalendarEventItem> GetEventsForDate(List<CalendarEventItem> all, DateTime date)
        {
            List<CalendarEventItem> list = new List<CalendarEventItem>();
            foreach (var ev in all)
            {
                if (DoesEventOccurOnDate(ev, date))
                    list.Add(ev);
            }
            list.Sort((a, b) =>
            {
                if (a.AllDay != b.AllDay) return a.AllDay ? -1 : 1;
                return a.Start.CompareTo(b.Start);
            });
            return list;
        }

        private void AddCustomCalendarEventFromUi()
        {
            try
            {
                string title = txtCalQuickTitle.Text.Trim();
                if (string.IsNullOrEmpty(title))
                {
                    ShowToast(L("Lütfen etkinlik başlığı girin", "Please enter an event title"));
                    return;
                }
                string timePart = txtCalQuickTime.Text.Trim();
                DateTime startDt = calSelectedDate.Date.AddHours(12);
                DateTime endDt = startDt.AddHours(1);
                bool allDay = false;

                if (string.IsNullOrEmpty(timePart) ||
                    timePart.Equals("tüm gün", StringComparison.OrdinalIgnoreCase) ||
                    timePart.Equals("all day", StringComparison.OrdinalIgnoreCase))
                {
                    allDay = true;
                    startDt = calSelectedDate.Date;
                    endDt = startDt.AddDays(1);
                }
                else
                {
                    string[] rangeParts = timePart.Split(new char[] { '-', '–' }, StringSplitOptions.RemoveEmptyEntries);
                    TimeSpan ts1;
                    if (rangeParts.Length >= 1 && TimeSpan.TryParse(rangeParts[0].Trim(), out ts1))
                    {
                        startDt = calSelectedDate.Date.Add(ts1);
                        endDt = startDt.AddHours(1);
                    }
                    TimeSpan ts2;
                    if (rangeParts.Length >= 2 && TimeSpan.TryParse(rangeParts[1].Trim(), out ts2))
                    {
                        endDt = calSelectedDate.Date.Add(ts2);
                    }
                }

                string customFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "custom_events.dat");
                string cleanTitle = title.Replace("|", "/");
                string line = string.Format("{0}|{1}|{2}|custom|{3}|{4}|||https://calendar.google.com",
                    allDay ? startDt.ToString("yyyy-MM-dd") : startDt.ToString("yyyy-MM-ddTHH:mm:ss"),
                    allDay ? endDt.ToString("yyyy-MM-dd") : endDt.ToString("yyyy-MM-ddTHH:mm:ss"),
                    allDay ? "True" : "False",
                    accentColorHex,
                    cleanTitle);

                List<string> existingCustom = new List<string>();
                string[] prevCustom = ReadAllLinesCrashSafe(customFile, Encoding.UTF8);
                if (prevCustom != null) existingCustom.AddRange(prevCustom);
                existingCustom.Add(line);
                WriteAllTextAtomic(customFile, string.Join(Environment.NewLine, existingCustom.ToArray()) + Environment.NewLine, new UTF8Encoding(false));
                txtCalQuickTitle.Text = "";
                calQuickAddPanel.Visibility = Visibility.Collapsed;
                Task.Run(() => LoadAndSyncCalendarEventsAsync(false));
                ShowToast(L("Etkinlik takvime eklendi: ", "Event added to calendar: ") + cleanTitle);
            }
            catch { }
        }

        private void RemoveCustomCalendarEvent(string rawLine)
        {
            try
            {
                string customFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "custom_events.dat");
                string[] existingCustom = ReadAllLinesCrashSafe(customFile, Encoding.UTF8);
                if (existingCustom != null)
                {
                    List<string> kept = new List<string>();
                    foreach (string l in existingCustom)
                    {
                        if (l.Trim() != rawLine.Trim()) kept.Add(l);
                    }
                    WriteAllTextAtomic(customFile, kept.Count > 0 ? (string.Join(Environment.NewLine, kept.ToArray()) + Environment.NewLine) : "", new UTF8Encoding(false));
                }
                Task.Run(() => LoadAndSyncCalendarEventsAsync(false));
                ShowToast(L("Etkinlik silindi", "Event deleted"));
            }
            catch { }
        }

        private void LoadAndSyncCalendarEventsAsync(bool showToastOnDone)
        {
            try
            {
                string datPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "calendar_events.dat");
                string customPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "custom_events.dat");

                DateTime datWrite = File.Exists(datPath) ? File.GetLastWriteTimeUtc(datPath) : DateTime.MinValue;
                DateTime customWrite = File.Exists(customPath) ? File.GetLastWriteTimeUtc(customPath) : DateTime.MinValue;
                bool hasLiveIcal = !string.IsNullOrEmpty(calendarSecretIcalUrl) && calendarSecretIcalUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase);

                if (!showToastOnDone && !hasLiveIcal && datWrite == lastCalDatWriteUtc && customWrite == lastCustomDatWriteUtc && allCalendarEvents.Count > 0)
                {
                    return;
                }

                List<CalendarEventItem> loaded = new List<CalendarEventItem>(128);

                if (File.Exists(datPath))
                {
                    foreach (string line in File.ReadAllLines(datPath, Encoding.UTF8))
                    {
                        CalendarEventItem item = ParsePipeEventLine(line, false);
                        if (item != null) loaded.Add(item);
                    }
                }

                string[] customLines = ReadAllLinesCrashSafe(customPath, Encoding.UTF8);
                if (customLines != null)
                {
                    foreach (string line in customLines)
                    {
                        CalendarEventItem item = ParsePipeEventLine(line, true);
                        if (item != null) loaded.Add(item);
                    }
                }

                if (hasLiveIcal)
                {
                    try
                    {
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(calendarSecretIcalUrl);
                        req.Timeout = 7000;
                        req.UserAgent = "OpenWidgetCalendar/2.2";
                        using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                        using (StreamReader reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                        {
                            string icsContent = reader.ReadToEnd();
                            List<CalendarEventItem> icsItems = ParseIcsContent(icsContent, "primary", "#10B981");
                            if (icsItems.Count > 0)
                            {
                                loaded.RemoveAll(x => x.Source == "primary");
                                loaded.AddRange(icsItems);
                            }
                        }
                    }
                    catch { }
                }

                Dictionary<DateTime, byte> dotsMap = new Dictionary<DateTime, byte>(loaded.Count + 16);
                for (int i = 0; i < loaded.Count; i++)
                {
                    CalendarEventItem ev = loaded[i];
                    bool isExam = (ev.ColorHex == "#C084FC") || (ev.Title != null &&
                        (ev.Title.IndexOf("Sınav", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         ev.Title.IndexOf("Exam", StringComparison.OrdinalIgnoreCase) >= 0));
                    byte bit = (ev.Source == "holiday") ? (byte)2 : (isExam ? (byte)4 : (byte)1);
                    if (ev.AllDay)
                    {
                        DateTime s = ev.Start.Date;
                        DateTime e = ev.End.Date;
                        if (e <= s) e = s.AddDays(1);
                        for (DateTime d = s; d < e; d = d.AddDays(1))
                        {
                            byte cur;
                            dotsMap.TryGetValue(d, out cur);
                            dotsMap[d] = (byte)(cur | bit);
                        }
                    }
                    else
                    {
                        DateTime d = ev.Start.Date;
                        byte cur;
                        dotsMap.TryGetValue(d, out cur);
                        dotsMap[d] = (byte)(cur | bit);
                    }
                }

                lock (calEventsLock)
                {
                    allCalendarEvents.Clear();
                    allCalendarEvents.AddRange(loaded);
                    calEventDotsByDate = dotsMap;
                    calEventsVersion++;
                    lastCalDatWriteUtc = datWrite;
                    lastCustomDatWriteUtc = customWrite;
                }

                Dispatcher.Invoke(() =>
                {
                    RefreshCalendarWidgetUi();
                    if (showToastOnDone)
                        ShowToast(string.Format(L("Takvim senkronize edildi ({0} etkinlik)", "Calendar synced ({0} events)"), loaded.Count));
                });

                TrimProcessMemory();
            }
            catch { }
        }

        private CalendarEventItem ParsePipeEventLine(string line, bool isCustom)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;
            string[] p = line.Split('|');
            if (p.Length < 6) return null;

            DateTime startDt, endDt;
            if (!DateTime.TryParse(p[0], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out startDt))
                return null;
            if (!DateTime.TryParse(p[1], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out endDt))
                endDt = startDt.AddHours(1);

            bool allDay = p[2].Equals("True", StringComparison.OrdinalIgnoreCase);
            return new CalendarEventItem
            {
                Start = startDt.ToLocalTime(),
                End = endDt.ToLocalTime(),
                AllDay = allDay,
                Source = isCustom ? "custom" : p[3],
                ColorHex = string.IsNullOrEmpty(p[4]) ? "#10B981" : p[4],
                Title = p[5],
                Location = p.Length > 6 ? p[6] : "",
                Description = p.Length > 7 ? p[7] : "",
                HtmlLink = p.Length > 8 ? p[8] : "",
                RawCustomLine = isCustom ? line : null
            };
        }

        private List<CalendarEventItem> ParseIcsContent(string ics, string sourceTag, string defaultColor)
        {
            List<CalendarEventItem> results = new List<CalendarEventItem>();
            if (string.IsNullOrEmpty(ics)) return results;

            string unfolded = ics.Replace("\r\n ", "").Replace("\r\n\t", "").Replace("\n ", "").Replace("\n\t", "");
            string[] lines = unfolded.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            bool inEvent = false;
            string summary = "", location = "", desc = "", dtStartRaw = "", dtEndRaw = "", uid = "", rrule = "", recurIdRaw = "", status = "";
            List<string> exdates = new List<string>();

            List<CalendarEventItem> singles = new List<CalendarEventItem>();
            Dictionary<string, HashSet<DateTime>> overriddenStarts = new Dictionary<string, HashSet<DateTime>>();
            List<object[]> masters = new List<object[]>();

            DateTime windowStart = DateTime.Today.AddMonths(-3);
            DateTime windowEnd = DateTime.Today.AddMonths(9);

            foreach (string rawLine in lines)
            {
                if (rawLine == "BEGIN:VEVENT")
                {
                    inEvent = true;
                    summary = ""; location = ""; desc = ""; dtStartRaw = ""; dtEndRaw = ""; uid = ""; rrule = ""; recurIdRaw = ""; status = "";
                    exdates = new List<string>();
                }
                else if (rawLine == "END:VEVENT" && inEvent)
                {
                    inEvent = false;
                    DateTime sDt, eDt;
                    bool isAllDay;
                    if (!TryParseIcsDate(dtStartRaw, out sDt, out isAllDay)) continue;
                    bool dummy;
                    if (!TryParseIcsDate(dtEndRaw, out eDt, out dummy))
                        eDt = isAllDay ? sDt.AddDays(1) : sDt.AddHours(1);

                    string ttl = UnescapeIcsText(summary);
                    bool isExam = ttl.IndexOf("Sınav", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  ttl.IndexOf("Vize", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  ttl.IndexOf("Final", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  ttl.IndexOf("Exam", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  ttl.IndexOf("Midterm", StringComparison.OrdinalIgnoreCase) >= 0;
                    CalendarEventItem item = new CalendarEventItem
                    {
                        Start = sDt,
                        End = eDt,
                        AllDay = isAllDay,
                        Source = sourceTag,
                        ColorHex = isExam ? "#C084FC" : defaultColor,
                        Title = ttl,
                        Location = UnescapeIcsText(location),
                        Description = UnescapeIcsText(desc),
                        HtmlLink = "https://calendar.google.com"
                    };
                    bool cancelled = status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase);

                    if (!string.IsNullOrEmpty(recurIdRaw))
                    {
                        DateTime rid; bool ridAllDay;
                        if (TryParseIcsDate(recurIdRaw, out rid, out ridAllDay))
                        {
                            HashSet<DateTime> set;
                            if (!overriddenStarts.TryGetValue(uid, out set)) { set = new HashSet<DateTime>(); overriddenStarts[uid] = set; }
                            set.Add(rid);
                        }
                        if (!cancelled) singles.Add(item);
                    }
                    else if (!string.IsNullOrEmpty(rrule))
                    {
                        HashSet<DateTime> exSet = new HashSet<DateTime>();
                        foreach (string exLine in exdates)
                        {
                            foreach (string part in exLine.Split(','))
                            {
                                DateTime ex; bool exAd;
                                if (TryParseIcsDate(part, out ex, out exAd)) exSet.Add(ex);
                            }
                        }
                        if (!cancelled) masters.Add(new object[] { item, rrule, uid, exSet });
                    }
                    else if (!cancelled)
                    {
                        singles.Add(item);
                    }
                }
                else if (inEvent)
                {
                    int colon = rawLine.IndexOf(':');
                    if (colon > 0)
                    {
                        string keyPart = rawLine.Substring(0, colon).ToUpperInvariant();
                        string valPart = rawLine.Substring(colon + 1);
                        if (keyPart.StartsWith("DTSTART")) dtStartRaw = valPart;
                        else if (keyPart.StartsWith("DTEND")) dtEndRaw = valPart;
                        else if (keyPart.StartsWith("SUMMARY")) summary = valPart;
                        else if (keyPart.StartsWith("LOCATION")) location = valPart;
                        else if (keyPart.StartsWith("DESCRIPTION")) desc = valPart;
                        else if (keyPart == "UID") uid = valPart.Trim();
                        else if (keyPart == "RRULE") rrule = valPart.Trim();
                        else if (keyPart.StartsWith("EXDATE")) exdates.Add(valPart.Trim());
                        else if (keyPart.StartsWith("RECURRENCE-ID")) recurIdRaw = valPart.Trim();
                        else if (keyPart == "STATUS") status = valPart.Trim();
                    }
                }
            }

            foreach (object[] mInfo in masters)
            {
                CalendarEventItem master = (CalendarEventItem)mInfo[0];
                string rule = (string)mInfo[1];
                string mUid = (string)mInfo[2];
                HashSet<DateTime> exSet = (HashSet<DateTime>)mInfo[3];
                HashSet<DateTime> ovSet;
                overriddenStarts.TryGetValue(mUid, out ovSet);

                foreach (DateTime occ in ExpandRRule(master.Start, rule, windowStart, windowEnd))
                {
                    if (exSet.Contains(occ)) continue;
                    if (ovSet != null && ovSet.Contains(occ)) continue;
                    TimeSpan dur = master.End - master.Start;
                    results.Add(new CalendarEventItem
                    {
                        Start = occ,
                        End = occ + dur,
                        AllDay = master.AllDay,
                        Source = master.Source,
                        ColorHex = master.ColorHex,
                        Title = master.Title,
                        Location = master.Location,
                        Description = master.Description,
                        HtmlLink = master.HtmlLink
                    });
                }
            }
            results.AddRange(singles);
            return results;
        }

        private IEnumerable<DateTime> ExpandRRule(DateTime dtStart, string rule, DateTime windowStart, DateTime windowEnd)
        {
            string freq = "WEEKLY";
            int interval = 1;
            int count = -1;
            DateTime until = DateTime.MaxValue;
            List<DayOfWeek> byDays = new List<DayOfWeek>();

            foreach (string kv in rule.Split(';'))
            {
                int eq = kv.IndexOf('=');
                if (eq <= 0) continue;
                string k = kv.Substring(0, eq).ToUpperInvariant();
                string v = kv.Substring(eq + 1);
                if (k == "FREQ") freq = v.ToUpperInvariant();
                else if (k == "INTERVAL") { int iv; if (int.TryParse(v, out iv) && iv > 0) interval = iv; }
                else if (k == "COUNT") { int c; if (int.TryParse(v, out c)) count = c; }
                else if (k == "UNTIL") { DateTime u; bool uad; if (TryParseIcsDate(v, out u, out uad)) until = uad ? u.AddDays(1).AddSeconds(-1) : u; }
                else if (k == "BYDAY")
                {
                    foreach (string d in v.Split(','))
                    {
                        string dd = d.Trim().ToUpperInvariant();
                        if (dd.Length > 2) dd = dd.Substring(dd.Length - 2);
                        switch (dd)
                        {
                            case "MO": byDays.Add(DayOfWeek.Monday); break;
                            case "TU": byDays.Add(DayOfWeek.Tuesday); break;
                            case "WE": byDays.Add(DayOfWeek.Wednesday); break;
                            case "TH": byDays.Add(DayOfWeek.Thursday); break;
                            case "FR": byDays.Add(DayOfWeek.Friday); break;
                            case "SA": byDays.Add(DayOfWeek.Saturday); break;
                            case "SU": byDays.Add(DayOfWeek.Sunday); break;
                        }
                    }
                }
            }

            int produced = 0;
            int safety = 0;
            if (freq == "WEEKLY")
            {
                if (byDays.Count == 0) byDays.Add(dtStart.DayOfWeek);
                int startOffset = ((int)dtStart.DayOfWeek + 6) % 7;
                DateTime weekStart = dtStart.Date.AddDays(-startOffset);
                while (safety++ < 2000)
                {
                    for (int i = 0; i < 7; i++)
                    {
                        DateTime day = weekStart.AddDays(i);
                        if (!byDays.Contains(day.DayOfWeek)) continue;
                        DateTime occ = day + dtStart.TimeOfDay;
                        if (occ < dtStart) continue;
                        if (occ > until || (count >= 0 && produced >= count)) yield break;
                        produced++;
                        if (occ > windowEnd) yield break;
                        if (occ >= windowStart) yield return occ;
                    }
                    weekStart = weekStart.AddDays(7 * interval);
                }
            }
            else
            {
                DateTime occ = dtStart;
                while (safety++ < 5000)
                {
                    if (occ > until || (count >= 0 && produced >= count) || occ > windowEnd) yield break;
                    produced++;
                    if (occ >= windowStart) yield return occ;
                    if (freq == "DAILY") occ = occ.AddDays(interval);
                    else if (freq == "MONTHLY") occ = occ.AddMonths(interval);
                    else if (freq == "YEARLY") occ = occ.AddYears(interval);
                    else yield break;
                }
            }
        }

        private bool TryParseIcsDate(string raw, out DateTime dt, out bool allDay)
        {
            dt = DateTime.MinValue;
            allDay = false;
            if (string.IsNullOrEmpty(raw)) return false;
            raw = raw.Trim();
            if (raw.Length == 8 && DateTime.TryParseExact(raw, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
            {
                allDay = true;
                return true;
            }
            if (raw.EndsWith("Z") && DateTime.TryParseExact(raw, "yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out dt))
            {
                dt = dt.ToLocalTime();
                return true;
            }
            if (DateTime.TryParseExact(raw, "yyyyMMdd'T'HHmmss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
            {
                return true;
            }
            return false;
        }

        private string UnescapeIcsText(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\n", " ").Replace("\\N", " ").Replace("\\,", ",").Replace("\\;", ";").Trim();
        }

        // =========================================================================
        // STANDALONE TIMER WIDGET (MODES, AUTO GAME MODE, POMODORO & HEATMAP)
        // =========================================================================
        public void SetTimerWidgetVisible(bool visible)
        {
            showTimerWidget = visible;
            SaveSettings();
            if (trayTimerItem != null)
            {
                trayTimerItem.Text = showTimerWidget
                    ? L("⏱ Timer Widgeti'ni Gizle", "⏱ Hide Timer Widget")
                    : L("⏱ Timer Widgeti'ni Göster", "⏱ Show Timer Widget");
            }
            if (timerWindow == null && showTimerWidget)
            {
                InitializeTimerWidget();
                return;
            }
            if (timerWindow != null)
            {
                if (showTimerWidget)
                {
                    timerWindow.Show();
                    IntPtr thwnd = cachedTimerHwnd != IntPtr.Zero ? cachedTimerHwnd : new WindowInteropHelper(timerWindow).Handle;
                    if (thwnd != IntPtr.Zero) SendWidgetToDesktopBottom(thwnd);
                    UpdateTimerWidgetUi();
                }
                else
                {
                    SaveTimerActivityData();
                    timerWindow.Hide();
                }
            }
        }

        private string SanitizeGameKey(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return L("Manuel Eğlence", "Manual Gaming");
            return raw.Replace('|', ' ').Replace(';', ' ').Replace('=', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
        }

        private static Dictionary<DateTime, DailyActivityRecord> TryParseTimerActivityFile(string path)
        {
            Dictionary<DateTime, DailyActivityRecord> map = new Dictionary<DateTime, DailyActivityRecord>();
            if (!IsValidTextFile(path)) return map;
            try
            {
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string[] p = line.Split('|');
                    DateTime dt;
                    int st, gm, pc;
                    if (p.Length >= 4 &&
                        DateTime.TryParseExact(p[0].Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt) &&
                        int.TryParse(p[1], out st) &&
                        int.TryParse(p[2], out gm) &&
                        int.TryParse(p[3], out pc))
                    {
                        DailyActivityRecord rec = new DailyActivityRecord
                        {
                            Date = dt.Date,
                            StudySeconds = Math.Max(0, st),
                            GameSeconds = Math.Max(0, gm),
                            PcSeconds = Math.Max(0, pc)
                        };
                        if (p.Length >= 5 && !string.IsNullOrWhiteSpace(p[4]))
                        {
                            string[] entries = p[4].Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int ei = 0; ei < entries.Length; ei++)
                            {
                                int eq = entries[ei].LastIndexOf('=');
                                if (eq > 0 && eq < entries[ei].Length - 1)
                                {
                                    string gName = entries[ei].Substring(0, eq).Trim();
                                    int gSec;
                                    if (!string.IsNullOrEmpty(gName) && int.TryParse(entries[ei].Substring(eq + 1), out gSec) && gSec > 0)
                                    {
                                        int prevSec;
                                        rec.GameBreakdown.TryGetValue(gName, out prevSec);
                                        rec.GameBreakdown[gName] = prevSec + gSec;
                                    }
                                }
                            }
                        }
                        map[dt.Date] = rec;
                    }
                }
            }
            catch { }
            return map;
        }

        private void LoadTimerActivityData()
        {
            try
            {
                activityDataFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "timer_activity.dat");
                activityByDate.Clear();
                lastSavedActivityPayload = null;

                Dictionary<DateTime, DailyActivityRecord> primary = TryParseTimerActivityFile(activityDataFilePath);
                Dictionary<DateTime, DailyActivityRecord> backup = TryParseTimerActivityFile(activityDataFilePath + ".bak");
                Dictionary<DateTime, DailyActivityRecord> temp = TryParseTimerActivityFile(activityDataFilePath + ".tmp");

                bool needsRepairSave = false;
                DateTime cutoff = DateTime.Today.AddDays(-400);

                // Start from primary if valid; otherwise pick the richer recovery source (.bak or .tmp)
                if (primary.Count > 0)
                {
                    foreach (var kv in primary) activityByDate[kv.Key] = kv.Value;
                }
                else if (backup.Count >= temp.Count && backup.Count > 0)
                {
                    foreach (var kv in backup) activityByDate[kv.Key] = kv.Value;
                    needsRepairSave = true;
                }
                else if (temp.Count > 0)
                {
                    foreach (var kv in temp) activityByDate[kv.Key] = kv.Value;
                    needsRepairSave = true;
                }

                // Merge any missing historical days from .bak / .tmp so a power-loss reset can never wipe history
                Dictionary<DateTime, DailyActivityRecord>[] fallbacks = new Dictionary<DateTime, DailyActivityRecord>[] { backup, temp };
                for (int fi = 0; fi < fallbacks.Length; fi++)
                {
                    foreach (var kv in fallbacks[fi])
                    {
                        if (kv.Key < cutoff) continue;
                        if (!activityByDate.ContainsKey(kv.Key))
                        {
                            activityByDate[kv.Key] = kv.Value;
                            needsRepairSave = true;
                        }
                    }
                }

                DailyActivityRecord todayRec = GetOrCreateActivityRecord(DateTime.Today);
                if (todayRec.PcSeconds == 0)
                {
                    double bootSec = GetTickCount64() / 1000.0;
                    if (DateTime.Now.AddSeconds(-bootSec).Date == DateTime.Today)
                    {
                        todayRec.PcSeconds = (int)Math.Max(0, Math.Min(86400, bootSec));
                    }
                }

                if (needsRepairSave && activityByDate.Count > 0)
                {
                    SaveTimerActivityData();
                }
            }
            catch { }
        }

        private DailyActivityRecord GetOrCreateActivityRecord(DateTime date)
        {
            DateTime d = date.Date;
            DailyActivityRecord rec;
            if (!activityByDate.TryGetValue(d, out rec))
            {
                rec = new DailyActivityRecord { Date = d };
                activityByDate[d] = rec;
            }
            return rec;
        }

        private void SaveTimerActivityData()
        {
            try
            {
                if (string.IsNullOrEmpty(activityDataFilePath))
                    activityDataFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "timer_activity.dat");

                List<DailyActivityRecord> rows = new List<DailyActivityRecord>(activityByDate.Values);
                rows.Sort((a, b) => a.Date.CompareTo(b.Date));

                StringBuilder sb = new StringBuilder(rows.Count * 40);
                DateTime cutoff = DateTime.Today.AddDays(-400);
                for (int i = 0; i < rows.Count; i++)
                {
                    DailyActivityRecord r = rows[i];
                    if (r.Date < cutoff) continue;
                    if (r.StudySeconds == 0 && r.GameSeconds == 0 && r.PcSeconds == 0) continue;
                    sb.Append(r.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))
                      .Append('|').Append(r.StudySeconds)
                      .Append('|').Append(r.GameSeconds)
                      .Append('|').Append(r.PcSeconds);
                    if (r.GameBreakdown != null && r.GameBreakdown.Count > 0)
                    {
                        sb.Append('|');
                        bool first = true;
                        foreach (var kv in r.GameBreakdown)
                        {
                            if (kv.Value <= 0) continue;
                            if (!first) sb.Append(';');
                            sb.Append(SanitizeGameKey(kv.Key)).Append('=').Append(kv.Value);
                            first = false;
                        }
                    }
                    sb.AppendLine();
                }

                string payload = sb.ToString();
                // Never overwrite a valid history file with an empty payload
                if (string.IsNullOrWhiteSpace(payload)) return;
                // Skip redundant disk flush when nothing changed since last save
                if (string.Equals(payload, lastSavedActivityPayload, StringComparison.Ordinal)) return;

                WriteAllTextAtomic(activityDataFilePath, payload, new UTF8Encoding(false));
                lastSavedActivityPayload = payload;
            }
            catch { }
        }

        private void InitializeTimerWidget()
        {
            if (timerWindow != null) return;

            activeTrackingDate = DateTime.Today;
            pomoRemainingSeconds = pomoFocusMinutes * 60;
            LoadTimerActivityData();

            timerWindow = new Window
            {
                Title = L("OpenWidget • Timer Widgeti", "OpenWidget • Timer Widget"),
                MinWidth = 460,
                MinHeight = 275,
                Width = savedTimerWidth,
                Height = savedTimerHeight,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = isDraggable ? ResizeMode.CanResize : ResizeMode.NoResize,
                ShowInTaskbar = false,
                Topmost = false,
                UseLayoutRounding = true,
                SnapsToDevicePixels = true,
                FontFamily = new FontFamily("Segoe UI")
            };
            Program.TryApplyWindowIcon(timerWindow);
            TextOptions.SetTextFormattingMode(timerWindow, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(timerWindow, TextRenderingMode.ClearType);
            RenderOptions.SetClearTypeHint(timerWindow, ClearTypeHint.Enabled);

            if (IsPositionWithinVirtualDesktop(savedTimerLeft, savedTimerTop, savedTimerWidth, savedTimerHeight))
            {
                timerWindow.Left = savedTimerLeft;
                timerWindow.Top = savedTimerTop;
            }
            else
            {
                Rect workArea = GetWindowMonitorWorkArea(this);
                double baseLeft = !double.IsNaN(savedCalLeft) ? savedCalLeft : (this.Left - savedTimerWidth - 10);
                if (baseLeft < workArea.Left + 10) baseLeft = workArea.Left + 14;
                double baseTop = (!double.IsNaN(savedCalTop) ? savedCalTop : this.Top) + savedCalHeight + 10;
                if (baseTop + savedTimerHeight > workArea.Bottom - 10)
                    baseTop = Math.Max(workArea.Top + 14, workArea.Bottom - savedTimerHeight - 14);
                timerWindow.Left = baseLeft;
                timerWindow.Top = baseTop;
                savedTimerLeft = timerWindow.Left;
                savedTimerTop = timerWindow.Top;
            }

            timerWindowChrome = new WindowChrome
            {
                CaptionHeight = 0,
                CornerRadius = new CornerRadius(0),
                GlassFrameThickness = new Thickness(0),
                NonClientFrameEdges = NonClientFrameEdges.None,
                ResizeBorderThickness = isDraggable ? new Thickness(8) : new Thickness(0)
            };
            WindowChrome.SetWindowChrome(timerWindow, timerWindowChrome);

            timerWindow.SizeChanged += (s, e) =>
            {
                if (timerWindow.ActualWidth >= 460 && timerWindow.ActualHeight >= 275)
                {
                    savedTimerWidth = timerWindow.ActualWidth;
                    savedTimerHeight = timerWindow.ActualHeight;
                    SaveSettingsDebounced();
                }
            };

            timerWindow.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (!isDraggable) return;
                DependencyObject dep = e.OriginalSource as DependencyObject;
                while (dep != null && dep != timerWindow)
                {
                    if (dep is ButtonBase || dep is Slider || dep is Thumb || dep is TextBox || dep is ScrollBar)
                        return;
                    if (dep is FrameworkElement)
                    {
                        FrameworkElement fe = (FrameworkElement)dep;
                        if (fe.Tag != null && fe.Tag.ToString() == "Interactive") return;
                    }
                    dep = VisualTreeHelper.GetParent(dep);
                }
                try
                {
                    IntPtr thwnd = cachedTimerHwnd != IntPtr.Zero ? cachedTimerHwnd : new WindowInteropHelper(timerWindow).Handle;
                    ReleaseCapture();
                    SendMessage(thwnd, WM_NCLBUTTONDOWN, (IntPtr)HT_CAPTION, IntPtr.Zero);
                    savedTimerLeft = timerWindow.Left;
                    savedTimerTop = timerWindow.Top;
                    SaveSettingsDebounced();
                    e.Handled = true;
                }
                catch { }
            };

            timerWindow.LocationChanged += (s, e) =>
            {
                if (isTimerWindowInitialized && !double.IsNaN(timerWindow.Left) && !double.IsNaN(timerWindow.Top) && timerWindow.Left > -10000 && timerWindow.Top > -10000)
                {
                    savedTimerLeft = timerWindow.Left;
                    savedTimerTop = timerWindow.Top;
                    SaveSettingsDebounced();
                }
            };

            timerWindow.Loaded += (s, e) =>
            {
                IntPtr thwnd = new WindowInteropHelper(timerWindow).Handle;
                cachedTimerHwnd = thwnd;
                int exStyle = GetWindowLong(thwnd, GWL_EXSTYLE);
                SetWindowLong(thwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
                AttachToDesktopShell(thwnd);
                HwndSource src = HwndSource.FromHwnd(thwnd);
                if (src != null) src.AddHook(WndProc);
                isTimerWindowInitialized = true;
            };

            timerWindow.StateChanged += (s, e) =>
            {
                if (timerWindow.WindowState == WindowState.Minimized)
                    timerWindow.WindowState = WindowState.Normal;
            };

            BuildTimerWidgetUi();
            ApplyTimerWidgetTheme();

            if (showTimerWidget)
            {
                timerWindow.Show();
            }

            timerWidgetTickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timerWidgetTickTimer.Tick += (s, e) => OnTimerWidgetSecondTick();
            timerWidgetTickTimer.Start();
        }

        private void BuildTimerWidgetUi()
        {
            timerOuterBorder = new Border
            {
                CornerRadius = new CornerRadius(16),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(2),
                Padding = new Thickness(14, 10, 14, 10)
            };

            Grid rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.50, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.92, GridUnitType.Star) });

            Grid topBar = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new TextBlock
            {
                Text = L("⏱ Timer Widgeti", "⏱ Timer Widget"),
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            });
            Grid.SetColumn(titleStack, 0);
            topBar.Children.Add(titleStack);

            StackPanel modeSwitcherPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = "Interactive"
            };
            string[] modeLabels = { L("🖥 Default", "🖥 Default"), L("🎮 Eğlence", "🎮 Gaming"), L("📚 Ders Modu", "📚 Study") };
            string[] modeTips = {
                L("Default Mod (Normal zamanlayıcı)", "Default Mode (Standard stopwatch timer)"),
                L("Eğlence / Oyun Modu (Oyun oynarken geçen süre günlük kaydedilir)", "Gaming Mode (Tracks daily gaming time)"),
                L("Ders / Pomodoro Modu (Pomodoro & günlük çalışma süresi)", "Study Mode (Pomodoro & daily study time)")
            };
            for (int i = 0; i < 3; i++)
            {
                int mIdx = i;
                Button mb = CreateStyledIconButton("#26201D", "#3E322B", modeTips[i]);
                mb.Padding = new Thickness(8, 3, 8, 3);
                mb.Margin = new Thickness(i == 0 ? 0 : 4, 0, 0, 0);
                mb.Content = new TextBlock
                {
                    Text = modeLabels[i],
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = HexBrush("#C7B9AE")
                };
                mb.Click += (s, e) => SwitchTimerMode(mIdx);
                btnTimerModes[i] = mb;
                modeSwitcherPanel.Children.Add(mb);
            }
            Grid.SetColumn(modeSwitcherPanel, 1);
            topBar.Children.Add(modeSwitcherPanel);

            StackPanel rightUtilBtns = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = "Interactive"
            };
            Button btnPomoSettings = CreateStyledIconButton("#26201D", "Transparent", L("Timer ayarları, renkler ve günlere detaylı bakış", "Timer settings, mode colors & detailed day view"));
            btnPomoSettings.Padding = new Thickness(7, 3, 7, 3);
            btnPomoSettings.Margin = new Thickness(6, 0, 4, 0);
            btnPomoSettings.Content = new TextBlock { Text = L("⚙ Ayarlar", "⚙ Config"), Foreground = HexBrush("#C7B9AE"), FontSize = 10, FontWeight = FontWeights.SemiBold };
            btnPomoSettings.Click += (s, e) => OpenTimerSettingsWindow();
            rightUtilBtns.Children.Add(btnPomoSettings);

            btnTimerLockDrag = CreateStyledIconButton("#26201D", "Transparent", L("Sabit / Taşınabilir Modu Değiştir", "Toggle Lock / Draggable Mode"));
            btnTimerLockDrag.Width = 24;
            btnTimerLockDrag.Height = 22;
            btnTimerLockDrag.Content = new TextBlock { Text = isDraggable ? "🔓" : "📌", FontSize = 10.5, Foreground = HexBrush("#C7B9AE") };
            btnTimerLockDrag.Click += (s, e) => ToggleDraggableMode();
            rightUtilBtns.Children.Add(btnTimerLockDrag);

            Grid.SetColumn(rightUtilBtns, 2);
            topBar.Children.Add(rightUtilBtns);

            Grid.SetRow(topBar, 0);
            rootGrid.Children.Add(topBar);

            Grid middleGrid = new Grid { Margin = new Thickness(0, 0, 0, 9) };
            middleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.42, GridUnitType.Star), MinWidth = 196 });
            middleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            middleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.58, GridUnitType.Star) });

            Border dialCard = new Border
            {
                CornerRadius = new CornerRadius(11),
                Background = HexBrush("#77221B18"),
                BorderBrush = HexBrush("#3E322B"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8)
            };

            Grid dialLayout = new Grid();
            dialLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            txtTimerDialPhase = new TextBlock
            {
                Text = L("Zamanlayıcı", "Stopwatch"),
                Foreground = HexBrush("#B8A99E"),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            Grid.SetRow(txtTimerDialPhase, 0);
            dialLayout.Children.Add(txtTimerDialPhase);

            Grid dialBody = new Grid();
            dialBody.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(116) });
            dialBody.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid dialFace = new Grid { Width = 114, Height = 114 };
            timerDialInnerCircle = new Border
            {
                Width = 108,
                Height = 108,
                CornerRadius = new CornerRadius(54),
                Background = HexBrush("#662E2520"),
                BorderBrush = HexBrush("#443730"),
                BorderThickness = new Thickness(1.2)
            };
            dialFace.Children.Add(timerDialInnerCircle);

            Canvas tickCanvas = new Canvas { Width = 114, Height = 114, IsHitTestVisible = false };
            double cx = 57.0, cy = 57.0;
            for (int t = 0; t < 24; t++)
            {
                double rad = ((t * 15.0) - 90.0) * (Math.PI / 180.0);
                double r1 = 39.0;
                double r2 = 47.0;
                Line tick = new Line
                {
                    X1 = cx + r1 * Math.Cos(rad),
                    Y1 = cy + r1 * Math.Sin(rad),
                    X2 = cx + r2 * Math.Cos(rad),
                    Y2 = cy + r2 * Math.Sin(rad),
                    Stroke = HexBrush("#50433B"),
                    StrokeThickness = 2.0,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                timerDialTicks[t] = tick;
                tickCanvas.Children.Add(tick);
            }
            dialFace.Children.Add(tickCanvas);

            StackPanel dialCenterText = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            txtTimerDialCenter = new TextBlock
            {
                Text = "00:00",
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 18.5,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            txtTimerDialSub = new TextBlock
            {
                Text = L("⏱ Hazır", "⏱ Ready"),
                Foreground = HexBrush("#A8988C"),
                FontSize = 9.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, -1, 0, 0)
            };
            dialCenterText.Children.Add(txtTimerDialCenter);
            dialCenterText.Children.Add(txtTimerDialSub);
            dialFace.Children.Add(dialCenterText);

            Grid.SetColumn(dialFace, 0);
            dialBody.Children.Add(dialFace);

            StackPanel dialSideBtns = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                Tag = "Interactive"
            };

            btnPomoPlayPause = new Button
            {
                Width = 34,
                Height = 34,
                Cursor = Cursors.Hand,
                Background = HexBrush("#2EA043"),
                BorderBrush = HexBrush("#56D364"),
                BorderThickness = new Thickness(1.2),
                ToolTip = L("Başlat / Duraklat", "Start / Pause"),
                Tag = "Interactive"
            };
            ControlTemplate circleTpl = new ControlTemplate(typeof(Button));
            FrameworkElementFactory bdFactory = new FrameworkElementFactory(typeof(Border));
            bdFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(17));
            bdFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            bdFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            bdFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            FrameworkElementFactory cpFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            cpFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cpFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bdFactory.AppendChild(cpFactory);
            circleTpl.VisualTree = bdFactory;
            btnPomoPlayPause.Template = circleTpl;

            txtPomoPlayPauseIcon = new TextBlock
            {
                Text = "▶",
                Foreground = Brushes.White,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(1, 0, 0, 0)
            };
            btnPomoPlayPause.Content = txtPomoPlayPauseIcon;
            btnPomoPlayPause.Click += (s, e) =>
            {
                if (timerCurrentMode == 0)
                {
                    defaultTimerIsRunning = !defaultTimerIsRunning;
                    ShowToast(defaultTimerIsRunning ? L("▶ Zamanlayıcı başlatıldı", "▶ Timer started") : L("⏸ Zamanlayıcı duraklatıldı", "⏸ Timer paused"));
                }
                else if (timerCurrentMode == 1)
                {
                    gameTimerIsRunning = !gameTimerIsRunning;
                    ShowToast(gameTimerIsRunning ? L("▶ Eğlence sayacı başlatıldı", "▶ Gaming timer started") : L("⏸ Eğlence sayacı duraklatıldı", "⏸ Gaming timer paused"));
                }
                else
                {
                    if (pomoRemainingSeconds <= 0)
                        pomoRemainingSeconds = (pomoIsBreakPhase ? pomoBreakMinutes : pomoFocusMinutes) * 60;
                    pomoIsRunning = !pomoIsRunning;
                    ShowToast(pomoIsRunning ? L("▶ Pomodoro başlatıldı", "▶ Pomodoro started") : L("⏸ Pomodoro duraklatıldı", "⏸ Pomodoro paused"));
                }
                UpdateTimerWidgetUi();
            };
            dialSideBtns.Children.Add(btnPomoPlayPause);

            btnPomoReset = new Button
            {
                Width = 28,
                Height = 28,
                Margin = new Thickness(0, 8, 0, 0),
                Cursor = Cursors.Hand,
                Background = HexBrush("#2C2420"),
                BorderBrush = HexBrush("#463932"),
                BorderThickness = new Thickness(1),
                ToolTip = L("Süreyi sıfırla (Ders modunda Odak/Mola geçişi için 2 kez tıkla)", "Reset timer (Click twice in Study mode to switch Focus/Break)"),
                Tag = "Interactive",
                Template = circleTpl
            };
            btnPomoReset.Content = new TextBlock
            {
                Text = "↺",
                Foreground = HexBrush("#C7B9AE"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, -1, 0, 0)
            };
            btnPomoReset.Click += (s, e) =>
            {
                if (timerCurrentMode == 0)
                {
                    defaultTimerIsRunning = false;
                    defaultTimerSeconds = 0;
                    ShowToast(L("↺ Zamanlayıcı sıfırlandı", "↺ Timer reset"));
                }
                else if (timerCurrentMode == 1)
                {
                    gameTimerIsRunning = false;
                    timerGameSessionSeconds = 0;
                    ShowToast(L("↺ Oyun oturum sayacı sıfırlandı (günlük toplam korundu)", "↺ Session counter reset (daily total preserved)"));
                }
                else
                {
                    int fullSec = (pomoIsBreakPhase ? pomoBreakMinutes : pomoFocusMinutes) * 60;
                    if (!pomoIsRunning && Math.Abs(pomoRemainingSeconds - fullSec) < 2)
                    {
                        pomoIsBreakPhase = !pomoIsBreakPhase;
                    }
                    pomoIsRunning = false;
                    pomoRemainingSeconds = (pomoIsBreakPhase ? pomoBreakMinutes : pomoFocusMinutes) * 60;
                    ShowToast(pomoIsBreakPhase
                        ? string.Format(L("☕ Mola süresine geçildi ({0} dk)", "☕ Switched to Break ({0} min)"), pomoBreakMinutes)
                        : string.Format(L("📚 Odak süresine geçildi ({0} dk)", "📚 Switched to Focus ({0} min)"), pomoFocusMinutes));
                }
                UpdateTimerWidgetUi();
            };
            dialSideBtns.Children.Add(btnPomoReset);

            Grid.SetColumn(dialSideBtns, 1);
            dialBody.Children.Add(dialSideBtns);

            Viewbox dialViewbox = new Viewbox
            {
                Stretch = Stretch.Uniform,
                MaxHeight = 190,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = dialBody
            };
            Grid.SetRow(dialViewbox, 1);
            dialLayout.Children.Add(dialViewbox);
            dialCard.Child = dialLayout;
            Grid.SetColumn(dialCard, 0);
            middleGrid.Children.Add(dialCard);

            Grid rightStatsGrid = new Grid();
            rightStatsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rightStatsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rightStatsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            timerPomoSettingsPopup = new Border
            {
                Visibility = Visibility.Collapsed,
                CornerRadius = new CornerRadius(9),
                Background = HexBrush("#F2241D19"),
                BorderBrush = HexBrush(accentColorHex),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(9, 6, 9, 6),
                Margin = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Top,
                Tag = "Interactive"
            };
            Panel.SetZIndex(timerPomoSettingsPopup, 10);
            StackPanel psStack = new StackPanel();
            Grid psRow = new Grid();
            psRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            psRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            string minUnit = L(" dk", " min");
            StackPanel fAdj = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            fAdj.Children.Add(new TextBlock { Text = L("Odak:", "Focus:"), Foreground = HexBrush("#C7B9AE"), FontSize = 10.5, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center });
            Button bfMinus = CreateStyledIconButton("#221C18", "#4A3C34", L("Odak süresini azalt", "Decrease focus duration"));
            bfMinus.Padding = new Thickness(5, 1, 5, 1);
            bfMinus.Content = new TextBlock { Text = "−", Foreground = HexBrush("#FAF6F2"), FontSize = 10.5 };
            txtPomoFocusSettingVal = new TextBlock { Text = pomoFocusMinutes + minUnit, Foreground = HexBrush("#FAF6F2"), FontSize = 10.5, FontWeight = FontWeights.Bold, Margin = new Thickness(5, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
            Button bfPlus = CreateStyledIconButton("#221C18", "#4A3C34", L("Odak süresini artır", "Increase focus duration"));
            bfPlus.Padding = new Thickness(5, 1, 5, 1);
            bfPlus.Content = new TextBlock { Text = "+", Foreground = HexBrush("#FAF6F2"), FontSize = 10.5 };
            bfMinus.Click += (s, e) => AdjustPomodoroSettings(-5, 0);
            bfPlus.Click += (s, e) => AdjustPomodoroSettings(5, 0);
            fAdj.Children.Add(bfMinus); fAdj.Children.Add(txtPomoFocusSettingVal); fAdj.Children.Add(bfPlus);
            Grid.SetColumn(fAdj, 0);
            psRow.Children.Add(fAdj);

            StackPanel bAdj = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            bAdj.Children.Add(new TextBlock { Text = L("Mola:", "Break:"), Foreground = HexBrush("#C7B9AE"), FontSize = 10.5, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center });
            Button bbMinus = CreateStyledIconButton("#221C18", "#4A3C34", L("Mola süresini azalt", "Decrease break duration"));
            bbMinus.Padding = new Thickness(5, 1, 5, 1);
            bbMinus.Content = new TextBlock { Text = "−", Foreground = HexBrush("#FAF6F2"), FontSize = 10.5 };
            txtPomoBreakSettingVal = new TextBlock { Text = pomoBreakMinutes + minUnit, Foreground = HexBrush("#FAF6F2"), FontSize = 10.5, FontWeight = FontWeights.Bold, Margin = new Thickness(5, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
            Button bbPlus = CreateStyledIconButton("#221C18", "#4A3C34", L("Mola süresini artır", "Increase break duration"));
            bbPlus.Padding = new Thickness(5, 1, 5, 1);
            bbPlus.Content = new TextBlock { Text = "+", Foreground = HexBrush("#FAF6F2"), FontSize = 10.5 };
            bbMinus.Click += (s, e) => AdjustPomodoroSettings(0, -1);
            bbPlus.Click += (s, e) => AdjustPomodoroSettings(0, 1);
            bAdj.Children.Add(bbMinus); bAdj.Children.Add(txtPomoBreakSettingVal); bAdj.Children.Add(bbPlus);
            Grid.SetColumn(bAdj, 1);
            psRow.Children.Add(bAdj);
            psStack.Children.Add(psRow);

            StackPanel presetsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
            int[,] pPresets = { { 25, 5 }, { 45, 10 }, { 50, 10 }, { 60, 15 } };
            for (int pi = 0; pi < 4; pi++)
            {
                int pf = pPresets[pi, 0];
                int pb = pPresets[pi, 1];
                Button bp = CreateStyledIconButton("#26201D", "#3E322B", pf + L(" dk odak / ", "m focus / ") + pb + L(" dk mola", "m break"));
                bp.Padding = new Thickness(6, 2, 6, 2);
                bp.Margin = new Thickness(0, 0, 4, 0);
                bp.Content = new TextBlock { Text = pf + "/" + pb, Foreground = HexBrush("#D7CBC2"), FontSize = 9.5, FontWeight = FontWeights.SemiBold };
                bp.Click += (s, e) =>
                {
                    pomoFocusMinutes = pf;
                    pomoBreakMinutes = pb;
                    if (!pomoIsRunning) pomoRemainingSeconds = (pomoIsBreakPhase ? pomoBreakMinutes : pomoFocusMinutes) * 60;
                    txtPomoFocusSettingVal.Text = pomoFocusMinutes + L(" dk", " min");
                    txtPomoBreakSettingVal.Text = pomoBreakMinutes + L(" dk", " min");
                    SaveSettings();
                    UpdateTimerWidgetUi();
                };
                presetsRow.Children.Add(bp);
            }
            psStack.Children.Add(presetsRow);
            timerPomoSettingsPopup.Child = psStack;

            Border statStudyCard = CreateTimerStatCard(timerStudyColorHex, L("📚 Bugün Çalışılan", "📚 Studied Today"), out txtTodayStudyVal, out timerStudyStatBar);
            Border statGameCard = CreateTimerStatCard(timerGameColorHex, L("🎮 Bugün Oyun Oynanan", "🎮 Played Today"), out txtTodayGameVal, out timerGameStatBar);
            Border statPcCard = CreateTimerStatCard(accentColorHex, L("🖥 Bilgisayar Açık Kalma", "🖥 PC Uptime Today"), out txtTodayPcVal, out timerPcStatBar, true);
            Grid.SetRow(statStudyCard, 0);
            Grid.SetRow(statGameCard, 1);
            Grid.SetRow(statPcCard, 2);
            rightStatsGrid.Children.Add(statStudyCard);
            rightStatsGrid.Children.Add(statGameCard);
            rightStatsGrid.Children.Add(statPcCard);

            Grid.SetColumn(rightStatsGrid, 2);
            middleGrid.Children.Add(rightStatsGrid);
            Grid.SetColumn(timerPomoSettingsPopup, 2);
            middleGrid.Children.Add(timerPomoSettingsPopup);

            Grid.SetRow(middleGrid, 1);
            rootGrid.Children.Add(middleGrid);

            Border heatmapBorder = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = HexBrush("#661E1815"),
                BorderBrush = HexBrush("#382E28"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6)
            };

            timerHeatmapHost = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                ClipToBounds = true,
                Tag = "Interactive"
            };
            timerHeatmapCanvas = new Canvas
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            timerHeatmapHost.Children.Add(timerHeatmapCanvas);
            timerHeatmapHost.SizeChanged += (s, e) =>
            {
                int w = (int)Math.Round(timerHeatmapHost.ActualWidth);
                int h = (int)Math.Round(timerHeatmapHost.ActualHeight);
                if (Math.Abs(w - lastHeatmapLayoutWidth) >= 2 || Math.Abs(h - lastHeatmapLayoutHeight) >= 2)
                {
                    BuildTimerHeatmapGrid();
                }
            };

            heatmapBorder.Child = timerHeatmapHost;
            Grid.SetRow(heatmapBorder, 2);
            rootGrid.Children.Add(heatmapBorder);

            timerResizeGrip = new Border
            {
                Width = 12,
                Height = 12,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, -8, -4),
                Visibility = isDraggable ? Visibility.Visible : Visibility.Collapsed,
                Child = new TextBlock { Text = "◢", FontSize = 9, Foreground = HexBrush("#8C7A6B") }
            };
            Grid.SetRow(timerResizeGrip, 2);
            rootGrid.Children.Add(timerResizeGrip);

            timerOuterBorder.Child = rootGrid;
            timerWindow.Content = timerOuterBorder;
            UpdateDraggableButtonUi();
        }

        private Border CreateTimerStatCard(string pillHex, string label, out TextBlock valueTextBlock, out Border accentBar, bool isLast = false)
        {
            Border card = new Border
            {
                CornerRadius = new CornerRadius(9),
                Background = HexBrush("#7728211E"),
                BorderBrush = HexBrush("#3E322B"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(9, 7, 10, 7),
                Margin = new Thickness(0, 0, 0, isLast ? 0 : 6)
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            accentBar = new Border
            {
                Width = 3.5,
                Height = 16,
                CornerRadius = new CornerRadius(2),
                Background = HexBrush(pillHex),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(accentBar, 0);
            g.Children.Add(accentBar);

            TextBlock lbl = new TextBlock
            {
                Text = label,
                Foreground = HexBrush("#D7CBC2"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(lbl, 1);
            g.Children.Add(lbl);

            valueTextBlock = new TextBlock
            {
                Text = L("0 dk", "0 min"),
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(valueTextBlock, 2);
            g.Children.Add(valueTextBlock);

            card.Child = g;
            return card;
        }

        private void OnHeatmapCellToolTipOpening(object sender, ToolTipEventArgs e)
        {
            Border cell = sender as Border;
            if (cell != null && cell.Tag is DateTime)
            {
                cell.ToolTip = FormatActivityTooltip((DateTime)cell.Tag);
            }
        }

        private void OnHeatmapCellClicked(object sender, MouseButtonEventArgs e)
        {
            Border cell = sender as Border;
            if (cell != null && cell.Tag is DateTime)
            {
                OpenTimerDetailedViewWindow((DateTime)cell.Tag);
                e.Handled = true;
            }
        }

        private void BuildTimerHeatmapGrid()
        {
            if (timerHeatmapCanvas == null) return;
            try
            {
                double availW = (timerHeatmapHost != null && timerHeatmapHost.ActualWidth > 40)
                    ? timerHeatmapHost.ActualWidth
                    : Math.Max(260.0, savedTimerWidth - 48.0);
                double availH = (timerHeatmapHost != null && timerHeatmapHost.ActualHeight > 25)
                    ? timerHeatmapHost.ActualHeight
                    : Math.Max(68.0, (savedTimerHeight - 54.0) * (0.92 / 2.42) - 14.0);

                lastHeatmapLayoutWidth = (int)Math.Round(availW);
                lastHeatmapLayoutHeight = (int)Math.Round(availH);
                todayHeatmapCell = null;

                string[] shortMonths = IsEn
                    ? new string[] { "", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" }
                    : new string[] { "", "Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara" };
                DateTime today = DateTime.Today;
                int todayDowOffset = ((int)today.DayOfWeek + 6) % 7;
                DateTime currentWeekMonday = today.AddDays(-todayDowOffset);

                const double headerH = 15.0;
                double gridH = Math.Max(35.0, availH - headerH);
                double rowPitch = gridH / 7.0;
                int weeksCount = Math.Max(14, Math.Min(78, (int)Math.Round(availW / rowPitch)));
                double colPitch = availW / weeksCount;
                double cellStep = Math.Min(colPitch, rowPitch);
                double gap = Math.Max(1.5, Math.Round(cellStep * 0.18));
                double squareSize = Math.Max(4.0, Math.Floor(cellStep - gap));
                double cornerRad = Math.Max(1.4, Math.Round(squareSize * 0.22, 1));
                CornerRadius cellCorner = new CornerRadius(cornerRad);
                Brush todayBorderBrush = HexBrush("#FAF6F2");
                Brush monthLabelBrush = HexBrush("#9E8E82");

                DateTime gridStartMonday = currentWeekMonday.AddDays(-(weeksCount - 1) * 7);
                double[] rowY = new double[7];
                for (int r = 0; r < 7; r++)
                {
                    rowY[r] = headerH + Math.Round(r * (gridH - squareSize) / 6.0);
                }

                int lastLabeledMonth = -1;
                double lastLabelX = -100.0;
                int usedMonthCount = 0;
                int usedCellCount = 0;

                for (int w = 0; w < weeksCount; w++)
                {
                    DateTime colMonday = gridStartMonday.AddDays(w * 7);
                    double x = (weeksCount > 1) ? Math.Round(w * (availW - squareSize) / (weeksCount - 1)) : 0;

                    int mToLabel = -1;
                    for (int d = 0; d < 7; d++)
                    {
                        DateTime dt = colMonday.AddDays(d);
                        if (dt > today) break;
                        if (dt.Day <= 7 && dt.Month != lastLabeledMonth)
                        {
                            mToLabel = dt.Month;
                            break;
                        }
                    }
                    if (w == 0 && mToLabel < 0) mToLabel = colMonday.Month;

                    if (mToLabel > 0 && (x - lastLabelX) >= 28.0 && x <= availW - 22.0)
                    {
                        lastLabeledMonth = mToLabel;
                        lastLabelX = x;

                        TextBlock mLbl;
                        if (usedMonthCount < heatmapMonthPool.Count)
                        {
                            mLbl = heatmapMonthPool[usedMonthCount];
                        }
                        else
                        {
                            mLbl = new TextBlock
                            {
                                FontSize = 9.0,
                                FontWeight = FontWeights.SemiBold
                            };
                            heatmapMonthPool.Add(mLbl);
                            timerHeatmapCanvas.Children.Add(mLbl);
                        }
                        usedMonthCount++;

                        mLbl.Text = shortMonths[mToLabel];
                        mLbl.Foreground = monthLabelBrush;
                        mLbl.Visibility = Visibility.Visible;
                        Canvas.SetLeft(mLbl, x);
                        Canvas.SetTop(mLbl, 0);
                    }

                    for (int r = 0; r < 7; r++)
                    {
                        DateTime cellDate = colMonday.AddDays(r).Date;
                        if (cellDate > today) continue;

                        bool isToday = (cellDate == today);

                        Border cell;
                        if (usedCellCount < heatmapCellPool.Count)
                        {
                            cell = heatmapCellPool[usedCellCount];
                        }
                        else
                        {
                            cell = new Border
                            {
                                Cursor = Cursors.Hand,
                                ToolTip = ""
                            };
                            cell.ToolTipOpening += OnHeatmapCellToolTipOpening;
                            cell.MouseLeftButtonDown += OnHeatmapCellClicked;
                            heatmapCellPool.Add(cell);
                            timerHeatmapCanvas.Children.Add(cell);
                        }
                        usedCellCount++;

                        cell.Width = squareSize;
                        cell.Height = squareSize;
                        cell.CornerRadius = cellCorner;
                        cell.Background = GetHeatmapCellBrush(cellDate);
                        cell.BorderBrush = isToday ? todayBorderBrush : Brushes.Transparent;
                        cell.BorderThickness = new Thickness(isToday ? 1.0 : 0);
                        cell.Tag = cellDate;
                        cell.Visibility = Visibility.Visible;

                        Canvas.SetLeft(cell, x);
                        Canvas.SetTop(cell, rowY[r]);
                        if (isToday) todayHeatmapCell = cell;
                    }
                }

                for (int i = usedMonthCount; i < heatmapMonthPool.Count; i++)
                    heatmapMonthPool[i].Visibility = Visibility.Collapsed;
                for (int i = usedCellCount; i < heatmapCellPool.Count; i++)
                    heatmapCellPool[i].Visibility = Visibility.Collapsed;
            }
            catch { }
        }

        private Brush BuildStackedHourBrush(int studySec, int gameSec, int pcSec)
        {
            Color emptyDark = Color.FromRgb(34, 29, 26);
            Color accent = ParseColor(accentColorHex);
            cachedHeatmapAccentHex = accentColorHex;

            double uptimeRatio = Math.Max(0.0, Math.Min(1.0, (Math.Max(0, pcSec) / 3600.0) / 8.0));
            Color defaultCol = BlendColors(emptyDark, accent, uptimeRatio, 255);

            double studyHours = Math.Max(0, studySec) / 3600.0;
            double gameHours = Math.Max(0, gameSec) / 3600.0;
            double totalHours = studyHours + gameHours;

            if (totalHours <= 0.0)
            {
                SolidColorBrush sb = new SolidColorBrush(defaultCol);
                sb.Freeze();
                return sb;
            }

            double studyFrac, gameFrac;
            if (totalHours >= 10.0)
            {
                studyFrac = studyHours / totalHours;
                gameFrac = gameHours / totalHours;
            }
            else
            {
                studyFrac = studyHours / 10.0;
                gameFrac = gameHours / 10.0;
            }

            Color studyCol = ParseColor(timerStudyColorHex);
            Color gameCol = ParseColor(timerGameColorHex);

            LinearGradientBrush lg = new LinearGradientBrush { StartPoint = new Point(0, 1), EndPoint = new Point(0, 0) };
            double cur = 0.0;
            if (studyFrac > 0.0005)
            {
                double next = Math.Min(1.0, cur + studyFrac);
                lg.GradientStops.Add(new GradientStop(studyCol, cur));
                lg.GradientStops.Add(new GradientStop(studyCol, next));
                cur = next;
            }
            if (gameFrac > 0.0005 && cur < 0.9999)
            {
                double next = Math.Min(1.0, cur + gameFrac);
                lg.GradientStops.Add(new GradientStop(gameCol, cur));
                lg.GradientStops.Add(new GradientStop(gameCol, next));
                cur = next;
            }
            if (cur < 0.9999)
            {
                lg.GradientStops.Add(new GradientStop(defaultCol, cur));
                lg.GradientStops.Add(new GradientStop(defaultCol, 1.0));
            }
            lg.Freeze();
            return lg;
        }

        private Brush GetHeatmapCellBrush(DateTime date)
        {
            DailyActivityRecord rec;
            if (!activityByDate.TryGetValue(date.Date, out rec))
                return BuildStackedHourBrush(0, 0, 0);
            return BuildStackedHourBrush(rec.StudySeconds, rec.GameSeconds, rec.PcSeconds);
        }

        private string FormatDurationLocalized(int totalSeconds)
        {
            if (totalSeconds <= 0) return L("0 dk", "0 min");
            if (totalSeconds < 60) return totalSeconds + L(" sn", " sec");
            int totalMin = totalSeconds / 60;
            int hours = totalMin / 60;
            int mins = totalMin % 60;
            if (hours == 0) return totalMin + L(" dk", " min");
            return IsEn
                ? string.Format("{0}h {1:00}m", hours, mins)
                : string.Format("{0}s {1:00}dk", hours, mins);
        }

        private string FormatActivityTooltip(DateTime date)
        {
            string[] months = IsEn
                ? new string[] { "", "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" }
                : new string[] { "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };
            string[] days = IsEn
                ? new string[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" }
                : new string[] { "Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi" };
            DailyActivityRecord rec;
            activityByDate.TryGetValue(date.Date, out rec);
            int st = rec != null ? rec.StudySeconds : 0;
            int gm = rec != null ? rec.GameSeconds : 0;
            int pc = rec != null ? rec.PcSeconds : 0;

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0} {1} {2} {3}\n{4}: {5}\n{6}: {7}",
                date.Day, months[date.Month], date.Year, days[(int)date.DayOfWeek],
                L("📚 Ders Çalışılan", "📚 Studied"), FormatDurationLocalized(st),
                L("🎮 Oyun Oynanan", "🎮 Gaming"), FormatDurationLocalized(gm));
            if (rec != null && rec.GameBreakdown != null && rec.GameBreakdown.Count > 0)
            {
                List<KeyValuePair<string, int>> topGames = new List<KeyValuePair<string, int>>(rec.GameBreakdown);
                topGames.Sort((a, b) => b.Value.CompareTo(a.Value));
                int take = Math.Min(3, topGames.Count);
                for (int gi = 0; gi < take; gi++)
                {
                    if (topGames[gi].Value <= 0) continue;
                    sb.AppendFormat("\n   • {0}: {1}", topGames[gi].Key, FormatDurationLocalized(topGames[gi].Value));
                }
            }
            sb.AppendFormat("\n{0}: {1}\n{2}",
                L("🖥 Bilgisayar Açık", "🖥 PC Uptime"), FormatDurationLocalized(pc),
                L("(Detay için tıklayın)", "(Click for details)"));
            return sb.ToString();
        }

        private void SwitchTimerMode(int newMode)
        {
            if (newMode < 0 || newMode > 2) newMode = 0;
            if (timerCurrentMode == newMode)
                return;

            if (newMode != 1 && activeGamePid == 0)
            {
                activeGameName = "";
            }

            defaultTimerIsRunning = false;
            gameTimerIsRunning = false;
            pomoIsRunning = false;
            timerCurrentMode = newMode;

            if (timerCurrentMode == 1)
            {
                ShowToast(L("🎮 Eğlence Modu (Başlatmak için ▶ tuşuna basın)", "🎮 Gaming Mode (Press ▶ to start)"));
            }
            else if (timerCurrentMode == 2)
            {
                if (pomoRemainingSeconds <= 0)
                    pomoRemainingSeconds = (pomoIsBreakPhase ? pomoBreakMinutes : pomoFocusMinutes) * 60;
                ShowToast(L("📚 Ders Modu (Başlatmak için ▶ tuşuna basın)", "📚 Study Mode (Press ▶ to start)"));
            }
            else
            {
                ShowToast(L("🖥 Default Mod (Zamanlayıcı hazır)", "🖥 Default Mode (Timer ready)"));
            }

            SaveTimerActivityData();
            UpdateTimerWidgetUi();
        }

        private bool IsProcessStillAlive(uint pid)
        {
            if (pid == 0) return false;
            IntPtr hProc = IntPtr.Zero;
            try
            {
                hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                if (hProc == IntPtr.Zero)
                {
                    if (Marshal.GetLastWin32Error() == 5)
                    {
                        IntPtr fg = GetForegroundWindow();
                        if (fg != IntPtr.Zero)
                        {
                            uint fgPid;
                            GetWindowThreadProcessId(fg, out fgPid);
                            if (fgPid == pid) return true;
                        }
                    }
                    return false;
                }
                uint exitCode;
                return GetExitCodeProcess(hProc, out exitCode) && exitCode == STILL_ACTIVE;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (hProc != IntPtr.Zero) CloseHandle(hProc);
            }
        }

        private string TryGetProcessImagePath(uint pid)
        {
            if (pid == 0) return null;
            IntPtr hProc = IntPtr.Zero;
            try
            {
                hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                if (hProc == IntPtr.Zero) return null;
                procPathSb.Length = 0;
                int size = procPathSb.Capacity;
                if (QueryFullProcessImageName(hProc, 0, procPathSb, ref size) && size > 0)
                    return procPathSb.ToString();
                return null;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (hProc != IntPtr.Zero) CloseHandle(hProc);
            }
        }

        private static bool ExtractSubfolderAfterMarker(string fullPath, string lowerPath, string marker, out string folderName)
        {
            folderName = "";
            int idx = lowerPath.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return false;
            int start = idx + marker.Length;
            if (start >= fullPath.Length) return false;
            int slash = fullPath.IndexOf('\\', start);
            if (slash <= start) return false;
            folderName = fullPath.Substring(start, slash - start).Trim();
            return folderName.Length > 0;
        }

        private bool IsGameExecutable(string fullPath, IntPtr hwnd, out string friendlyName, out bool isJavaProcess)
        {
            friendlyName = "";
            isJavaProcess = false;
            if (string.IsNullOrEmpty(fullPath)) return false;

            string exeStem = System.IO.Path.GetFileNameWithoutExtension(fullPath);
            if (string.IsNullOrEmpty(exeStem)) return false;
            string lowerStem = exeStem.ToLowerInvariant();
            string lowerPath = fullPath.ToLowerInvariant();

            if (ExcludedNonGameExes.Contains(lowerStem)) return false;

            if ((lowerStem.Contains("launcher") && !lowerStem.Contains("minecraft")) ||
                lowerStem.EndsWith("helper") ||
                lowerStem.Contains("crashreporter") ||
                lowerStem.Contains("prereq") ||
                lowerStem.Contains("setup") ||
                lowerStem.Contains("unins") ||
                lowerStem.Contains("redist") ||
                lowerStem.Contains("updater") ||
                lowerStem.Contains("installer"))
            {
                return false;
            }

            if (lowerStem == "javaw" || lowerStem == "java")
            {
                isJavaProcess = true;
                windowTitleSb.Length = 0;
                int tLen = GetWindowText(hwnd, windowTitleSb, windowTitleSb.Capacity);
                if (tLen > 0)
                {
                    string wt = windowTitleSb.ToString().ToLowerInvariant();
                    if (wt.Contains("minecraft") || wt.Contains("lunar client") ||
                        wt.Contains("feather") || wt.Contains("badlion") ||
                        wt.Contains("modrinth") || wt.Contains("curseforge"))
                    {
                        friendlyName = "Minecraft";
                        return true;
                    }
                }
                return false;
            }

            string knownTitle;
            bool hasKnownTitle = KnownGameFriendlyNames.TryGetValue(lowerStem, out knownTitle);

            string folder;
            if (ExtractSubfolderAfterMarker(fullPath, lowerPath, "\\steamapps\\common\\", out folder))
            {
                string lf = folder.ToLowerInvariant();
                if (lf.Contains("steamworks") || lf == "wallpaper_engine" || lf.Contains("steamvr") ||
                    lf.StartsWith("proton") || lf == "soundpad" || lf.Contains("controller"))
                    return false;
                friendlyName = hasKnownTitle ? knownTitle : folder;
                return true;
            }
            if (ExtractSubfolderAfterMarker(fullPath, lowerPath, "\\epic games\\", out folder))
            {
                string lf = folder.ToLowerInvariant();
                if (lf == "launcher" || lf == "epic online services" || lf == "directxredist")
                    return false;
                friendlyName = hasKnownTitle ? knownTitle : folder;
                return true;
            }
            if (ExtractSubfolderAfterMarker(fullPath, lowerPath, "\\riot games\\", out folder))
            {
                string lf = folder.ToLowerInvariant();
                if (lf == "riot client" || lf == "riot vanguard")
                    return false;
                friendlyName = hasKnownTitle ? knownTitle : folder;
                return true;
            }
            if (ExtractSubfolderAfterMarker(fullPath, lowerPath, "\\xboxgames\\", out folder) ||
                ExtractSubfolderAfterMarker(fullPath, lowerPath, "\\gog galaxy\\games\\", out folder) ||
                ExtractSubfolderAfterMarker(fullPath, lowerPath, "\\gog games\\", out folder) ||
                ExtractSubfolderAfterMarker(fullPath, lowerPath, "\\ubisoft game launcher\\games\\", out folder) ||
                ExtractSubfolderAfterMarker(fullPath, lowerPath, "\\ea games\\", out folder) ||
                ExtractSubfolderAfterMarker(fullPath, lowerPath, "\\origin games\\", out folder) ||
                ExtractSubfolderAfterMarker(fullPath, lowerPath, "\\battle.net\\games\\", out folder))
            {
                if (folder.Equals("GamingServices", StringComparison.OrdinalIgnoreCase))
                    return false;
                friendlyName = hasKnownTitle ? knownTitle : folder;
                return true;
            }
            if (ExtractSubfolderAfterMarker(fullPath, lowerPath, "\\games\\", out folder) ||
                ExtractSubfolderAfterMarker(fullPath, lowerPath, "\\oyunlar\\", out folder))
            {
                string lf = folder.ToLowerInvariant();
                if (lf == "launcher" || lf == "riot client")
                    return false;
                friendlyName = hasKnownTitle ? knownTitle : folder;
                return true;
            }

            if (hasKnownTitle)
            {
                friendlyName = knownTitle;
                return true;
            }

            return false;
        }

        private void CheckAutoGameDetection()
        {
            if (activeGamePid != 0)
            {
                if (!IsProcessStillAlive(activeGamePid))
                {
                    string closedGame = string.IsNullOrEmpty(activeGameName) ? L("Oyun", "Game") : activeGameName;
                    activeGamePid = 0;
                    activeGameName = "";
                    lastCheckedNonGamePid = 0;
                    lastCheckedWasJava = false;

                    timerCurrentMode = 0;
                    defaultTimerIsRunning = false;
                    gameTimerIsRunning = false;
                    pomoIsRunning = false;
                    SaveTimerActivityData();
                    UpdateTimerWidgetUi();

                    try
                    {
                        if (trayIcon != null)
                        {
                            trayIcon.ShowBalloonTip(4000, L("🖥 Oyun Kapatıldı", "🖥 Game Closed"),
                                string.Format(L("{0} kapatıldı. Otomatik olarak Default Moda geçildi.", "{0} closed. Switched back to Default Mode."), closedGame),
                                System.Windows.Forms.ToolTipIcon.Info);
                        }
                    }
                    catch { }
                    ShowToast(string.Format(L("🖥 Oyun kapatıldı ({0}) • Default Moda geçildi", "🖥 Game closed ({0}) • Default Mode active"), closedGame));
                }
                return;
            }

            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == cachedMainHwnd || fg == cachedCalHwnd || fg == cachedTimerHwnd || fg == cachedTaskbarOverlayHwnd)
                return;

            uint pid;
            GetWindowThreadProcessId(fg, out pid);
            if (pid == 0 || (pid == lastCheckedNonGamePid && !lastCheckedWasJava))
                return;

            string fullPath = TryGetProcessImagePath(pid);
            if (string.IsNullOrEmpty(fullPath))
                return;

            string detectedName;
            bool isJava;
            if (!IsGameExecutable(fullPath, fg, out detectedName, out isJava))
            {
                lastCheckedNonGamePid = pid;
                lastCheckedWasJava = isJava;
                return;
            }

            activeGamePid = pid;
            activeGameName = detectedName;
            lastCheckedNonGamePid = 0;
            lastCheckedWasJava = false;

            int prevMode = timerCurrentMode;
            timerCurrentMode = 1;
            defaultTimerIsRunning = false;
            pomoIsRunning = false;
            gameTimerIsRunning = true;
            if (prevMode != 1)
            {
                timerGameSessionSeconds = 0;
            }

            SaveTimerActivityData();
            UpdateTimerWidgetUi();

            if (prevMode == 2)
            {
                try { System.Media.SystemSounds.Hand.Play(); } catch { }
                try
                {
                    if (trayIcon != null)
                    {
                        trayIcon.ShowBalloonTip(6000, L("⚠️ Ders Modunda Oyun Algılandı!", "⚠️ Game Detected During Study Mode!"),
                            string.Format(L("{0} açıldığı için Pomodoro duraklatıldı ve otomatik olarak Eğlence Moduna geçildi!", "{0} launched! Pomodoro paused and switched to Gaming Mode."), detectedName),
                            System.Windows.Forms.ToolTipIcon.Warning);
                    }
                }
                catch { }
                ShowToast(string.Format(L("⚠️ Ders modundayken oyun açıldı ({0})! Eğlence moduna geçildi.", "⚠️ Game opened in Study Mode ({0})! Switched to Gaming Mode."), detectedName));
            }
            else if (prevMode == 0)
            {
                try
                {
                    if (trayIcon != null)
                    {
                        trayIcon.ShowBalloonTip(4000, L("🎮 Oyun Algılandı", "🎮 Game Detected"),
                            string.Format(L("{0} algılandı. Otomatik olarak Eğlence Moduna geçildi.", "{0} detected. Automatically switched to Gaming Mode."), detectedName),
                            System.Windows.Forms.ToolTipIcon.Info);
                    }
                }
                catch { }
                ShowToast(string.Format(L("🎮 Oyun algılandı ({0}) • Eğlence Modu aktif", "🎮 Game detected ({0}) • Gaming Mode active"), detectedName));
            }
        }

        private void AdjustPomodoroSettings(int deltaFocus, int deltaBreak)
        {
            pomoFocusMinutes = Math.Max(1, Math.Min(180, pomoFocusMinutes + deltaFocus));
            pomoBreakMinutes = Math.Max(1, Math.Min(60, pomoBreakMinutes + deltaBreak));
            if (!pomoIsRunning)
            {
                pomoRemainingSeconds = (pomoIsBreakPhase ? pomoBreakMinutes : pomoFocusMinutes) * 60;
            }
            string unit = L(" dk", " min");
            if (txtPomoFocusSettingVal != null) txtPomoFocusSettingVal.Text = pomoFocusMinutes + unit;
            if (txtPomoBreakSettingVal != null) txtPomoBreakSettingVal.Text = pomoBreakMinutes + unit;
            SaveSettings();
            UpdateTimerWidgetUi();
        }

        private void OnTimerWidgetSecondTick()
        {
            DateTime today = DateTime.Today;
            if (today != activeTrackingDate)
            {
                activeTrackingDate = today;
                SaveTimerActivityData();
                BuildTimerHeatmapGrid();
            }

            autoGameCheckTick++;
            if (autoGameCheckTick >= 2)
            {
                autoGameCheckTick = 0;
                CheckAutoGameDetection();
            }

            DailyActivityRecord todayRec = GetOrCreateActivityRecord(today);
            todayRec.PcSeconds++;

            if (timerCurrentMode == 0 && defaultTimerIsRunning)
            {
                defaultTimerSeconds++;
            }
            else if (timerCurrentMode == 1 && gameTimerIsRunning)
            {
                todayRec.GameSeconds++;
                timerGameSessionSeconds++;
                string gKey = SanitizeGameKey(activeGameName);
                int prevGSec;
                todayRec.GameBreakdown.TryGetValue(gKey, out prevGSec);
                todayRec.GameBreakdown[gKey] = prevGSec + 1;
            }
            else if (timerCurrentMode == 2 && pomoIsRunning)
            {
                if (!pomoIsBreakPhase)
                {
                    todayRec.StudySeconds++;
                }
                pomoRemainingSeconds--;

                if (pomoRemainingSeconds <= 0)
                {
                    bool finishedFocus = !pomoIsBreakPhase;
                    pomoIsBreakPhase = !pomoIsBreakPhase;
                    pomoRemainingSeconds = (pomoIsBreakPhase ? pomoBreakMinutes : pomoFocusMinutes) * 60;

                    string title = finishedFocus
                        ? L("Odaklanma Oturumu Tamamlandı!", "Focus Session Complete!")
                        : L("Mola Süresi Bitti!", "Break Time Over!");
                    string msg = finishedFocus
                        ? string.Format(L("{0} dk odaklanma bitti! {1} dk mola başlıyor.", "{0}m focus finished! {1}m break ready."), pomoFocusMinutes, pomoBreakMinutes)
                        : string.Format(L("{0} dk mola bitti! Yeni {1} dk odaklanma oturumu hazır.", "{0}m break finished! {1}m focus ready."), pomoBreakMinutes, pomoFocusMinutes);

                    try { System.Media.SystemSounds.Exclamation.Play(); } catch { }
                    try { if (trayIcon != null) trayIcon.ShowBalloonTip(5000, title, msg, System.Windows.Forms.ToolTipIcon.Info); } catch { }
                    ShowToast("⏱ " + msg);
                }
            }

            timerSaveTickCounter++;
            if (timerSaveTickCounter >= 30)
            {
                timerSaveTickCounter = 0;
                SaveTimerActivityData();
            }

            bool anyTimerRunning = (timerCurrentMode == 0 && defaultTimerIsRunning) ||
                (timerCurrentMode == 1 && gameTimerIsRunning) ||
                (timerCurrentMode == 2 && pomoIsRunning);

            if (anyTimerRunning)
            {
                int totalActive = todayRec.StudySeconds + todayRec.GameSeconds;
                if (todayHeatmapCell != null && (totalActive <= 2 || (totalActive % 30) == 0))
                {
                    todayHeatmapCell.Background = GetHeatmapCellBrush(today);
                }

                if (showTimerWidget)
                {
                    UpdateTimerWidgetUi();
                }
                if (timerDetailedWindow != null && refreshTimerDetailedViewAction != null && (todayRec.PcSeconds % 5) == 0)
                {
                    refreshTimerDetailedViewAction();
                }
            }
            else
            {
                if (todayHeatmapCell != null && (todayRec.PcSeconds <= 5 || (todayRec.PcSeconds % 60) == 0))
                {
                    todayHeatmapCell.Background = GetHeatmapCellBrush(today);
                }
                if (showTimerWidget && txtTodayPcVal != null && (todayRec.PcSeconds < 60 || (todayRec.PcSeconds % 60) == 0))
                {
                    txtTodayPcVal.Text = FormatDurationLocalized(todayRec.PcSeconds);
                }
                if (timerDetailedWindow != null && refreshTimerDetailedViewAction != null && (todayRec.PcSeconds % 10) == 0)
                {
                    refreshTimerDetailedViewAction();
                }
            }
        }

        private string FormatClockMMSS(int totalSeconds)
        {
            int sec = Math.Max(0, totalSeconds);
            if (sec >= 3600)
                return string.Format("{0}:{1:00}:{2:00}", sec / 3600, (sec % 3600) / 60, sec % 60);
            return string.Format("{0:00}:{1:00}", sec / 60, sec % 60);
        }

        private void UpdateTimerWidgetUi()
        {
            if (timerWindow == null) return;
            try
            {
                string[] modeHighlightHex = { accentColorHex, timerGameColorHex, timerStudyColorHex };
                for (int i = 0; i < 3; i++)
                {
                    Button mb = btnTimerModes[i];
                    if (mb == null) continue;
                    bool isActive = (timerCurrentMode == i);
                    mb.Background = HexBrush(isActive ? "#38281E" : "#221C18");
                    mb.BorderBrush = HexBrush(isActive ? modeHighlightHex[i] : "#3A2F29");
                    TextBlock tb = mb.Content as TextBlock;
                    if (tb != null) tb.Foreground = HexBrush(isActive ? "#FAF6F2" : "#9E8E82");
                }

                string activeTickHex = timerStudyColorHex;
                string dialCenterStr = "00:00";
                int litTicks = 0;
                bool activeRunning = false;

                if (timerCurrentMode == 0)
                {
                    activeRunning = defaultTimerIsRunning;
                    txtTimerDialPhase.Text = L("Zamanlayıcı", "Stopwatch");
                    dialCenterStr = FormatClockMMSS(defaultTimerSeconds);
                    txtTimerDialSub.Text = defaultTimerIsRunning
                        ? L("⏱ Sayılıyor", "⏱ Running")
                        : (defaultTimerSeconds > 0 ? L("⏸ Duraklatıldı", "⏸ Paused") : L("⏱ Hazır", "⏱ Ready"));
                    activeTickHex = accentColorHex;
                    if (defaultTimerSeconds == 0 && !defaultTimerIsRunning)
                        litTicks = 0;
                    else
                    {
                        int secInMin = defaultTimerSeconds % 60;
                        litTicks = (secInMin == 0 && defaultTimerSeconds > 0) ? 24 : Math.Max(1, (secInMin * 24) / 60);
                    }
                }
                else if (timerCurrentMode == 1)
                {
                    activeRunning = gameTimerIsRunning;
                    txtTimerDialPhase.Text = L("Eğlence / Oyun Modu", "Gaming Mode");
                    dialCenterStr = FormatClockMMSS(timerGameSessionSeconds);
                    if (gameTimerIsRunning)
                    {
                        string subText = L("🎮 Kaydediliyor", "🎮 Recording");
                        if (!string.IsNullOrEmpty(activeGameName))
                        {
                            string dispName = activeGameName.Length > 18 ? activeGameName.Substring(0, 16) + ".." : activeGameName;
                            subText = "🎮 " + dispName;
                        }
                        txtTimerDialSub.Text = subText;
                    }
                    else
                    {
                        txtTimerDialSub.Text = timerGameSessionSeconds > 0 ? L("⏸ Duraklatıldı", "⏸ Paused") : L("🎮 Hazır", "🎮 Ready");
                    }
                    activeTickHex = timerGameColorHex;
                    litTicks = (timerGameSessionSeconds == 0 && !gameTimerIsRunning) ? 0 : Math.Max(1, ((timerGameSessionSeconds % 3600) * 24) / 3600);
                }
                else
                {
                    activeRunning = pomoIsRunning;
                    txtTimerDialPhase.Text = pomoIsBreakPhase ? L("Mola Oturumu", "Break Session") : L("Odaklanma oturumu", "Focus Session");
                    int totalPhaseSec = Math.Max(1, (pomoIsBreakPhase ? pomoBreakMinutes : pomoFocusMinutes) * 60);
                    if (pomoRemainingSeconds == totalPhaseSec && !pomoIsRunning)
                    {
                        dialCenterStr = (pomoIsBreakPhase ? pomoBreakMinutes : pomoFocusMinutes) + L(" dk", " min");
                        txtTimerDialSub.Text = pomoIsBreakPhase ? L("☕ Hazır", "☕ Ready") : L("📚 Hazır", "📚 Ready");
                    }
                    else
                    {
                        dialCenterStr = FormatClockMMSS(pomoRemainingSeconds);
                        txtTimerDialSub.Text = pomoIsRunning
                            ? (pomoIsBreakPhase ? L("☕ Dinlen", "☕ Break") : L("📚 Çalışılıyor", "📚 Focusing"))
                            : L("⏸ Duraklatıldı", "⏸ Paused");
                    }

                    activeTickHex = pomoIsBreakPhase ? accentColorHex : timerStudyColorHex;
                    double frac = Math.Max(0.0, Math.Min(1.0, (double)pomoRemainingSeconds / totalPhaseSec));
                    litTicks = (int)Math.Ceiling(frac * 24.0);
                }

                txtTimerDialCenter.Text = dialCenterStr;
                txtTimerDialCenter.FontSize = dialCenterStr.Length >= 7 ? 15.0 : 18.5;

                if (litTicks != lastRenderedLitTicks || !string.Equals(activeTickHex, lastRenderedTickHex, StringComparison.Ordinal))
                {
                    Brush litBrush = HexBrush(activeTickHex);
                    Brush dimBrush = HexBrush("#443730");
                    for (int t = 0; t < 24; t++)
                    {
                        if (timerDialTicks[t] != null)
                            timerDialTicks[t].Stroke = (t < litTicks) ? litBrush : dimBrush;
                    }
                    lastRenderedLitTicks = litTicks;
                    lastRenderedTickHex = activeTickHex;
                }
                if (timerDialInnerCircle != null)
                {
                    timerDialInnerCircle.BorderBrush = HexBrush(activeRunning ? activeTickHex : "#443730");
                }

                if (txtPomoPlayPauseIcon != null && btnPomoPlayPause != null)
                {
                    txtPomoPlayPauseIcon.Text = activeRunning ? "⏸" : "▶";
                    txtPomoPlayPauseIcon.Margin = new Thickness(activeRunning ? 0 : 1.5, 0, 0, 0);

                    string playBtnKey = timerCurrentMode + "|" + (activeRunning ? "1" : "0") + "|" + accentColorHex + "|" + timerStudyColorHex + "|" + timerGameColorHex + "|" + appLanguage;
                    if (!string.Equals(playBtnKey, lastPlayBtnBrushKey, StringComparison.Ordinal))
                    {
                        Color modeBase = timerCurrentMode == 0
                            ? ParseColor(accentColorHex)
                            : (timerCurrentMode == 1 ? ParseColor(timerGameColorHex) : ParseColor(timerStudyColorHex));
                        Color btnBgCol = activeRunning
                            ? modeBase
                            : BlendColors(Color.FromRgb(26, 22, 19), modeBase, 0.72, 255);
                        Color btnBdCol = BlendColors(modeBase, Colors.White, activeRunning ? 0.38 : 0.25, 255);

                        SolidColorBrush bgBr = new SolidColorBrush(btnBgCol);
                        bgBr.Freeze();
                        SolidColorBrush bdBr = new SolidColorBrush(btnBdCol);
                        bdBr.Freeze();
                        cachedPlayBtnBg = bgBr;
                        cachedPlayBtnBd = bdBr;
                        lastPlayBtnBrushKey = playBtnKey;

                        btnPomoPlayPause.ToolTip = timerCurrentMode == 0
                            ? L("Zamanlayıcıyı Başlat / Duraklat", "Start / Pause Timer")
                            : (timerCurrentMode == 1 ? L("Eğlence Sayacını Başlat / Duraklat", "Start / Pause Gaming Timer") : L("Pomodoro'yu Başlat / Duraklat", "Start / Pause Pomodoro"));
                    }
                    btnPomoPlayPause.Background = cachedPlayBtnBg;
                    btnPomoPlayPause.BorderBrush = cachedPlayBtnBd;
                }

                DailyActivityRecord rec = GetOrCreateActivityRecord(DateTime.Today);
                if (txtTodayStudyVal != null) txtTodayStudyVal.Text = FormatDurationLocalized(rec.StudySeconds);
                if (txtTodayGameVal != null) txtTodayGameVal.Text = FormatDurationLocalized(rec.GameSeconds);
                if (txtTodayPcVal != null) txtTodayPcVal.Text = FormatDurationLocalized(rec.PcSeconds);
            }
            catch { }
        }

        private void ApplyTimerWidgetTheme()
        {
            if (timerOuterBorder == null) return;
            try
            {
                Color accent = ParseColor(accentColorHex);
                byte mainAlpha = (byte)Math.Round((bgOpacityPercent / 100.0) * 255.0);
                Color darkTop = BlendColors(Color.FromRgb(22, 19, 17), accent, 0.08, mainAlpha);
                Color darkBottom = BlendColors(Color.FromRgb(28, 24, 21), accent, 0.12, mainAlpha);
                Color outerBorderCol = BlendColors(Color.FromRgb(56, 48, 42), accent, 0.35, 220);

                LinearGradientBrush grad = new LinearGradientBrush(darkTop, darkBottom, new Point(0, 0), new Point(0, 1));
                grad.Freeze();
                timerOuterBorder.Background = grad;

                SolidColorBrush ob = new SolidColorBrush(outerBorderCol);
                ob.Freeze();
                timerOuterBorder.BorderBrush = ob;

                if (timerPomoSettingsPopup != null)
                    timerPomoSettingsPopup.BorderBrush = HexBrush(accentColorHex);
                if (timerStudyStatBar != null) timerStudyStatBar.Background = HexBrush(timerStudyColorHex);
                if (timerGameStatBar != null) timerGameStatBar.Background = HexBrush(timerGameColorHex);
                if (timerPcStatBar != null) timerPcStatBar.Background = HexBrush(accentColorHex);

                if (timerHeatmapCanvas != null && (heatmapCellPool.Count == 0 ||
                    !string.Equals(cachedHeatmapAccentHex, accentColorHex, StringComparison.OrdinalIgnoreCase)))
                {
                    BuildTimerHeatmapGrid();
                }
                if (refreshTimerSettingsPreviewAction != null) refreshTimerSettingsPreviewAction();
                if (refreshTimerDetailedViewAction != null) refreshTimerDetailedViewAction();
                UpdateTimerWidgetUi();
            }
            catch { }
        }

        private void OpenTimerSettingsWindow()
        {
            if (timerSettingsWindow != null && timerSettingsWindow.IsVisible)
            {
                timerSettingsWindow.Activate();
                return;
            }

            timerSettingsWindow = new Window
            {
                Title = L("Timer Widgeti Ayarları", "Timer Widget Settings"),
                Width = 425,
                Height = Math.Max(420, Math.Min(600, SystemParameters.WorkArea.Height - 60)),
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize,
                Topmost = true,
                UseLayoutRounding = true,
                SnapsToDevicePixels = true,
                FontFamily = new FontFamily("Segoe UI")
            };
            TextOptions.SetTextFormattingMode(timerSettingsWindow, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(timerSettingsWindow, TextRenderingMode.ClearType);
            Program.TryApplyWindowIcon(timerSettingsWindow);

            Border card = new Border
            {
                CornerRadius = new CornerRadius(15),
                Background = HexBrush("#F71B1613"),
                BorderBrush = HexBrush(accentColorHex),
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(8)
            };
            card.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 16, Opacity = 0.68 };

            Grid rootLayout = new Grid();
            rootLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid hdr = new Grid { Margin = new Thickness(0, 0, 0, 10), Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
            hdr.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    try { timerSettingsWindow.DragMove(); } catch { }
                }
            };
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel hdrTitles = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
            hdrTitles.Children.Add(new TextBlock { Text = L("⏱ Timer Widgeti Ayarları", "⏱ Timer Widget Settings"), Foreground = HexBrush("#FAF6F2"), FontSize = 14.5, FontWeight = FontWeights.Bold });
            hdrTitles.Children.Add(new TextBlock { Text = L("Günlere detaylı bakış, Pomodoro süreleri ve ders/oyun renkleri", "Detailed day view, Pomodoro durations & mode colors"), Foreground = HexBrush("#9E8E82"), FontSize = 10.5, Margin = new Thickness(0, 1, 0, 0) });
            Grid.SetColumn(hdrTitles, 0);
            hdr.Children.Add(hdrTitles);

            Button btnClose = CreateStyledIconButton("#2E2520", "#4A3C34", L("Kapat", "Close"));
            btnClose.Padding = new Thickness(9, 4, 9, 4);
            btnClose.Content = new TextBlock { Text = "✕", Foreground = HexBrush("#C7B9AE"), FontSize = 11.5 };
            btnClose.Click += (s, e) => timerSettingsWindow.Close();
            Grid.SetColumn(btnClose, 1);
            hdr.Children.Add(btnClose);
            Grid.SetRow(hdr, 0);
            rootLayout.Children.Add(hdr);

            ScrollViewer tsScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                PanningMode = PanningMode.VerticalOnly,
                Padding = new Thickness(0, 0, 4, 0)
            };
            ApplyThinDarkScrollBarStyle(tsScroll);
            Grid.SetRow(tsScroll, 1);
            rootLayout.Children.Add(tsScroll);

            StackPanel body = new StackPanel { Margin = new Thickness(0, 0, 2, 4) };
            tsScroll.Content = body;

            Border detailCard = CreateSettingsSectionCard(
                L("📊 Günlere Detaylı Bakış", "📊 Detailed Day View"),
                L("Takvim karesine basarak o gün oynadığın oyunları, çalışma ve PC süreni detaylı incele", "Click any activity square to inspect games played, study & PC time"));
            StackPanel detailStack = (StackPanel)detailCard.Child;
            Button btnOpenDetail = CreateStretchSettingsButton("#38281E", accentColorHex, L("Günlere detaylı bakış penceresini aç", "Open detailed day view window"));
            btnOpenDetail.Padding = new Thickness(12, 9, 12, 9);
            Grid openDetGrid = new Grid();
            openDetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            openDetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            openDetGrid.Children.Add(new TextBlock { Text = L("🔍 Günlere Detaylı Bak (Oyun Geçmişi)", "🔍 Detailed Day Breakdown (Game History)"), Foreground = HexBrush("#FAF6F2"), FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            TextBlock arrowTb = new TextBlock { Text = L("Aç →", "Open →"), Foreground = HexBrush(accentColorHex), FontSize = 11.5, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(arrowTb, 1);
            openDetGrid.Children.Add(arrowTb);
            btnOpenDetail.Content = openDetGrid;
            btnOpenDetail.Click += (s, e) => OpenTimerDetailedViewWindow(DateTime.Today);
            detailStack.Children.Add(btnOpenDetail);
            body.Children.Add(detailCard);

            Border pomoCard = CreateSettingsSectionCard(
                L("📚 Pomodoro Süreleri", "📚 Pomodoro Durations"),
                L("Ders modundaki odaklanma ve mola dakikalarını özelleştirin", "Customize focus & break minutes for Study mode"));
            StackPanel pomoStack = (StackPanel)pomoCard.Child;
            Grid pomoRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            pomoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pomoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            string unitStr = L(" dk", " min");
            StackPanel fAdj = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            fAdj.Children.Add(new TextBlock { Text = L("Odak:", "Focus:"), Foreground = HexBrush("#C7B9AE"), FontSize = 11.5, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
            Button bfMinus = CreateStyledIconButton("#221C18", "#4A3C34", L("Odak süresini azalt (-5 dk)", "Decrease focus (-5m)"));
            bfMinus.Padding = new Thickness(7, 2, 7, 2);
            bfMinus.Content = new TextBlock { Text = "−", Foreground = HexBrush("#FAF6F2"), FontSize = 12 };
            txtPomoFocusSettingVal = new TextBlock { Text = pomoFocusMinutes + unitStr, Foreground = HexBrush("#FAF6F2"), FontSize = 11.5, FontWeight = FontWeights.Bold, Margin = new Thickness(7, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
            Button bfPlus = CreateStyledIconButton("#221C18", "#4A3C34", L("Odak süresini artır (+5 dk)", "Increase focus (+5m)"));
            bfPlus.Padding = new Thickness(7, 2, 7, 2);
            bfPlus.Content = new TextBlock { Text = "+", Foreground = HexBrush("#FAF6F2"), FontSize = 12 };
            bfMinus.Click += (s, e) => AdjustPomodoroSettings(-5, 0);
            bfPlus.Click += (s, e) => AdjustPomodoroSettings(5, 0);
            fAdj.Children.Add(bfMinus); fAdj.Children.Add(txtPomoFocusSettingVal); fAdj.Children.Add(bfPlus);
            Grid.SetColumn(fAdj, 0);
            pomoRow.Children.Add(fAdj);

            StackPanel bAdj = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            bAdj.Children.Add(new TextBlock { Text = L("Mola:", "Break:"), Foreground = HexBrush("#C7B9AE"), FontSize = 11.5, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
            Button bbMinus = CreateStyledIconButton("#221C18", "#4A3C34", L("Mola süresini azalt (-1 dk)", "Decrease break (-1m)"));
            bbMinus.Padding = new Thickness(7, 2, 7, 2);
            bbMinus.Content = new TextBlock { Text = "−", Foreground = HexBrush("#FAF6F2"), FontSize = 12 };
            txtPomoBreakSettingVal = new TextBlock { Text = pomoBreakMinutes + unitStr, Foreground = HexBrush("#FAF6F2"), FontSize = 11.5, FontWeight = FontWeights.Bold, Margin = new Thickness(7, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
            Button bbPlus = CreateStyledIconButton("#221C18", "#4A3C34", L("Mola süresini artır (+1 dk)", "Increase break (+1m)"));
            bbPlus.Padding = new Thickness(7, 2, 7, 2);
            bbPlus.Content = new TextBlock { Text = "+", Foreground = HexBrush("#FAF6F2"), FontSize = 12 };
            bbMinus.Click += (s, e) => AdjustPomodoroSettings(0, -1);
            bbPlus.Click += (s, e) => AdjustPomodoroSettings(0, 1);
            bAdj.Children.Add(bbMinus); bAdj.Children.Add(txtPomoBreakSettingVal); bAdj.Children.Add(bbPlus);
            Grid.SetColumn(bAdj, 1);
            pomoRow.Children.Add(bAdj);
            pomoStack.Children.Add(pomoRow);

            UniformGrid presetsGrid = new UniformGrid { Columns = 4 };
            int[,] pPresets = { { 25, 5 }, { 45, 10 }, { 50, 10 }, { 60, 15 } };
            for (int pi = 0; pi < 4; pi++)
            {
                int pf = pPresets[pi, 0];
                int pb = pPresets[pi, 1];
                Button bp = CreateStretchSettingsButton("#26201D", "#3E322B", pf + " / " + pb);
                bp.Padding = new Thickness(6, 4, 6, 4);
                bp.Margin = new Thickness(pi == 0 ? 0 : 3, 0, pi == 3 ? 0 : 3, 0);
                bp.Content = new TextBlock { Text = pf + "/" + pb, Foreground = HexBrush("#D7CBC2"), FontSize = 10.5, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
                bp.Click += (s, e) =>
                {
                    pomoFocusMinutes = pf;
                    pomoBreakMinutes = pb;
                    if (!pomoIsRunning) pomoRemainingSeconds = (pomoIsBreakPhase ? pomoBreakMinutes : pomoFocusMinutes) * 60;
                    if (txtPomoFocusSettingVal != null) txtPomoFocusSettingVal.Text = pomoFocusMinutes + L(" dk", " min");
                    if (txtPomoBreakSettingVal != null) txtPomoBreakSettingVal.Text = pomoBreakMinutes + L(" dk", " min");
                    SaveSettings();
                    UpdateTimerWidgetUi();
                };
                presetsGrid.Children.Add(bp);
            }
            pomoStack.Children.Add(presetsGrid);
            body.Children.Add(pomoCard);

            Border colorCard = CreateSettingsSectionCard(
                L("🎨 Ders & Oyun Modu Renkleri", "🎨 Study & Gaming Colors"),
                L("Her saat karenin 1/10'unu doldurur (10 saati aşarsa oranlanır)", "Each hour fills 1/10 of a square (proportional above 10h)"));
            StackPanel colorStack = (StackPanel)colorCard.Child;

            Grid previewRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            previewRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            previewRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border stackedPreviewBox = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                BorderBrush = HexBrush("#5E5047"),
                BorderThickness = new Thickness(1),
                Background = BuildStackedHourBrush(10800, 7200, 28800),
                Margin = new Thickness(0, 0, 10, 0),
                ToolTip = L("Örnek: 3s Ders (%30) + 2s Oyun (%20) + %50 Default (8s PC %100 doygunluk)", "Example: 3h Study (30%) + 2h Gaming (20%) + 50% Default (8h PC 100% saturation)")
            };
            Grid.SetColumn(stackedPreviewBox, 0);
            previewRow.Children.Add(stackedPreviewBox);

            TextBlock txtPreviewHint = new TextBlock
            {
                Text = L("Önizleme (3s Ders + 2s Oyun = %30 / %20 + %50 Default; 8s PC = %100 doygunluk)", "Preview (3h Study + 2h Gaming = 30% / 20% + 50% Default; 8h PC = 100% saturation)"),
                Foreground = HexBrush("#B8A99E"),
                FontSize = 10.5,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(txtPreviewHint, 1);
            previewRow.Children.Add(txtPreviewHint);
            colorStack.Children.Add(previewRow);

            Action applyTimerCustomColors = () =>
            {
                SaveSettings();
                stackedPreviewBox.Background = BuildStackedHourBrush(10800, 7200, 28800);
                if (timerStudyStatBar != null) timerStudyStatBar.Background = HexBrush(timerStudyColorHex);
                if (timerGameStatBar != null) timerGameStatBar.Background = HexBrush(timerGameColorHex);
                lastPlayBtnBrushKey = "";
                BuildTimerHeatmapGrid();
                UpdateTimerWidgetUi();
                if (refreshTimerDetailedViewAction != null) refreshTimerDetailedViewAction();
            };

            string[] studyPresets = { "#2EA043", "#10B981", "#38BDF8", "#3B82F6", "#E2925C", "#F59E0B", "#EC4899" };
            string[] gamePresets = { "#A78BFA", "#8B5CF6", "#F43F5E", "#FB7185", "#F97316", "#14B8A6", "#38BDF8" };
            List<Border> studySwatches = new List<Border>();
            List<Border> gameSwatches = new List<Border>();

            Action<string, string[], List<Border>, Func<string>, Action<string>> buildColorPickerGroup = (label, presets, list, getHex, setHex) =>
            {
                colorStack.Children.Add(new TextBlock { Text = label, Foreground = HexBrush("#FAF6F2"), FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 5) });
                Grid row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                UniformGrid ug = new UniformGrid { Columns = 7, VerticalAlignment = VerticalAlignment.Center };
                for (int si = 0; si < presets.Length; si++)
                {
                    string hex = presets[si];
                    Border sw = new Border
                    {
                        Width = 22,
                        Height = 22,
                        CornerRadius = new CornerRadius(11),
                        Background = HexBrush(hex),
                        BorderBrush = Brushes.White,
                        BorderThickness = new Thickness(hex.Equals(getHex(), StringComparison.OrdinalIgnoreCase) ? 2 : 0.6),
                        Cursor = Cursors.Hand,
                        ToolTip = hex
                    };
                    list.Add(sw);
                    sw.MouseLeftButtonDown += (s, e) =>
                    {
                        setHex(hex);
                        foreach (Border b in list) b.BorderThickness = new Thickness(0.6);
                        sw.BorderThickness = new Thickness(2);
                        applyTimerCustomColors();
                    };
                    ug.Children.Add(sw);
                }
                Grid.SetColumn(ug, 0);
                row.Children.Add(ug);

                Button btnPick = CreateStyledIconButton("#2A221E", "#4A3C34", L("Özel renk seç", "Pick custom color"));
                btnPick.Margin = new Thickness(8, 0, 0, 0);
                btnPick.Padding = new Thickness(8, 4, 8, 4);
                btnPick.Content = new TextBlock { Text = L("🎨 Seç...", "🎨 Pick..."), Foreground = HexBrush("#FAF6F2"), FontSize = 10.5 };
                btnPick.Click += (s, e) =>
                {
                    Color cur = ParseColor(getHex());
                    using (var dlg = new System.Windows.Forms.ColorDialog())
                    {
                        dlg.FullOpen = true;
                        dlg.AnyColor = true;
                        dlg.Color = System.Drawing.Color.FromArgb(cur.R, cur.G, cur.B);
                        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            string hex = string.Format("#{0:X2}{1:X2}{2:X2}", dlg.Color.R, dlg.Color.G, dlg.Color.B);
                            setHex(hex);
                            foreach (Border b in list) b.BorderThickness = new Thickness(0.6);
                            applyTimerCustomColors();
                        }
                    }
                };
                Grid.SetColumn(btnPick, 1);
                row.Children.Add(btnPick);
                colorStack.Children.Add(row);
            };

            buildColorPickerGroup(L("📚 Ders Modu Rengi", "📚 Study Mode Color"), studyPresets, studySwatches, () => timerStudyColorHex, (h) => timerStudyColorHex = h);
            buildColorPickerGroup(L("🎮 Oyun / Eğlence Modu Rengi", "🎮 Gaming Mode Color"), gamePresets, gameSwatches, () => timerGameColorHex, (h) => timerGameColorHex = h);
            body.Children.Add(colorCard);

            refreshTimerSettingsPreviewAction = () =>
            {
                stackedPreviewBox.Background = BuildStackedHourBrush(10800, 7200, 28800);
                card.BorderBrush = HexBrush(accentColorHex);
            };

            card.Child = rootLayout;
            timerSettingsWindow.Content = card;
            timerSettingsWindow.Closed += (s, e) =>
            {
                txtPomoFocusSettingVal = null;
                txtPomoBreakSettingVal = null;
                refreshTimerSettingsPreviewAction = null;
                timerSettingsWindow = null;
            };
            timerSettingsWindow.Show();
        }

        private void OpenTimerDetailedViewWindow(DateTime initialDate)
        {
            if (timerDetailedWindow != null && timerDetailedWindow.IsVisible)
            {
                timerDetailedWindow.Close();
            }

            DateTime selectedDate = initialDate.Date;
            if (selectedDate > DateTime.Today) selectedDate = DateTime.Today;

            timerDetailedWindow = new Window
            {
                Title = L("Günlere Detaylı Bakış", "Detailed Day View"),
                Width = 690,
                Height = 475,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize,
                Topmost = true,
                UseLayoutRounding = true,
                SnapsToDevicePixels = true,
                FontFamily = new FontFamily("Segoe UI")
            };
            TextOptions.SetTextFormattingMode(timerDetailedWindow, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(timerDetailedWindow, TextRenderingMode.ClearType);
            Program.TryApplyWindowIcon(timerDetailedWindow);

            Border outerCard = new Border
            {
                CornerRadius = new CornerRadius(15),
                Background = HexBrush("#F71B1613"),
                BorderBrush = HexBrush(accentColorHex),
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(16, 13, 16, 14),
                Margin = new Thickness(8)
            };
            outerCard.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 16, Opacity = 0.70 };

            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid hdr = new Grid { Margin = new Thickness(0, 0, 0, 10), Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
            hdr.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    try { timerDetailedWindow.DragMove(); } catch { }
                }
            };
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel hdrLeft = new StackPanel();
            hdrLeft.Children.Add(new TextBlock { Text = L("📊 Günlere Detaylı Bakış", "📊 Detailed Day Breakdown"), Foreground = HexBrush("#FAF6F2"), FontSize = 15, FontWeight = FontWeights.Bold });
            hdrLeft.Children.Add(new TextBlock { Text = L("İstediğin günün karesine tıklayarak o günkü çalışma ve oyun detaylarını görüntüle", "Click any square to inspect study, PC & per-game breakdown for that day"), Foreground = HexBrush("#9E8E82"), FontSize = 10.5, Margin = new Thickness(0, 1, 0, 0) });
            Grid.SetColumn(hdrLeft, 0);
            hdr.Children.Add(hdrLeft);

            Button btnClose = CreateStyledIconButton("#2E2520", "#4A3C34", L("Kapat", "Close"));
            btnClose.Padding = new Thickness(9, 4, 9, 4);
            btnClose.Content = new TextBlock { Text = "✕", Foreground = HexBrush("#C7B9AE"), FontSize = 11.5 };
            btnClose.Click += (s, e) => timerDetailedWindow.Close();
            Grid.SetColumn(btnClose, 1);
            hdr.Children.Add(btnClose);
            Grid.SetRow(hdr, 0);
            root.Children.Add(hdr);

            Border hmContainer = new Border
            {
                CornerRadius = new CornerRadius(11),
                Background = HexBrush("#1E1815"),
                BorderBrush = HexBrush("#382E28"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 10)
            };
            Canvas detailCanvas = new Canvas { Width = 626, Height = 102, ClipToBounds = true };
            hmContainer.Child = detailCanvas;
            Grid.SetRow(hmContainer, 1);
            root.Children.Add(hmContainer);

            Grid bottomGrid = new Grid();
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.45, GridUnitType.Star) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.55, GridUnitType.Star) });

            Border leftDetailCard = new Border
            {
                CornerRadius = new CornerRadius(11),
                Background = HexBrush("#1E1815"),
                BorderBrush = HexBrush("#3A2F29"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 10, 12, 10)
            };
            StackPanel leftDetailStack = new StackPanel();

            Grid dateHdrGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            dateHdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dateHdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel dateTextStack = new StackPanel();
            StackPanel dowRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            TextBlock txtSelDow = new TextBlock { Foreground = HexBrush(accentColorHex), FontSize = 10.5, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            Button btnManualAdd = CreateStyledIconButton("#2E2520", accentColorHex, L("Bu güne manuel süre ekle", "Add manual time to this day"));
            btnManualAdd.Width = 20;
            btnManualAdd.Height = 18;
            btnManualAdd.Margin = new Thickness(6, 0, 0, 0);
            btnManualAdd.Padding = new Thickness(0);
            btnManualAdd.Content = new TextBlock { Text = "+", Foreground = HexBrush("#FAF6F2"), FontSize = 11.5, FontWeight = FontWeights.Bold, Margin = new Thickness(0, -1.5, 0, 0) };
            dowRow.Children.Add(txtSelDow);
            dowRow.Children.Add(btnManualAdd);

            StackPanel dayMonthRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, -1, 0, 0) };
            TextBlock txtSelDayNum = new TextBlock { Foreground = HexBrush("#FAF6F2"), FontSize = 23, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 7, 0) };
            TextBlock txtSelMonthYear = new TextBlock { Foreground = HexBrush("#B8A99E"), FontSize = 11.5, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 4) };
            dayMonthRow.Children.Add(txtSelDayNum);
            dayMonthRow.Children.Add(txtSelMonthYear);
            dateTextStack.Children.Add(dowRow);
            dateTextStack.Children.Add(dayMonthRow);
            Grid.SetColumn(dateTextStack, 0);
            dateHdrGrid.Children.Add(dateTextStack);

            Border detailPreviewSwatch = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(7),
                BorderBrush = HexBrush("#5E5047"),
                BorderThickness = new Thickness(1.2),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(detailPreviewSwatch, 1);
            dateHdrGrid.Children.Add(detailPreviewSwatch);
            leftDetailStack.Children.Add(dateHdrGrid);

            TextBlock txtDetStudyVal, txtDetGameVal, txtDetPcVal;
            Border detStudyBar, detGameBar, detPcBar;
            leftDetailStack.Children.Add(CreateTimerStatCard(timerStudyColorHex, L("📚 Çalışılan", "📚 Studied"), out txtDetStudyVal, out detStudyBar));
            leftDetailStack.Children.Add(CreateTimerStatCard(timerGameColorHex, L("🎮 Oyun Oynanan", "🎮 Gaming"), out txtDetGameVal, out detGameBar));
            leftDetailStack.Children.Add(CreateTimerStatCard(accentColorHex, L("🖥 Bilgisayar Açık Kalma", "🖥 PC Uptime"), out txtDetPcVal, out detPcBar, true));

            leftDetailCard.Child = leftDetailStack;
            Grid.SetColumn(leftDetailCard, 0);
            bottomGrid.Children.Add(leftDetailCard);

            Border rightGamesCard = new Border
            {
                CornerRadius = new CornerRadius(11),
                Background = HexBrush("#1E1815"),
                BorderBrush = HexBrush("#3A2F29"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 10, 12, 10)
            };
            Grid rightGamesLayout = new Grid();
            rightGamesLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rightGamesLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid gamesHdr = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            gamesHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gamesHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            gamesHdr.Children.Add(new TextBlock { Text = L("🎮 Oynanan Oyunlar", "🎮 Games Played"), Foreground = HexBrush("#FAF6F2"), FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            TextBlock txtGamesBadge = new TextBlock { Foreground = HexBrush("#9E8E82"), FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(txtGamesBadge, 1);
            gamesHdr.Children.Add(txtGamesBadge);
            Grid.SetRow(gamesHdr, 0);
            rightGamesLayout.Children.Add(gamesHdr);

            ScrollViewer gamesScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                PanningMode = PanningMode.VerticalOnly,
                Padding = new Thickness(0, 0, 4, 0)
            };
            ApplyThinDarkScrollBarStyle(gamesScroll);
            StackPanel gamesListPanel = new StackPanel();
            gamesScroll.Content = gamesListPanel;
            Grid.SetRow(gamesScroll, 1);
            rightGamesLayout.Children.Add(gamesScroll);

            rightGamesCard.Child = rightGamesLayout;
            Grid.SetColumn(rightGamesCard, 2);
            bottomGrid.Children.Add(rightGamesCard);

            Grid.SetRow(bottomGrid, 2);
            root.Children.Add(bottomGrid);

            List<Border> detailCells = new List<Border>(375);

            Action renderSelectedDay = () =>
            {
                string[] daysUpper = IsEn
                    ? new string[] { "SUNDAY", "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY" }
                    : new string[] { "PAZAR", "PAZARTESİ", "SALI", "ÇARŞAMBA", "PERŞEMBE", "CUMA", "CUMARTESİ" };
                string[] months = IsEn
                    ? new string[] { "", "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" }
                    : new string[] { "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };

                outerCard.BorderBrush = HexBrush(accentColorHex);
                txtSelDow.Text = daysUpper[(int)selectedDate.DayOfWeek];
                txtSelDow.Foreground = HexBrush(accentColorHex);
                txtSelDayNum.Text = selectedDate.Day.ToString();
                txtSelMonthYear.Text = months[selectedDate.Month] + " " + selectedDate.Year;

                detStudyBar.Background = HexBrush(timerStudyColorHex);
                detGameBar.Background = HexBrush(timerGameColorHex);
                detPcBar.Background = HexBrush(accentColorHex);

                DailyActivityRecord rec;
                activityByDate.TryGetValue(selectedDate, out rec);
                int st = rec != null ? rec.StudySeconds : 0;
                int gm = rec != null ? rec.GameSeconds : 0;
                int pc = rec != null ? rec.PcSeconds : 0;

                txtDetStudyVal.Text = FormatDurationLocalized(st);
                txtDetGameVal.Text = FormatDurationLocalized(gm);
                txtDetPcVal.Text = FormatDurationLocalized(pc);
                detailPreviewSwatch.Background = GetHeatmapCellBrush(selectedDate);
                detailPreviewSwatch.ToolTip = string.Format("{0}: {1} • {2}: {3}",
                    L("Ders", "Study"), FormatDurationLocalized(st),
                    L("Oyun", "Gaming"), FormatDurationLocalized(gm));

                for (int ci = 0; ci < detailCells.Count; ci++)
                {
                    Border c = detailCells[ci];
                    if (c.Tag is DateTime)
                    {
                        DateTime dt = (DateTime)c.Tag;
                        c.Background = GetHeatmapCellBrush(dt);
                        if (dt == selectedDate)
                        {
                            c.BorderBrush = HexBrush("#FAF6F2");
                            c.BorderThickness = new Thickness(1.6);
                        }
                        else if (dt == DateTime.Today)
                        {
                            c.BorderBrush = HexBrush("#77FAF6F2");
                            c.BorderThickness = new Thickness(1.0);
                        }
                        else
                        {
                            c.BorderBrush = Brushes.Transparent;
                            c.BorderThickness = new Thickness(0);
                        }
                    }
                }

                gamesListPanel.Children.Clear();
                List<KeyValuePair<string, int>> entries = new List<KeyValuePair<string, int>>();
                int trackedSum = 0;
                if (rec != null && rec.GameBreakdown != null)
                {
                    foreach (var kv in rec.GameBreakdown)
                    {
                        if (kv.Value > 0)
                        {
                            entries.Add(kv);
                            trackedSum += kv.Value;
                        }
                    }
                }
                if (gm - trackedSum >= 10 || (gm > 0 && entries.Count == 0))
                {
                    int rem = Math.Max(gm - trackedSum, entries.Count == 0 ? gm : 0);
                    if (rem > 0)
                    {
                        entries.Add(new KeyValuePair<string, int>(
                            entries.Count == 0 ? L("Kaydedilmiş Oyun Süresi", "Recorded Gaming Time") : L("Diğer / Önceki Kayıtlar", "Other / Prior Sessions"),
                            rem));
                    }
                }

                if (entries.Count == 0)
                {
                    txtGamesBadge.Text = L("Kayıt yok", "No records");
                    Border emptyBox = new Border
                    {
                        CornerRadius = new CornerRadius(9),
                        Background = HexBrush("#161210"),
                        BorderBrush = HexBrush("#2C2420"),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(12, 18, 12, 18),
                        Margin = new Thickness(0, 6, 0, 0)
                    };
                    emptyBox.Child = new TextBlock
                    {
                        Text = L("Bu tarihte kaydedilmiş bir oyun bulunmuyor.", "No games recorded on this date."),
                        Foreground = HexBrush("#8E7F74"),
                        FontSize = 11.5,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    };
                    gamesListPanel.Children.Add(emptyBox);
                }
                else
                {
                    entries.Sort((a, b) => b.Value.CompareTo(a.Value));
                    int maxSec = Math.Max(1, entries[0].Value);
                    int denomTotal = Math.Max(1, Math.Max(gm, trackedSum));
                    txtGamesBadge.Text = entries.Count + L(" oyun", " games");

                    for (int gi = 0; gi < entries.Count; gi++)
                    {
                        var gItem = entries[gi];
                        int pct = (int)Math.Round((gItem.Value * 100.0) / denomTotal);
                        Border rowCard = new Border
                        {
                            CornerRadius = new CornerRadius(8),
                            Background = HexBrush("#26201C"),
                            BorderBrush = HexBrush("#362C26"),
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(10, 7, 10, 7),
                            Margin = new Thickness(0, 0, 0, 6)
                        };
                        StackPanel rs = new StackPanel();
                        Grid topLine = new Grid();
                        topLine.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        topLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        topLine.Children.Add(new TextBlock
                        {
                            Text = "🎮 " + gItem.Key,
                            Foreground = HexBrush("#FAF6F2"),
                            FontSize = 11.5,
                            FontWeight = FontWeights.SemiBold,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        });
                        TextBlock durTb = new TextBlock
                        {
                            Text = string.Format("{0}  ({1}%)", FormatDurationLocalized(gItem.Value), pct),
                            Foreground = HexBrush("#C7B9AE"),
                            FontSize = 11,
                            FontWeight = FontWeights.Bold,
                            Margin = new Thickness(8, 0, 0, 0)
                        };
                        Grid.SetColumn(durTb, 1);
                        topLine.Children.Add(durTb);
                        rs.Children.Add(topLine);

                        Border track = new Border
                        {
                            Height = 4,
                            CornerRadius = new CornerRadius(2),
                            Background = HexBrush("#181412"),
                            Margin = new Thickness(0, 5, 0, 0),
                            HorizontalAlignment = HorizontalAlignment.Stretch
                        };
                        Border fill = new Border
                        {
                            Height = 4,
                            CornerRadius = new CornerRadius(2),
                            Background = HexBrush(timerGameColorHex),
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Width = Math.Max(6.0, 290.0 * (gItem.Value / (double)maxSec))
                        };
                        track.Child = fill;
                        rs.Children.Add(track);
                        rowCard.Child = rs;
                        gamesListPanel.Children.Add(rowCard);
                    }
                }
            };

            string[] shortMonths = IsEn
                ? new string[] { "", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" }
                : new string[] { "", "Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara" };
            DateTime today = DateTime.Today;
            int todayDow = ((int)today.DayOfWeek + 6) % 7;
            DateTime curMonday = today.AddDays(-todayDow);
            const int weeks = 52;
            double cellSz = 10.0;
            double pitchX = 616.0 / (weeks - 1);
            DateTime startMonday = curMonday.AddDays(-(weeks - 1) * 7);
            int lastMonthLbl = -1;
            double lastLblX = -100;

            for (int w = 0; w < weeks; w++)
            {
                DateTime colMonday = startMonday.AddDays(w * 7);
                double x = Math.Round(w * pitchX);
                int mLblIdx = -1;
                for (int d = 0; d < 7; d++)
                {
                    DateTime dt = colMonday.AddDays(d);
                    if (dt > today) break;
                    if (dt.Day <= 7 && dt.Month != lastMonthLbl)
                    {
                        mLblIdx = dt.Month;
                        break;
                    }
                }
                if (w == 0 && mLblIdx < 0) mLblIdx = colMonday.Month;
                if (mLblIdx > 0 && (x - lastLblX) >= 32 && x <= 600)
                {
                    lastMonthLbl = mLblIdx;
                    lastLblX = x;
                    TextBlock mLbl = new TextBlock { Text = shortMonths[mLblIdx], Foreground = HexBrush("#9E8E82"), FontSize = 9.5, FontWeight = FontWeights.SemiBold };
                    Canvas.SetLeft(mLbl, x);
                    Canvas.SetTop(mLbl, 0);
                    detailCanvas.Children.Add(mLbl);
                }

                for (int r = 0; r < 7; r++)
                {
                    DateTime cDate = colMonday.AddDays(r).Date;
                    if (cDate > today) continue;
                    Border c = new Border
                    {
                        Width = cellSz,
                        Height = cellSz,
                        CornerRadius = new CornerRadius(2.2),
                        Cursor = Cursors.Hand,
                        Tag = cDate,
                        ToolTip = ""
                    };
                    c.ToolTipOpening += OnHeatmapCellToolTipOpening;
                    c.MouseLeftButtonDown += (s, e) =>
                    {
                        selectedDate = cDate;
                        renderSelectedDay();
                        e.Handled = true;
                    };
                    detailCells.Add(c);
                    Canvas.SetLeft(c, x);
                    Canvas.SetTop(c, 16.0 + r * 12.2);
                    detailCanvas.Children.Add(c);
                }
            }

            refreshTimerDetailedViewAction = renderSelectedDay;
            renderSelectedDay();

            // Manual Time Entry Modal Overlay
            int manualAddCategory = 0; // 0 = Study, 1 = Gaming, 2 = PC Uptime (Default)
            Border manualAddOverlay = new Border
            {
                Visibility = Visibility.Collapsed,
                Background = HexBrush("#B80E0B09"),
                CornerRadius = new CornerRadius(11)
            };
            Panel.SetZIndex(manualAddOverlay, 25);

            Border manualModalCard = new Border
            {
                Width = 370,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(13),
                Background = HexBrush("#FA201916"),
                BorderBrush = HexBrush(accentColorHex),
                BorderThickness = new Thickness(1.4),
                Padding = new Thickness(16, 14, 16, 14)
            };
            manualModalCard.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 20, Opacity = 0.8 };

            StackPanel mStack = new StackPanel();
            Grid mHdr = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            mHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel mTitles = new StackPanel();
            mTitles.Children.Add(new TextBlock { Text = L("➕ Manuel Süre Ekle", "➕ Add Manual Time"), Foreground = HexBrush("#FAF6F2"), FontSize = 13.5, FontWeight = FontWeights.Bold });
            TextBlock txtManualDateSub = new TextBlock { Text = "", Foreground = HexBrush("#B8A99E"), FontSize = 10.5, Margin = new Thickness(0, 1, 0, 0) };
            mTitles.Children.Add(txtManualDateSub);
            Grid.SetColumn(mTitles, 0);
            mHdr.Children.Add(mTitles);

            Button btnCloseManual = CreateStyledIconButton("#2E2520", "#4A3C34", L("Kapat", "Close"));
            btnCloseManual.Padding = new Thickness(7, 2, 7, 2);
            btnCloseManual.VerticalAlignment = VerticalAlignment.Top;
            btnCloseManual.Content = new TextBlock { Text = "✕", Foreground = HexBrush("#C7B9AE"), FontSize = 11 };
            btnCloseManual.Click += (s, e) => manualAddOverlay.Visibility = Visibility.Collapsed;
            Grid.SetColumn(btnCloseManual, 1);
            mHdr.Children.Add(btnCloseManual);
            mStack.Children.Add(mHdr);

            UniformGrid catSwitch = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 10) };
            Button btnCatStudy = CreateStretchSettingsButton("#2A221E", timerStudyColorHex, L("Ders kategorisine ekle", "Add to Study"));
            btnCatStudy.Margin = new Thickness(0, 0, 3, 0);
            btnCatStudy.Padding = new Thickness(6, 6, 6, 6);
            TextBlock lblCatStudy = new TextBlock { Text = L("📚 Ders", "📚 Study"), Foreground = HexBrush("#FAF6F2"), FontSize = 11, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
            btnCatStudy.Content = lblCatStudy;

            Button btnCatGame = CreateStretchSettingsButton("#1D1815", "#3A2F29", L("Oyun kategorisine ekle", "Add to Gaming"));
            btnCatGame.Margin = new Thickness(3, 0, 3, 0);
            btnCatGame.Padding = new Thickness(6, 6, 6, 6);
            TextBlock lblCatGame = new TextBlock { Text = L("🎮 Oyun", "🎮 Gaming"), Foreground = HexBrush("#9E8E82"), FontSize = 11, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
            btnCatGame.Content = lblCatGame;

            Button btnCatPc = CreateStretchSettingsButton("#1D1815", "#3A2F29", L("Default PC açık kalma süresine ekle", "Add to PC Uptime (Default)"));
            btnCatPc.Margin = new Thickness(3, 0, 0, 0);
            btnCatPc.Padding = new Thickness(6, 6, 6, 6);
            TextBlock lblCatPc = new TextBlock { Text = L("🖥 PC Açık", "🖥 PC Uptime"), Foreground = HexBrush("#9E8E82"), FontSize = 11, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
            btnCatPc.Content = lblCatPc;

            catSwitch.Children.Add(btnCatStudy);
            catSwitch.Children.Add(btnCatGame);
            catSwitch.Children.Add(btnCatPc);
            mStack.Children.Add(catSwitch);

            StackPanel gameNamePanel = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 10) };
            gameNamePanel.Children.Add(new TextBlock { Text = L("Oyun Adı (isteğe bağlı)", "Game Name (optional)"), Foreground = HexBrush("#B8A99E"), FontSize = 10.5, Margin = new Thickness(0, 0, 0, 4) });
            TextBox txtManualGameName = new TextBox
            {
                Text = "",
                Background = HexBrush("#161210"),
                Foreground = HexBrush("#FAF6F2"),
                BorderBrush = HexBrush("#463932"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 5, 8, 5),
                FontSize = 11.5,
                ToolTip = L("Örn: VALORANT, Fortnite, Xero (boş bırakılırsa Manuel Oyun sayılır)", "e.g. VALORANT, Fortnite, Xero (defaults to Manual Gaming if empty)")
            };
            gameNamePanel.Children.Add(txtManualGameName);
            mStack.Children.Add(gameNamePanel);

            Action syncCatUi = () =>
            {
                btnCatStudy.Background = HexBrush(manualAddCategory == 0 ? "#2A221E" : "#1D1815");
                btnCatStudy.BorderBrush = HexBrush(manualAddCategory == 0 ? timerStudyColorHex : "#3A2F29");
                lblCatStudy.Foreground = HexBrush(manualAddCategory == 0 ? "#FAF6F2" : "#9E8E82");

                btnCatGame.Background = HexBrush(manualAddCategory == 1 ? "#2A221E" : "#1D1815");
                btnCatGame.BorderBrush = HexBrush(manualAddCategory == 1 ? timerGameColorHex : "#3A2F29");
                lblCatGame.Foreground = HexBrush(manualAddCategory == 1 ? "#FAF6F2" : "#9E8E82");

                btnCatPc.Background = HexBrush(manualAddCategory == 2 ? "#2A221E" : "#1D1815");
                btnCatPc.BorderBrush = HexBrush(manualAddCategory == 2 ? accentColorHex : "#3A2F29");
                lblCatPc.Foreground = HexBrush(manualAddCategory == 2 ? "#FAF6F2" : "#9E8E82");

                gameNamePanel.Visibility = manualAddCategory == 1 ? Visibility.Visible : Visibility.Collapsed;
            };
            btnCatStudy.Click += (s, e) => { manualAddCategory = 0; syncCatUi(); };
            btnCatGame.Click += (s, e) => { manualAddCategory = 1; syncCatUi(); };
            btnCatPc.Click += (s, e) => { manualAddCategory = 2; syncCatUi(); };

            Grid hmInputs = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            hmInputs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hmInputs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            hmInputs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel hCol = new StackPanel();
            hCol.Children.Add(new TextBlock { Text = L("Saat", "Hours"), Foreground = HexBrush("#B8A99E"), FontSize = 10.5, Margin = new Thickness(0, 0, 0, 4) });
            TextBox txtManualHours = new TextBox
            {
                Text = "0",
                Background = HexBrush("#161210"),
                Foreground = HexBrush("#FAF6F2"),
                BorderBrush = HexBrush("#463932"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 5, 8, 5),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            };
            hCol.Children.Add(txtManualHours);
            Grid.SetColumn(hCol, 0);
            hmInputs.Children.Add(hCol);

            StackPanel mCol = new StackPanel();
            mCol.Children.Add(new TextBlock { Text = L("Dakika", "Minutes"), Foreground = HexBrush("#B8A99E"), FontSize = 10.5, Margin = new Thickness(0, 0, 0, 4) });
            TextBox txtManualMins = new TextBox
            {
                Text = "30",
                Background = HexBrush("#161210"),
                Foreground = HexBrush("#FAF6F2"),
                BorderBrush = HexBrush("#463932"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 5, 8, 5),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            };
            mCol.Children.Add(txtManualMins);
            Grid.SetColumn(mCol, 2);
            hmInputs.Children.Add(mCol);
            mStack.Children.Add(hmInputs);

            UniformGrid quickChips = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 12) };
            int[,] chipVals = { { 0, 15 }, { 0, 30 }, { 1, 0 }, { 2, 0 } };
            string[] chipLbls = IsEn
                ? new string[] { "+15m", "+30m", "+1h", "+2h" }
                : new string[] { "+15 dk", "+30 dk", "+1s", "+2s" };
            for (int qi = 0; qi < 4; qi++)
            {
                int ch = chipVals[qi, 0];
                int cm = chipVals[qi, 1];
                Button qBtn = CreateStretchSettingsButton("#26201D", "#3E322B", chipLbls[qi]);
                qBtn.Padding = new Thickness(4, 4, 4, 4);
                qBtn.Margin = new Thickness(qi == 0 ? 0 : 2, 0, qi == 3 ? 0 : 2, 0);
                qBtn.Content = new TextBlock { Text = chipLbls[qi], Foreground = HexBrush("#D7CBC2"), FontSize = 10, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
                qBtn.Click += (s, e) =>
                {
                    txtManualHours.Text = ch.ToString();
                    txtManualMins.Text = cm.ToString();
                };
                quickChips.Children.Add(qBtn);
            }
            mStack.Children.Add(quickChips);

            Grid footerBtns = new Grid();
            footerBtns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerBtns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            footerBtns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });

            Button btnCancelManual = CreateStretchSettingsButton("#26201D", "#463932", L("Vazgeç", "Cancel"));
            btnCancelManual.Padding = new Thickness(8, 7, 8, 7);
            btnCancelManual.Content = new TextBlock { Text = L("İptal", "Cancel"), Foreground = HexBrush("#C7B9AE"), FontSize = 11.5, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
            btnCancelManual.Click += (s, e) => manualAddOverlay.Visibility = Visibility.Collapsed;
            Grid.SetColumn(btnCancelManual, 0);
            footerBtns.Children.Add(btnCancelManual);

            Button btnConfirmManual = CreateStretchSettingsButton("#38281E", accentColorHex, L("Seçili güne süreyi ekle", "Add time to selected day"));
            btnConfirmManual.Padding = new Thickness(8, 7, 8, 7);
            btnConfirmManual.Content = new TextBlock { Text = L("✓ Ekle", "✓ Add"), Foreground = HexBrush("#FAF6F2"), FontSize = 11.5, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
            btnConfirmManual.Click += (s, e) =>
            {
                int hrs = 0, mins = 0;
                if (!int.TryParse(txtManualHours.Text.Trim(), out hrs) || !int.TryParse(txtManualMins.Text.Trim(), out mins) || hrs < 0 || mins < 0)
                {
                    ShowToast(L("Geçerli bir saat/dakika değeri girin", "Enter a valid hours/minutes value"));
                    return;
                }
                int addSec = (hrs * 3600) + (mins * 60);
                if (addSec <= 0 || addSec > 86400)
                {
                    ShowToast(L("Eklenecek süre 1 dk ile 24 saat arasında olmalıdır", "Duration must be between 1 min and 24 hours"));
                    return;
                }
                DailyActivityRecord rec = GetOrCreateActivityRecord(selectedDate);
                if (manualAddCategory == 1)
                {
                    rec.GameSeconds += addSec;
                    string rawName = txtManualGameName.Text.Trim();
                    string gKey = SanitizeGameKey(string.IsNullOrEmpty(rawName) ? L("Manuel Oyun", "Manual Gaming") : rawName);
                    int prevSec;
                    rec.GameBreakdown.TryGetValue(gKey, out prevSec);
                    rec.GameBreakdown[gKey] = prevSec + addSec;
                }
                else if (manualAddCategory == 2)
                {
                    rec.PcSeconds = Math.Min(86400, rec.PcSeconds + addSec);
                }
                else
                {
                    rec.StudySeconds += addSec;
                }
                SaveTimerActivityData();
                renderSelectedDay();
                BuildTimerHeatmapGrid();
                UpdateTimerWidgetUi();
                manualAddOverlay.Visibility = Visibility.Collapsed;
                string catLabel = manualAddCategory == 1 ? L("Oyun", "Gaming") : (manualAddCategory == 2 ? L("PC Açık", "PC Uptime") : L("Ders", "Study"));
                ShowToast(string.Format("✓ {0} ({1})", FormatDurationLocalized(addSec), catLabel));
            };
            Grid.SetColumn(btnConfirmManual, 2);
            footerBtns.Children.Add(btnConfirmManual);
            mStack.Children.Add(footerBtns);

            manualModalCard.Child = mStack;
            manualAddOverlay.Child = manualModalCard;
            manualAddOverlay.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource == manualAddOverlay)
                {
                    manualAddOverlay.Visibility = Visibility.Collapsed;
                    e.Handled = true;
                }
            };

            btnManualAdd.Click += (s, e) =>
            {
                string[] monthsFull = IsEn
                    ? new string[] { "", "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" }
                    : new string[] { "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };
                string[] daysFull = IsEn
                    ? new string[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" }
                    : new string[] { "Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi" };
                txtManualDateSub.Text = string.Format("{0} {1} {2} {3}", selectedDate.Day, monthsFull[selectedDate.Month], selectedDate.Year, daysFull[(int)selectedDate.DayOfWeek]);
                btnManualAdd.BorderBrush = HexBrush(accentColorHex);
                manualModalCard.BorderBrush = HexBrush(accentColorHex);
                btnConfirmManual.BorderBrush = HexBrush(accentColorHex);
                syncCatUi();
                manualAddOverlay.Visibility = Visibility.Visible;
                txtManualMins.Focus();
                txtManualMins.SelectAll();
            };

            Grid cardHost = new Grid();
            cardHost.Children.Add(root);
            cardHost.Children.Add(manualAddOverlay);
            outerCard.Child = cardHost;
            timerDetailedWindow.Content = outerCard;
            timerDetailedWindow.Closed += (s, e) =>
            {
                refreshTimerDetailedViewAction = null;
                timerDetailedWindow = null;
            };
            timerDetailedWindow.Show();
        }
    }
}
