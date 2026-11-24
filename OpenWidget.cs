using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
        public static extern int nvmlDeviceGetTemperature(IntPtr device, int sensorType, out uint temp);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nvmlDeviceGetUtilizationRates(IntPtr device, out nvmlUtilization_t util);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nvmlDeviceGetMemoryInfo(IntPtr device, out nvmlMemory_t mem);
    }

    // Zero-Bloat Direct HID & XInput Wireless Battery Reader (Scyrox V8, HyperX Cloud III Wireless, Gamepad)
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

        [DllImport("hid.dll")]
        public static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, IntPtr Enumerator, IntPtr hwndParent, int Flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInterfaces(IntPtr DeviceInfoSet, IntPtr DeviceInfoData, ref Guid InterfaceClassGuid, int MemberIndex, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr DeviceInfoSet, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData, IntPtr DeviceInterfaceDetailData, int DeviceInterfaceDetailDataSize, out int RequiredSize, IntPtr DeviceInfoData);

        [DllImport("setupapi.dll")]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

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

        public static void FindHidDevicePaths(out string scyroxPath, out string hyperxPath)
        {
            scyroxPath = null;
            hyperxPath = null;
            try
            {
                Guid hidGuid;
                HidD_GetHidGuid(out hidGuid);
                IntPtr hDevInfo = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, 0x12);
                if (hDevInfo == IntPtr.Zero || hDevInfo == new IntPtr(-1)) return;
                try
                {
                    int index = 0;
                    SP_DEVICE_INTERFACE_DATA devData = new SP_DEVICE_INTERFACE_DATA();
                    devData.cbSize = Marshal.SizeOf(devData);
                    while (SetupDiEnumDeviceInterfaces(hDevInfo, IntPtr.Zero, ref hidGuid, index, ref devData))
                    {
                        int reqSize;
                        SetupDiGetDeviceInterfaceDetail(hDevInfo, ref devData, IntPtr.Zero, 0, out reqSize, IntPtr.Zero);
                        if (reqSize > 0)
                        {
                            IntPtr detailBuf = Marshal.AllocHGlobal(reqSize);
                            try
                            {
                                Marshal.WriteInt32(detailBuf, IntPtr.Size == 8 ? 8 : 6);
                                if (SetupDiGetDeviceInterfaceDetail(hDevInfo, ref devData, detailBuf, reqSize, out reqSize, IntPtr.Zero))
                                {
                                    string p = Marshal.PtrToStringAuto(new IntPtr(detailBuf.ToInt64() + 4));
                                    if (p != null)
                                    {
                                        if (p.IndexOf("vid_3554&pid_f5", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                            p.IndexOf("&col05", StringComparison.OrdinalIgnoreCase) >= 0)
                                        {
                                            scyroxPath = p;
                                        }
                                        else if (p.IndexOf("vid_03f0&pid_05b7&mi_03&col01", StringComparison.OrdinalIgnoreCase) >= 0)
                                        {
                                            hyperxPath = p;
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(detailBuf);
                            }
                        }
                        index++;
                    }
                }
                finally
                {
                    SetupDiDestroyDeviceInfoList(hDevInfo);
                }
            }
            catch { }
        }

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
    }

    public class ClipboardEntry
    {
        public string Text { get; set; }
        public string Time { get; set; }
    }

    public class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(int value);

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

            // Force Per-Monitor High-DPI Crisp Rendering on 2K / 4K monitors
            try { SetProcessDpiAwareness(2); }
            catch { try { SetProcessDPIAware(); } catch { } }

            bool createdNew;
            using (Mutex mutex = new Mutex(true, "OpenWidget_Desktop_Singleton_Mutex", out createdNew))
            {
                if (!createdNew)
                {
                    return;
                }
                Application app = new Application();
                MainWindow win = new MainWindow();
                app.Run(win);
            }
        }
    }

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

        public class WmiBrightnessInfo
        {
            public string InstanceName;
            public int CurrentBrightness;
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
    }

    internal class PhysicalMonitorSlot
    {
        public IntPtr Handle = IntPtr.Zero;
        public MonitorDdcNative.LUID AdapterId;
        public uint TargetId = 0;
        public bool HdrSdrMode = false;
        public bool UseWmiBrightness = false;
        public string WmiInstanceName = "";
        public string GdiDeviceName = "";
        public bool PreferHighLevelDdc = false;
        public uint MinVal = 0;
        public uint MaxVal = 100;
        public string RawModel = "";
        public string FriendlyName = "Monitör";
        public bool IsPrimary = false;
        public int CurrentBrightness = 60;
        public int PendingBrightness = -1;
        public int WorkerBusy = 0;
    }

    public class MainWindow : Window
    {
        // Win32 Interop
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

        // Theme & Settings State
        private string settingsFilePath;
        private string accentColorHex = "#E2925C";
        private int bgOpacityPercent = 88;
        private bool isDraggable = false;
        private double savedFreeWidth = 375;
        private double savedFreeHeight = 760;
        private double savedLeft = double.NaN;
        private double savedTop = double.NaN;
        private bool isWindowInitialized = false;
        private bool hasSavedDimensions = false;
        private bool isCalWindowInitialized = false;
        private bool isTimerWindowInitialized = false;

        // WiZ Smart Bulb State (Reusable Single UdpClient)
        private string bulbIp = "192.168.0.132";
        private bool bulbOn = true;
        private int bulbDimming = 50;
        private int bulbTemp = 2700;
        private bool bulbIsCustomColor = false;
        private string bulbCustomColorHex = "#FF7A2E";
        private bool isUpdatingBulbUi = false;
        private DateTime lastBulbCommandSent = DateTime.MinValue;
        private UdpClient sharedUdpClient;
        private readonly object udpLock = new object();

        // Audio / Tray State (Cached Single COM Endpoint to prevent Handle Leaks)
        private bool isMicMuted = false;
        private IAudioEndpointVolume cachedMicVolumeConsole;
        private IAudioEndpointVolume cachedMicVolumeComm;
        private System.Windows.Forms.NotifyIcon trayIcon;
        private System.Drawing.Icon iconMicUnmuted;
        private System.Drawing.Icon iconMicMuted;
        private System.Windows.Forms.ToolStripMenuItem trayMuteItem;
        private System.Windows.Forms.ToolStripMenuItem trayLockItem;
        private Window settingsWindow;

        // Telemetry Counters & Direct NVML Handle
        private PerformanceCounter cpuCounter;
        private bool nvmlInitialized = false;
        private IntPtr nvmlDeviceHandle = IntPtr.Zero;
        private Ping sharedPing;
        private MEMORYSTATUSEX sharedMemStatus = new MEMORYSTATUSEX();
        private DriveInfo driveC;
        private DriveInfo driveD;

        private List<double> cpuHistory = new List<double>(20);
        private List<double> gpuHistory = new List<double>(20);
        private NetworkInterface[] cachedActiveNics = null;
        private int telemetryTickCounter = 0;
        private long lastBytesRecv = 0;
        private long lastBytesSent = 0;
        private DateTime lastNetTime = DateTime.Now;
        private double lastPingMs = 9;
        private double cpuTemp = 48;
        private string gpuModelName = "RTX 5070";
        private double gpuTemp = 49;
        private double gpuLoad = 0;
        private double vramUsedGb = 2.1;
        private double vramTotalGb = 12.0;

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

        // Weather UI
        private TextBlock txtLocationBadge;
        private TextBlock txtWeatherTemp;
        private TextBlock txtWeatherDesc;
        private TextBlock txtWeatherFeels;
        private TextBlock txtWeatherMainIcon;
        private TextBlock txtWeatherHumidity;
        private TextBlock txtWeatherWind;
        private TextBlock txtWeatherRain;
        private TextBlock[] txtDayNames = new TextBlock[3];
        private TextBlock[] txtDayIcons = new TextBlock[3];
        private TextBlock[] txtDayTemps = new TextBlock[3];

        // Smart Bulb UI
        private Border bulbStatusDot;
        private TextBlock txtBulbStatus;
        private Button btnBulbPower;
        private TextBlock txtBulbPercent;
        private Slider sliderBulbDim;
        private Border sliderTrackContainer;
        private Button[] btnScenes = new Button[4];
        private Border bulbCustomColorDot;
        private StackPanel panelBulbCustomSpectrum;
        private TextBlock txtBulbCustomHexLabel;
        private Border bulbSpectrumPreviewSwatch;
        private Action<string> syncBulbSpectrumPosition;

        // Monitor DDC/CI Brightness UI & State (Replaces Twinkle Tray with zero RAM overhead)
        private readonly List<PhysicalMonitorSlot> monitorSlots = new List<PhysicalMonitorSlot>();
        private readonly object monitorHandleLock = new object();
        private readonly object monitorRefreshLock = new object();
        private CancellationTokenSource monitorRefreshCts;
        private EventHandler displaySettingsChangedHandler;
        private PowerModeChangedEventHandler powerModeChangedHandler;
        private bool monitorLinkSync = false;
        private bool isUpdatingMonitorUi = false;
        private Border monitorCard;
        private Button btnMonitorLink;
        private TextBlock[] txtMonitorNames = new TextBlock[2];
        private TextBlock[] txtMonitorPercents = new TextBlock[2];
        private Slider[] sliderMonitors = new Slider[2];
        private Border[] monitorSliderContainers = new Border[2];
        private Grid[] rowMonitors = new Grid[2];

        // Wireless Peripherals Battery UI & State (Scyrox V8, HyperX Cloud III, Wireless Controller)
        private Grid wirelessHeaderGrid;
        private StackPanel panelWirelessContent;
        private Grid rowMouse;
        private TextBlock txtMouseDetail;
        private TextBlock txtMousePct;
        private Border barMouseFill;
        private Grid rowHeadset;
        private TextBlock txtHeadsetDetail;
        private TextBlock txtHeadsetPct;
        private Border barHeadsetFill;
        private Grid rowController;
        private TextBlock txtControllerDetail;
        private TextBlock txtControllerPct;
        private Border barControllerFill;
        private string cachedScyroxPath = null;
        private string cachedHyperXPath = null;
        private int lastScyroxPct = -1;
        private string lastScyroxHzText = "1000 Hz";
        private int wirelessPollingBusy = 0;

        // Telemetry UI
        private StackPanel panelTelemetryContent;
        private Grid rowTeleCpu;
        private Grid rowTeleRam;
        private Grid rowTeleGpu;
        private Grid rowTeleVram;
        private Grid rowTeleNet;
        private Grid rowTeleDiskC;
        private Grid rowTeleDiskD;
        private TextBlock txtCpuDetail;
        private TextBlock txtCpuPct;
        private Polyline sparkCpu;
        private TextBlock txtRamDetail;
        private TextBlock txtRamPct;
        private Border barRamFill;
        private TextBlock txtGpuDetail;
        private TextBlock txtGpuPct;
        private Polyline sparkGpu;
        private TextBlock txtVramDetail;
        private TextBlock txtVramPct;
        private Border barVramFill;
        private TextBlock txtNetSpeed;
        private TextBlock txtNetBadge;
        private TextBlock txtNetPing;
        private TextBlock txtDiskCDetail;
        private TextBlock txtDiskCPct;
        private Border barDiskCFill;
        private TextBlock txtDiskDDetail;
        private TextBlock txtDiskDPct;
        private Border barDiskDFill;

        // Layout Order & Visibility State
        private List<string> sectionOrder = new List<string> { "weather", "bulb", "monitors", "wireless", "telemetry", "clipboard" };
        private bool showSecWeather = true;
        private bool showSecBulb = true;
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
        private bool anyWirelessDeviceActive = true;
        private int currentSettingsTab = 0;

        // Standalone Google Calendar Widget (Apple Calendar iOS Style Desktop Companion)
        private bool showCalendarWidget = true;
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

        // Standalone Timer Widget (Modes, Pomodoro, Daily Study/Game/PC Tracker & GitHub Activity Heatmap)
        private bool showTimerWidget = true;
        private double savedTimerWidth = 540;
        private double savedTimerHeight = 310;
        private double savedTimerLeft = double.NaN;
        private double savedTimerTop = double.NaN;
        private int timerCurrentMode = 0; // 0 = Default, 1 = Eğlence (Oyun) Modu, 2 = Ders (Pomodoro) Modu
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
        private bool syncActivityToSite = true;
        private int timerCloudSyncTickCounter = 885;
        private int cloudActivitySyncBusy = 0;
        private string lastCloudSyncedSignature = null;
        private string cachedGithubSyncToken = null;
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

        // Taskbar Wireless Devices Battery Overlay
        private bool showTaskbarDevices = true;
        private Window taskbarDevicesWindow;
        private IntPtr cachedTaskbarOverlayHwnd = IntPtr.Zero;
        private StackPanel taskbarDevicesPanel;
        private System.Windows.Forms.ToolStripMenuItem trayTaskbarDevicesItem;
        private string lastTaskbarDevicesSignature = "";
        private int lastTaskbarOverlayX = int.MinValue;
        private int lastTaskbarOverlayY = int.MinValue;
        private int lastTaskbarOverlayW = int.MinValue;
        private bool taskbarOverlayHiddenByFullscreen = false;
        private bool notifyLowBattery = true;
        private readonly HashSet<string> lowBatteryNotifiedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private int lastMousePctState = -1;
        private int lastHeadsetPctState = -1;
        private int lastPadPctState = -1;
        private bool lastMouseChargingState = false;
        private bool lastHeadsetChargingState = false;
        private bool lastPadChargingState = false;
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
            public string Source; // "primary", "holiday", "custom", "ical"
            public string ColorHex;
            public string Title;
            public string Location;
            public string Description;
            public string HtmlLink;
            public string RawCustomLine;
        }

        // Section Containers for Dynamic Reordering & Visibility
        private StackPanel modulesHostPanel;
        private StackPanel secContainerWeather;
        private StackPanel secContainerBulb;
        private StackPanel secContainerMonitors;
        private StackPanel secContainerWireless;
        private StackPanel secContainerTelemetry;
        private StackPanel secContainerClipboard;

        // Clipboard UI
        private TextBlock txtClipCount;
        private StackPanel panelClipItems;
        private Popup toastPopup;
        private Border toastBorder;
        private TextBlock txtToastMessage;
        private DispatcherTimer toastTimer;

        public MainWindow()
        {
            settingsFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.cfg");
            LoadSettings();

            for (int i = 0; i < 20; i++)
            {
                cpuHistory.Add(8);
                gpuHistory.Add(2);
            }

            try
            {
                sharedUdpClient = new UdpClient();
                sharedUdpClient.EnableBroadcast = true;
                sharedUdpClient.Client.SendTimeout = 700;
                sharedUdpClient.Client.ReceiveTimeout = 700;
            }
            catch { }

            try { sharedPing = new Ping(); } catch { }
            try { driveC = new DriveInfo("C:\\"); } catch { }
            try { driveD = new DriveInfo("D:\\"); } catch { }

            InitializeWindowProps();
            BuildUserInterface();
            ApplyTheme(accentColorHex, bgOpacityPercent);
            InitializeTrayAndAudio();
            InitializeTimers();
        }

        private static bool IsValidTextFile(string path)
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

        private static void WriteAllTextAtomic(string path, string content, Encoding encoding)
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

        private static string[] ReadAllLinesCrashSafe(string path, Encoding encoding)
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
            EnsureAutoStart(true);
            try
            {
                string[] settingsLines = ReadAllLinesCrashSafe(settingsFilePath, Encoding.UTF8);
                if (settingsLines != null)
                {
                    foreach (string line in settingsLines)
                    {
                        string[] kv = line.Split('=');
                        if (kv.Length == 2)
                        {
                            string k = kv[0].Trim();
                            string v = kv[1].Trim();
                            if (k == "Accent" && v.StartsWith("#") && (v.Length == 7 || v.Length == 9))
                                accentColorHex = v;
                            else if (k == "BulbColor" && v.StartsWith("#") && v.Length == 7)
                                bulbCustomColorHex = v;
                            else if (k == "MonitorLink")
                                monitorLinkSync = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                            else if (k == "Opacity")
                            {
                                int op;
                                if (int.TryParse(v, out op)) bgOpacityPercent = Math.Max(30, Math.Min(100, op));
                            }
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
                                    savedFreeHeight = Math.Max(420, Math.Min(1400, fh));
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
                            else if (k == "SectionOrder" && !string.IsNullOrEmpty(v))
                            {
                                string[] parts = v.Split(',');
                                List<string> valid = new List<string>();
                                string[] allKeys = { "weather", "bulb", "monitors", "wireless", "telemetry", "clipboard" };
                                foreach (string p in parts)
                                {
                                    string clean = p.Trim().ToLowerInvariant();
                                    if (Array.IndexOf(allKeys, clean) >= 0 && !valid.Contains(clean)) valid.Add(clean);
                                }
                                foreach (string ak in allKeys)
                                {
                                    if (!valid.Contains(ak)) valid.Add(ak);
                                }
                                sectionOrder = valid;
                            }
                            else if (k == "ShowWeather") showSecWeather = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                            else if (k == "ShowBulb") showSecBulb = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                            else if (k == "ShowMonitors") showSecMonitors = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                            else if (k == "ShowWireless") showSecWireless = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                            else if (k == "ShowTelemetry") showSecTelemetry = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                            else if (k == "ShowClipboard") showSecClipboard = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                            else if (k == "ShowTeleCpu") showTeleCpu = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                            else if (k == "ShowTeleRam") showTeleRam = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                            else if (k == "ShowTeleGpu") showTeleGpu = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                            else if (k == "ShowTeleVram") showTeleVram = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                            else if (k == "ShowTeleNet") showTeleNet = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                            else if (k == "ShowTeleDisks") showTeleDisks = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                            else if (k == "ShowCalendarWidget") showCalendarWidget = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                            else if (k == "CalWidth")
                            {
                                double cw;
                                if (double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out cw))
                                    savedCalWidth = Math.Max(380, Math.Min(900, cw));
                            }
                            else if (k == "CalHeight")
                            {
                                double ch;
                                if (double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out ch))
                                    savedCalHeight = Math.Max(210, Math.Min(800, ch));
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
                            else if (k == "SyncActivityToSite")
                            {
                                syncActivityToSite = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
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
        }

        private void SaveSettings()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Accent=" + accentColorHex);
                sb.AppendLine("BulbColor=" + bulbCustomColorHex);
                sb.AppendLine("MonitorLink=" + (monitorLinkSync ? "1" : "0"));
                sb.AppendLine("Opacity=" + bgOpacityPercent);
                sb.AppendLine("FreeWidth=" + savedFreeWidth.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("FreeHeight=" + savedFreeHeight.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                if (!double.IsNaN(savedLeft))
                    sb.AppendLine("Left=" + savedLeft.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                if (!double.IsNaN(savedTop))
                    sb.AppendLine("Top=" + savedTop.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("SectionOrder=" + string.Join(",", sectionOrder.ToArray()));
                sb.AppendLine("ShowWeather=" + (showSecWeather ? "1" : "0"));
                sb.AppendLine("ShowBulb=" + (showSecBulb ? "1" : "0"));
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
                sb.AppendLine("SyncActivityToSite=" + (syncActivityToSite ? "1" : "0"));
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
                    if (key != null) return key.GetValue("OpenWidget") != null;
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
                        if (enable) key.SetValue("OpenWidget", "\"" + exePath + "\"");
                        else key.DeleteValue("OpenWidget", false);
                    }
                }
                if (enable)
                {
                    // Eliminate Windows 10/11 Explorer 10-second shell startup throttle for Run apps
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

        private static ImageSource cachedWindowIcon;
        private static void TryApplyWindowIcon(Window w)
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
            TryApplyWindowIcon(this);
            this.MinWidth = 335;
            this.MinHeight = 420;
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

            // Crisp 2K / High-DPI Font & Layout Snapping Properties
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
                if (this.ActualWidth >= 335 && this.ActualHeight >= 420)
                {
                    savedFreeWidth = this.ActualWidth;
                    savedFreeHeight = this.ActualHeight;
                    SaveSettingsDebounced();
                }
            };

            this.LocationChanged += (s, e) =>
            {
                if (isWindowInitialized && !double.IsNaN(this.Left) && !double.IsNaN(this.Top) && this.Left > -10000 && this.Top > -10000)
                {
                    savedLeft = this.Left;
                    savedTop = this.Top;
                    SaveSettingsDebounced();
                }
            };

            this.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (!isDraggable) return;
                DependencyObject dep = e.OriginalSource as DependencyObject;
                if (IsInteractiveControl(dep)) return;

                try
                {
                    IntPtr hwnd = cachedMainHwnd != IntPtr.Zero ? cachedMainHwnd : new WindowInteropHelper(this).Handle;
                    ReleaseCapture();
                    SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)HT_CAPTION, IntPtr.Zero);
                    savedLeft = this.Left;
                    savedTop = this.Top;
                    SaveSettingsDebounced();
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

                // Attach to Desktop SHELLDLL_DefView parent if available so Win+D treats widget as part of the Desktop
                AttachToDesktopShell(hwnd);

                // Register event-driven Clipboard Listener (zero polling overhead)
                try { AddClipboardFormatListener(hwnd); } catch { }

                // Global Hotkey: Ctrl + Shift + M for Mic Mute
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
                        InitializeCalendarWidget();
                        InitializeTimerWidget();
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
                try
                {
                    if (nvmlInitialized) NvmlNative.nvmlShutdown();
                }
                catch { }
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
                try
                {
                    if (sharedUdpClient != null) sharedUdpClient.Close();
                    if (sharedPing != null) sharedPing.Dispose();
                }
                catch { }
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
                    // Fast path: if foreground window hasn't changed and we're already in normal desktop-bottom state, skip Win32 class/z-order queries
                    if (fg == lastVerifiedFgHwnd && !isShowDesktopActive)
                        return;

                    fgClassBuilder.Length = 0;
                    GetClassName(fg, fgClassBuilder, 64);
                    string cls = fgClassBuilder.ToString();
                    // When user presses Win+D, WorkerW or Progman becomes the foreground window
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
                        double targetH = Math.Min(workArea.Height - 28, Math.Max(460, desiredH));
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
                // Event-driven clipboard update: triggered ONLY when user copies something
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

        // ==========================================
        // DYNAMIC THEME & OPACITY ENGINE
        // ==========================================
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
                RefreshWirelessRowColors();

                if (sliderTrackContainer != null)
                {
                    double currentVal = sliderBulbDim != null ? sliderBulbDim.Value : bulbDimming;
                    sliderBulbDim = CreateModernSlider(accentColorHex, currentVal);
                    sliderBulbDim.ValueChanged += SliderBulbDim_ValueChanged;
                    sliderTrackContainer.Child = sliderBulbDim;
                }

                for (int i = 0; i < 2; i++)
                {
                    if (monitorSliderContainers[i] != null)
                    {
                        int idx = i;
                        double curVal = sliderMonitors[i] != null ? sliderMonitors[i].Value : (i < monitorSlots.Count ? monitorSlots[i].CurrentBrightness : 100);
                        Slider ms = CreateModernSlider(accentColorHex, curVal, 0, "6,0,8,0");
                        ms.ValueChanged += (s, e) => OnMonitorSliderValueChanged(idx, e.NewValue);
                        AttachMonitorSliderWheel(ms, idx);
                        sliderMonitors[i] = ms;
                        monitorSliderContainers[i].Child = ms;
                    }
                }

                UpdateBulbPowerButtonUi();
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

            // 1. TOP HEADER (Clock + Date + Uptime + Quick Mic + Drag Toggle + Settings)
            Grid headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            txtClockAndUptime = new TextBlock
            {
                Text = "05:25 · 6 Kas Cum  Açık: 2s 10dk",
                Foreground = HexBrush("#F5EFEA"),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(txtClockAndUptime, 0);
            headerGrid.Children.Add(txtClockAndUptime);

            // Header Quick Mic Button (Icon-only, no outline when unmuted)
            btnMicHeader = CreateStyledIconButton("#26201D", "Transparent", "Mikrofonu Sustur / Aç (Ctrl+Shift+M)");
            btnMicHeader.Margin = new Thickness(4, 0, 4, 0);
            btnMicHeader.Padding = new Thickness(7, 3, 7, 3);
            txtMicHeaderIcon = new TextBlock { Text = "🎙", FontSize = 11.5, Foreground = HexBrush("#FAF6F2"), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            btnMicHeader.Content = txtMicHeaderIcon;
            btnMicHeader.Click += (s, e) => ToggleMicrophoneMute();
            Grid.SetColumn(btnMicHeader, 1);
            headerGrid.Children.Add(btnMicHeader);

            // Lock / Drag & Resize Toggle Button (Icon-only)
            btnLockDrag = CreateStyledIconButton("#26201D", "Transparent", "Sabit Mod (Taşınabilir ve Boyutlandırılabilir Modu Açmak İçin Tıklayın)");
            btnLockDrag.Padding = new Thickness(7, 3, 7, 3);
            btnLockDrag.Margin = new Thickness(0, 0, 4, 0);
            btnLockDrag.Content = new TextBlock { Text = "📌", FontSize = 11.5, Foreground = HexBrush("#C7B9AE"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            btnLockDrag.Click += (s, e) => ToggleDraggableMode();
            Grid.SetColumn(btnLockDrag, 2);
            headerGrid.Children.Add(btnLockDrag);

            // Quick Settings Button in Header (Icon-only, subtle no-outline)
            btnSettingsHeader = CreateStyledIconButton("#26201D", "Transparent", "Widget Ayarları (Tema Rengi & Opaklık)");
            btnSettingsHeader.Padding = new Thickness(7, 3, 7, 3);
            btnSettingsHeader.Content = new TextBlock { Text = "⚙", FontSize = 11.5, Foreground = HexBrush("#C7B9AE") };
            btnSettingsHeader.Click += (s, e) => OpenSettingsWindow();
            Grid.SetColumn(btnSettingsHeader, 3);
            headerGrid.Children.Add(btnSettingsHeader);

            rootStack.Children.Add(headerGrid);

            modulesHostPanel = new StackPanel();
            rootStack.Children.Add(modulesHostPanel);

            secContainerWeather = new StackPanel();
            secContainerBulb = new StackPanel();
            secContainerMonitors = new StackPanel();
            secContainerWireless = new StackPanel();
            secContainerTelemetry = new StackPanel();
            secContainerClipboard = new StackPanel();

            // 2. WEATHER SECTION (Hava Durumu - Kütahya / Merkez)
            Grid weatherTitleGrid = new Grid { Margin = new Thickness(0, 2, 0, 6) };
            weatherTitleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            weatherTitleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel weatherTitleLeft = new StackPanel { Orientation = Orientation.Horizontal };
            weatherTitleLeft.Children.Add(new TextBlock { Text = "☀ ", Foreground = HexBrush("#C7B9AE"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            weatherTitleLeft.Children.Add(new TextBlock { Text = "Hava Durumu", Foreground = HexBrush("#E6DDD6"), FontSize = 12, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(weatherTitleLeft, 0);
            weatherTitleGrid.Children.Add(weatherTitleLeft);

            txtLocationBadge = new TextBlock
            {
                Text = "Kütahya / Merkez",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                Tag = "Interactive",
                ToolTip = "Hava durumunu şimdi güncellemek için tıklayın"
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
                Text = "3°",
                Foreground = Brushes.White,
                FontSize = 30,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            Grid.SetColumn(txtWeatherTemp, 0);
            mainWeatherRow.Children.Add(txtWeatherTemp);

            StackPanel descStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            txtWeatherDesc = new TextBlock { Text = "Parçalı Bulutlu", Foreground = HexBrush("#FAF6F2"), FontSize = 13.5, FontWeight = FontWeights.SemiBold };
            txtWeatherFeels = new TextBlock { Text = "Hissedilen 1°C", Foreground = HexBrush("#B8A99E"), FontSize = 11.5, Margin = new Thickness(0, 2, 0, 0) };
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
            metricsGrid.Children.Add(CreateWeatherMetricBox("💧 Nem", "%92", out txtWeatherHumidity, new Thickness(0, 0, 4, 0)));
            metricsGrid.Children.Add(CreateWeatherMetricBox("💨 Rüzgar", "8 km/s", out txtWeatherWind, new Thickness(2, 0, 2, 0)));
            metricsGrid.Children.Add(CreateWeatherMetricBox("🌧 Yağış", "0 mm", out txtWeatherRain, new Thickness(4, 0, 0, 0)));
            weatherStack.Children.Add(metricsGrid);

            weatherStack.Children.Add(new Border { Height = 1, Background = HexBrush("#3D322C"), Margin = new Thickness(0, 0, 0, 8) });

            UniformGrid forecastGrid = new UniformGrid { Columns = 3 };
            string[] defaultDays = { "Bugün", "Yarın", "Pazar" };
            for (int i = 0; i < 3; i++)
            {
                StackPanel dayBox = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                txtDayNames[i] = new TextBlock { Text = defaultDays[i], Foreground = HexBrush("#B8A99E"), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center };
                txtDayIcons[i] = new TextBlock { Text = "⛅", FontSize = 13, Margin = new Thickness(0, 3, 0, 3), HorizontalAlignment = HorizontalAlignment.Center };
                accentTextBlocks.Add(txtDayIcons[i]);
                txtDayTemps[i] = new TextBlock { Text = "3° / 14°", Foreground = HexBrush("#FAF6F2"), FontSize = 11.5, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
                dayBox.Children.Add(txtDayNames[i]);
                dayBox.Children.Add(txtDayIcons[i]);
                dayBox.Children.Add(txtDayTemps[i]);
                forecastGrid.Children.Add(dayBox);
            }
            weatherStack.Children.Add(forecastGrid);
            weatherCard.Child = weatherStack;
            secContainerWeather.Children.Add(weatherCard);

            // 3. MATTER / WiZ SMART BULB SECTION
            Grid bulbHeaderGrid = new Grid { Margin = new Thickness(0, 12, 0, 6) };
            bulbHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bulbHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel bulbHeaderLeft = new StackPanel { Orientation = Orientation.Horizontal };
            bulbHeaderLeft.Children.Add(new TextBlock { Text = "💡 ", FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            bulbHeaderLeft.Children.Add(new TextBlock { Text = "Akıllı Ampul (Matter / WiZ)", Foreground = HexBrush("#E6DDD6"), FontSize = 12, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(bulbHeaderLeft, 0);
            bulbHeaderGrid.Children.Add(bulbHeaderLeft);

            StackPanel bulbStatusRight = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            bulbStatusDot = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(3.5), Background = HexBrush("#E2925C"), Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
            accentBarFills.Add(bulbStatusDot);
            txtBulbStatus = new TextBlock { Text = "Bağlı (" + bulbIp + ")", Foreground = HexBrush("#B8A99E"), FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center };
            bulbStatusRight.Children.Add(bulbStatusDot);
            bulbStatusRight.Children.Add(txtBulbStatus);
            Grid.SetColumn(bulbStatusRight, 1);
            bulbHeaderGrid.Children.Add(bulbStatusRight);
            secContainerBulb.Children.Add(bulbHeaderGrid);

            Border bulbCard = CreateGlassCard();
            StackPanel bulbStack = new StackPanel();

            Grid bulbControlTop = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            bulbControlTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bulbControlTop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bulbControlTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            btnBulbPower = CreateStyledIconButton("#38281E", "#E2925C", "Ampulü Aç / Kapat");
            btnBulbPower.Padding = new Thickness(10, 4, 10, 4);
            btnBulbPower.Content = new TextBlock { Text = "⚡ Açık", Foreground = HexBrush("#E2925C"), FontSize = 11.5, FontWeight = FontWeights.SemiBold };
            btnBulbPower.Click += (s, e) => ToggleBulbPower();
            Grid.SetColumn(btnBulbPower, 0);
            bulbControlTop.Children.Add(btnBulbPower);

            TextBlock txtSliderTitle = new TextBlock
            {
                Text = "Parlaklık Seviyesi",
                Foreground = HexBrush("#B8A99E"),
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };
            Grid.SetColumn(txtSliderTitle, 1);
            bulbControlTop.Children.Add(txtSliderTitle);

            txtBulbPercent = new TextBlock
            {
                Text = "%13",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            accentTextBlocks.Add(txtBulbPercent);
            Grid.SetColumn(txtBulbPercent, 2);
            bulbControlTop.Children.Add(txtBulbPercent);
            bulbStack.Children.Add(bulbControlTop);

            sliderTrackContainer = new Border();
            sliderBulbDim = CreateModernSlider(accentColorHex, 13);
            sliderBulbDim.ValueChanged += SliderBulbDim_ValueChanged;
            sliderTrackContainer.Child = sliderBulbDim;
            bulbStack.Children.Add(sliderTrackContainer);

            UniformGrid presetGrid = new UniformGrid { Columns = 4 };
            string[] sceneLabels = { "🌙 Gece %10", "🕯 Sıcak", "☀ Gün Işığı", "🎨 Özel" };
            int[] sceneTemps = { 2200, 2700, 4200, 0 };
            int[] sceneDims = { 10, -1, -1, -1 };

            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                Button b = CreateStyledIconButton("#26201D", "#443831", idx == 3 ? "Özel RGB renk spektrumunu aç / kapat" : "Renk sıcaklığı / mod seç");
                b.Margin = new Thickness(i == 0 ? 0 : 2, 0, i == 3 ? 0 : 2, 0);
                b.Padding = new Thickness(2, 4, 2, 4);

                if (idx == 3)
                {
                    StackPanel customBtnContent = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                    bulbCustomColorDot = new Border
                    {
                        Width = 8,
                        Height = 8,
                        CornerRadius = new CornerRadius(4),
                        Background = HexBrush(bulbCustomColorHex),
                        BorderBrush = HexBrush("#FAF6F2"),
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(0, 0, 4, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    customBtnContent.Children.Add(bulbCustomColorDot);
                    customBtnContent.Children.Add(new TextBlock { Text = "Özel", Foreground = HexBrush("#E6DDD6"), FontSize = 10, VerticalAlignment = VerticalAlignment.Center });
                    b.Content = customBtnContent;

                    b.Click += (s, e) =>
                    {
                        if (panelBulbCustomSpectrum.Visibility == Visibility.Visible)
                        {
                            panelBulbCustomSpectrum.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            panelBulbCustomSpectrum.Visibility = Visibility.Visible;
                            bulbIsCustomColor = true;
                            bulbOn = true;
                            UpdateBulbPowerButtonUi();
                            UpdateBulbSceneButtonsUi();
                            int targetDim = (int)Math.Round(sliderBulbDim.Value);
                            string hexCopy = bulbCustomColorHex;
                            Task.Run(() => SendWizPilotCustomHexCommand(true, targetDim, hexCopy));
                        }
                    };
                }
                else
                {
                    b.Content = new TextBlock { Text = sceneLabels[i], Foreground = HexBrush("#E6DDD6"), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center };
                    b.Click += (s, e) =>
                    {
                        int targetTemp = sceneTemps[idx];
                        int targetDim = sceneDims[idx] > 0 ? sceneDims[idx] : (int)Math.Round(sliderBulbDim.Value);
                        bulbTemp = targetTemp;
                        bulbIsCustomColor = false;
                        if (sceneDims[idx] > 0)
                        {
                            isUpdatingBulbUi = true;
                            sliderBulbDim.Value = targetDim;
                            txtBulbPercent.Text = "%" + targetDim;
                            isUpdatingBulbUi = false;
                        }
                        bulbOn = true;
                        UpdateBulbPowerButtonUi();
                        UpdateBulbSceneButtonsUi();
                        Task.Run(() => SendWizPilotCommand(true, targetDim, targetTemp));
                    };
                }
                btnScenes[i] = b;
                presetGrid.Children.Add(b);
            }
            bulbStack.Children.Add(presetGrid);

            panelBulbCustomSpectrum = CreateBulbInlineSpectrumPanel();
            bulbStack.Children.Add(panelBulbCustomSpectrum);

            bulbCard.Child = bulbStack;
            secContainerBulb.Children.Add(bulbCard);

            // 3B. MONITOR SDR BRIGHTNESS SECTION (DDC/CI Hardware Luminance - Replaces Twinkle Tray)
            Grid monHeaderGrid = new Grid { Margin = new Thickness(0, 12, 0, 6), Cursor = Cursors.Hand, Tag = "Interactive", ToolTip = "Aç/Kapat (Monitör donanım parlaklığını DDC/CI üzerinden sıfır RAM yüküyle kontrol eder)" };
            monHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            monHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            monHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            monHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel monHeaderLeft = new StackPanel { Orientation = Orientation.Horizontal };
            monHeaderLeft.Children.Add(new TextBlock { Text = "🖥 ", FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });
            monHeaderLeft.Children.Add(new TextBlock { Text = "Monitör Parlaklığı (SDR / DDC)", Foreground = HexBrush("#E6DDD6"), FontSize = 12, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(monHeaderLeft, 0);
            monHeaderGrid.Children.Add(monHeaderLeft);

            btnMonitorLink = CreateStyledIconButton("#26201D", "#443831", "İki monitörün parlaklığını birlikte (senkron) değiştir");
            btnMonitorLink.Padding = new Thickness(6, 2, 6, 2);
            btnMonitorLink.Margin = new Thickness(0, 0, 4, 0);
            btnMonitorLink.Click += (s, e) =>
            {
                e.Handled = true;
                monitorLinkSync = !monitorLinkSync;
                UpdateMonitorLinkButtonUi();
                SaveSettings();
                if (monitorLinkSync && sliderMonitors[0] != null)
                {
                    SetMonitorBrightnessFromUi(0, (int)Math.Round(sliderMonitors[0].Value), true);
                }
            };
            Grid.SetColumn(btnMonitorLink, 1);
            monHeaderGrid.Children.Add(btnMonitorLink);

            Button btnRefreshMonitors = CreateStyledIconButton("Transparent", "Transparent", "Monitör parlaklıklarını donanımdan yeniden oku");
            btnRefreshMonitors.Padding = new Thickness(5, 1, 5, 1);
            btnRefreshMonitors.Margin = new Thickness(0, 0, 4, 0);
            btnRefreshMonitors.Content = new TextBlock { Text = "↻", Foreground = HexBrush("#B8A99E"), FontSize = 12, FontWeight = FontWeights.Bold };
            btnRefreshMonitors.Click += (s, e) =>
            {
                e.Handled = true;
                Task.Run(() => DiscoverAndSyncMonitors(true));
                ShowToast("Monitör parlaklıkları okunuyor...");
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

            StackPanel monStack = new StackPanel();
            string[] defaultMonNames = { "Lenovo 27Q-11", "ViewSonic VX2428" };
            for (int i = 0; i < 2; i++)
            {
                int idx = i;
                Grid mRow = new Grid { Margin = new Thickness(0, i == 0 ? 0 : 4, 0, 4) };
                mRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
                mRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                mRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });

                txtMonitorNames[i] = new TextBlock
                {
                    Text = defaultMonNames[i],
                    Foreground = HexBrush("#F5EFEA"),
                    FontSize = 11.5,
                    FontWeight = FontWeights.Medium,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    ToolTip = defaultMonNames[i] + " (Fare tekerleği ile ±%5 değiştirebilirsiniz)"
                };
                Grid.SetColumn(txtMonitorNames[i], 0);
                mRow.Children.Add(txtMonitorNames[i]);

                monitorSliderContainers[i] = new Border { VerticalAlignment = VerticalAlignment.Center };
                sliderMonitors[i] = CreateModernSlider(accentColorHex, 100, 0, "6,0,8,0");
                sliderMonitors[i].ValueChanged += (s, e) => OnMonitorSliderValueChanged(idx, e.NewValue);
                AttachMonitorSliderWheel(sliderMonitors[i], idx);
                monitorSliderContainers[i].Child = sliderMonitors[i];
                Grid.SetColumn(monitorSliderContainers[i], 1);
                mRow.Children.Add(monitorSliderContainers[i]);

                txtMonitorPercents[i] = new TextBlock
                {
                    Text = "%100",
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                accentTextBlocks.Add(txtMonitorPercents[i]);
                Grid.SetColumn(txtMonitorPercents[i], 2);
                mRow.Children.Add(txtMonitorPercents[i]);

                rowMonitors[i] = mRow;
                monStack.Children.Add(mRow);
            }

            UniformGrid monPresetGrid = new UniformGrid { Columns = 4, Margin = new Thickness(0, 4, 0, 0) };
            string[] monPresetLabels = { "🌙 %15", "🔉 %40", "🔆 %75", "☀ %100" };
            int[] monPresetVals = { 15, 40, 75, 100 };
            for (int i = 0; i < 4; i++)
            {
                int targetVal = monPresetVals[i];
                Button b = CreateStyledIconButton("#26201D", "#443831", "Tüm monitörleri %" + targetVal + " parlaklığa ayarla");
                b.Margin = new Thickness(i == 0 ? 0 : 2, 0, i == 3 ? 0 : 2, 0);
                b.Padding = new Thickness(2, 3, 2, 3);
                b.Content = new TextBlock { Text = monPresetLabels[i], Foreground = HexBrush("#E6DDD6"), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center };
                b.Click += (s, e) =>
                {
                    SetMonitorBrightnessFromUi(0, targetVal, true);
                };
                monPresetGrid.Children.Add(b);
            }
            monStack.Children.Add(monPresetGrid);
            monitorCard.Child = monStack;
            secContainerMonitors.Children.Add(monitorCard);
            UpdateMonitorLinkButtonUi();

            // 4. WIRELESS DEVICES BATTERY SECTION (Scyrox V8, HyperX Cloud III, Wireless Controller)
            wirelessHeaderGrid = new Grid { Margin = new Thickness(0, 12, 0, 4), Cursor = Cursors.Hand, Tag = "Interactive", ToolTip = "Aç/Kapat (Şarj durumunu anında yenilemek için ↻ ikonuna tıklayın)" };
            wirelessHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            wirelessHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            wirelessHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel wirelessTitleLeft = new StackPanel { Orientation = Orientation.Horizontal };
            wirelessTitleLeft.Children.Add(new TextBlock { Text = "🔋 ", Foreground = HexBrush("#B8A99E"), FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });
            wirelessTitleLeft.Children.Add(new TextBlock { Text = "Kablosuz Cihazlar", Foreground = HexBrush("#E6DDD6"), FontSize = 12.5, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(wirelessTitleLeft, 0);
            wirelessHeaderGrid.Children.Add(wirelessTitleLeft);

            Button btnRefreshWireless = CreateStyledIconButton("Transparent", "Transparent", "Kablosuz cihaz şarjlarını şimdi yenile");
            btnRefreshWireless.Padding = new Thickness(5, 1, 5, 1);
            btnRefreshWireless.Margin = new Thickness(0, 0, 4, 0);
            btnRefreshWireless.Content = new TextBlock { Text = "↻", Foreground = HexBrush("#B8A99E"), FontSize = 12, FontWeight = FontWeights.Bold };
            btnRefreshWireless.Click += (s, e) =>
            {
                e.Handled = true;
                Task.Run(() => PollWirelessDevicesAsync(true));
                ShowToast("Kablosuz cihaz şarjları güncelleniyor...");
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
                    Task.Run(() => PollWirelessDevicesAsync(false));
                }
            };
            secContainerWireless.Children.Add(wirelessHeaderGrid);

            rowMouse = CreateTelemetryBarRow("🖱", "Scyrox V8", "1000 Hz • Taranıyor...", false, out txtMouseDetail, out barMouseFill, out txtMousePct);
            panelWirelessContent.Children.Add(rowMouse);

            rowHeadset = CreateTelemetryBarRow("🎧", "HyperX Cloud III", "2.4GHz • Açık", false, out txtHeadsetDetail, out barHeadsetFill, out txtHeadsetPct);
            rowHeadset.Visibility = Visibility.Collapsed;
            panelWirelessContent.Children.Add(rowHeadset);

            rowController = CreateTelemetryBarRow("🎮", "Kablosuz Kumanda", "Bağlı", false, out txtControllerDetail, out barControllerFill, out txtControllerPct);
            rowController.Visibility = Visibility.Collapsed;
            panelWirelessContent.Children.Add(rowController);

            secContainerWireless.Children.Add(panelWirelessContent);

            // 5. TELEMETRY SECTION
            Grid teleHeader = new Grid { Margin = new Thickness(0, 12, 0, 4), Cursor = Cursors.Hand, Tag = "Interactive" };
            teleHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            teleHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            teleHeader.Children.Add(new TextBlock { Text = "Telemetri", Foreground = HexBrush("#E6DDD6"), FontSize = 12.5, FontWeight = FontWeights.Medium });
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

            rowTeleCpu = CreateTelemetrySparkRow("⚙", "İşlemci (CPU)", "Ryzen 5 7500F • 48°C", true, out txtCpuDetail, out sparkCpu, out txtCpuPct);
            panelTelemetryContent.Children.Add(rowTeleCpu);
            rowTeleRam = CreateTelemetryBarRow("📟", "Bellek (RAM)", "12,4 / 32 GB", true, out txtRamDetail, out barRamFill, out txtRamPct);
            panelTelemetryContent.Children.Add(rowTeleRam);
            rowTeleGpu = CreateTelemetrySparkRow("🎮", "Ekran Kartı (GPU)", "RTX 5070 • 49°C", true, out txtGpuDetail, out sparkGpu, out txtGpuPct);
            panelTelemetryContent.Children.Add(rowTeleGpu);
            rowTeleVram = CreateTelemetryBarRow("🎞", "Video Belleği (VRAM)", "2,1 / 12 GB", true, out txtVramDetail, out barVramFill, out txtVramPct);
            panelTelemetryContent.Children.Add(rowTeleVram);

            // Network Row
            Grid netRow = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            netRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            netRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            netRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            netRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel netLeft = new StackPanel { Orientation = Orientation.Horizontal };
            netLeft.Children.Add(CreateMiniIconBadge("⇄"));
            netLeft.Children.Add(new TextBlock { Text = "Ağ Trafiği", Foreground = HexBrush("#F5EFEA"), FontSize = 12, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(netLeft, 0);
            netRow.Children.Add(netLeft);

            txtNetSpeed = new TextBlock
            {
                Text = "↓ 120 Kbps ↑ 45 Kbps",
                Foreground = HexBrush("#B8A99E"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(4, 0, 6, 0)
            };
            Grid.SetColumn(txtNetSpeed, 1);
            netRow.Children.Add(txtNetSpeed);

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
            txtNetBadge = new TextBlock { Text = "Harika", FontSize = 10, FontWeight = FontWeights.SemiBold };
            accentTextBlocks.Add(txtNetBadge);
            netBadgeBorder.Child = txtNetBadge;
            Grid.SetColumn(netBadgeBorder, 2);
            netRow.Children.Add(netBadgeBorder);

            txtNetPing = new TextBlock
            {
                Text = "9 ms",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Width = 48,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            accentTextBlocks.Add(txtNetPing);
            Grid.SetColumn(txtNetPing, 3);
            netRow.Children.Add(txtNetPing);
            rowTeleNet = netRow;
            panelTelemetryContent.Children.Add(netRow);

            rowTeleDiskC = CreateTelemetryBarRow("💾", "C: Diski (NVMe)", "385 GB Boş / 931 GB", true, out txtDiskCDetail, out barDiskCFill, out txtDiskCPct);
            panelTelemetryContent.Children.Add(rowTeleDiskC);
            rowTeleDiskD = CreateTelemetryBarRow("💿", "D: Diski (NVMe)", "467 GB Boş / 931 GB", true, out txtDiskDDetail, out barDiskDFill, out txtDiskDPct);
            panelTelemetryContent.Children.Add(rowTeleDiskD);

            secContainerTelemetry.Children.Add(panelTelemetryContent);

            // 6. CLIPBOARD HISTORY SECTION
            Grid clipHeader = new Grid { Margin = new Thickness(0, 12, 0, 6) };
            clipHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            clipHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            clipHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel clipTitleLeft = new StackPanel { Orientation = Orientation.Horizontal };
            clipTitleLeft.Children.Add(new TextBlock { Text = "📋 ", Foreground = HexBrush("#B8A99E"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            clipTitleLeft.Children.Add(new TextBlock { Text = "Pano Geçmişi", Foreground = HexBrush("#E6DDD6"), FontSize = 12.5, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(clipTitleLeft, 0);
            clipHeader.Children.Add(clipTitleLeft);

            Button btnClearClip = CreateStyledIconButton("Transparent", "Transparent", "Pano geçmişini temizle");
            btnClearClip.Padding = new Thickness(5, 2, 5, 2);
            btnClearClip.Margin = new Thickness(0, 0, 6, 0);
            btnClearClip.Content = new TextBlock { Text = "🗑", Foreground = HexBrush("#B8A99E"), FontSize = 12 };
            btnClearClip.Click += (s, e) =>
            {
                clipboardHistory.Clear();
                RefreshClipboardUi();
                ShowToast("Pano geçmişi temizlendi");
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
            txtClipCount = new TextBlock { Text = "0 öğe", Foreground = HexBrush("#E6DDD6"), FontSize = 11 };
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
            txtToastMessage = new TextBlock { Text = "Panoya kopyalandı", Foreground = Brushes.White, FontSize = 11.5, FontWeight = FontWeights.SemiBold };
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

        private void ApplyLayoutAndVisibility()
        {
            if (modulesHostPanel == null) return;

            if (secContainerWeather != null) secContainerWeather.Visibility = showSecWeather ? Visibility.Visible : Visibility.Collapsed;
            if (secContainerBulb != null) secContainerBulb.Visibility = showSecBulb ? Visibility.Visible : Visibility.Collapsed;
            if (secContainerMonitors != null) secContainerMonitors.Visibility = showSecMonitors ? Visibility.Visible : Visibility.Collapsed;
            if (secContainerWireless != null) secContainerWireless.Visibility = (showSecWireless && anyWirelessDeviceActive) ? Visibility.Visible : Visibility.Collapsed;
            if (secContainerTelemetry != null) secContainerTelemetry.Visibility = showSecTelemetry ? Visibility.Visible : Visibility.Collapsed;
            if (secContainerClipboard != null) secContainerClipboard.Visibility = showSecClipboard ? Visibility.Visible : Visibility.Collapsed;

            if (rowTeleCpu != null) rowTeleCpu.Visibility = showTeleCpu ? Visibility.Visible : Visibility.Collapsed;
            if (rowTeleRam != null) rowTeleRam.Visibility = showTeleRam ? Visibility.Visible : Visibility.Collapsed;
            if (rowTeleGpu != null) rowTeleGpu.Visibility = showTeleGpu ? Visibility.Visible : Visibility.Collapsed;
            if (rowTeleVram != null) rowTeleVram.Visibility = showTeleVram ? Visibility.Visible : Visibility.Collapsed;
            if (rowTeleNet != null) rowTeleNet.Visibility = showTeleNet ? Visibility.Visible : Visibility.Collapsed;
            if (rowTeleDiskC != null) rowTeleDiskC.Visibility = showTeleDisks ? Visibility.Visible : Visibility.Collapsed;
            if (rowTeleDiskD != null) rowTeleDiskD.Visibility = showTeleDisks ? Visibility.Visible : Visibility.Collapsed;

            modulesHostPanel.Children.Clear();
            bool isFirstVisible = true;
            foreach (string key in sectionOrder)
            {
                StackPanel container = null;
                if (key == "weather") container = secContainerWeather;
                else if (key == "bulb") container = secContainerBulb;
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
            return CreateModernSlider(accentHex, initialVal, 10, "0,2,0,10");
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

        private Border CreateWeatherMetricBox(string label, string initialValue, out TextBlock valueBlock, Thickness margin)
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
            sp.Children.Add(new TextBlock { Text = label, Foreground = HexBrush("#B8A99E"), FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Center });
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

        private Grid CreateTelemetrySparkRow(string icon, string label, string detail, bool useThemeAccent, out TextBlock detailBlock, out Polyline sparkline, out TextBlock pctBlock)
        {
            Grid g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel left = new StackPanel { Orientation = Orientation.Horizontal };
            left.Children.Add(CreateMiniIconBadge(icon));
            left.Children.Add(new TextBlock { Text = label, Foreground = HexBrush("#F5EFEA"), FontSize = 12, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center });
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
                Text = "%8",
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

        private Grid CreateTelemetryBarRow(string icon, string label, string detail, bool useThemeAccent, out TextBlock detailBlock, out Border barFill, out TextBlock pctBlock)
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
            TextBlock labelBlock = new TextBlock
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
            trayMuteItem = new System.Windows.Forms.ToolStripMenuItem("🎙 Mikrofonu Sustur / Aç (Sol Tık)");
            trayMuteItem.Click += (s, e) => ToggleMicrophoneMute();
            menu.Items.Add(trayMuteItem);

            var settingsItem = new System.Windows.Forms.ToolStripMenuItem("⚙ Widget Ayarları (Tema & Opaklık)...");
            settingsItem.Font = new System.Drawing.Font(settingsItem.Font, System.Drawing.FontStyle.Bold);
            settingsItem.Click += (s, e) => OpenSettingsWindow();
            menu.Items.Add(settingsItem);

            trayCalendarItem = new System.Windows.Forms.ToolStripMenuItem(showCalendarWidget ? "📅 Google Takvim Widget'ını Gizle" : "📅 Google Takvim Widget'ını Göster");
            trayCalendarItem.Click += (s, e) =>
            {
                SetCalendarWidgetVisible(!showCalendarWidget);
            };
            menu.Items.Add(trayCalendarItem);

            trayTimerItem = new System.Windows.Forms.ToolStripMenuItem(showTimerWidget ? "⏱ Timer Widgeti'ni Gizle" : "⏱ Timer Widgeti'ni Göster");
            trayTimerItem.Click += (s, e) =>
            {
                SetTimerWidgetVisible(!showTimerWidget);
            };
            menu.Items.Add(trayTimerItem);

            trayTaskbarDevicesItem = new System.Windows.Forms.ToolStripMenuItem(showTaskbarDevices ? "🔋 Görev Çubuğu Cihaz Şarjlarını Gizle" : "🔋 Görev Çubuğu Cihaz Şarjlarını Göster");
            trayTaskbarDevicesItem.Click += (s, e) =>
            {
                SetTaskbarDevicesVisible(!showTaskbarDevices);
            };
            menu.Items.Add(trayTaskbarDevicesItem);

            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            trayLockItem = new System.Windows.Forms.ToolStripMenuItem("🔓 Taşınabilir & Boyutlandırılabilir Modu Aç");
            trayLockItem.Click += (s, e) => ToggleDraggableMode();
            menu.Items.Add(trayLockItem);

            var fitHeightItem = new System.Windows.Forms.ToolStripMenuItem("📐 Yüksekliği İçeriğe Göre Otomatik Sığdır");
            fitHeightItem.Click += (s, e) => FitHeightToContentIfCompact();
            menu.Items.Add(fitHeightItem);

            var dockRightItem = new System.Windows.Forms.ToolStripMenuItem("➡ Sağ Kenara Hizala");
            dockRightItem.Click += (s, e) => DockWindow("Right");
            menu.Items.Add(dockRightItem);

            var dockLeftItem = new System.Windows.Forms.ToolStripMenuItem("⬅ Sol Kenara Hizala");
            dockLeftItem.Click += (s, e) => DockWindow("Left");
            menu.Items.Add(dockLeftItem);

            var toggleVisItem = new System.Windows.Forms.ToolStripMenuItem("👁 Widget'ı Göster / Gizle");
            toggleVisItem.Click += (s, e) =>
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
            menu.Items.Add(toggleVisItem);

            var autoStartItem = new System.Windows.Forms.ToolStripMenuItem("🚀 Windows Başlangıcında Otomatik Aç")
            {
                Checked = IsAutoStartEnabled(),
                CheckOnClick = true
            };
            autoStartItem.Click += (s, e) =>
            {
                EnsureAutoStart(autoStartItem.Checked);
                ShowToast(autoStartItem.Checked ? "Başlangıçta otomatik açılma aktif edildi" : "Başlangıçta otomatik açılma kapatıldı");
            };
            menu.Items.Add(autoStartItem);

            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            var exitItem = new System.Windows.Forms.ToolStripMenuItem("✖ Çıkış");
            exitItem.Click += (s, e) => Application.Current.Shutdown();
            menu.Items.Add(exitItem);

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
            ShowToast(isMicMuted ? "🔇 Mikrofon Susturuldu (Muted)" : "🎙 Mikrofon Açıldı (Canlı)");
        }

        private void UpdateMicrophoneUi()
        {
            Dispatcher.Invoke(() =>
            {
                if (trayIcon != null)
                {
                    trayIcon.Icon = isMicMuted ? iconMicMuted : iconMicUnmuted;
                    trayIcon.Text = isMicMuted ? "Mikrofon: KAPALI (Susturuldu) - Açmak için tıkla" : "Mikrofon: AÇIK (Canlı) - Susturmak için tıkla";
                }
                if (txtMicHeaderIcon != null && btnMicHeader != null)
                {
                    if (isMicMuted)
                    {
                        txtMicHeaderIcon.Text = "🔇";
                        txtMicHeaderIcon.Foreground = HexBrush("#FB7185");
                        btnMicHeader.Background = HexBrush("#3B1622");
                        btnMicHeader.BorderBrush = HexBrush("#E11D48");
                        btnMicHeader.ToolTip = "Mikrofon Sessiz (Açmak için tıklayın veya Ctrl+Shift+M)";
                    }
                    else
                    {
                        txtMicHeaderIcon.Text = "🎙";
                        txtMicHeaderIcon.Foreground = HexBrush("#FAF6F2");
                        btnMicHeader.Background = HexBrush("#26201D");
                        btnMicHeader.BorderBrush = Brushes.Transparent;
                        btnMicHeader.ToolTip = "Mikrofon Açık (Susturmak için tıklayın veya Ctrl+Shift+M)";
                    }
                }
            });
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
                if (calendarWindow != null) calendarWindow.ResizeMode = ResizeMode.CanResize;
                if (calWindowChrome != null) calWindowChrome.ResizeBorderThickness = new Thickness(8);
                if (calResizeGrip != null) calResizeGrip.Visibility = Visibility.Visible;
                if (timerWindow != null) timerWindow.ResizeMode = ResizeMode.CanResize;
                if (timerWindowChrome != null) timerWindowChrome.ResizeBorderThickness = new Thickness(8);
                if (timerResizeGrip != null) timerResizeGrip.Visibility = Visibility.Visible;
                if (trayLockItem != null) trayLockItem.Text = "📌 Konumu ve Boyutu Kilitle (Sabit Mod)";
                ShowToast("Taşınabilir Mod Açık: Sürükleyebilir veya kenarlardan boyutlandırabilirsiniz");
            }
            else
            {
                this.ResizeMode = ResizeMode.NoResize;
                if (windowChrome != null) windowChrome.ResizeBorderThickness = new Thickness(0);
                if (resizeGripIndicator != null) resizeGripIndicator.Visibility = Visibility.Collapsed;
                if (calendarWindow != null) calendarWindow.ResizeMode = ResizeMode.NoResize;
                if (calWindowChrome != null) calWindowChrome.ResizeBorderThickness = new Thickness(0);
                if (calResizeGrip != null) calResizeGrip.Visibility = Visibility.Collapsed;
                if (timerWindow != null) timerWindow.ResizeMode = ResizeMode.NoResize;
                if (timerWindowChrome != null) timerWindowChrome.ResizeBorderThickness = new Thickness(0);
                if (timerResizeGrip != null) timerResizeGrip.Visibility = Visibility.Collapsed;
                if (trayLockItem != null) trayLockItem.Text = "🔓 Taşınabilir & Boyutlandırılabilir Modu Aç";
                SaveSettings();
                ShowToast("Konum ve boyut kilitlendi");
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
                    btnLockDrag.ToolTip = "Taşınabilir & Boyutlandırılabilir Mod Açık (Sabitlemek için tıklayın)";
                }
                else
                {
                    btnLockDrag.Content = new TextBlock { Text = "📌", FontSize = 11.5, Foreground = HexBrush("#C7B9AE"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    btnLockDrag.Background = HexBrush("#26201D");
                    btnLockDrag.BorderBrush = Brushes.Transparent;
                    btnLockDrag.ToolTip = "Sabit Mod (Taşınabilir ve Boyutlandırılabilir Modu Açmak İçin Tıklayın)";
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
            if (key == "weather") return "☀  Hava Durumu";
            if (key == "bulb") return "💡  Akıllı Ampul (WiZ)";
            if (key == "monitors") return "🖥  Monitör Parlaklığı";
            if (key == "wireless") return "🔋  Kablosuz Cihazlar";
            if (key == "telemetry") return "⚙  Telemetri";
            if (key == "clipboard") return "📋  Pano Geçmişi";
            return key;
        }

        private bool IsSectionEnabled(string key)
        {
            if (key == "weather") return showSecWeather;
            if (key == "bulb") return showSecBulb;
            if (key == "monitors") return showSecMonitors;
            if (key == "wireless") return showSecWireless;
            if (key == "telemetry") return showSecTelemetry;
            if (key == "clipboard") return showSecClipboard;
            return true;
        }

        private void SetSectionEnabled(string key, bool val)
        {
            if (key == "weather") showSecWeather = val;
            else if (key == "bulb") showSecBulb = val;
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
                Title = "OpenWidget Ayarları",
                Width = 445,
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
            TryApplyWindowIcon(settingsWindow);

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

            // Root 3-Row Grid: Fixed Sticky Header + Segmented Tab Bar + Scrollable Content Viewport
            Grid rootLayout = new Grid();
            rootLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Row 0: Draggable Header
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
                Text = "⚙ OpenWidget Ayarları",
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 15,
                FontWeight = FontWeights.Bold
            });
            hdrTitleStack.Children.Add(new TextBlock
            {
                Text = "Görünüm, masaüstü eklentileri ve modül düzeni",
                Foreground = HexBrush("#9E8E82"),
                FontSize = 10.5,
                Margin = new Thickness(0, 1, 0, 0)
            });
            Grid.SetColumn(hdrTitleStack, 0);
            hdr.Children.Add(hdrTitleStack);

            Button btnClose = CreateStyledIconButton("#2E2520", "#4A3C34", "Kapat");
            btnClose.Padding = new Thickness(9, 4, 9, 4);
            btnClose.VerticalAlignment = VerticalAlignment.Center;
            btnClose.Content = new TextBlock { Text = "✕", Foreground = HexBrush("#C7B9AE"), FontSize = 11.5 };
            btnClose.Click += (s, e) => settingsWindow.Close();
            Grid.SetColumn(btnClose, 1);
            hdr.Children.Add(btnClose);
            Grid.SetRow(hdr, 0);
            rootLayout.Children.Add(hdr);

            // Row 1: Modern 3-Tab Segmented Bar
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
            string[] tabTitles = { "🎨 Görünüm", "✨ Ek Widget'lar", "🧩 Modül Düzeni" };
            string[] tabTips = {
                "Tema rengi, saydamlık ve boyut ayarları",
                "Google Takvim ve Timer Widget eklentileri",
                "Ana widget modül sıralaması ve telemetri öğeleri"
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

            // Row 2: Smooth ScrollViewer containing active tab stack
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
            // TAB 0: APPEARANCE & THEME
            // =========================================================
            Border colorSecCard = CreateSettingsSectionCard("🎨 Tema Rengi", "Tüm widget çerçeveleri, grafikler ve yıllık ısı haritası bu renge uyarlanır");
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

            Button btnApplyHex = CreateStyledIconButton("#38281E", accentColorHex, "HEX Kodunu Uygula");
            btnApplyHex.Margin = new Thickness(6, 0, 0, 0);
            btnApplyHex.Padding = new Thickness(10, 5, 10, 5);
            btnApplyHex.Content = new TextBlock { Text = "Uygula", Foreground = HexBrush("#FAF6F2"), FontSize = 11.5, FontWeight = FontWeights.SemiBold };
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
                    ShowToast("Geçersiz renk kodu! Örn: #E2925C");
                }
            };
            Grid.SetColumn(btnApplyHex, 2);
            colorCustomRow.Children.Add(btnApplyHex);

            Button btnWinPalette = CreateStyledIconButton("#2A221E", "#4A3C34", "Gelişmiş Renk Seçici Penceresini Aç");
            btnWinPalette.Margin = new Thickness(6, 0, 0, 0);
            btnWinPalette.Padding = new Thickness(9, 5, 9, 5);
            btnWinPalette.Content = new TextBlock { Text = "🎨 Palet...", Foreground = HexBrush("#FAF6F2"), FontSize = 11.5 };
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
            string[] presetNames = { "Varsayılan (#E2925C)", "Kehribar", "Bronz Kahve", "Mercan", "Zümrüt", "Gökyüzü", "Lavanta", "Yakut" };
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

            // Opacity & Window Controls Card
            Border opSecCard = CreateSettingsSectionCard("🪟 Saydamlık & Pencere", "Arka plan opaklığını ve ana widget yüksekliğini buradan ayarlayın");
            StackPanel opSecStack = (StackPanel)opSecCard.Child;

            Grid opRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            opRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            opRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            opRow.Children.Add(new TextBlock { Text = "Arka Plan Opaklığı", Foreground = HexBrush("#C7B9AE"), FontSize = 11.5 });
            TextBlock txtOpVal = new TextBlock { Text = "%" + bgOpacityPercent, Foreground = HexBrush("#FAF6F2"), FontSize = 12, FontWeight = FontWeights.Bold };
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
                    txtOpVal.Text = "%" + v;
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

            Button btnFit = CreateStyledIconButton("#2E241E", "#4A3C34", "Widget boyunu içerik kadar küçült/sığdır");
            btnFit.Margin = new Thickness(0, 4, 0, 0);
            btnFit.Padding = new Thickness(10, 7, 10, 7);
            btnFit.Content = new TextBlock { Text = "📐 Widget Yüksekliğini İçeriğe Sığdır", Foreground = HexBrush("#FAF6F2"), FontSize = 11.5, FontWeight = FontWeights.Medium };
            btnFit.Click += (s, e) =>
            {
                FitHeightToContentIfCompact();
                ShowToast("Yükseklik içeriğe göre optimize edildi");
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
                Text = "📅 Google Takvim Widget'ı",
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap
            });
            calCompanionInfo.Children.Add(new TextBlock
            {
                Text = "Aylık takvim ve günlük etkinlik/sınav takibi için ayrı masaüstü penceresi",
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
                "Ayrı Google Takvim masaüstü widget'ını aç veya kapat");
            btnToggleCalWidget.Padding = new Thickness(11, 5, 11, 5);
            btnToggleCalWidget.MinWidth = 72;
            btnToggleCalWidget.VerticalAlignment = VerticalAlignment.Center;
            TextBlock txtToggleCalWidget = new TextBlock
            {
                Text = showCalendarWidget ? "✓ Açık" : "Kapalı",
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
                txtToggleCalWidget.Text = showCalendarWidget ? "✓ Açık" : "Kapalı";
                txtToggleCalWidget.Foreground = HexBrush(showCalendarWidget ? "#FAF6F2" : "#8E7F74");
            };
            Grid.SetColumn(btnToggleCalWidget, 1);
            calCompanionTop.Children.Add(btnToggleCalWidget);
            calCompanionStack.Children.Add(calCompanionTop);

            calCompanionStack.Children.Add(new TextBlock
            {
                Text = "Google Takvim Gizli iCal (.ics) Adresi",
                Foreground = HexBrush("#B8A99E"),
                FontSize = 10.5,
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
                ToolTip = "Google Takvim Ayarları -> 'iCal biçimindeki gizli adres' (.ics) linkini buraya yapıştırabilirsiniz"
            };
            Grid.SetColumn(txtIcalUrl, 0);
            calIcalRow.Children.Add(txtIcalUrl);

            Button btnSaveIcal = CreateStyledIconButton("#2E241E", "#4A3C34", "Gizli iCal (.ics) adresini kaydet ve senkronize et");
            btnSaveIcal.Margin = new Thickness(6, 0, 0, 0);
            btnSaveIcal.Padding = new Thickness(10, 4, 10, 4);
            btnSaveIcal.Content = new TextBlock { Text = "🔗 Kaydet", Foreground = HexBrush("#E6DDD6"), FontSize = 10.5, FontWeight = FontWeights.SemiBold };
            btnSaveIcal.Click += (s, e) =>
            {
                calendarSecretIcalUrl = txtIcalUrl.Text.Trim();
                SaveSettings();
                Task.Run(() => LoadAndSyncCalendarEventsAsync(true));
                ShowToast("Google Takvim bağlantısı kaydedildi ve yenileniyor...");
            };
            Grid.SetColumn(btnSaveIcal, 1);
            calIcalRow.Children.Add(btnSaveIcal);
            calCompanionStack.Children.Add(calIcalRow);

            calCompanionCard.Child = calCompanionStack;
            tabPanels[1].Children.Add(calCompanionCard);

            // Standalone Timer Widgeti Companion Card
            Border timerCompanionCard = new Border
            {
                CornerRadius = new CornerRadius(11),
                Background = HexBrush(showTimerWidget ? "#2C221C" : "#221C18"),
                BorderBrush = HexBrush(showTimerWidget ? accentColorHex : "#3B302A"),
                BorderThickness = new Thickness(1.2),
                Padding = new Thickness(13, 11, 13, 11),
                Margin = new Thickness(0, 0, 0, 10)
            };
            StackPanel timerCompStack = new StackPanel();
            Grid timerCompanionTop = new Grid();
            timerCompanionTop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            timerCompanionTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel timerCompanionInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            timerCompanionInfo.Children.Add(new TextBlock
            {
                Text = "⏱ Timer Widgeti",
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap
            });
            timerCompanionInfo.Children.Add(new TextBlock
            {
                Text = "Zamanlayıcı, otomatik oyun algılayan Eğlence Modu, Pomodoro ve yıllık aktivite haritası",
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
                "Ayrı Timer Widgeti penceresini aç veya kapat");
            btnToggleTimerWidget.Padding = new Thickness(11, 5, 11, 5);
            btnToggleTimerWidget.MinWidth = 72;
            btnToggleTimerWidget.VerticalAlignment = VerticalAlignment.Center;
            TextBlock txtToggleTimerWidget = new TextBlock
            {
                Text = showTimerWidget ? "✓ Açık" : "Kapalı",
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
                txtToggleTimerWidget.Text = showTimerWidget ? "✓ Açık" : "Kapalı";
                txtToggleTimerWidget.Foreground = HexBrush(showTimerWidget ? "#FAF6F2" : "#8E7F74");
            };
            Grid.SetColumn(btnToggleTimerWidget, 1);
            timerCompanionTop.Children.Add(btnToggleTimerWidget);
            timerCompStack.Children.Add(timerCompanionTop);
            timerCompanionCard.Child = timerCompStack;
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
            StackPanel tbDevStack = new StackPanel();
            Grid tbDevTop = new Grid();
            tbDevTop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tbDevTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel tbDevInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            tbDevInfo.Children.Add(new TextBlock
            {
                Text = "🔋 Görev Çubuğu Cihaz Şarjları",
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap
            });
            tbDevInfo.Children.Add(new TextBlock
            {
                Text = "Tespit edilen kablosuz kulaklık, fare ve kumanda şarj yüzdelerini görev çubuğunun sağında gösterir",
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
                "Görev çubuğunda kablosuz cihaz şarj yüzdelerini aç veya kapat");
            btnToggleTbDev.Padding = new Thickness(11, 5, 11, 5);
            btnToggleTbDev.MinWidth = 72;
            btnToggleTbDev.VerticalAlignment = VerticalAlignment.Center;
            TextBlock txtToggleTbDev = new TextBlock
            {
                Text = showTaskbarDevices ? "✓ Açık" : "Kapalı",
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
                txtToggleTbDev.Text = showTaskbarDevices ? "✓ Açık" : "Kapalı";
                txtToggleTbDev.Foreground = HexBrush(showTaskbarDevices ? "#FAF6F2" : "#8E7F74");
            };
            Grid.SetColumn(btnToggleTbDev, 1);
            tbDevTop.Children.Add(btnToggleTbDev);
            tbDevStack.Children.Add(tbDevTop);
            taskbarDevCard.Child = tbDevStack;
            tabPanels[1].Children.Add(taskbarDevCard);

            // Low Battery Notification Toggle Card (<= 10%)
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
                Text = "🔔 Düşük Pil Bildirimi (%10 ve altı)",
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap
            });
            lowBatInfo.Children.Add(new TextBlock
            {
                Text = "Kablosuz cihazların pili %10 veya altına düştüğünde Windows bildirimi gönderir (şarjda yeşil, %10 altında kırmızı görünür)",
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
                "Düşük pil bildirimlerini aç veya kapat");
            btnToggleLowBat.Padding = new Thickness(11, 5, 11, 5);
            btnToggleLowBat.MinWidth = 72;
            btnToggleLowBat.VerticalAlignment = VerticalAlignment.Center;
            TextBlock txtToggleLowBat = new TextBlock
            {
                Text = notifyLowBattery ? "✓ Açık" : "Kapalı",
                Foreground = HexBrush(notifyLowBattery ? "#FAF6F2" : "#8E7F74"),
                FontSize = 11,
                FontWeight = FontWeights.Bold
            };
            btnToggleLowBat.Content = txtToggleLowBat;
            btnToggleLowBat.Click += (s, e) =>
            {
                notifyLowBattery = !notifyLowBattery;
                SaveSettings();
                lowBatCard.Background = HexBrush(notifyLowBattery ? "#2C221C" : "#221C18");
                lowBatCard.BorderBrush = HexBrush(notifyLowBattery ? accentColorHex : "#3B302A");
                btnToggleLowBat.Background = HexBrush(notifyLowBattery ? "#38281E" : "#1D1815");
                btnToggleLowBat.BorderBrush = HexBrush(notifyLowBattery ? accentColorHex : "#3E322B");
                txtToggleLowBat.Text = notifyLowBattery ? "✓ Açık" : "Kapalı";
                txtToggleLowBat.Foreground = HexBrush(notifyLowBattery ? "#FAF6F2" : "#8E7F74");
            };
            Grid.SetColumn(btnToggleLowBat, 1);
            lowBatTop.Children.Add(btnToggleLowBat);
            lowBatCard.Child = lowBatTop;
            tabPanels[1].Children.Add(lowBatCard);

            // safeapricot.com Live Activity Heatmap Sync Card
            Border siteSyncCard = new Border
            {
                CornerRadius = new CornerRadius(11),
                Background = HexBrush(syncActivityToSite ? "#2C221C" : "#221C18"),
                BorderBrush = HexBrush(syncActivityToSite ? accentColorHex : "#3B302A"),
                BorderThickness = new Thickness(1.2),
                Padding = new Thickness(13, 11, 13, 11),
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid siteSyncTop = new Grid();
            siteSyncTop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            siteSyncTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel siteSyncInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            siteSyncInfo.Children.Add(new TextBlock
            {
                Text = "☁ safeapricot.com Aktivite Senkronizasyonu",
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap
            });
            siteSyncInfo.Children.Add(new TextBlock
            {
                Text = "Timer aktivite verilerini 15 dakikada bir (sadece değiştiğinde) sitendeki heatmap akışına otomatik gönderir",
                Foreground = HexBrush("#9E8E82"),
                FontSize = 10.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(siteSyncInfo, 0);
            siteSyncTop.Children.Add(siteSyncInfo);

            StackPanel siteSyncBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Button btnSyncNow = CreateStyledIconButton("#1D1815", "#3E322B", "Aktivite verisini hemen safeapricot.com'a gönder");
            btnSyncNow.Padding = new Thickness(9, 5, 9, 5);
            btnSyncNow.Margin = new Thickness(0, 0, 6, 0);
            btnSyncNow.Content = new TextBlock { Text = "↻ Gönder", Foreground = HexBrush("#E8DFD7"), FontSize = 10.5, FontWeight = FontWeights.SemiBold };
            btnSyncNow.Click += (s, e) =>
            {
                ShowToast("☁ Aktivite verisi gönderiliyor...");
                Task.Run(() => SyncTimerActivityToSiteAsync(true, true));
            };
            siteSyncBtns.Children.Add(btnSyncNow);

            Button btnToggleSiteSync = CreateStyledIconButton(
                syncActivityToSite ? "#38281E" : "#1D1815",
                syncActivityToSite ? accentColorHex : "#3E322B",
                "Otomatik site senkronizasyonunu aç veya kapat");
            btnToggleSiteSync.Padding = new Thickness(11, 5, 11, 5);
            btnToggleSiteSync.MinWidth = 68;
            TextBlock txtToggleSiteSync = new TextBlock
            {
                Text = syncActivityToSite ? "✓ Açık" : "Kapalı",
                Foreground = HexBrush(syncActivityToSite ? "#FAF6F2" : "#8E7F74"),
                FontSize = 11,
                FontWeight = FontWeights.Bold
            };
            btnToggleSiteSync.Content = txtToggleSiteSync;
            btnToggleSiteSync.Click += (s, e) =>
            {
                syncActivityToSite = !syncActivityToSite;
                SaveSettings();
                siteSyncCard.Background = HexBrush(syncActivityToSite ? "#2C221C" : "#221C18");
                siteSyncCard.BorderBrush = HexBrush(syncActivityToSite ? accentColorHex : "#3B302A");
                btnToggleSiteSync.Background = HexBrush(syncActivityToSite ? "#38281E" : "#1D1815");
                btnToggleSiteSync.BorderBrush = HexBrush(syncActivityToSite ? accentColorHex : "#3E322B");
                txtToggleSiteSync.Text = syncActivityToSite ? "✓ Açık" : "Kapalı";
                txtToggleSiteSync.Foreground = HexBrush(syncActivityToSite ? "#FAF6F2" : "#8E7F74");
                if (syncActivityToSite) Task.Run(() => SyncTimerActivityToSiteAsync(false, false));
            };
            siteSyncBtns.Children.Add(btnToggleSiteSync);
            Grid.SetColumn(siteSyncBtns, 1);
            siteSyncTop.Children.Add(siteSyncBtns);
            siteSyncCard.Child = siteSyncTop;
            tabPanels[1].Children.Add(siteSyncCard);

            // =========================================================
            // TAB 2: MODULE ORDER, VISIBILITY & TELEMETRY SUB-TOGGLES
            // =========================================================
            Border modulesOrderCard = CreateSettingsSectionCard("🧩 Ana Widget Modülleri", "Tut-sürükle veya ▲/▼ ile modülleri sıralayın, sağdaki butonla gizleyip açın");
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
                        ToolTip = "Sıralamayı değiştirmek için basılı tutup yukarı/aşağı sürükleyin"
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

                    Button btnUp = CreateStyledIconButton("#26201C", "#3E322B", "Yukarı Taşı");
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

                    Button btnDown = CreateStyledIconButton("#26201C", "#3E322B", "Aşağı Taşı");
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
                        "Bu modülü göster veya gizle");
                    btnToggle.Padding = new Thickness(9, 3, 9, 3);
                    btnToggle.MinWidth = 60;
                    btnToggle.Content = new TextBlock
                    {
                        Text = enabled ? "✓ Açık" : "Kapalı",
                        Foreground = HexBrush(enabled ? "#FAF6F2" : "#8E7F74"),
                        FontSize = 10.5,
                        FontWeight = FontWeights.SemiBold
                    };
                    btnToggle.Click += (s, e) =>
                    {
                        SetSectionEnabled(key, !IsSectionEnabled(key));
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
                    double rowH = Math.Max(30.0, modulesOrderList.ActualHeight / Math.Max(1, sectionOrder.Count));
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
            Border teleSubCard = CreateSettingsSectionCard("📊 Telemetri İçeriği", "Ana widget içindeki hangi donanım sensörlerinin gösterileceğini seçin");
            StackPanel teleSubStack = (StackPanel)teleSubCard.Child;

            UniformGrid teleSubGrid = new UniformGrid { Columns = 2 };
            Action rebuildTeleSubGrid = null;
            rebuildTeleSubGrid = () =>
            {
                teleSubGrid.Children.Clear();
                string[] tKeys = { "cpu", "ram", "gpu", "vram", "net", "disks" };
                string[] tLabels = {
                    "⚙ İşlemci (CPU)",
                    "📟 Bellek (RAM)",
                    "🎮 Ekran Kartı (GPU)",
                    "🎞 Video Belleği (VRAM)",
                    "⇄ Ağ Trafiği & Ping",
                    "💾 Diskler (C: & D:)"
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

            Button btnFitLayout = CreateStyledIconButton("#2E241E", "#4A3C34", "Widget boyunu seçili modüllere göre otomatik sığdır");
            btnFitLayout.Margin = new Thickness(0, 4, 0, 0);
            btnFitLayout.Padding = new Thickness(10, 7, 10, 7);
            btnFitLayout.Content = new TextBlock { Text = "📐 Widget Yüksekliğini Modüllere Göre Sığdır", Foreground = HexBrush("#FAF6F2"), FontSize = 11.5, FontWeight = FontWeights.Medium };
            btnFitLayout.Click += (s, e) =>
            {
                FitHeightToContentIfCompact();
                ShowToast("Yükseklik aktif modüllere göre optimize edildi");
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

            double svWidth = 322;
            double svHeight = 96;

            // 1. 2D Saturation-Brightness (SV) Box
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

            // 2. Rainbow Hue Bar
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

            // Position thumbs initially without triggering redundant save
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
        // MATTER / WiZ SMART BULB LAN CONTROLLER
        // ==========================================
        private StackPanel CreateBulbInlineSpectrumPanel()
        {
            StackPanel panel = new StackPanel
            {
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 8, 0, 0),
                Tag = "Interactive"
            };

            Border innerBox = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = HexBrush("#211B18"),
                BorderBrush = HexBrush("#3E322C"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 7, 8, 8),
                Tag = "Interactive"
            };

            StackPanel innerStack = new StackPanel { Tag = "Interactive" };

            // Header row: title + active hex preview + 6500K cool white quick button + close
            Grid topRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel leftInfo = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            bulbSpectrumPreviewSwatch = new Border
            {
                Width = 12,
                Height = 12,
                CornerRadius = new CornerRadius(3),
                Background = HexBrush(bulbCustomColorHex),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            txtBulbCustomHexLabel = new TextBlock
            {
                Text = "Renk Spektrumu (" + bulbCustomColorHex + ")",
                Foreground = HexBrush("#D8CBC2"),
                FontSize = 10.5,
                FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center
            };
            leftInfo.Children.Add(bulbSpectrumPreviewSwatch);
            leftInfo.Children.Add(txtBulbCustomHexLabel);
            Grid.SetColumn(leftInfo, 0);
            topRow.Children.Add(leftInfo);

            Button btnCoolWhite = CreateStyledIconButton("#2B231F", "#4A3C34", "Soğuk Beyaz (6500K) uygula");
            btnCoolWhite.Padding = new Thickness(6, 2, 6, 2);
            btnCoolWhite.Margin = new Thickness(0, 0, 4, 0);
            btnCoolWhite.Content = new TextBlock { Text = "❄ 6500K", Foreground = HexBrush("#D8E8F8"), FontSize = 9.5 };
            btnCoolWhite.Click += (s, e) =>
            {
                bulbTemp = 6500;
                bulbIsCustomColor = false;
                bulbOn = true;
                UpdateBulbPowerButtonUi();
                UpdateBulbSceneButtonsUi();
                int targetDim = (int)Math.Round(sliderBulbDim.Value);
                Task.Run(() => SendWizPilotCommand(true, targetDim, 6500));
            };
            Grid.SetColumn(btnCoolWhite, 1);
            topRow.Children.Add(btnCoolWhite);

            Button btnCloseSpec = CreateStyledIconButton("#2B231F", "#4A3C34", "Spektrumu gizle");
            btnCloseSpec.Padding = new Thickness(6, 1, 6, 1);
            btnCloseSpec.Content = new TextBlock { Text = "✕", Foreground = HexBrush("#B8A99E"), FontSize = 9.5 };
            btnCloseSpec.Click += (s, e) => panel.Visibility = Visibility.Collapsed;
            Grid.SetColumn(btnCloseSpec, 2);
            topRow.Children.Add(btnCloseSpec);

            innerStack.Children.Add(topRow);

            // Responsive 2D Saturation-Brightness + Rainbow Hue Picker
            double currentH = 22, currentS = 0.82, currentV = 1.0;
            try
            {
                Color initC = ParseColor(bulbCustomColorHex);
                RgbToHsv(initC.R, initC.G, initC.B, out currentH, out currentS, out currentV);
            }
            catch { }

            double svHeight = 82;
            Border svBorder = new Border
            {
                Height = svHeight,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(7),
                BorderBrush = HexBrush("#4A3C34"),
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                Cursor = Cursors.Cross,
                Margin = new Thickness(0, 0, 0, 6),
                Tag = "Interactive"
            };

            Grid svGrid = new Grid();
            Border hueBaseLayer = new Border { Background = new SolidColorBrush(HsvToRgb(currentH, 1.0, 1.0)) };

            LinearGradientBrush whiteHoriz = new LinearGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255), new Point(0, 0), new Point(1, 0));
            whiteHoriz.Freeze();
            Border satLayer = new Border { Background = whiteHoriz };

            LinearGradientBrush blackVert = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, new Point(0, 0), new Point(0, 1));
            blackVert.Freeze();
            Border valLayer = new Border { Background = blackVert };

            Canvas svCanvas = new Canvas { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            Border svThumb = new Border
            {
                Width = 13,
                Height = 13,
                CornerRadius = new CornerRadius(6.5),
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
                Height = 14,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(7),
                BorderBrush = HexBrush("#4A3C34"),
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 0, 6),
                Tag = "Interactive"
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

            Canvas hueCanvas = new Canvas { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, Background = rainbow };
            Border hueThumb = new Border
            {
                Width = 8,
                Height = 14,
                CornerRadius = new CornerRadius(3),
                Background = Brushes.White,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                IsHitTestVisible = false
            };
            hueCanvas.Children.Add(hueThumb);
            hueBarBorder.Child = hueCanvas;

            Action updateThumbsOnly = () =>
            {
                double w = svBorder.ActualWidth > 20 ? svBorder.ActualWidth : 290;
                double h = svBorder.ActualHeight > 20 ? svBorder.ActualHeight : svHeight;
                double hw = hueBarBorder.ActualWidth > 20 ? hueBarBorder.ActualWidth : w;

                Canvas.SetLeft(svThumb, Math.Max(0, Math.Min(w - 13, currentS * w - 6.5)));
                Canvas.SetTop(svThumb, Math.Max(0, Math.Min(h - 13, (1.0 - currentV) * h - 6.5)));
                Canvas.SetLeft(hueThumb, Math.Max(0, Math.Min(hw - 8, (currentH / 360.0) * hw - 4)));
                hueBaseLayer.Background = new SolidColorBrush(HsvToRgb(currentH, 1.0, 1.0));
            };

            svBorder.SizeChanged += (s, e) => updateThumbsOnly();
            hueBarBorder.SizeChanged += (s, e) => updateThumbsOnly();

            syncBulbSpectrumPosition = (hexToSync) =>
            {
                try
                {
                    Color c = ParseColor(hexToSync);
                    RgbToHsv(c.R, c.G, c.B, out currentH, out currentS, out currentV);
                    updateThumbsOnly();
                    if (bulbSpectrumPreviewSwatch != null) bulbSpectrumPreviewSwatch.Background = HexBrush(hexToSync);
                    if (bulbCustomColorDot != null) bulbCustomColorDot.Background = HexBrush(hexToSync);
                    if (txtBulbCustomHexLabel != null) txtBulbCustomHexLabel.Text = "Renk Spektrumu (" + hexToSync + ")";
                }
                catch { }
            };

            Action<bool> applyPickedBulbColor = (forceSendNow) =>
            {
                updateThumbsOnly();
                Color picked = HsvToRgb(currentH, currentS, currentV);
                string hex = string.Format("#{0:X2}{1:X2}{2:X2}", picked.R, picked.G, picked.B);
                bulbCustomColorHex = hex;
                bulbIsCustomColor = true;
                bulbOn = true;

                if (bulbSpectrumPreviewSwatch != null) bulbSpectrumPreviewSwatch.Background = HexBrush(hex);
                if (bulbCustomColorDot != null) bulbCustomColorDot.Background = HexBrush(hex);
                if (txtBulbCustomHexLabel != null) txtBulbCustomHexLabel.Text = "Renk Spektrumu (" + hex + ")";
                UpdateBulbPowerButtonUi();
                UpdateBulbSceneButtonsUi();

                if (forceSendNow || (DateTime.Now - lastBulbCommandSent).TotalMilliseconds > 70)
                {
                    lastBulbCommandSent = DateTime.Now;
                    int dimCopy = (int)Math.Round(sliderBulbDim.Value);
                    string hexCopy = hex;
                    Task.Run(() => SendWizPilotCustomHexCommand(true, dimCopy, hexCopy));
                    if (forceSendNow) SaveSettings();
                }
            };

            Action<Point, bool> handleSvMouse = (pt, isFinal) =>
            {
                double w = svBorder.ActualWidth > 20 ? svBorder.ActualWidth : 290;
                double h = svBorder.ActualHeight > 20 ? svBorder.ActualHeight : svHeight;
                currentS = Math.Max(0.0, Math.Min(1.0, pt.X / w));
                currentV = Math.Max(0.10, Math.Min(1.0, 1.0 - (pt.Y / h)));
                applyPickedBulbColor(isFinal);
            };

            svBorder.MouseLeftButtonDown += (s, e) =>
            {
                svBorder.CaptureMouse();
                handleSvMouse(e.GetPosition(svBorder), true);
                e.Handled = true;
            };
            svBorder.MouseMove += (s, e) =>
            {
                if (svBorder.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
                {
                    handleSvMouse(e.GetPosition(svBorder), false);
                    e.Handled = true;
                }
            };
            svBorder.MouseLeftButtonUp += (s, e) =>
            {
                if (svBorder.IsMouseCaptured)
                {
                    handleSvMouse(e.GetPosition(svBorder), true);
                    svBorder.ReleaseMouseCapture();
                }
                e.Handled = true;
            };

            Action<Point, bool> handleHueMouse = (pt, isFinal) =>
            {
                double hw = hueBarBorder.ActualWidth > 20 ? hueBarBorder.ActualWidth : 290;
                currentH = Math.Max(0.0, Math.Min(360.0, (pt.X / hw) * 360.0));
                applyPickedBulbColor(isFinal);
            };

            hueBarBorder.MouseLeftButtonDown += (s, e) =>
            {
                hueBarBorder.CaptureMouse();
                handleHueMouse(e.GetPosition(hueBarBorder), true);
                e.Handled = true;
            };
            hueBarBorder.MouseMove += (s, e) =>
            {
                if (hueBarBorder.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
                {
                    handleHueMouse(e.GetPosition(hueBarBorder), false);
                    e.Handled = true;
                }
            };
            hueBarBorder.MouseLeftButtonUp += (s, e) =>
            {
                if (hueBarBorder.IsMouseCaptured)
                {
                    handleHueMouse(e.GetPosition(hueBarBorder), true);
                    hueBarBorder.ReleaseMouseCapture();
                }
                e.Handled = true;
            };

            innerStack.Children.Add(svBorder);
            innerStack.Children.Add(hueBarBorder);

            // Quick Favorite RGB Swatches
            string[] quickColors = { "#FF2A2A", "#FF7A18", "#FFB800", "#10E868", "#00E5FF", "#2979FF", "#AA00FF", "#FF1493" };
            string[] quickNames = { "Kırmızı", "Kehribar Turuncu", "Altın Sarısı", "Zümrüt Yeşili", "Buz Camgöbeği", "Derin Mavi", "Neon Mor", "Fuşya Pembe" };
            UniformGrid quickGrid = new UniformGrid { Columns = 8, Margin = new Thickness(0, 2, 0, 0) };
            for (int i = 0; i < quickColors.Length; i++)
            {
                string qHex = quickColors[i];
                Border sw = new Border
                {
                    Width = 22,
                    Height = 16,
                    CornerRadius = new CornerRadius(5),
                    Background = HexBrush(qHex),
                    BorderBrush = HexBrush("#5A4A42"),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    ToolTip = quickNames[i],
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                sw.MouseLeftButtonDown += (s, e) =>
                {
                    bulbCustomColorHex = qHex;
                    if (syncBulbSpectrumPosition != null) syncBulbSpectrumPosition(qHex);
                    bulbIsCustomColor = true;
                    bulbOn = true;
                    UpdateBulbPowerButtonUi();
                    UpdateBulbSceneButtonsUi();
                    SaveSettings();
                    int dimCopy = (int)Math.Round(sliderBulbDim.Value);
                    Task.Run(() => SendWizPilotCustomHexCommand(true, dimCopy, qHex));
                    e.Handled = true;
                };
                quickGrid.Children.Add(sw);
            }
            innerStack.Children.Add(quickGrid);

            innerBox.Child = innerStack;
            panel.Children.Add(innerBox);
            return panel;
        }

        private void UpdateBulbSceneButtonsUi()
        {
            Dispatcher.Invoke(() =>
            {
                for (int i = 0; i < 4; i++)
                {
                    if (btnScenes[i] == null) continue;
                    bool isSelected = (i == 3 && bulbIsCustomColor);
                    btnScenes[i].BorderBrush = HexBrush(isSelected ? bulbCustomColorHex : "#443831");
                    btnScenes[i].Background = HexBrush(isSelected ? "#332620" : "#26201D");
                }
            });
        }

        private void SliderBulbDim_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isUpdatingBulbUi) return;
            int val = (int)Math.Round(e.NewValue);
            bulbDimming = val;
            if (txtBulbPercent != null) txtBulbPercent.Text = "%" + val;

            if (!bulbOn)
            {
                bulbOn = true;
                UpdateBulbPowerButtonUi();
            }

            if ((DateTime.Now - lastBulbCommandSent).TotalMilliseconds > 90)
            {
                lastBulbCommandSent = DateTime.Now;
                if (bulbIsCustomColor)
                {
                    string hexCopy = bulbCustomColorHex;
                    Task.Run(() => SendWizPilotCustomHexCommand(true, val, hexCopy));
                }
                else
                {
                    int tempCopy = bulbTemp;
                    Task.Run(() => SendWizPilotCommand(true, val, tempCopy));
                }
            }
        }

        private void ToggleBulbPower()
        {
            bulbOn = !bulbOn;
            UpdateBulbPowerButtonUi();
            bool targetState = bulbOn;
            int targetDim = bulbDimming;
            if (targetState && bulbIsCustomColor)
            {
                string hexCopy = bulbCustomColorHex;
                Task.Run(() => SendWizPilotCustomHexCommand(true, targetDim, hexCopy));
            }
            else
            {
                int targetTemp = bulbTemp;
                Task.Run(() => SendWizPilotCommand(targetState, targetDim, targetTemp));
            }
        }

        private void UpdateBulbPowerButtonUi()
        {
            Dispatcher.Invoke(() =>
            {
                if (btnBulbPower == null) return;
                if (bulbOn)
                {
                    btnBulbPower.Background = HexBrush("#36261D");
                    btnBulbPower.BorderBrush = HexBrush(accentColorHex);
                    btnBulbPower.Content = new TextBlock { Text = "⚡ Açık", Foreground = HexBrush(accentColorHex), FontSize = 11.5, FontWeight = FontWeights.SemiBold };
                }
                else
                {
                    btnBulbPower.Background = HexBrush("#241E1B");
                    btnBulbPower.BorderBrush = HexBrush("#443832");
                    btnBulbPower.Content = new TextBlock { Text = "⭕ Kapalı", Foreground = HexBrush("#B8A99E"), FontSize = 11.5, FontWeight = FontWeights.SemiBold };
                }
            });
        }

        private void SendWizPilotCustomHexCommand(bool state, int dimming, string hexColor)
        {
            try
            {
                if (sharedUdpClient == null) return;
                string payload;
                if (state)
                {
                    Color c = ParseColor(hexColor);
                    payload = string.Format("{{\"method\":\"setPilot\",\"params\":{{\"state\":true,\"dimming\":{0},\"r\":{1},\"g\":{2},\"b\":{3}}}}}",
                        Math.Max(10, Math.Min(100, dimming)), c.R, c.G, c.B);
                }
                else
                {
                    payload = "{\"method\":\"setPilot\",\"params\":{\"state\":false}}";
                }
                byte[] bytes = Encoding.UTF8.GetBytes(payload);
                lock (udpLock)
                {
                    sharedUdpClient.Send(bytes, bytes.Length, bulbIp, 38899);
                }
            }
            catch { }
        }

        private void SendWizPilotCommand(bool state, int dimming, int temp)
        {
            try
            {
                if (sharedUdpClient == null) return;
                string payload;
                if (state)
                {
                    if (temp > 0)
                        payload = string.Format("{{\"method\":\"setPilot\",\"params\":{{\"state\":true,\"dimming\":{0},\"temp\":{1}}}}}", Math.Max(10, Math.Min(100, dimming)), temp);
                    else
                        payload = string.Format("{{\"method\":\"setPilot\",\"params\":{{\"state\":true,\"dimming\":{0}}}}}", Math.Max(10, Math.Min(100, dimming)));
                }
                else
                {
                    payload = "{\"method\":\"setPilot\",\"params\":{\"state\":false}}";
                }
                byte[] bytes = Encoding.UTF8.GetBytes(payload);
                lock (udpLock)
                {
                    sharedUdpClient.Send(bytes, bytes.Length, bulbIp, 38899);
                }
            }
            catch { }
        }

        private void QueryWizBulbStateAsync()
        {
            try
            {
                if (sharedUdpClient == null) return;
                byte[] msg = Encoding.UTF8.GetBytes("{\"method\":\"getPilot\",\"params\":{}}");
                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] resp;
                lock (udpLock)
                {
                    sharedUdpClient.Send(msg, msg.Length, new IPEndPoint(IPAddress.Parse(bulbIp), 38899));
                    resp = sharedUdpClient.Receive(ref remote);
                }
                string json = Encoding.UTF8.GetString(resp);
                bulbIp = remote.Address.ToString();

                bool state = json.Contains("\"state\":true");
                int dim = bulbDimming;
                Match mDim = Regex.Match(json, "\"dimming\"\\s*:\\s*(\\d+)");
                if (mDim.Success) int.TryParse(mDim.Groups[1].Value, out dim);

                Match mR = Regex.Match(json, "\"r\"\\s*:\\s*(\\d+)");
                Match mG = Regex.Match(json, "\"g\"\\s*:\\s*(\\d+)");
                Match mB = Regex.Match(json, "\"b\"\\s*:\\s*(\\d+)");
                bool hasRgb = mR.Success && mG.Success && mB.Success;
                string detectedHex = null;
                if (hasRgb)
                {
                    int rVal, gVal, bVal;
                    if (int.TryParse(mR.Groups[1].Value, out rVal) &&
                        int.TryParse(mG.Groups[1].Value, out gVal) &&
                        int.TryParse(mB.Groups[1].Value, out bVal))
                    {
                        detectedHex = string.Format("#{0:X2}{1:X2}{2:X2}",
                            Math.Max(0, Math.Min(255, rVal)),
                            Math.Max(0, Math.Min(255, gVal)),
                            Math.Max(0, Math.Min(255, bVal)));
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    bulbOn = state;
                    bulbDimming = dim;
                    if (detectedHex != null)
                    {
                        bulbIsCustomColor = true;
                        bulbCustomColorHex = detectedHex;
                        if (syncBulbSpectrumPosition != null) syncBulbSpectrumPosition(detectedHex);
                    }
                    else if (json.Contains("\"temp\""))
                    {
                        bulbIsCustomColor = false;
                    }
                    isUpdatingBulbUi = true;
                    if (sliderBulbDim != null) sliderBulbDim.Value = Math.Max(10, Math.Min(100, dim));
                    if (txtBulbPercent != null) txtBulbPercent.Text = "%" + dim;
                    isUpdatingBulbUi = false;
                    UpdateBulbPowerButtonUi();
                    UpdateBulbSceneButtonsUi();
                    txtBulbStatus.Text = "Bağlı (" + bulbIp + ")";
                });
            }
            catch { }
        }

        // ==========================================
        // MONITOR DDC/CI HARDWARE BRIGHTNESS ENGINE
        // ==========================================
        private void UpdateMonitorLinkButtonUi()
        {
            if (btnMonitorLink == null) return;
            if (monitorLinkSync)
            {
                btnMonitorLink.Background = HexBrush("#36261D");
                btnMonitorLink.BorderBrush = HexBrush(accentColorHex);
                btnMonitorLink.Content = new TextBlock { Text = "🔗 Birlikte", Foreground = HexBrush(accentColorHex), FontSize = 10, FontWeight = FontWeights.SemiBold };
            }
            else
            {
                btnMonitorLink.Background = HexBrush("#26201D");
                btnMonitorLink.BorderBrush = HexBrush("#443831");
                btnMonitorLink.Content = new TextBlock { Text = "🔗 Ayrı", Foreground = HexBrush("#B8A99E"), FontSize = 10 };
            }
        }

        private void AttachMonitorSliderWheel(Slider slider, int idx)
        {
            slider.PreviewMouseWheel += (s, e) =>
            {
                e.Handled = true;
                int step = e.Delta > 0 ? 5 : -5;
                int newVal = Math.Max(0, Math.Min(100, (int)Math.Round(slider.Value) + step));
                SetMonitorBrightnessFromUi(idx, newVal, monitorLinkSync);
            };
        }

        private void OnMonitorSliderValueChanged(int idx, double newVal)
        {
            if (isUpdatingMonitorUi) return;
            int val = Math.Max(0, Math.Min(100, (int)Math.Round(newVal)));
            SetMonitorBrightnessFromUi(idx, val, monitorLinkSync);
        }

        private void SetMonitorBrightnessFromUi(int sourceIdx, int val, bool applyToAll)
        {
            isUpdatingMonitorUi = true;
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    if (applyToAll || i == sourceIdx)
                    {
                        if (sliderMonitors[i] != null && Math.Abs(sliderMonitors[i].Value - val) >= 0.5)
                            sliderMonitors[i].Value = val;
                        if (txtMonitorPercents[i] != null)
                            txtMonitorPercents[i].Text = "%" + val;

                        QueueMonitorBrightness(i, val);
                    }
                }
            }
            finally
            {
                isUpdatingMonitorUi = false;
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

        private void QueueMonitorBrightness(int idx, int val)
        {
            if (idx < 0 || idx >= monitorSlots.Count) return;
            PhysicalMonitorSlot slot = monitorSlots[idx];

            // Fast-path: When HDR is active, Windows SDR White Level (DisplayConfigSetDeviceInfo -18)
            // takes <0.1ms and bypasses the monitor's locked hardware DDC backlight!
            if (slot.HdrSdrMode && slot.TargetId != 0)
            {
                var pkt = new MonitorDdcNative.DISPLAYCONFIG_SET_SDR_WHITE_LEVEL();
                pkt.header.type = -18; // DISPLAYCONFIG_DEVICE_INFO_SET_SDR_WHITE_LEVEL
                pkt.header.size = (uint)Marshal.SizeOf(typeof(MonitorDdcNative.DISPLAYCONFIG_SET_SDR_WHITE_LEVEL));
                pkt.header.adapterId = slot.AdapterId;
                pkt.header.id = slot.TargetId;
                pkt.SDRWhiteLevel = (uint)(1000 + Math.Max(0, Math.Min(100, val)) * 50);
                pkt.finalValue = 1;
                if (MonitorDdcNative.DisplayConfigSetDeviceInfo(ref pkt) == 0)
                {
                    slot.CurrentBrightness = val;
                    return;
                }
            }

            // Built-in panel WMI or external DDC/CI background worker
            slot.PendingBrightness = val;
            if (Interlocked.CompareExchange(ref slot.WorkerBusy, 1, 0) == 0)
            {
                Task.Run(() => RunMonitorDdcWorker(slot));
            }
        }

        private bool TrySetPhysicalDdcBrightness(PhysicalMonitorSlot slot, uint rawVal)
        {
            if (slot.Handle == IntPtr.Zero) return false;
            if (slot.PreferHighLevelDdc)
            {
                if (MonitorDdcNative.SetMonitorBrightness(slot.Handle, rawVal)) return true;
                if (MonitorDdcNative.SetVCPFeature(slot.Handle, 0x10, rawVal))
                {
                    slot.PreferHighLevelDdc = false;
                    return true;
                }
            }
            else
            {
                if (MonitorDdcNative.SetVCPFeature(slot.Handle, 0x10, rawVal)) return true;
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
                    int nextVal = Interlocked.Exchange(ref slot.PendingBrightness, -1);
                    if (nextVal < 0) break;

                    if (slot.UseWmiBrightness)
                    {
                        MonitorDdcNative.SetWmiBrightness(slot.WmiInstanceName, nextVal);
                        slot.CurrentBrightness = nextVal;
                        Thread.Sleep(12);
                        continue;
                    }

                    uint span = (slot.MaxVal > slot.MinVal) ? (slot.MaxVal - slot.MinVal) : 100u;
                    uint rawVal = slot.MinVal + (uint)Math.Round((nextVal / 100.0) * span);

                    bool ok = false;
                    lock (monitorHandleLock)
                    {
                        ok = TrySetPhysicalDdcBrightness(slot, rawVal);
                    }
                    if (!ok)
                    {
                        DiscoverAndSyncMonitors(false);
                        lock (monitorHandleLock)
                        {
                            span = (slot.MaxVal > slot.MinVal) ? (slot.MaxVal - slot.MinVal) : 100u;
                            rawVal = slot.MinVal + (uint)Math.Round((nextVal / 100.0) * span);
                            TrySetPhysicalDdcBrightness(slot, rawVal);
                        }
                    }
                    slot.CurrentBrightness = nextVal;
                    Thread.Sleep(25);
                }
            }
            catch { }
            finally
            {
                Interlocked.Exchange(ref slot.WorkerBusy, 0);
                if (slot.PendingBrightness >= 0 && Interlocked.CompareExchange(ref slot.WorkerBusy, 1, 0) == 0)
                {
                    Task.Run(() => RunMonitorDdcWorker(slot));
                }
            }
        }

        private static string FormatFriendlyMonitorName(string rawDesc)
        {
            if (string.IsNullOrEmpty(rawDesc)) return "Monitör";
            string d = rawDesc.Trim();
            if (d.IndexOf("27Q-11", StringComparison.OrdinalIgnoreCase) >= 0) return "Lenovo 27Q-11";
            if (d.IndexOf("VX2428", StringComparison.OrdinalIgnoreCase) >= 0) return "ViewSonic VX2428";
            d = d.Replace("-FHD", "").Replace("-QHD", "").Replace("Generic PnP Monitor", "Monitör").Trim();
            return d;
        }

        private void DiscoverAndSyncMonitors(bool readHardwareValues)
        {
            lock (monitorHandleLock)
            {
                try
                {
                    // 1. Query Windows DisplayConfig for HDR state & SDRWhiteLevel (1000..6000)
                    Dictionary<string, PhysicalMonitorSlot> displayConfigByModel = new Dictionary<string, PhysicalMonitorSlot>(StringComparer.OrdinalIgnoreCase);
                    try
                    {
                        uint numPath, numMode;
                        if (MonitorDdcNative.GetDisplayConfigBufferSizes(2, out numPath, out numMode) == 0 && numPath > 0)
                        {
                            var paths = new MonitorDdcNative.DISPLAYCONFIG_PATH_INFO[numPath];
                            var modes = new MonitorDdcNative.DISPLAYCONFIG_MODE_INFO[numMode];
                            if (MonitorDdcNative.QueryDisplayConfig(2, ref numPath, paths, ref numMode, modes, IntPtr.Zero) == 0)
                            {
                                for (int i = 0; i < numPath; i++)
                                {
                                    var tName = new MonitorDdcNative.DISPLAYCONFIG_TARGET_DEVICE_NAME();
                                    tName.header.type = 2;
                                    tName.header.size = (uint)Marshal.SizeOf(typeof(MonitorDdcNative.DISPLAYCONFIG_TARGET_DEVICE_NAME));
                                    tName.header.adapterId = paths[i].targetInfo.adapterId;
                                    tName.header.id = paths[i].targetInfo.id;
                                    MonitorDdcNative.DisplayConfigGetDeviceInfo(ref tName);

                                    bool hdrEnabled = false;
                                    var adv2 = new MonitorDdcNative.DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2();
                                    adv2.header.type = 15;
                                    adv2.header.size = (uint)Marshal.SizeOf(typeof(MonitorDdcNative.DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2));
                                    adv2.header.adapterId = paths[i].targetInfo.adapterId;
                                    adv2.header.id = paths[i].targetInfo.id;
                                    if (MonitorDdcNative.DisplayConfigGetDeviceInfo(ref adv2) == 0)
                                    {
                                        hdrEnabled = (adv2.activeColorMode == 2);
                                    }
                                    else
                                    {
                                        var adv = new MonitorDdcNative.DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO();
                                        adv.header.type = 9;
                                        adv.header.size = (uint)Marshal.SizeOf(typeof(MonitorDdcNative.DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO));
                                        adv.header.adapterId = paths[i].targetInfo.adapterId;
                                        adv.header.id = paths[i].targetInfo.id;
                                        if (MonitorDdcNative.DisplayConfigGetDeviceInfo(ref adv) == 0)
                                        {
                                            hdrEnabled = ((adv.value & 0x2) != 0) && ((adv.value & 0x4) == 0) && ((adv.value & 0x8) == 0);
                                        }
                                    }

                                    int sdrPct = 60;
                                    if (hdrEnabled)
                                    {
                                        var sdr = new MonitorDdcNative.DISPLAYCONFIG_SDR_WHITE_LEVEL();
                                        sdr.header.type = 11;
                                        sdr.header.size = (uint)Marshal.SizeOf(typeof(MonitorDdcNative.DISPLAYCONFIG_SDR_WHITE_LEVEL));
                                        sdr.header.adapterId = paths[i].targetInfo.adapterId;
                                        sdr.header.id = paths[i].targetInfo.id;
                                        if (MonitorDdcNative.DisplayConfigGetDeviceInfo(ref sdr) == 0 && sdr.SDRWhiteLevel >= 1000)
                                        {
                                            sdrPct = (int)Math.Round(Math.Max(0, Math.Min(100, (sdr.SDRWhiteLevel - 1000.0) / 50.0)));
                                        }
                                    }

                                    string model = (tName.monitorFriendlyDeviceName ?? "").Trim();
                                    if (!string.IsNullOrEmpty(model))
                                    {
                                        displayConfigByModel[model] = new PhysicalMonitorSlot
                                        {
                                            AdapterId = paths[i].targetInfo.adapterId,
                                            TargetId = paths[i].targetInfo.id,
                                            HdrSdrMode = hdrEnabled,
                                            RawModel = model,
                                            FriendlyName = FormatFriendlyMonitorName(model),
                                            IsPrimary = (paths[i].sourceInfo.id == 0),
                                            CurrentBrightness = sdrPct
                                        };
                                    }
                                }
                            }
                        }
                    }
                    catch { }

                    List<MonitorDdcNative.WmiBrightnessInfo> wmiDisplays = MonitorDdcNative.QueryWmiBrightnessDisplays();
                    HashSet<string> usedWmi = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    // 2. Enumerate Physical Monitors (for primary/secondary ordering and DDC/CI / WMI fallback)
                    List<PhysicalMonitorSlot> found = new List<PhysicalMonitorSlot>();
                    MonitorDdcNative.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref MonitorDdcNative.RECT r, IntPtr data) =>
                    {
                        try
                        {
                            MonitorDdcNative.MONITORINFOEX mi = new MonitorDdcNative.MONITORINFOEX();
                            mi.cbSize = Marshal.SizeOf(typeof(MonitorDdcNative.MONITORINFOEX));
                            MonitorDdcNative.GetMonitorInfo(hMon, ref mi);
                            string gdiName = mi.szDevice ?? "";
                            bool isPrimary = (mi.dwFlags & 1) != 0 || (r.left == 0 && r.top == 0);

                            uint count = 0;
                            if (MonitorDdcNative.GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out count) && count > 0)
                            {
                                MonitorDdcNative.PHYSICAL_MONITOR[] arr = new MonitorDdcNative.PHYSICAL_MONITOR[count];
                                if (MonitorDdcNative.GetPhysicalMonitorsFromHMONITOR(hMon, count, arr))
                                {
                                    for (int i = 0; i < count; i++)
                                    {
                                        string rawDesc = (arr[i].szPhysicalMonitorDescription ?? "").Trim();
                                        PhysicalMonitorSlot s = new PhysicalMonitorSlot
                                        {
                                            Handle = arr[i].hPhysicalMonitor,
                                            GdiDeviceName = gdiName,
                                            RawModel = rawDesc,
                                            FriendlyName = FormatFriendlyMonitorName(rawDesc),
                                            IsPrimary = isPrimary
                                        };

                                        // Match with DisplayConfig target
                                        foreach (var kvp in displayConfigByModel)
                                        {
                                            if (rawDesc.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                kvp.Key.IndexOf(rawDesc, StringComparison.OrdinalIgnoreCase) >= 0)
                                            {
                                                s.AdapterId = kvp.Value.AdapterId;
                                                s.TargetId = kvp.Value.TargetId;
                                                s.HdrSdrMode = kvp.Value.HdrSdrMode;
                                                if (s.HdrSdrMode)
                                                {
                                                    s.CurrentBrightness = kvp.Value.CurrentBrightness;
                                                }
                                                break;
                                            }
                                        }

                                        if (!s.HdrSdrMode && s.Handle != IntPtr.Zero)
                                        {
                                            uint hlMin = 0, hlCur = 0, hlMax = 100;
                                            bool ddcOk = false;
                                            if (MonitorDdcNative.GetMonitorBrightness(s.Handle, out hlMin, out hlCur, out hlMax) && hlMax > hlMin)
                                            {
                                                s.MinVal = hlMin;
                                                s.MaxVal = hlMax;
                                                s.PreferHighLevelDdc = true;
                                                if (readHardwareValues)
                                                {
                                                    s.CurrentBrightness = Math.Max(0, Math.Min(100, (int)Math.Round(((hlCur - hlMin) * 100.0) / (hlMax - hlMin))));
                                                }
                                                ddcOk = true;
                                            }
                                            else
                                            {
                                                uint vct, cur, max;
                                                if (MonitorDdcNative.GetVCPFeatureAndVCPFeatureReply(s.Handle, 0x10, out vct, out cur, out max) && max > 0)
                                                {
                                                    s.MinVal = 0;
                                                    s.MaxVal = max;
                                                    s.PreferHighLevelDdc = false;
                                                    if (readHardwareValues)
                                                    {
                                                        s.CurrentBrightness = Math.Max(0, Math.Min(100, (int)Math.Round((cur * 100.0) / max)));
                                                    }
                                                    ddcOk = true;
                                                }
                                            }

                                            if (!ddcOk && wmiDisplays.Count > 0)
                                            {
                                                for (int w = 0; w < wmiDisplays.Count; w++)
                                                {
                                                    if (!usedWmi.Contains(wmiDisplays[w].InstanceName))
                                                    {
                                                        s.UseWmiBrightness = true;
                                                        s.WmiInstanceName = wmiDisplays[w].InstanceName;
                                                        usedWmi.Add(s.WmiInstanceName);
                                                        if (readHardwareValues) s.CurrentBrightness = wmiDisplays[w].CurrentBrightness;
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                        found.Add(s);
                                    }
                                }
                            }
                        }
                        catch { }
                        return true;
                    }, IntPtr.Zero);

                    // Sort so Primary (Lenovo 27Q-11) is always index 0, Secondary (ViewSonic VX2428) is index 1
                    found.Sort((a, b) => b.IsPrimary.CompareTo(a.IsPrimary));

                    if (monitorSlots.Count == 0)
                    {
                        foreach (var s in found) monitorSlots.Add(s);
                    }
                    else
                    {
                        for (int i = 0; i < monitorSlots.Count; i++)
                        {
                            if (monitorSlots[i].Handle != IntPtr.Zero)
                            {
                                try { MonitorDdcNative.DestroyPhysicalMonitor(monitorSlots[i].Handle); } catch { }
                                monitorSlots[i].Handle = IntPtr.Zero;
                            }
                        }
                        for (int i = 0; i < found.Count; i++)
                        {
                            if (i < monitorSlots.Count)
                            {
                                monitorSlots[i].Handle = found[i].Handle;
                                monitorSlots[i].GdiDeviceName = found[i].GdiDeviceName;
                                monitorSlots[i].AdapterId = found[i].AdapterId;
                                monitorSlots[i].TargetId = found[i].TargetId;
                                monitorSlots[i].HdrSdrMode = found[i].HdrSdrMode;
                                monitorSlots[i].UseWmiBrightness = found[i].UseWmiBrightness;
                                monitorSlots[i].WmiInstanceName = found[i].WmiInstanceName;
                                monitorSlots[i].PreferHighLevelDdc = found[i].PreferHighLevelDdc;
                                monitorSlots[i].MinVal = found[i].MinVal;
                                monitorSlots[i].MaxVal = found[i].MaxVal;
                                monitorSlots[i].RawModel = found[i].RawModel;
                                monitorSlots[i].FriendlyName = found[i].FriendlyName;
                                if (readHardwareValues) monitorSlots[i].CurrentBrightness = found[i].CurrentBrightness;
                            }
                            else
                            {
                                monitorSlots.Add(found[i]);
                            }
                        }
                    }

                    if (readHardwareValues)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            isUpdatingMonitorUi = true;
                            try
                            {
                                for (int i = 0; i < 2; i++)
                                {
                                    if (i < monitorSlots.Count)
                                    {
                                        if (rowMonitors[i] != null) rowMonitors[i].Visibility = Visibility.Visible;
                                        if (txtMonitorNames[i] != null) txtMonitorNames[i].Text = monitorSlots[i].FriendlyName;
                                        int bVal = Math.Max(0, Math.Min(100, monitorSlots[i].CurrentBrightness));
                                        if (sliderMonitors[i] != null) sliderMonitors[i].Value = bVal;
                                        if (txtMonitorPercents[i] != null) txtMonitorPercents[i].Text = "%" + bVal;
                                    }
                                    else
                                    {
                                        if (rowMonitors[i] != null) rowMonitors[i].Visibility = Visibility.Collapsed;
                                    }
                                }
                            }
                            finally
                            {
                                isUpdatingMonitorUi = false;
                            }
                        });
                    }
                }
                catch { }
            }
        }

        private void CleanupMonitorHandles()
        {
            lock (monitorHandleLock)
            {
                foreach (var s in monitorSlots)
                {
                    if (s.Handle != IntPtr.Zero)
                    {
                        try { MonitorDdcNative.DestroyPhysicalMonitor(s.Handle); } catch { }
                        s.Handle = IntPtr.Zero;
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
                QueryWizBulbStateAsync();
                DiscoverAndSyncMonitors(true);
                FetchWeatherAsync();
                UpdatePingAsync();
                PollWirelessDevicesAsync(true);
            });

            clipboardHistory.Add(new ClipboardEntry { Text = "OpenWidget Masaüstü Hazır • Kütahya", Time = DateTime.Now.ToString("HH:mm") });
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

            // 1. Fast Timer (250ms): Enforces Win+D Show Desktop immunity so widget immediately stays visible
            DispatcherTimer desktopGuardTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            desktopGuardTimer.Tick += (s, e) => CheckAndEnforceShowDesktopImmunity();
            desktopGuardTimer.Start();

            // 2. Telemetry Timer (1.5s): Zero-allocation updates for CPU, NVML GPU, RAM, Network, Disk, Mic
            DispatcherTimer fastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            fastTimer.Tick += (s, e) =>
            {
                UpdateClockAndUptime();
                UpdateTelemetryFast();
                SyncMicrophoneStateFromOs(false);
            };
            fastTimer.Start();

            // 3. Medium Timer (6s): Ping, WiZ Bulb & Wireless Peripherals Battery sync (every 18s)
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
                    if ((DateTime.Now - lastBulbCommandSent).TotalSeconds > 4)
                    {
                        QueryWizBulbStateAsync();
                    }
                    if (pollWirelessNow)
                    {
                        PollWirelessDevicesAsync(false);
                    }
                    if (trimNow)
                    {
                        TrimProcessMemory();
                    }
                });
            };
            mediumTimer.Start();

            // 4. Weather & Memory Trim Timer (15 mins)
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
            string[] aylar = { "", "Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara" };
            string[] gunler = { "Pazar", "Pzt", "Salı", "Çar", "Per", "Cuma", "Cmt" };

            ulong tickMs = GetTickCount64();
            TimeSpan up = TimeSpan.FromMilliseconds(tickMs);
            int totalHours = (int)up.TotalHours;

            txtClockAndUptime.Text = string.Format("{0:HH:mm} · {1} {2} {3}  Açık: {4}s {5}dk",
                now, now.Day, aylar[now.Month], gunler[(int)now.DayOfWeek], totalHours, up.Minutes);
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
            if (!showSecTelemetry) return;
            telemetryTickCounter++;

            // Direct C-API GPU poll (takes ~10 microseconds, zero process creation!)
            if (showTeleGpu || showTeleVram || showTeleCpu)
            {
                PollGpuDirectNvml();
            }

            // CPU Load & Temperature
            if (showTeleCpu)
            {
                double cpuVal = 8;
                try
                {
                    if (cpuCounter != null) cpuVal = Math.Round(cpuCounter.NextValue());
                }
                catch { }
                cpuVal = Math.Max(1, Math.Min(100, cpuVal));
                cpuHistory.Add(cpuVal);
                if (cpuHistory.Count > 20) cpuHistory.RemoveAt(0);

                double cTemp = ReadCpuTemperature(cpuVal);
                txtCpuDetail.Text = string.Format("Ryzen 5 7500F • {0:0}°C", cTemp);
                txtCpuPct.Text = "%" + (int)cpuVal;
                DrawSparkline(sparkCpu, cpuHistory, 46, 16);
            }

            // RAM (Reuses single sharedMemStatus buffer)
            if (showTeleRam)
            {
                try
                {
                    if (GlobalMemoryStatusEx(sharedMemStatus))
                    {
                        double totalGb = sharedMemStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                        double usedGb = (sharedMemStatus.ullTotalPhys - sharedMemStatus.ullAvailPhys) / (1024.0 * 1024.0 * 1024.0);
                        int pct = (int)sharedMemStatus.dwMemoryLoad;
                        txtRamDetail.Text = string.Format("{0:0.0} / {1:0} GB", usedGb, Math.Round(totalGb));
                        txtRamPct.Text = "%" + pct;
                        barRamFill.Width = Math.Max(3, (42.0 * pct) / 100.0);
                    }
                }
                catch { }
            }

            // GPU & VRAM UI
            if (showTeleGpu)
            {
                txtGpuDetail.Text = string.Format("{0} • {1:0}°C", gpuModelName, gpuTemp);
                txtGpuPct.Text = "%" + (int)gpuLoad;
                DrawSparkline(sparkGpu, gpuHistory, 46, 16);
            }

            if (showTeleVram)
            {
                int vramPct = vramTotalGb > 0 ? (int)Math.Round((vramUsedGb / vramTotalGb) * 100.0) : 0;
                txtVramDetail.Text = string.Format("{0:0.0} / {1:0} GB", vramUsedGb, vramTotalGb);
                txtVramPct.Text = "%" + vramPct;
                barVramFill.Width = Math.Max(3, (42.0 * vramPct) / 100.0);
            }

            // Network throughput (caches active NICs for 30s to avoid per-tick GetAdaptersAddresses allocations)
            if (showTeleNet)
            {
                try
                {
                    if (cachedActiveNics == null || (telemetryTickCounter % 20 == 1))
                    {
                        List<NetworkInterface> activeList = new List<NetworkInterface>(4);
                        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                        {
                            if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                            {
                                activeList.Add(ni);
                            }
                        }
                        cachedActiveNics = activeList.ToArray();
                    }

                    long totalRecv = 0;
                    long totalSent = 0;
                    for (int i = 0; i < cachedActiveNics.Length; i++)
                    {
                        var stats = cachedActiveNics[i].GetIPv4Statistics();
                        totalRecv += stats.BytesReceived;
                        totalSent += stats.BytesSent;
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
                catch { cachedActiveNics = null; }

                txtNetPing.Text = string.Format("{0:0} ms", lastPingMs);
                txtNetBadge.Text = lastPingMs < 40 ? "Harika" : (lastPingMs < 90 ? "İyi" : "Orta");
            }

            // Disks C & D (Queried every 15s / 10th tick or on first tick, since GB disk space changes slowly)
            if (showTeleDisks && (telemetryTickCounter == 1 || telemetryTickCounter % 10 == 0))
            {
                UpdateDiskRow(driveC, txtDiskCDetail, barDiskCFill, txtDiskCPct);
                UpdateDiskRow(driveD, txtDiskDDetail, barDiskDFill, txtDiskDPct);
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
                    detail.Text = string.Format("{0:0} GB Boş / {1:0} GB", freeGb, totalGb);
                    pctBlock.Text = "%" + usedPct + " Dolu";
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
        // WIRELESS PERIPHERALS BATTERY TELEMETRY
        // ==========================================
        private void PollWirelessDevicesAsync(bool forceRescan)
        {
            if (Interlocked.CompareExchange(ref wirelessPollingBusy, 1, 0) != 0) return;
            try
            {
                if (forceRescan || cachedScyroxPath == null || cachedHyperXPath == null)
                {
                    string sPath, hPath;
                    WirelessBatteryNative.FindHidDevicePaths(out sPath, out hPath);
                    if (sPath != null) cachedScyroxPath = sPath;
                    if (hPath != null) cachedHyperXPath = hPath;
                }

                // 1. Scyrox V8 Wireless Mouse (VID_3554 & PID_F5F7, Col05, 17-byte Report 0x08)
                string mouseDetail = "Alıcı Takılı Değil";
                string mousePctText = "—";
                int mouseBarPct = 0;
                bool mouseCharging = false;

                if (cachedScyroxPath != null)
                {
                    IntPtr hMouse = WirelessBatteryNative.CreateFile(cachedScyroxPath, 0xC0000000U, 3, IntPtr.Zero, 3, 0x40000000U, IntPtr.Zero);
                    if (hMouse != IntPtr.Zero && hMouse != new IntPtr(-1))
                    {
                        try
                        {
                            // Read hardware Polling Rate (Hz) from CompX EEPROM offset 0x0000 (Cmd 0x08)
                            byte[] txHz = new byte[17];
                            txHz[0] = 0x08;
                            txHz[1] = 0x08;
                            txHz[3] = 0x00;
                            txHz[4] = 0x00;
                            txHz[5] = 0x02;
                            txHz[16] = 0x43; // Checksum: 0x55 - (0x08 + 0x08 + 0x02) = 0x43
                            byte[] rxHz;
                            if (WirelessBatteryNative.OverlappedHidQuery(hMouse, txHz, out rxHz, 17, 100) && rxHz[0] == 0x08 && rxHz[1] == 0x08)
                            {
                                byte rateCode = rxHz[6];
                                if (rateCode == 0x01) lastScyroxHzText = "1000 Hz";
                                else if (rateCode == 0x02) lastScyroxHzText = "500 Hz";
                                else if (rateCode == 0x04) lastScyroxHzText = "250 Hz";
                                else if (rateCode == 0x08) lastScyroxHzText = "125 Hz";
                                else if (rateCode == 0x10) lastScyroxHzText = "2000 Hz";
                                else if (rateCode == 0x20) lastScyroxHzText = "4000 Hz";
                                else if (rateCode == 0x40) lastScyroxHzText = "8000 Hz";
                            }

                            byte[] tx = new byte[17];
                            tx[0] = 0x08;
                            tx[1] = 0x04;
                            tx[16] = 0x49;
                            byte[] rx;
                            if (WirelessBatteryNative.OverlappedHidQuery(hMouse, tx, out rx, 17, 130) && rx[0] == 0x08 && rx[1] == 0x04)
                            {
                                int pct = rx[6];
                                bool charging = rx[7] == 1;
                                int mv = (rx[8] << 8) | rx[9];
                                if (pct > 0 && pct <= 100)
                                {
                                    lastScyroxPct = pct;
                                    mouseBarPct = pct;
                                    mouseCharging = charging;
                                    mousePctText = "%" + pct;
                                    if (charging)
                                        mouseDetail = lastScyroxHzText + " • ⚡ Şarjda";
                                    else if (mv > 3000 && mv < 4500)
                                        mouseDetail = string.Format("{0} • {1:0.00}V", lastScyroxHzText, mv / 1000.0);
                                    else
                                        mouseDetail = lastScyroxHzText + " • Açık";
                                }
                                else if (lastScyroxPct > 0)
                                {
                                    mouseBarPct = lastScyroxPct;
                                    mousePctText = "%" + lastScyroxPct;
                                    mouseDetail = lastScyroxHzText + " • Uyku";
                                }
                            }
                            else if (lastScyroxPct > 0)
                            {
                                mouseBarPct = lastScyroxPct;
                                mousePctText = "%" + lastScyroxPct;
                                mouseDetail = lastScyroxHzText + " • Uyku";
                            }
                        }
                        finally
                        {
                            WirelessBatteryNative.CloseHandle(hMouse);
                        }
                    }
                    else
                    {
                        cachedScyroxPath = null;
                    }
                }

                // 2. HyperX Cloud III Wireless Headset (VID_03F0 & PID_05B7, MI_03 Col01, 62-byte Report 0x66)
                string headsetDetail = "2.4GHz • Açık";
                string headsetPctText = "—";
                int headsetBarPct = 0;
                bool headsetCharging = false;

                if (cachedHyperXPath != null)
                {
                    IntPtr hHeadset = WirelessBatteryNative.CreateFile(cachedHyperXPath, 0xC0000000U, 3, IntPtr.Zero, 3, 0x40000000U, IntPtr.Zero);
                    if (hHeadset != IntPtr.Zero && hHeadset != new IntPtr(-1))
                    {
                        try
                        {
                            byte[] tx = new byte[62];
                            tx[0] = 0x66;
                            tx[1] = 0x89;
                            byte[] rx;
                            if (WirelessBatteryNative.OverlappedHidQuery(hHeadset, tx, out rx, 62, 130) && rx[0] == 0x66 && rx[1] == 0x89)
                            {
                                int pct = (rx[4] > 0 && rx[4] <= 100) ? (int)rx[4] : ((rx[3] > 0 && rx[3] <= 100) ? (int)rx[3] : 0);
                                if (pct > 0)
                                {
                                    headsetBarPct = pct;
                                    headsetPctText = "%" + pct;

                                    bool isCharging = false;
                                    byte[] txChg = new byte[62];
                                    txChg[0] = 0x66;
                                    txChg[1] = 0x8A;
                                    byte[] rxChg;
                                    if (WirelessBatteryNative.OverlappedHidQuery(hHeadset, txChg, out rxChg, 62, 100) && rxChg[0] == 0x66 && rxChg[1] == 0x8A)
                                    {
                                        if (rxChg[4] == 1 || rxChg[3] == 1) isCharging = true;
                                    }
                                    headsetCharging = isCharging;
                                    headsetDetail = isCharging ? "Wireless • ⚡ Şarjda" : "2.4GHz • Açık";
                                }
                            }
                        }
                        finally
                        {
                            WirelessBatteryNative.CloseHandle(hHeadset);
                        }
                    }
                    else
                    {
                        cachedHyperXPath = null;
                    }
                }

                // 3. Wireless Controller (XInput 0..3 + WinMM DirectInput Fallback)
                bool padConnected = false;
                string padDetail = "Bağlı Değil";
                string padPctText = "Kapalı";
                int padBarPct = 0;
                bool padCharging = false;

                try
                {
                    for (uint i = 0; i < 4; i++)
                    {
                        WirelessBatteryNative.XINPUT_STATE st;
                        if (WirelessBatteryNative.XInputGetState(i, out st) == 0)
                        {
                            padConnected = true;
                            WirelessBatteryNative.XINPUT_BATTERY_INFORMATION bat;
                            if (WirelessBatteryNative.XInputGetBatteryInformation(i, 0, out bat) == 0)
                            {
                                if (bat.BatteryType == 1)
                                {
                                    padBarPct = 100;
                                    padCharging = true;
                                    padPctText = "%100";
                                    padDetail = "Bağlı • Kablolu / Şarjda";
                                }
                                else if (bat.BatteryType == 2 || bat.BatteryType == 3)
                                {
                                    if (bat.BatteryLevel == 3) { padBarPct = 100; padPctText = "%100"; padDetail = "Kablosuz • Tam Dolu"; }
                                    else if (bat.BatteryLevel == 2) { padBarPct = 65; padPctText = "%65"; padDetail = "Kablosuz • İyi Seviye"; }
                                    else if (bat.BatteryLevel == 1) { padBarPct = 30; padPctText = "%30"; padDetail = "Kablosuz • Düşük Pil"; }
                                    else { padBarPct = 10; padPctText = "%10"; padDetail = "Kablosuz • Çok Düşük!"; }
                                }
                                else
                                {
                                    padBarPct = 100;
                                    padPctText = "Açık";
                                    padDetail = "Bağlı • Aktif";
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
                        int numDevs = Math.Min(16, WirelessBatteryNative.joyGetNumDevs());
                        for (int i = 0; i < numDevs; i++)
                        {
                            WirelessBatteryNative.JOYINFOEX ji = new WirelessBatteryNative.JOYINFOEX();
                            ji.dwSize = Marshal.SizeOf(typeof(WirelessBatteryNative.JOYINFOEX));
                            ji.dwFlags = 0xFF;
                            if (WirelessBatteryNative.joyGetPosEx(i, ref ji) == 0)
                            {
                                padConnected = true;
                                padBarPct = 100;
                                padPctText = "Açık";
                                padDetail = "Bağlı • Kablosuz Aktif";
                                break;
                            }
                        }
                    }
                    catch { }
                }

                Dispatcher.Invoke(() =>
                {
                    bool visChanged = false;

                    bool showMouse = mouseBarPct > 0;
                    lastMousePctState = showMouse ? mouseBarPct : -1;
                    lastMouseChargingState = mouseCharging;
                    if (rowMouse != null)
                    {
                        Visibility targetMouseVis = showMouse ? Visibility.Visible : Visibility.Collapsed;
                        if (rowMouse.Visibility != targetMouseVis) { rowMouse.Visibility = targetMouseVis; visChanged = true; }
                        if (showMouse)
                        {
                            txtMouseDetail.Text = mouseDetail;
                            txtMousePct.Text = mousePctText;
                            barMouseFill.Width = Math.Max(3, (42.0 * mouseBarPct) / 100.0);
                            CheckAndNotifyLowBattery("mouse:scyrox", "Scyrox V8", mouseBarPct, mouseCharging);
                        }
                    }

                    bool showHeadset = headsetBarPct > 0;
                    lastHeadsetPctState = showHeadset ? headsetBarPct : -1;
                    lastHeadsetChargingState = headsetCharging;
                    if (rowHeadset != null)
                    {
                        Visibility targetHeadsetVis = showHeadset ? Visibility.Visible : Visibility.Collapsed;
                        if (rowHeadset.Visibility != targetHeadsetVis) { rowHeadset.Visibility = targetHeadsetVis; visChanged = true; }
                        if (showHeadset)
                        {
                            txtHeadsetDetail.Text = headsetDetail;
                            txtHeadsetPct.Text = headsetPctText;
                            barHeadsetFill.Width = Math.Max(3, (42.0 * headsetBarPct) / 100.0);
                            CheckAndNotifyLowBattery("headset:hyperx", "HyperX Cloud III", headsetBarPct, headsetCharging);
                        }
                    }

                    bool showPad = padConnected && padBarPct > 0;
                    lastPadPctState = showPad ? padBarPct : -1;
                    lastPadChargingState = padCharging;
                    if (rowController != null)
                    {
                        Visibility targetPadVis = showPad ? Visibility.Visible : Visibility.Collapsed;
                        if (rowController.Visibility != targetPadVis) { rowController.Visibility = targetPadVis; visChanged = true; }
                        if (showPad)
                        {
                            txtControllerDetail.Text = padDetail;
                            txtControllerPct.Text = padPctText;
                            barControllerFill.Width = Math.Max(3, (42.0 * padBarPct) / 100.0);
                            CheckAndNotifyLowBattery("gamepad:controller", "Kablosuz Kumanda", padBarPct, padCharging);
                        }
                    }

                    RefreshWirelessRowColors();

                    bool anyDeviceVisible = showMouse || showHeadset || showPad;
                    anyWirelessDeviceActive = anyDeviceVisible;
                    Visibility targetSecVis = (showSecWireless && anyDeviceVisible) ? Visibility.Visible : Visibility.Collapsed;
                    if (secContainerWireless != null && secContainerWireless.Visibility != targetSecVis)
                    {
                        ApplyLayoutAndVisibility();
                        visChanged = true;
                    }

                    if (visChanged && isWindowInitialized)
                    {
                        FitHeightToContentIfCompact();
                    }

                    latestTaskbarDevices.Clear();
                    if (showHeadset)
                        latestTaskbarDevices.Add(new TaskbarDeviceItem { Kind = "Headset", Name = "HyperX Cloud III Wireless", Pct = headsetBarPct, Detail = headsetDetail, IsCharging = headsetCharging });
                    if (showMouse)
                        latestTaskbarDevices.Add(new TaskbarDeviceItem { Kind = "Mouse", Name = "Scyrox V8", Pct = mouseBarPct, Detail = mouseDetail, IsCharging = mouseCharging });
                    if (showPad)
                        latestTaskbarDevices.Add(new TaskbarDeviceItem { Kind = "Gamepad", Name = "Kablosuz Kumanda", Pct = padBarPct, Detail = padDetail, IsCharging = padCharging });
                    RefreshTaskbarDevicesOverlayUi();
                });
            }
            catch { }
            finally
            {
                Interlocked.Exchange(ref wirelessPollingBusy, 0);
            }
        }

        private Brush GetWirelessStatusBrush(int pct, bool isCharging)
        {
            if (isCharging) return HexBrush("#22C55E");
            if (pct > 0 && pct <= 10) return HexBrush("#EF4444");
            return HexBrush(accentColorHex);
        }

        private void RefreshWirelessRowColors()
        {
            Brush mouseBr = GetWirelessStatusBrush(lastMousePctState, lastMouseChargingState);
            if (barMouseFill != null) barMouseFill.Background = mouseBr;
            if (txtMousePct != null) txtMousePct.Foreground = mouseBr;

            Brush headsetBr = GetWirelessStatusBrush(lastHeadsetPctState, lastHeadsetChargingState);
            if (barHeadsetFill != null) barHeadsetFill.Background = headsetBr;
            if (txtHeadsetPct != null) txtHeadsetPct.Foreground = headsetBr;

            Brush padBr = GetWirelessStatusBrush(lastPadPctState, lastPadChargingState);
            if (barControllerFill != null) barControllerFill.Background = padBr;
            if (txtControllerPct != null) txtControllerPct.Foreground = padBr;
        }

        private void CheckAndNotifyLowBattery(string key, string displayName, int pct, bool isCharging)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (isCharging || pct > 20)
            {
                lowBatteryNotifiedKeys.Remove(key);
                return;
            }
            if (!notifyLowBattery || pct <= 0 || pct > 10) return;
            if (lowBatteryNotifiedKeys.Add(key))
            {
                try
                {
                    if (trayIcon != null)
                    {
                        trayIcon.ShowBalloonTip(
                            5000,
                            "Düşük Pil Uyarısı",
                            string.Format("{0} şarjı %{1} seviyesine düştü. Şarja takmanız önerilir.", displayName, pct),
                            System.Windows.Forms.ToolTipIcon.Warning);
                    }
                }
                catch { }
            }
        }

        public void SetTaskbarDevicesVisible(bool visible)
        {
            showTaskbarDevices = visible;
            SaveSettings();
            if (trayTaskbarDevicesItem != null)
            {
                trayTaskbarDevicesItem.Text = showTaskbarDevices
                    ? "🔋 Görev Çubuğu Cihaz Şarjlarını Gizle"
                    : "🔋 Görev Çubuğu Cihaz Şarjlarını Göster";
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
            Task.Run(() => PollWirelessDevicesAsync(true));
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
            sigSb.Append(lightTheme ? "L|" : "D|");
            for (int i = 0; i < latestTaskbarDevices.Count; i++)
            {
                sigSb.Append(latestTaskbarDevices[i].Kind).Append(':').Append(latestTaskbarDevices[i].Pct).Append(':').Append(latestTaskbarDevices[i].IsCharging ? 'C' : 'N').Append(':').Append(latestTaskbarDevices[i].Detail).Append('|');
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
            Brush lowFg = HexBrush(lightTheme ? "#C42B1C" : "#FF5A5A");
            Brush chgFg = HexBrush(lightTheme ? "#16A34A" : "#4ADE80");
            Brush hoverBg = HexBrush(lightTheme ? "#16000000" : "#1CFFFFFF");

            for (int i = 0; i < latestTaskbarDevices.Count; i++)
            {
                TaskbarDeviceItem d = latestTaskbarDevices[i];
                bool low = !d.IsCharging && d.Pct > 0 && d.Pct <= 10;
                Brush fg = d.IsCharging ? chgFg : (low ? lowFg : normalFg);

                Border pill = new Border
                {
                    CornerRadius = new CornerRadius(5),
                    Background = HexBrush("#01000000"),
                    Padding = new Thickness(7, 4, 7, 4),
                    Margin = new Thickness(i == 0 ? 0 : 2, 0, 0, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = string.Format("{0}\n%{1} • {2}\n(Yenile: Sol tık • Ayarlar: Sağ tık)", d.Name, d.Pct, d.Detail)
                };
                pill.MouseEnter += (s, e) => pill.Background = hoverBg;
                pill.MouseLeave += (s, e) => pill.Background = HexBrush("#01000000");
                pill.MouseLeftButtonDown += (s, e) =>
                {
                    Task.Run(() => PollWirelessDevicesAsync(true));
                    ShowToast("🔋 Cihaz şarjları yenileniyor...");
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
        // WEATHER FETCHER (Kütahya / Merkez)
        // ==========================================
        private void FetchWeatherAsync()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                string url = "https://api.open-meteo.com/v1/forecast?latitude=39.4242&longitude=29.9833&current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=4";

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = 6000;
                req.UserAgent = "OpenWidgetDesktop/2.5";

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

                    Dispatcher.Invoke(() =>
                    {
                        txtWeatherTemp.Text = Math.Round(temp) + "°";
                        txtWeatherFeels.Text = string.Format("Hissedilen {0:0}°C", feels);
                        txtWeatherDesc.Text = WeatherCodeToTurkish(wCode);
                        txtWeatherMainIcon.Text = WeatherCodeToIcon(wCode);
                        txtWeatherHumidity.Text = "%" + Math.Round(hum);
                        txtWeatherWind.Text = Math.Round(wind) + " km/s";
                        txtWeatherRain.Text = precip.ToString("0.#") + " mm";

                        string[] gunIsimleri = { "Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi" };
                        for (int i = 0; i < 3; i++)
                        {
                            txtDayNames[i].Text = i == 0 ? "Bugün" : (i == 1 ? "Yarın" : gunIsimleri[(int)DateTime.Now.AddDays(i).DayOfWeek]);
                            if (minTemps.Length > i && maxTemps.Length > i)
                            {
                                txtDayTemps[i].Text = string.Format("{0:0}° / {1:0}°", minTemps[i], maxTemps[i]);
                            }
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

        private string WeatherCodeToTurkish(int code)
        {
            if (code == 0) return "Açık";
            if (code == 1 || code == 2) return "Parçalı Bulutlu";
            if (code == 3) return "Çok Bulutlu";
            if (code == 45 || code == 48) return "Sisli";
            if (code >= 51 && code <= 67) return "Yağmurlu";
            if (code >= 71 && code <= 77) return "Kar Yağışlı";
            if (code >= 80 && code <= 82) return "Sağanak Yağışlı";
            if (code >= 95) return "Fırtınalı";
            return "Parçalı Bulutlu";
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
            txtClipCount.Text = clipboardHistory.Count + " öğe";

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
                    Text = "Henüz kopyalanan metin yok...",
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
                    ToolTip = "Tekrar kopyalamak için tıklayın:\n" + (copyText.Length > 200 ? copyText.Substring(0, 200) + "..." : copyText)
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
                        ShowToast("Panoya kopyalandı!");
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
                trayCalendarItem.Text = showCalendarWidget ? "📅 Google Takvim Widget'ını Gizle" : "📅 Google Takvim Widget'ını Göster";
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
                    IntPtr calHwnd = new WindowInteropHelper(calendarWindow).Handle;
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
                Title = "OpenWidget • Google Takvim",
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
            TryApplyWindowIcon(calendarWindow);
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

            // Refresh calendar every 5 minutes and at minute boundaries for live countdowns
            DispatcherTimer calUiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(45) };
            int calTick = 0;
            calUiTimer.Tick += (s, e) =>
            {
                calTick++;
                if (showCalendarWidget) RefreshCalendarWidgetUi(false);
                if (calTick % 7 == 0)
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

            // Apple iOS Medium/Large Calendar Two-Pane Layout:
            // Left Pane (182px): Big Day Name + Huge Date Number + Mini Monthly Calendar Grid with Event Dots
            // Divider (1px)
            // Right Pane (*): Selected Day / Today Schedule + "Sıradaki (Yarın)" Apple-style Pill Cards
            Grid twoPaneGrid = new Grid();
            twoPaneGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(182) });
            twoPaneGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            twoPaneGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // ==========================================
            // LEFT PANE: APPLE CALENDAR DATE & MINI MONTH
            // ==========================================
            Grid leftPane = new Grid();
            leftPane.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            leftPane.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            leftPane.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Top Date Header: Day of Week (Accent) + Big Day Number + Month Navigation
            Grid dateTopRow = new Grid { Margin = new Thickness(2, 0, 2, 6) };
            dateTopRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dateTopRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel dateLeftStack = new StackPanel();
            txtCalDayOfWeekHeader = new TextBlock
            {
                Text = "ÇARŞAMBA",
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
                Text = "Kasım 2026",
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

            // Month Prev / Today / Next mini buttons
            StackPanel monthNavBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
            Button btnPrevMonth = CreateStyledIconButton("#26201D", "Transparent", "Önceki Ay");
            btnPrevMonth.Width = 20; btnPrevMonth.Height = 20;
            btnPrevMonth.Content = new TextBlock { Text = "‹", Foreground = HexBrush("#C7B9AE"), FontSize = 13, FontWeight = FontWeights.Bold, Margin = new Thickness(0, -2, 0, 0) };
            btnPrevMonth.Click += (s, e) =>
            {
                calDisplayedMonth = calDisplayedMonth.AddMonths(-1);
                RefreshCalendarWidgetUi();
            };

            Button btnGoToday = CreateStyledIconButton("#26201D", "Transparent", "Bugüne Dön");
            btnGoToday.Width = 20; btnGoToday.Height = 20;
            btnGoToday.Margin = new Thickness(2, 0, 2, 0);
            btnGoToday.Content = new TextBlock { Text = "•", Foreground = HexBrush(accentColorHex), FontSize = 13, FontWeight = FontWeights.Bold, Margin = new Thickness(0, -2, 0, 0) };
            btnGoToday.Click += (s, e) =>
            {
                calSelectedDate = DateTime.Today;
                calDisplayedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                RefreshCalendarWidgetUi();
            };

            Button btnNextMonth = CreateStyledIconButton("#26201D", "Transparent", "Sonraki Ay");
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

            // Weekday Headers: Pzt Sal Çar Per Cum Cmt Paz
            UniformGrid weekDayHdr = new UniformGrid { Columns = 7, Margin = new Thickness(0, 0, 0, 3) };
            string[] shortDays = { "P", "S", "Ç", "P", "C", "C", "P" };
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

            // Dynamic Multi-Month / Progressive Week Host (never stretches row gaps; adds subsequent weeks/months to fill height)
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

            // Vertical Hairline Divider
            Border vDivider = new Border
            {
                Width = 1,
                Background = HexBrush("#3B302A"),
                Margin = new Thickness(6, 2, 6, 2)
            };
            Grid.SetColumn(vDivider, 1);
            twoPaneGrid.Children.Add(vDivider);

            // ==========================================
            // RIGHT PANE: SCHEDULE & APPLE PILL CARDS
            // ==========================================
            Grid rightPane = new Grid();
            rightPane.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rightPane.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rightPane.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Right Header: Selected Date Schedule Label + Quick Action Buttons (+, Refresh, Open Web, Lock)
            Grid rightHeader = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            rightHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rightHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            txtCalRightScheduleTitle = new TextBlock
            {
                Text = "BUGÜN • DERS & ETKİNLİKLER",
                Foreground = HexBrush("#C7B9AE"),
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(txtCalRightScheduleTitle, 0);
            rightHeader.Children.Add(txtCalRightScheduleTitle);

            StackPanel rightBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            Button btnQuickAdd = CreateStyledIconButton("#26201D", "Transparent", "Hızlı Etkinlik / Hatırlatıcı Ekle");
            btnQuickAdd.Width = 22; btnQuickAdd.Height = 22;
            btnQuickAdd.Margin = new Thickness(0, 0, 3, 0);
            btnQuickAdd.Content = new TextBlock { Text = "+", Foreground = HexBrush(accentColorHex), FontSize = 13, FontWeight = FontWeights.Bold, Margin = new Thickness(0, -2, 0, 0) };
            btnQuickAdd.Click += (s, e) =>
            {
                calQuickAddPanel.Visibility = (calQuickAddPanel.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible;
                FitCalendarHeightToContent();
            };
            rightBtns.Children.Add(btnQuickAdd);

            Button btnSyncCal = CreateStyledIconButton("#26201D", "Transparent", "Google Takvim'i Şimdi Senkronize Et");
            btnSyncCal.Width = 22; btnSyncCal.Height = 22;
            btnSyncCal.Margin = new Thickness(0, 0, 3, 0);
            btnSyncCal.Content = new TextBlock { Text = "↻", Foreground = HexBrush("#C7B9AE"), FontSize = 11.5, FontWeight = FontWeights.Bold };
            btnSyncCal.Click += (s, e) =>
            {
                Task.Run(() => LoadAndSyncCalendarEventsAsync(true));
            };
            rightBtns.Children.Add(btnSyncCal);

            Button btnOpenGCalWeb = CreateStyledIconButton("#26201D", "Transparent", "Google Takvim'i Tarayıcıda Aç (safaacikgoz43@gmail.com)");
            btnOpenGCalWeb.Width = 22; btnOpenGCalWeb.Height = 22;
            btnOpenGCalWeb.Margin = new Thickness(0, 0, 3, 0);
            btnOpenGCalWeb.Content = new TextBlock { Text = "📅", Foreground = HexBrush("#C7B9AE"), FontSize = 10.5 };
            btnOpenGCalWeb.Click += (s, e) =>
            {
                try { Process.Start("https://calendar.google.com/calendar/u/0/r"); } catch { }
            };
            rightBtns.Children.Add(btnOpenGCalWeb);

            btnCalLockDrag = CreateStyledIconButton("#26201D", "Transparent", "Taşıma ve Boyutlandırma Kilidi");
            btnCalLockDrag.Width = 22; btnCalLockDrag.Height = 22;
            btnCalLockDrag.Click += (s, e) => ToggleDraggableMode();
            rightBtns.Children.Add(btnCalLockDrag);

            Grid.SetColumn(rightBtns, 1);
            rightHeader.Children.Add(rightBtns);
            Grid.SetRow(rightHeader, 0);
            rightPane.Children.Add(rightHeader);

            // Collapsible Quick-Add Event Row
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
                ToolTip = "Saat (Örn: 14:00 veya 09:30-11:00)"
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
                ToolTip = "Etkinlik başlığı yazın (Örn: Sınav, Toplantı, Ödev)"
            };
            txtCalQuickLoc = new TextBox { Text = "" };
            Grid.SetColumn(txtCalQuickTitle, 1);
            qaGrid.Children.Add(txtCalQuickTitle);

            Button btnSaveQuickEvent = CreateStyledIconButton("#38281E", accentColorHex, "Seçili güne etkinlik ekle");
            btnSaveQuickEvent.Padding = new Thickness(8, 2, 8, 2);
            btnSaveQuickEvent.Content = new TextBlock { Text = "Ekle", Foreground = HexBrush("#FAF6F2"), FontSize = 10, FontWeight = FontWeights.Bold };
            btnSaveQuickEvent.Click += (s, e) => AddCustomCalendarEventFromUi();
            Grid.SetColumn(btnSaveQuickEvent, 2);
            qaGrid.Children.Add(btnSaveQuickEvent);

            calQuickAddPanel.Child = qaGrid;
            Grid.SetRow(calQuickAddPanel, 1);
            rightPane.Children.Add(calQuickAddPanel);

            // Scrollable Events Stack with Sleek Ultra-Thin Accent ScrollBar
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
            if (sv == null) return;
            try
            {
                string thumbHex = string.IsNullOrEmpty(accentColorHex) ? "#E2925C" : accentColorHex;
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
                string[] trDayNamesUpper = { "PAZAR", "PAZARTESİ", "SALI", "ÇARŞAMBA", "PERŞEMBE", "CUMA", "CUMARTESİ" };
                string[] trDayNamesShortUpper = { "PAZ", "PZT", "SAL", "ÇAR", "PER", "CUM", "CMT" };
                string[] trMonthNames = { "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };
                string[] trMonthNamesUpper = { "", "OCAK", "ŞUBAT", "MART", "NİSAN", "MAYIS", "HAZİRAN", "TEMMUZ", "AĞUSTOS", "EYLÜL", "EKİM", "KASIM", "ARALIK" };

                DateTime today = DateTime.Today;
                DateTime headerDate = calSelectedDate.Date;

                txtCalDayOfWeekHeader.Text = trDayNamesUpper[(int)headerDate.DayOfWeek];
                txtCalDayOfWeekHeader.Foreground = HexBrush(accentColorHex);
                txtCalBigDayNum.Text = headerDate.Day.ToString();
                txtCalMonthYearLabel.Text = trMonthNames[calDisplayedMonth.Month] + " " + calDisplayedMonth.Year;

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

                // Compute status signature for right pane so 45s timer skips rebuilding UI when nothing changed
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
                string rightSig = string.Format("{0:yyyyMMdd}|{1:yyyyMMdd}|{2}|{3}|{4}|{5}",
                    today, calSelectedDate.Date, evVer, accentColorHex, dayEvents.Count, activeStateMask);

                if (autoShrinkHeight || rightSig != lastRightScheduleSignature)
                {
                    lastRightScheduleSignature = rightSig;

                    // 1. Rebuild Right Pane Event Pill Cards FIRST so we know the exact target window height
                    calEventsListPanel.Children.Clear();

                    if (isViewingToday)
                    {
                        txtCalRightScheduleTitle.Text = string.Format("BUGÜN • {0} {1} ({2})", today.Day, trMonthNamesUpper[today.Month], dayEvents.Count > 0 ? (dayEvents.Count + " ETKİNLİK") : "BOŞ");
                    }
                    else
                    {
                        txtCalRightScheduleTitle.Text = string.Format("{0} {1} {2} • {3} ETKİNLİK",
                            calSelectedDate.Day,
                            trMonthNamesUpper[calSelectedDate.Month],
                            trDayNamesShortUpper[(int)calSelectedDate.DayOfWeek],
                            dayEvents.Count);
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
                        emptyBox.Child = new TextBlock
                        {
                            Text = isViewingToday ? "Bugün için planlanmış ders veya etkinlik yok." : "Bu tarihte kayıtlı etkinlik bulunmuyor.",
                            Foreground = HexBrush("#9E8C7E"),
                            FontSize = 11
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

                    // Just like iPhone Apple Calendar widget: if viewing Today, also show "SIRADAKİ (YARIN / YAKLAŞAN)" below!
                    if (isViewingToday)
                    {
                        for (int ahead = 1; ahead <= 7; ahead++)
                        {
                            DateTime nextDate = today.AddDays(ahead);
                            List<CalendarEventItem> nextEvents = GetEventsForDate(snapshot, nextDate);
                            if (nextEvents.Count > 0)
                            {
                                string nextHdr = ahead == 1
                                    ? string.Format("SIRADAKİ • YARIN ({0} {1})", nextDate.Day, trMonthNamesUpper[nextDate.Month])
                                    : string.Format("SIRADAKİ • {0} {1} {2}", nextDate.Day, trMonthNamesUpper[nextDate.Month], trDayNamesUpper[(int)nextDate.DayOfWeek]);

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

                    // Auto-fit window height to the events on the right so no scrollbar is needed
                    FitCalendarHeightToContent(autoShrinkHeight);
                }

                // 2. Rebuild Left Pane Mini Month Stack to fill vertical height with fixed-row-height weeks/months (no ugly row stretching)
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

                // Quantize available height into discrete 22px week steps so dragging/resizing doesn't rebuild the visual tree on every pixel
                int quantizedSteps = (int)(availLeftH / 11.0);
                Dictionary<DateTime, byte> dotsLookup;
                int evVer;
                lock (calEventsLock)
                {
                    dotsLookup = calEventDotsByDate;
                    evVer = calEventsVersion;
                }

                string leftSig = string.Format("{0:yyyyMM}|{1:yyyyMMdd}|{2:yyyyMMdd}|{3}|{4}|{5}",
                    calDisplayedMonth, calSelectedDate.Date, today, evVer, accentColorHex, quantizedSteps);
                if (leftSig == lastLeftColumnSignature && calMiniMonthGrid.Children.Count > 0)
                    return;
                lastLeftColumnSignature = leftSig;

                string[] trMonthNamesUpper = { "", "OCAK", "ŞUBAT", "MART", "NİSAN", "MAYIS", "HAZİRAN", "TEMMUZ", "AĞUSTOS", "EYLÜL", "EKİM", "KASIM", "ARALIK" };

                calMiniMonthGrid.Children.Clear();
                double usedH = 0.0;

                for (int m = 0; m < 12; m++)
                {
                    DateTime monthDate = new DateTime(calDisplayedMonth.Year, calDisplayedMonth.Month, 1).AddMonths(m);
                    int firstDayOffset = ((int)monthDate.DayOfWeek + 6) % 7; // Monday = 0, Sunday = 6
                    int daysInMonth = DateTime.DaysInMonth(monthDate.Year, monthDate.Month);
                    int weekRows = (int)Math.Ceiling((firstDayOffset + daysInMonth) / 7.0);
                    DateTime gridStart = monthDate.AddDays(-firstDayOffset);

                    if (m > 0)
                    {
                        // Need room for at least the month header + 1 week row
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
                            Text = trMonthNamesUpper[monthDate.Month] + " " + monthDate.Year,
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

                            // Avoid duplicating boundary days when subsequent months are stacked below
                            if (!isCurrentMonth && (m > 0 || (willFitNextMonth && cellDate > monthDate)))
                            {
                                weekRowGrid.Children.Add(new Border { Width = 21, Height = 21 });
                                continue;
                            }

                            bool isToday = (cellDate == today);
                            bool isSelected = (cellDate == calSelectedDate.Date);

                            // O(1) pre-indexed dictionary lookup instead of O(N) loop over all events per cell
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
                ToolTip = ev.Title + (string.IsNullOrEmpty(ev.Location) ? "" : ("\n📍 " + ev.Location)) + (string.IsNullOrEmpty(ev.Description) ? "" : ("\n👤 " + ev.Description))
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left Vertical Color Pill Bar (Signature Apple Calendar Widget look)
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

            // Row 1: Event Title + Status/Time Badge
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
                ? "Tüm Gün"
                : (ev.Start.ToString("HH:mm") + " – " + ev.End.ToString("HH:mm"));
            if (isOngoing) timeBadgeText = "● ŞİMDİ (" + ev.End.ToString("HH:mm") + "'e kadar)";

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

            // Row 2: Location & Instructor / Subtitle
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
                Button btnDel = CreateStyledIconButton("Transparent", "Transparent", "Bu özel etkinliği sil");
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
                    ShowToast("Lütfen etkinlik başlığı girin");
                    return;
                }
                string timePart = txtCalQuickTime.Text.Trim();
                DateTime startDt = calSelectedDate.Date.AddHours(12);
                DateTime endDt = startDt.AddHours(1);
                bool allDay = false;

                if (string.IsNullOrEmpty(timePart) || timePart.Equals("tüm gün", StringComparison.OrdinalIgnoreCase))
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
                ShowToast("Etkinlik takvime eklendi: " + cleanTitle);
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
                ShowToast("Etkinlik silindi");
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

                // Fast path: if background poll triggered and neither local .dat file changed nor live iCal URL is set, skip disk I/O & parsing
                if (!showToastOnDone && !hasLiveIcal && datWrite == lastCalDatWriteUtc && customWrite == lastCustomDatWriteUtc && allCalendarEvents.Count > 0)
                {
                    return;
                }

                List<CalendarEventItem> loaded = new List<CalendarEventItem>(256);

                // 1. Load pre-synced Google Calendar events (safaacikgoz43@gmail.com + Holidays) from calendar_events.dat
                if (File.Exists(datPath))
                {
                    foreach (string line in File.ReadAllLines(datPath, Encoding.UTF8))
                    {
                        CalendarEventItem item = ParsePipeEventLine(line, false);
                        if (item != null) loaded.Add(item);
                    }
                }

                // 2. Load user's custom quick-added desktop events from custom_events.dat
                string[] customLines = ReadAllLinesCrashSafe(customPath, Encoding.UTF8);
                if (customLines != null)
                {
                    foreach (string line in customLines)
                    {
                        CalendarEventItem item = ParsePipeEventLine(line, true);
                        if (item != null) loaded.Add(item);
                    }
                }

                // 3. If user configured a live Google Calendar Secret iCal (.ics) URL, fetch & merge live!
                if (hasLiveIcal)
                {
                    try
                    {
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(calendarSecretIcalUrl);
                        req.Timeout = 7000;
                        req.UserAgent = "OpenWidgetCalendar/2.1";
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

                // Pre-index O(1) event dot flags by date (bit 1 = primary/custom, bit 2 = holiday, bit 4 = exam/purple)
                Dictionary<DateTime, byte> dotsMap = new Dictionary<DateTime, byte>(loaded.Count + 16);
                for (int i = 0; i < loaded.Count; i++)
                {
                    CalendarEventItem ev = loaded[i];
                    bool isExam = (ev.ColorHex == "#C084FC") || (ev.Title != null && ev.Title.IndexOf("Sınav", StringComparison.OrdinalIgnoreCase) >= 0);
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
                        ShowToast("Google Takvim senkronize edildi (" + loaded.Count + " etkinlik)");
                });

                // Trim startup/reload working set allocations in the background
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

            // Unfold RFC 5545 multi-line fields
            string unfolded = ics.Replace("\r\n ", "").Replace("\r\n\t", "").Replace("\n ", "").Replace("\n\t", "");
            string[] lines = unfolded.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            bool inEvent = false;
            string summary = "", location = "", desc = "", dtStartRaw = "", dtEndRaw = "", uid = "", rrule = "", recurIdRaw = "", status = "";
            List<string> exdates = new List<string>();

            // Weekly classes are stored as ONE recurring VEVENT (RRULE) plus optional overrides (RECURRENCE-ID) and EXDATEs.
            // We must expand them, otherwise only the very first occurrence (September) is shown.
            List<CalendarEventItem> singles = new List<CalendarEventItem>();
            Dictionary<string, HashSet<DateTime>> overriddenStarts = new Dictionary<string, HashSet<DateTime>>();
            List<object[]> masters = new List<object[]>(); // [item, rrule, uid, exdateSet]

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
                    bool isExam = ttl.IndexOf("Sınav", StringComparison.OrdinalIgnoreCase) >= 0 || ttl.IndexOf("Vize", StringComparison.OrdinalIgnoreCase) >= 0 || ttl.IndexOf("Final", StringComparison.OrdinalIgnoreCase) >= 0;
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
                        // Override of a single occurrence of a recurring series
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

            // Expand recurring masters into concrete occurrences
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

        // Minimal RFC 5545 RRULE expander: FREQ=DAILY/WEEKLY/MONTHLY/YEARLY, INTERVAL, COUNT, UNTIL, BYDAY (weekly)
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
                DateTime weekStart = dtStart.Date.AddDays(-startOffset); // Monday of first week
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
        // STANDALONE TIMER WIDGETİ (MODES, POMODORO, DAILY STATS & YEARLY HEATMAP)
        // =========================================================================
        public void SetTimerWidgetVisible(bool visible)
        {
            showTimerWidget = visible;
            SaveSettings();
            if (trayTimerItem != null)
            {
                trayTimerItem.Text = showTimerWidget ? "⏱ Timer Widgeti'ni Gizle" : "⏱ Timer Widgeti'ni Göster";
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

        private static string SanitizeGameKey(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Manuel Eğlence";
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

        private static string JsonEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch == '\\') sb.Append("\\\\");
                else if (ch == '"') sb.Append("\\\"");
                else if (ch == '\r') sb.Append("\\r");
                else if (ch == '\n') sb.Append("\\n");
                else if (ch == '\t') sb.Append("\\t");
                else if (ch < 32) sb.AppendFormat("\\u{0:x4}", (int)ch);
                else sb.Append(ch);
            }
            return sb.ToString();
        }

        private string ReadGithubSyncToken(bool forceRefresh)
        {
            if (!forceRefresh && !string.IsNullOrEmpty(cachedGithubSyncToken)) return cachedGithubSyncToken;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "credential fill",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using (Process proc = Process.Start(psi))
                {
                    if (proc == null) return null;
                    proc.StandardInput.Write("protocol=https\nhost=github.com\n\n");
                    proc.StandardInput.Close();
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(3000);
                    foreach (string line in output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (line.StartsWith("password=", StringComparison.Ordinal))
                        {
                            string tok = line.Substring(9).Trim();
                            if (!string.IsNullOrEmpty(tok))
                            {
                                cachedGithubSyncToken = tok;
                                return tok;
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private string BuildActivityCloudJson(out string dataSignature)
        {
            List<DailyActivityRecord> snapshot = new List<DailyActivityRecord>();
            string accentSnap = accentColorHex, studySnap = timerStudyColorHex, gameSnap = timerGameColorHex;
            try
            {
                Dispatcher.Invoke(() =>
                {
                    accentSnap = accentColorHex;
                    studySnap = timerStudyColorHex;
                    gameSnap = timerGameColorHex;
                    foreach (DailyActivityRecord r in activityByDate.Values)
                    {
                        DailyActivityRecord copy = new DailyActivityRecord
                        {
                            Date = r.Date,
                            StudySeconds = r.StudySeconds,
                            GameSeconds = r.GameSeconds,
                            PcSeconds = r.PcSeconds
                        };
                        if (r.GameBreakdown != null)
                        {
                            foreach (var kv in r.GameBreakdown) copy.GameBreakdown[kv.Key] = kv.Value;
                        }
                        snapshot.Add(copy);
                    }
                });
            }
            catch
            {
                dataSignature = null;
                return null;
            }

            snapshot.Sort((a, b) => a.Date.CompareTo(b.Date));
            DateTime cutoff = DateTime.Today.AddDays(-370);

            StringBuilder bodySb = new StringBuilder(snapshot.Count * 80 + 128);
            bodySb.Append("\"colors\":{");
            bodySb.Append("\"accent\":\"").Append(JsonEscape(accentSnap)).Append("\",");
            bodySb.Append("\"study\":\"").Append(JsonEscape(studySnap)).Append("\",");
            bodySb.Append("\"game\":\"").Append(JsonEscape(gameSnap)).Append("\"},");
            bodySb.Append("\"days\":{");

            bool firstDay = true;
            for (int i = 0; i < snapshot.Count; i++)
            {
                DailyActivityRecord r = snapshot[i];
                if (r.Date < cutoff) continue;
                if (r.StudySeconds == 0 && r.GameSeconds == 0 && r.PcSeconds == 0) continue;
                if (!firstDay) bodySb.Append(',');
                firstDay = false;
                bodySb.Append('"').Append(r.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)).Append("\":{");
                bodySb.Append("\"study\":").Append(r.StudySeconds)
                      .Append(",\"game\":").Append(r.GameSeconds)
                      .Append(",\"pc\":").Append(r.PcSeconds);
                if (r.GameBreakdown != null && r.GameBreakdown.Count > 0)
                {
                    List<KeyValuePair<string, int>> games = new List<KeyValuePair<string, int>>(r.GameBreakdown);
                    games.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
                    StringBuilder gSb = new StringBuilder();
                    bool firstG = true;
                    for (int gi = 0; gi < games.Count; gi++)
                    {
                        if (games[gi].Value <= 0) continue;
                        if (!firstG) gSb.Append(',');
                        firstG = false;
                        gSb.Append('"').Append(JsonEscape(games[gi].Key)).Append("\":").Append(games[gi].Value);
                    }
                    if (!firstG)
                    {
                        bodySb.Append(",\"games\":{").Append(gSb.ToString()).Append('}');
                    }
                }
                bodySb.Append('}');
            }
            bodySb.Append('}');

            dataSignature = bodySb.ToString();
            if (firstDay) return null; // never publish empty history

            string updatedIso = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);
            return "{\"version\":1,\"updatedAt\":\"" + updatedIso + "\"," + dataSignature + "}\n";
        }

        private void SyncTimerActivityToSiteAsync(bool force, bool showToastResult)
        {
            if (!force && !syncActivityToSite) return;
            if (Interlocked.CompareExchange(ref cloudActivitySyncBusy, 1, 0) != 0) return;
            try
            {
                string dataSig;
                string json = BuildActivityCloudJson(out dataSig);
                if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(dataSig)) return;
                if (!force && string.Equals(dataSig, lastCloudSyncedSignature, StringComparison.Ordinal)) return;

                string token = ReadGithubSyncToken(false);
                if (string.IsNullOrEmpty(token))
                {
                    if (showToastResult) Dispatcher.BeginInvoke(new Action(() => ShowToast("⚠ GitHub kimliği bulunamadı")));
                    return;
                }

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                const string contentsApi = "https://api.github.com/repos/safeapricot/safeapricot.github.io/contents/activity.json";

                for (int attempt = 0; attempt < 2; attempt++)
                {
                    try
                    {
                        string existingSha = null;
                        HttpWebRequest getReq = (HttpWebRequest)WebRequest.Create(contentsApi + "?ref=activity-data");
                        getReq.Method = "GET";
                        getReq.Timeout = 8000;
                        getReq.UserAgent = "OpenWidget-Sync";
                        getReq.Accept = "application/vnd.github+json";
                        getReq.Headers["Authorization"] = "token " + token;
                        try
                        {
                            using (HttpWebResponse getResp = (HttpWebResponse)getReq.GetResponse())
                            using (StreamReader sr = new StreamReader(getResp.GetResponseStream(), Encoding.UTF8))
                            {
                                string body = sr.ReadToEnd();
                                Match m = Regex.Match(body, "\"sha\"\\s*:\\s*\"([0-9a-fA-F]{40})\"");
                                if (m.Success) existingSha = m.Groups[1].Value;
                            }
                        }
                        catch (WebException wex)
                        {
                            HttpWebResponse er = wex.Response as HttpWebResponse;
                            if (er != null && (int)er.StatusCode == 401 && attempt == 0)
                            {
                                token = ReadGithubSyncToken(true);
                                if (!string.IsNullOrEmpty(token)) continue;
                            }
                            if (er == null || (int)er.StatusCode != 404) throw;
                        }

                        string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
                        StringBuilder putSb = new StringBuilder(b64.Length + 256);
                        putSb.Append("{\"message\":\"sync: update OpenWidget activity data\",\"branch\":\"activity-data\",\"content\":\"")
                             .Append(b64).Append('"');
                        if (!string.IsNullOrEmpty(existingSha))
                            putSb.Append(",\"sha\":\"").Append(existingSha).Append('"');
                        putSb.Append('}');

                        byte[] putBytes = Encoding.UTF8.GetBytes(putSb.ToString());
                        HttpWebRequest putReq = (HttpWebRequest)WebRequest.Create(contentsApi);
                        putReq.Method = "PUT";
                        putReq.Timeout = 10000;
                        putReq.UserAgent = "OpenWidget-Sync";
                        putReq.Accept = "application/vnd.github+json";
                        putReq.ContentType = "application/json; charset=utf-8";
                        putReq.Headers["Authorization"] = "token " + token;
                        putReq.ContentLength = putBytes.Length;
                        using (Stream reqStream = putReq.GetRequestStream())
                        {
                            reqStream.Write(putBytes, 0, putBytes.Length);
                        }
                        using (HttpWebResponse putResp = (HttpWebResponse)putReq.GetResponse())
                        {
                            if ((int)putResp.StatusCode >= 200 && (int)putResp.StatusCode < 300)
                            {
                                lastCloudSyncedSignature = dataSig;
                                if (showToastResult)
                                    Dispatcher.BeginInvoke(new Action(() => ShowToast("✓ Aktivite safeapricot.com'a senkronize edildi")));
                                return;
                            }
                        }
                    }
                    catch (WebException wex)
                    {
                        HttpWebResponse er = wex.Response as HttpWebResponse;
                        if (er != null && (int)er.StatusCode == 401 && attempt == 0)
                        {
                            token = ReadGithubSyncToken(true);
                            if (!string.IsNullOrEmpty(token)) continue;
                        }
                        if (showToastResult)
                            Dispatcher.BeginInvoke(new Action(() => ShowToast("⚠ Senkronizasyon başarısız")));
                        return;
                    }
                }
            }
            catch
            {
                if (showToastResult)
                    Dispatcher.BeginInvoke(new Action(() => ShowToast("⚠ Senkronizasyon başarısız")));
            }
            finally
            {
                Interlocked.Exchange(ref cloudActivitySyncBusy, 0);
            }
        }

        private void InitializeTimerWidget()
        {
            if (timerWindow != null) return;

            activeTrackingDate = DateTime.Today;
            pomoRemainingSeconds = pomoFocusMinutes * 60;
            LoadTimerActivityData();

            timerWindow = new Window
            {
                Title = "OpenWidget • Timer Widgeti",
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
            TryApplyWindowIcon(timerWindow);
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

            // 1-second precision tracker for PC uptime, Study/Game modes & Pomodoro
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
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header + Mode Switcher
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.50, GridUnitType.Star) }); // Middle Dial + Stats
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.92, GridUnitType.Star) }); // Heatmap

            // ==========================================
            // 1. TOP HEADER BAR & 3-MODE SWITCHER
            // ==========================================
            Grid topBar = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new TextBlock
            {
                Text = "⏱ Timer Widgeti",
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            });
            Grid.SetColumn(titleStack, 0);
            topBar.Children.Add(titleStack);

            // 3 Mode Pills: Default | Eğlence | Ders Modu
            StackPanel modeSwitcherPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = "Interactive"
            };
            string[] modeLabels = { "🖥 Default", "🎮 Eğlence", "📚 Ders Modu" };
            string[] modeTips = {
                "Default Mod (Sadece bilgisayar süresi takip edilir)",
                "Eğlence / Oyun Modu (Oyun oynarken geçen süre günlük kaydedilir)",
                "Ders / Pomodoro Modu (Pomodoro başlar ve günlük çalışma süresi kaydedilir)"
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

            // Right utility buttons: Pomodoro Settings + Lock/Drag
            StackPanel rightUtilBtns = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = "Interactive"
            };
            Button btnPomoSettings = CreateStyledIconButton("#26201D", "Transparent", "Timer ayarları, renkler ve günlere detaylı bakış");
            btnPomoSettings.Padding = new Thickness(7, 3, 7, 3);
            btnPomoSettings.Margin = new Thickness(6, 0, 4, 0);
            btnPomoSettings.Content = new TextBlock { Text = "⚙ Ayarlar", Foreground = HexBrush("#C7B9AE"), FontSize = 10, FontWeight = FontWeights.SemiBold };
            btnPomoSettings.Click += (s, e) => OpenTimerSettingsWindow();
            rightUtilBtns.Children.Add(btnPomoSettings);

            btnTimerLockDrag = CreateStyledIconButton("#26201D", "Transparent", "Sabit / Taşınabilir Modu Değiştir");
            btnTimerLockDrag.Width = 24;
            btnTimerLockDrag.Height = 22;
            btnTimerLockDrag.Content = new TextBlock { Text = isDraggable ? "🔓" : "📌", FontSize = 10.5, Foreground = HexBrush("#C7B9AE") };
            btnTimerLockDrag.Click += (s, e) => ToggleDraggableMode();
            rightUtilBtns.Children.Add(btnTimerLockDrag);

            Grid.SetColumn(rightUtilBtns, 2);
            topBar.Children.Add(rightUtilBtns);

            Grid.SetRow(topBar, 0);
            rootGrid.Children.Add(topBar);

            // ==========================================
            // 2. MIDDLE: CIRCULAR DIAL + DAILY STATS
            // ==========================================
            Grid middleGrid = new Grid { Margin = new Thickness(0, 0, 0, 9) };
            middleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.42, GridUnitType.Star), MinWidth = 196 });
            middleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            middleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.58, GridUnitType.Star) });

            // Left: Circular Focus/Mode Dial + Play/Pause & Reset Controls
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
                Text = "Default Mod",
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

            // 114x114 Tick Dial
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
                Text = "25 dk",
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 18.5,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            txtTimerDialSub = new TextBlock
            {
                Text = "Hazır",
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

            // Right of circle: Green Pause/Resume Button + Dark Reset Button (matching mockup)
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
                ToolTip = "Pomodoro'yu Başlat / Duraklat",
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
                    ShowToast(defaultTimerIsRunning ? "▶ Zamanlayıcı başlatıldı" : "⏸ Zamanlayıcı duraklatıldı");
                }
                else if (timerCurrentMode == 1)
                {
                    gameTimerIsRunning = !gameTimerIsRunning;
                    ShowToast(gameTimerIsRunning ? "▶ Eğlence sayacı başlatıldı" : "⏸ Eğlence sayacı duraklatıldı");
                }
                else
                {
                    if (pomoRemainingSeconds <= 0)
                        pomoRemainingSeconds = (pomoIsBreakPhase ? pomoBreakMinutes : pomoFocusMinutes) * 60;
                    pomoIsRunning = !pomoIsRunning;
                    ShowToast(pomoIsRunning ? "▶ Pomodoro başlatıldı" : "⏸ Pomodoro duraklatıldı");
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
                ToolTip = "Süreyi sıfırla (Ders modunda Odak/Mola geçişi için 2 kez tıkla)",
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
                    ShowToast("↺ Zamanlayıcı sıfırlandı");
                }
                else if (timerCurrentMode == 1)
                {
                    gameTimerIsRunning = false;
                    timerGameSessionSeconds = 0;
                    ShowToast("↺ Oyun oturum sayacı sıfırlandı (günlük toplam korundu)");
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
                    ShowToast(pomoIsBreakPhase ? "☕ Mola süresine geçildi (" + pomoBreakMinutes + " dk)" : "📚 Odak süresine geçildi (" + pomoFocusMinutes + " dk)");
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

            // Right: 3 Daily Stat Cards + Overlay Collapsible Pomodoro Settings
            Grid rightStatsGrid = new Grid();
            rightStatsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rightStatsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rightStatsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Collapsible Pomodoro Settings Bar (Overlay in Column 2 so toggling it doesn't shrink heatmap)
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

            // Focus adjuster
            StackPanel fAdj = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            fAdj.Children.Add(new TextBlock { Text = "Odak:", Foreground = HexBrush("#C7B9AE"), FontSize = 10.5, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center });
            Button bfMinus = CreateStyledIconButton("#221C18", "#4A3C34", "Odak süresini azalt");
            bfMinus.Padding = new Thickness(5, 1, 5, 1);
            bfMinus.Content = new TextBlock { Text = "−", Foreground = HexBrush("#FAF6F2"), FontSize = 10.5 };
            txtPomoFocusSettingVal = new TextBlock { Text = pomoFocusMinutes + " dk", Foreground = HexBrush("#FAF6F2"), FontSize = 10.5, FontWeight = FontWeights.Bold, Margin = new Thickness(5, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
            Button bfPlus = CreateStyledIconButton("#221C18", "#4A3C34", "Odak süresini artır");
            bfPlus.Padding = new Thickness(5, 1, 5, 1);
            bfPlus.Content = new TextBlock { Text = "+", Foreground = HexBrush("#FAF6F2"), FontSize = 10.5 };
            bfMinus.Click += (s, e) => AdjustPomodoroSettings(-5, 0);
            bfPlus.Click += (s, e) => AdjustPomodoroSettings(5, 0);
            fAdj.Children.Add(bfMinus); fAdj.Children.Add(txtPomoFocusSettingVal); fAdj.Children.Add(bfPlus);
            Grid.SetColumn(fAdj, 0);
            psRow.Children.Add(fAdj);

            // Break adjuster
            StackPanel bAdj = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            bAdj.Children.Add(new TextBlock { Text = "Mola:", Foreground = HexBrush("#C7B9AE"), FontSize = 10.5, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center });
            Button bbMinus = CreateStyledIconButton("#221C18", "#4A3C34", "Mola süresini azalt");
            bbMinus.Padding = new Thickness(5, 1, 5, 1);
            bbMinus.Content = new TextBlock { Text = "−", Foreground = HexBrush("#FAF6F2"), FontSize = 10.5 };
            txtPomoBreakSettingVal = new TextBlock { Text = pomoBreakMinutes + " dk", Foreground = HexBrush("#FAF6F2"), FontSize = 10.5, FontWeight = FontWeights.Bold, Margin = new Thickness(5, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
            Button bbPlus = CreateStyledIconButton("#221C18", "#4A3C34", "Mola süresini artır");
            bbPlus.Padding = new Thickness(5, 1, 5, 1);
            bbPlus.Content = new TextBlock { Text = "+", Foreground = HexBrush("#FAF6F2"), FontSize = 10.5 };
            bbMinus.Click += (s, e) => AdjustPomodoroSettings(0, -1);
            bbPlus.Click += (s, e) => AdjustPomodoroSettings(0, 1);
            bAdj.Children.Add(bbMinus); bAdj.Children.Add(txtPomoBreakSettingVal); bAdj.Children.Add(bbPlus);
            Grid.SetColumn(bAdj, 1);
            psRow.Children.Add(bAdj);
            psStack.Children.Add(psRow);

            // Quick Presets Row
            StackPanel presetsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
            int[,] pPresets = { { 25, 5 }, { 45, 10 }, { 50, 10 }, { 60, 15 } };
            for (int pi = 0; pi < 4; pi++)
            {
                int pf = pPresets[pi, 0];
                int pb = pPresets[pi, 1];
                Button bp = CreateStyledIconButton("#26201D", "#3E322B", pf + " dk odak / " + pb + " dk mola");
                bp.Padding = new Thickness(6, 2, 6, 2);
                bp.Margin = new Thickness(0, 0, 4, 0);
                bp.Content = new TextBlock { Text = pf + "/" + pb, Foreground = HexBrush("#D7CBC2"), FontSize = 9.5, FontWeight = FontWeights.SemiBold };
                bp.Click += (s, e) =>
                {
                    pomoFocusMinutes = pf;
                    pomoBreakMinutes = pb;
                    if (!pomoIsRunning) pomoRemainingSeconds = (pomoIsBreakPhase ? pomoBreakMinutes : pomoFocusMinutes) * 60;
                    txtPomoFocusSettingVal.Text = pomoFocusMinutes + " dk";
                    txtPomoBreakSettingVal.Text = pomoBreakMinutes + " dk";
                    SaveSettings();
                    UpdateTimerWidgetUi();
                };
                presetsRow.Children.Add(bp);
            }
            psStack.Children.Add(presetsRow);
            timerPomoSettingsPopup.Child = psStack;

            // 3 Daily Summary Stat Cards (exact Turkish metrics requested)
            Border statStudyCard = CreateTimerStatCard(timerStudyColorHex, "📚 Bugün Çalışılan", out txtTodayStudyVal, out timerStudyStatBar);
            Border statGameCard = CreateTimerStatCard(timerGameColorHex, "🎮 Bugün Oyun Oynanan", out txtTodayGameVal, out timerGameStatBar);
            Border statPcCard = CreateTimerStatCard(accentColorHex, "🖥 Bilgisayar Açık Kalma", out txtTodayPcVal, out timerPcStatBar, true);
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

            // ==========================================
            // 3. BOTTOM: RESPONSIVE PIXEL-EXACT HEATMAP
            // ==========================================
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
                Text = "0 dk",
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

                string[] trShortMonths = { "", "Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara" };
                DateTime today = DateTime.Today;
                int todayDowOffset = ((int)today.DayOfWeek + 6) % 7; // Monday = 0 .. Sunday = 6
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

                        mLbl.Text = trShortMonths[mToLabel];
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

        private string FormatDurationTurkish(int totalSeconds)
        {
            if (totalSeconds <= 0) return "0 dk";
            if (totalSeconds < 60) return totalSeconds + " sn";
            int totalMin = totalSeconds / 60;
            int hours = totalMin / 60;
            int mins = totalMin % 60;
            if (hours == 0) return totalMin + " dk";
            return string.Format("{0}s {1:00}dk", hours, mins);
        }

        private string FormatActivityTooltip(DateTime date)
        {
            string[] trMonths = { "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };
            string[] trDays = { "Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi" };
            DailyActivityRecord rec;
            activityByDate.TryGetValue(date.Date, out rec);
            int st = rec != null ? rec.StudySeconds : 0;
            int gm = rec != null ? rec.GameSeconds : 0;
            int pc = rec != null ? rec.PcSeconds : 0;

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0} {1} {2} {3}\n📚 Ders Çalışılan: {4}\n🎮 Oyun Oynanan: {5}",
                date.Day, trMonths[date.Month], date.Year, trDays[(int)date.DayOfWeek],
                FormatDurationTurkish(st),
                FormatDurationTurkish(gm));
            if (rec != null && rec.GameBreakdown != null && rec.GameBreakdown.Count > 0)
            {
                List<KeyValuePair<string, int>> topGames = new List<KeyValuePair<string, int>>(rec.GameBreakdown);
                topGames.Sort((a, b) => b.Value.CompareTo(a.Value));
                int take = Math.Min(3, topGames.Count);
                for (int gi = 0; gi < take; gi++)
                {
                    if (topGames[gi].Value <= 0) continue;
                    sb.AppendFormat("\n   • {0}: {1}", topGames[gi].Key, FormatDurationTurkish(topGames[gi].Value));
                }
            }
            sb.AppendFormat("\n🖥 Bilgisayar Açık: {0}\n(Detay için tıklayın)", FormatDurationTurkish(pc));
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
                ShowToast("🎮 Eğlence Modu (Başlatmak için ▶ tuşuna basın)");
            }
            else if (timerCurrentMode == 2)
            {
                if (pomoRemainingSeconds <= 0)
                    pomoRemainingSeconds = (pomoIsBreakPhase ? pomoBreakMinutes : pomoFocusMinutes) * 60;
                ShowToast("📚 Ders Modu (Başlatmak için ▶ tuşuna basın)");
            }
            else
            {
                ShowToast("🖥 Default Mod (Zamanlayıcı hazır)");
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

            // Special case: Minecraft / Java edition runs inside javaw.exe / java.exe
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

            // Check well-known game library directories
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
                    string closedGame = string.IsNullOrEmpty(activeGameName) ? "Oyun" : activeGameName;
                    activeGamePid = 0;
                    activeGameName = "";
                    lastCheckedNonGamePid = 0;
                    lastCheckedWasJava = false;

                    // When the detected game closes, ALWAYS return to Default Mode
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
                            trayIcon.ShowBalloonTip(4000, "🖥 Oyun Kapatıldı",
                                closedGame + " kapatıldı. Otomatik olarak Default Moda geçildi.",
                                System.Windows.Forms.ToolTipIcon.Info);
                        }
                    }
                    catch { }
                    ShowToast("🖥 Oyun kapatıldı (" + closedGame + ") • Default Moda geçildi");
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
                        trayIcon.ShowBalloonTip(6000, "⚠️ Ders Modunda Oyun Algılandı!",
                            detectedName + " açıldığı için Pomodoro duraklatıldı ve otomatik olarak Eğlence Moduna geçildi!",
                            System.Windows.Forms.ToolTipIcon.Warning);
                    }
                }
                catch { }
                ShowToast("⚠️ Ders modundayken oyun açıldı (" + detectedName + ")! Eğlence moduna geçildi.");
            }
            else if (prevMode == 0)
            {
                try
                {
                    if (trayIcon != null)
                    {
                        trayIcon.ShowBalloonTip(4000, "🎮 Oyun Algılandı",
                            detectedName + " algılandı. Otomatik olarak Eğlence Moduna geçildi.",
                            System.Windows.Forms.ToolTipIcon.Info);
                    }
                }
                catch { }
                ShowToast("🎮 Oyun algılandı (" + detectedName + ") • Eğlence Modu aktif");
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
            if (txtPomoFocusSettingVal != null) txtPomoFocusSettingVal.Text = pomoFocusMinutes + " dk";
            if (txtPomoBreakSettingVal != null) txtPomoBreakSettingVal.Text = pomoBreakMinutes + " dk";
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

                    string title = finishedFocus ? "Odaklanma Oturumu Tamamlandı!" : "Mola Süresi Bitti!";
                    string msg = finishedFocus
                        ? string.Format("{0} dk odaklanma bitti! {1} dk mola başlıyor.", pomoFocusMinutes, pomoBreakMinutes)
                        : string.Format("{0} dk mola bitti! Yeni {1} dk odaklanma oturumu başlıyor.", pomoBreakMinutes, pomoFocusMinutes);

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

            if (syncActivityToSite)
            {
                timerCloudSyncTickCounter++;
                if (timerCloudSyncTickCounter >= 900)
                {
                    timerCloudSyncTickCounter = 0;
                    Task.Run(() => SyncTimerActivityToSiteAsync(false, false));
                }
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
                    txtTodayPcVal.Text = FormatDurationTurkish(todayRec.PcSeconds);
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
                // 1. Update 3 Mode Pill Buttons
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

                // 2. Update Dial Center Text & 24 Radial Ticks
                string activeTickHex = timerStudyColorHex;
                string dialCenterStr = "00:00";
                int litTicks = 0;
                bool activeRunning = false;

                if (timerCurrentMode == 0)
                {
                    activeRunning = defaultTimerIsRunning;
                    txtTimerDialPhase.Text = "Zamanlayıcı";
                    dialCenterStr = FormatClockMMSS(defaultTimerSeconds);
                    txtTimerDialSub.Text = defaultTimerIsRunning ? "⏱ Sayılıyor" : (defaultTimerSeconds > 0 ? "⏸ Duraklatıldı" : "⏱ Hazır");
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
                    txtTimerDialPhase.Text = "Eğlence / Oyun Modu";
                    dialCenterStr = FormatClockMMSS(timerGameSessionSeconds);
                    if (gameTimerIsRunning)
                    {
                        string subText = "🎮 Kaydediliyor";
                        if (!string.IsNullOrEmpty(activeGameName))
                        {
                            string dispName = activeGameName.Length > 18 ? activeGameName.Substring(0, 16) + ".." : activeGameName;
                            subText = "🎮 " + dispName;
                        }
                        txtTimerDialSub.Text = subText;
                    }
                    else
                    {
                        txtTimerDialSub.Text = timerGameSessionSeconds > 0 ? "⏸ Duraklatıldı" : "🎮 Hazır";
                    }
                    activeTickHex = timerGameColorHex;
                    litTicks = (timerGameSessionSeconds == 0 && !gameTimerIsRunning) ? 0 : Math.Max(1, ((timerGameSessionSeconds % 3600) * 24) / 3600);
                }
                else
                {
                    activeRunning = pomoIsRunning;
                    txtTimerDialPhase.Text = pomoIsBreakPhase ? "Mola Oturumu" : "Odaklanma oturumu";
                    int totalPhaseSec = Math.Max(1, (pomoIsBreakPhase ? pomoBreakMinutes : pomoFocusMinutes) * 60);
                    if (pomoRemainingSeconds == totalPhaseSec && !pomoIsRunning)
                    {
                        dialCenterStr = (pomoIsBreakPhase ? pomoBreakMinutes : pomoFocusMinutes) + " dk";
                        txtTimerDialSub.Text = pomoIsBreakPhase ? "☕ Hazır" : "📚 Hazır";
                    }
                    else
                    {
                        dialCenterStr = FormatClockMMSS(pomoRemainingSeconds);
                        txtTimerDialSub.Text = pomoIsRunning ? (pomoIsBreakPhase ? "☕ Dinlen" : "📚 Çalışılıyor") : "⏸ Duraklatıldı";
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

                // 3. Play/Pause icon & color matching active mode (cached to avoid per-tick Brush allocations)
                if (txtPomoPlayPauseIcon != null && btnPomoPlayPause != null)
                {
                    txtPomoPlayPauseIcon.Text = activeRunning ? "⏸" : "▶";
                    txtPomoPlayPauseIcon.Margin = new Thickness(activeRunning ? 0 : 1.5, 0, 0, 0);

                    string playBtnKey = timerCurrentMode + "|" + (activeRunning ? "1" : "0") + "|" + accentColorHex + "|" + timerStudyColorHex + "|" + timerGameColorHex;
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
                            ? "Zamanlayıcıyı Başlat / Duraklat"
                            : (timerCurrentMode == 1 ? "Eğlence Sayacını Başlat / Duraklat" : "Pomodoro'yu Başlat / Duraklat");
                    }
                    btnPomoPlayPause.Background = cachedPlayBtnBg;
                    btnPomoPlayPause.BorderBrush = cachedPlayBtnBd;
                }

                // 4. Today Stats Labels
                DailyActivityRecord rec = GetOrCreateActivityRecord(DateTime.Today);
                if (txtTodayStudyVal != null) txtTodayStudyVal.Text = FormatDurationTurkish(rec.StudySeconds);
                if (txtTodayGameVal != null) txtTodayGameVal.Text = FormatDurationTurkish(rec.GameSeconds);
                if (txtTodayPcVal != null) txtTodayPcVal.Text = FormatDurationTurkish(rec.PcSeconds);
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
                Title = "Timer Widgeti Ayarları",
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
            TryApplyWindowIcon(timerSettingsWindow);

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

            // Sticky Draggable Header
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
            hdrTitles.Children.Add(new TextBlock { Text = "⏱ Timer Widgeti Ayarları", Foreground = HexBrush("#FAF6F2"), FontSize = 14.5, FontWeight = FontWeights.Bold });
            hdrTitles.Children.Add(new TextBlock { Text = "Günlere detaylı bakış, Pomodoro süreleri ve ders/oyun renkleri", Foreground = HexBrush("#9E8E82"), FontSize = 10.5, Margin = new Thickness(0, 1, 0, 0) });
            Grid.SetColumn(hdrTitles, 0);
            hdr.Children.Add(hdrTitles);

            Button btnClose = CreateStyledIconButton("#2E2520", "#4A3C34", "Kapat");
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

            // Card 1: Detailed Day View Entry
            Border detailCard = CreateSettingsSectionCard("📊 Günlere Detaylı Bakış", "Takvim karesine basarak o gün oynadığın oyunları, çalışma ve PC süreni detaylı incele");
            StackPanel detailStack = (StackPanel)detailCard.Child;
            Button btnOpenDetail = CreateStretchSettingsButton("#38281E", accentColorHex, "Seçili günlerin detaylı aktivite ve oyun dökümünü aç");
            btnOpenDetail.Padding = new Thickness(12, 9, 12, 9);
            Grid openDetGrid = new Grid();
            openDetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            openDetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            openDetGrid.Children.Add(new TextBlock { Text = "🔍 Günlere Detaylı Bak (Oyun Geçmişi)", Foreground = HexBrush("#FAF6F2"), FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            TextBlock arrowTb = new TextBlock { Text = "Aç →", Foreground = HexBrush(accentColorHex), FontSize = 11.5, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(arrowTb, 1);
            openDetGrid.Children.Add(arrowTb);
            btnOpenDetail.Content = openDetGrid;
            btnOpenDetail.Click += (s, e) => OpenTimerDetailedViewWindow(DateTime.Today);
            detailStack.Children.Add(btnOpenDetail);
            body.Children.Add(detailCard);

            // Card 2: Pomodoro Durations
            Border pomoCard = CreateSettingsSectionCard("📚 Pomodoro Süreleri", "Ders modundaki odaklanma ve mola dakikalarını özelleştirin");
            StackPanel pomoStack = (StackPanel)pomoCard.Child;
            Grid pomoRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            pomoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pomoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel fAdj = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            fAdj.Children.Add(new TextBlock { Text = "Odak:", Foreground = HexBrush("#C7B9AE"), FontSize = 11.5, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
            Button bfMinus = CreateStyledIconButton("#221C18", "#4A3C34", "Odak süresini azalt (-5 dk)");
            bfMinus.Padding = new Thickness(7, 2, 7, 2);
            bfMinus.Content = new TextBlock { Text = "−", Foreground = HexBrush("#FAF6F2"), FontSize = 12 };
            txtPomoFocusSettingVal = new TextBlock { Text = pomoFocusMinutes + " dk", Foreground = HexBrush("#FAF6F2"), FontSize = 11.5, FontWeight = FontWeights.Bold, Margin = new Thickness(7, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
            Button bfPlus = CreateStyledIconButton("#221C18", "#4A3C34", "Odak süresini artır (+5 dk)");
            bfPlus.Padding = new Thickness(7, 2, 7, 2);
            bfPlus.Content = new TextBlock { Text = "+", Foreground = HexBrush("#FAF6F2"), FontSize = 12 };
            bfMinus.Click += (s, e) => AdjustPomodoroSettings(-5, 0);
            bfPlus.Click += (s, e) => AdjustPomodoroSettings(5, 0);
            fAdj.Children.Add(bfMinus); fAdj.Children.Add(txtPomoFocusSettingVal); fAdj.Children.Add(bfPlus);
            Grid.SetColumn(fAdj, 0);
            pomoRow.Children.Add(fAdj);

            StackPanel bAdj = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            bAdj.Children.Add(new TextBlock { Text = "Mola:", Foreground = HexBrush("#C7B9AE"), FontSize = 11.5, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
            Button bbMinus = CreateStyledIconButton("#221C18", "#4A3C34", "Mola süresini azalt (-1 dk)");
            bbMinus.Padding = new Thickness(7, 2, 7, 2);
            bbMinus.Content = new TextBlock { Text = "−", Foreground = HexBrush("#FAF6F2"), FontSize = 12 };
            txtPomoBreakSettingVal = new TextBlock { Text = pomoBreakMinutes + " dk", Foreground = HexBrush("#FAF6F2"), FontSize = 11.5, FontWeight = FontWeights.Bold, Margin = new Thickness(7, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
            Button bbPlus = CreateStyledIconButton("#221C18", "#4A3C34", "Mola süresini artır (+1 dk)");
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
                Button bp = CreateStretchSettingsButton("#26201D", "#3E322B", pf + " dk odak / " + pb + " dk mola");
                bp.Padding = new Thickness(6, 4, 6, 4);
                bp.Margin = new Thickness(pi == 0 ? 0 : 3, 0, pi == 3 ? 0 : 3, 0);
                bp.Content = new TextBlock { Text = pf + "/" + pb, Foreground = HexBrush("#D7CBC2"), FontSize = 10.5, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
                bp.Click += (s, e) =>
                {
                    pomoFocusMinutes = pf;
                    pomoBreakMinutes = pb;
                    if (!pomoIsRunning) pomoRemainingSeconds = (pomoIsBreakPhase ? pomoBreakMinutes : pomoFocusMinutes) * 60;
                    if (txtPomoFocusSettingVal != null) txtPomoFocusSettingVal.Text = pomoFocusMinutes + " dk";
                    if (txtPomoBreakSettingVal != null) txtPomoBreakSettingVal.Text = pomoBreakMinutes + " dk";
                    SaveSettings();
                    UpdateTimerWidgetUi();
                };
                presetsGrid.Children.Add(bp);
            }
            pomoStack.Children.Add(presetsGrid);
            body.Children.Add(pomoCard);

            // Card 3: Study & Game Mode Colors + Live Stacked Square Preview
            Border colorCard = CreateSettingsSectionCard("🎨 Ders & Oyun Modu Renkleri", "Her saat karenin 1/10'unu doldurur (10 saati aşarsa oranlanır)");
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
                ToolTip = "Örnek: 3s Ders (%30) + 2s Oyun (%20) + %50 Default (8s PC %100 doygunluk)"
            };
            Grid.SetColumn(stackedPreviewBox, 0);
            previewRow.Children.Add(stackedPreviewBox);

            TextBlock txtPreviewHint = new TextBlock
            {
                Text = "Önizleme (3s Ders + 2s Oyun = %30 / %20 + %50 Default; 8s PC = %100 doygunluk)",
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
                if (syncActivityToSite) Task.Run(() => SyncTimerActivityToSiteAsync(true, false));
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

                Button btnPick = CreateStyledIconButton("#2A221E", "#4A3C34", "Özel renk seç");
                btnPick.Margin = new Thickness(8, 0, 0, 0);
                btnPick.Padding = new Thickness(8, 4, 8, 4);
                btnPick.Content = new TextBlock { Text = "🎨 Seç...", Foreground = HexBrush("#FAF6F2"), FontSize = 10.5 };
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

            buildColorPickerGroup("📚 Ders Modu Rengi", studyPresets, studySwatches, () => timerStudyColorHex, (h) => timerStudyColorHex = h);
            buildColorPickerGroup("🎮 Oyun / Eğlence Modu Rengi", gamePresets, gameSwatches, () => timerGameColorHex, (h) => timerGameColorHex = h);
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
                Title = "Günlere Detaylı Bakış",
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
            TryApplyWindowIcon(timerDetailedWindow);

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

            // Header
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
            hdrLeft.Children.Add(new TextBlock { Text = "📊 Günlere Detaylı Bakış", Foreground = HexBrush("#FAF6F2"), FontSize = 15, FontWeight = FontWeights.Bold });
            hdrLeft.Children.Add(new TextBlock { Text = "İstediğin günün karesine tıklayarak o günkü çalışma ve oyun detaylarını görüntüle", Foreground = HexBrush("#9E8E82"), FontSize = 10.5, Margin = new Thickness(0, 1, 0, 0) });
            Grid.SetColumn(hdrLeft, 0);
            hdr.Children.Add(hdrLeft);

            Button btnClose = CreateStyledIconButton("#2E2520", "#4A3C34", "Kapat");
            btnClose.Padding = new Thickness(9, 4, 9, 4);
            btnClose.Content = new TextBlock { Text = "✕", Foreground = HexBrush("#C7B9AE"), FontSize = 11.5 };
            btnClose.Click += (s, e) => timerDetailedWindow.Close();
            Grid.SetColumn(btnClose, 1);
            hdr.Children.Add(btnClose);
            Grid.SetRow(hdr, 0);
            root.Children.Add(hdr);

            // Row 1: Full Clickable 52-Week Heatmap Strip
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

            // Row 2: Selected Day Summary (Left) + Played Games Breakdown (Right)
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
            Button btnManualAdd = CreateStyledIconButton("#2E2520", accentColorHex, "Bu güne manuel süre ekle");
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
            leftDetailStack.Children.Add(CreateTimerStatCard(timerStudyColorHex, "📚 Çalışılan", out txtDetStudyVal, out detStudyBar));
            leftDetailStack.Children.Add(CreateTimerStatCard(timerGameColorHex, "🎮 Oyun Oynanan", out txtDetGameVal, out detGameBar));
            leftDetailStack.Children.Add(CreateTimerStatCard(accentColorHex, "🖥 Bilgisayar Açık Kalma", out txtDetPcVal, out detPcBar, true));

            leftDetailCard.Child = leftDetailStack;
            Grid.SetColumn(leftDetailCard, 0);
            bottomGrid.Children.Add(leftDetailCard);

            // Right: Games Played Breakdown Card
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
            gamesHdr.Children.Add(new TextBlock { Text = "🎮 Oynanan Oyunlar", Foreground = HexBrush("#FAF6F2"), FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
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
                string[] trDaysUpper = { "PAZAR", "PAZARTESİ", "SALI", "ÇARŞAMBA", "PERŞEMBE", "CUMA", "CUMARTESİ" };
                string[] trMonths = { "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };

                txtSelDow.Text = trDaysUpper[(int)selectedDate.DayOfWeek];
                txtSelDow.Foreground = HexBrush(accentColorHex);
                txtSelDayNum.Text = selectedDate.Day.ToString();
                txtSelMonthYear.Text = trMonths[selectedDate.Month] + " " + selectedDate.Year;

                detStudyBar.Background = HexBrush(timerStudyColorHex);
                detGameBar.Background = HexBrush(timerGameColorHex);
                detPcBar.Background = HexBrush(accentColorHex);

                DailyActivityRecord rec;
                activityByDate.TryGetValue(selectedDate, out rec);
                int st = rec != null ? rec.StudySeconds : 0;
                int gm = rec != null ? rec.GameSeconds : 0;
                int pc = rec != null ? rec.PcSeconds : 0;

                txtDetStudyVal.Text = FormatDurationTurkish(st);
                txtDetGameVal.Text = FormatDurationTurkish(gm);
                txtDetPcVal.Text = FormatDurationTurkish(pc);
                detailPreviewSwatch.Background = GetHeatmapCellBrush(selectedDate);
                detailPreviewSwatch.ToolTip = string.Format("Ders: {0} • Oyun: {1}\n(Saat başına karenin 1/10'u boyanır)", FormatDurationTurkish(st), FormatDurationTurkish(gm));

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
                        entries.Add(new KeyValuePair<string, int>(entries.Count == 0 ? "Kaydedilmiş Oyun Süresi" : "Diğer / Önceki Kayıtlar", rem));
                    }
                }

                if (entries.Count == 0)
                {
                    txtGamesBadge.Text = "Kayıt yok";
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
                        Text = "Bu tarihte kaydedilmiş bir oyun bulunmuyor.",
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
                    txtGamesBadge.Text = entries.Count + " oyun";

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
                            Text = string.Format("{0}  (%{1})", FormatDurationTurkish(gItem.Value), pct),
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

            // Build 52-week interactive heatmap strip inside detailCanvas
            string[] shortMonths = { "", "Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara" };
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
            bool manualAddIsGame = false;
            Border manualAddOverlay = new Border
            {
                Visibility = Visibility.Collapsed,
                Background = HexBrush("#B80E0B09"),
                CornerRadius = new CornerRadius(11)
            };
            Panel.SetZIndex(manualAddOverlay, 25);

            Border manualModalCard = new Border
            {
                Width = 345,
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
            mTitles.Children.Add(new TextBlock { Text = "➕ Manuel Süre Ekle", Foreground = HexBrush("#FAF6F2"), FontSize = 13.5, FontWeight = FontWeights.Bold });
            TextBlock txtManualDateSub = new TextBlock { Text = "", Foreground = HexBrush("#B8A99E"), FontSize = 10.5, Margin = new Thickness(0, 1, 0, 0) };
            mTitles.Children.Add(txtManualDateSub);
            Grid.SetColumn(mTitles, 0);
            mHdr.Children.Add(mTitles);

            Button btnCloseManual = CreateStyledIconButton("#2E2520", "#4A3C34", "Kapat");
            btnCloseManual.Padding = new Thickness(7, 2, 7, 2);
            btnCloseManual.VerticalAlignment = VerticalAlignment.Top;
            btnCloseManual.Content = new TextBlock { Text = "✕", Foreground = HexBrush("#C7B9AE"), FontSize = 11 };
            btnCloseManual.Click += (s, e) => manualAddOverlay.Visibility = Visibility.Collapsed;
            Grid.SetColumn(btnCloseManual, 1);
            mHdr.Children.Add(btnCloseManual);
            mStack.Children.Add(mHdr);

            UniformGrid catSwitch = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 10) };
            Button btnCatStudy = CreateStretchSettingsButton("#2A221E", timerStudyColorHex, "Ders kategorisine ekle");
            btnCatStudy.Margin = new Thickness(0, 0, 4, 0);
            btnCatStudy.Padding = new Thickness(8, 6, 8, 6);
            TextBlock lblCatStudy = new TextBlock { Text = "📚 Ders", Foreground = HexBrush("#FAF6F2"), FontSize = 11.5, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
            btnCatStudy.Content = lblCatStudy;

            Button btnCatGame = CreateStretchSettingsButton("#1D1815", "#3A2F29", "Oyun kategorisine ekle");
            btnCatGame.Margin = new Thickness(4, 0, 0, 0);
            btnCatGame.Padding = new Thickness(8, 6, 8, 6);
            TextBlock lblCatGame = new TextBlock { Text = "🎮 Oyun", Foreground = HexBrush("#9E8E82"), FontSize = 11.5, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
            btnCatGame.Content = lblCatGame;
            catSwitch.Children.Add(btnCatStudy);
            catSwitch.Children.Add(btnCatGame);
            mStack.Children.Add(catSwitch);

            StackPanel gameNamePanel = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 10) };
            gameNamePanel.Children.Add(new TextBlock { Text = "Oyun Adı (isteğe bağlı)", Foreground = HexBrush("#B8A99E"), FontSize = 10.5, Margin = new Thickness(0, 0, 0, 4) });
            TextBox txtManualGameName = new TextBox
            {
                Text = "",
                Background = HexBrush("#161210"),
                Foreground = HexBrush("#FAF6F2"),
                BorderBrush = HexBrush("#463932"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 5, 8, 5),
                FontSize = 11.5,
                ToolTip = "Örn: VALORANT, Fortnite, Xero (boş bırakılırsa Manuel Oyun sayılır)"
            };
            gameNamePanel.Children.Add(txtManualGameName);
            mStack.Children.Add(gameNamePanel);

            Action syncCatUi = () =>
            {
                btnCatStudy.Background = HexBrush(!manualAddIsGame ? "#2A221E" : "#1D1815");
                btnCatStudy.BorderBrush = HexBrush(!manualAddIsGame ? timerStudyColorHex : "#3A2F29");
                lblCatStudy.Foreground = HexBrush(!manualAddIsGame ? "#FAF6F2" : "#9E8E82");

                btnCatGame.Background = HexBrush(manualAddIsGame ? "#2A221E" : "#1D1815");
                btnCatGame.BorderBrush = HexBrush(manualAddIsGame ? timerGameColorHex : "#3A2F29");
                lblCatGame.Foreground = HexBrush(manualAddIsGame ? "#FAF6F2" : "#9E8E82");

                gameNamePanel.Visibility = manualAddIsGame ? Visibility.Visible : Visibility.Collapsed;
            };
            btnCatStudy.Click += (s, e) => { manualAddIsGame = false; syncCatUi(); };
            btnCatGame.Click += (s, e) => { manualAddIsGame = true; syncCatUi(); };

            Grid hmInputs = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            hmInputs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hmInputs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            hmInputs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel hCol = new StackPanel();
            hCol.Children.Add(new TextBlock { Text = "Saat", Foreground = HexBrush("#B8A99E"), FontSize = 10.5, Margin = new Thickness(0, 0, 0, 4) });
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
            mCol.Children.Add(new TextBlock { Text = "Dakika", Foreground = HexBrush("#B8A99E"), FontSize = 10.5, Margin = new Thickness(0, 0, 0, 4) });
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
            string[] chipLbls = { "+15 dk", "+30 dk", "+1s", "+2s" };
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

            Button btnCancelManual = CreateStretchSettingsButton("#26201D", "#463932", "Vazgeç");
            btnCancelManual.Padding = new Thickness(8, 7, 8, 7);
            btnCancelManual.Content = new TextBlock { Text = "İptal", Foreground = HexBrush("#C7B9AE"), FontSize = 11.5, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
            btnCancelManual.Click += (s, e) => manualAddOverlay.Visibility = Visibility.Collapsed;
            Grid.SetColumn(btnCancelManual, 0);
            footerBtns.Children.Add(btnCancelManual);

            Button btnConfirmManual = CreateStretchSettingsButton("#38281E", accentColorHex, "Seçili güne süreyi ekle");
            btnConfirmManual.Padding = new Thickness(8, 7, 8, 7);
            btnConfirmManual.Content = new TextBlock { Text = "✓ Ekle", Foreground = HexBrush("#FAF6F2"), FontSize = 11.5, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
            btnConfirmManual.Click += (s, e) =>
            {
                int hrs = 0, mins = 0;
                if (!int.TryParse(txtManualHours.Text.Trim(), out hrs) || !int.TryParse(txtManualMins.Text.Trim(), out mins) || hrs < 0 || mins < 0)
                {
                    ShowToast("Geçerli bir saat/dakika değeri girin");
                    return;
                }
                int addSec = (hrs * 3600) + (mins * 60);
                if (addSec <= 0 || addSec > 86400)
                {
                    ShowToast("Eklenecek süre 1 dk ile 24 saat arasında olmalıdır");
                    return;
                }
                DailyActivityRecord rec = GetOrCreateActivityRecord(selectedDate);
                if (manualAddIsGame)
                {
                    rec.GameSeconds += addSec;
                    string rawName = txtManualGameName.Text.Trim();
                    string gKey = SanitizeGameKey(string.IsNullOrEmpty(rawName) ? "Manuel Oyun" : rawName);
                    int prevSec;
                    rec.GameBreakdown.TryGetValue(gKey, out prevSec);
                    rec.GameBreakdown[gKey] = prevSec + addSec;
                }
                else
                {
                    rec.StudySeconds += addSec;
                }
                SaveTimerActivityData();
                if (syncActivityToSite) Task.Run(() => SyncTimerActivityToSiteAsync(true, false));
                renderSelectedDay();
                BuildTimerHeatmapGrid();
                UpdateTimerWidgetUi();
                manualAddOverlay.Visibility = Visibility.Collapsed;
                ShowToast(string.Format("✓ {0} ({1}) eklendi", FormatDurationTurkish(addSec), manualAddIsGame ? "Oyun" : "Ders"));
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
                string[] trMonthsFull = { "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };
                string[] trDaysFull = { "Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi" };
                txtManualDateSub.Text = string.Format("{0} {1} {2} {3}", selectedDate.Day, trMonthsFull[selectedDate.Month], selectedDate.Year, trDaysFull[(int)selectedDate.DayOfWeek]);
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
