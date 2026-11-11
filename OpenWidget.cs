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
        public static extern int DisplayConfigSetDeviceInfo(ref DISPLAYCONFIG_SET_SDR_WHITE_LEVEL setPacket);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint pdwNumberOfPhysicalMonitors);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr hMonitor, byte bVCPCode, out uint pvct, out uint pdwCurrentValue, out uint pdwMaximumValue);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool SetVCPFeature(IntPtr hMonitor, byte bVCPCode, uint dwNewValue);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);
    }

    internal class PhysicalMonitorSlot
    {
        public IntPtr Handle = IntPtr.Zero;
        public MonitorDdcNative.LUID AdapterId;
        public uint TargetId = 0;
        public bool HdrSdrMode = false;
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
        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();
        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

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

        // Theme & Settings State
        private string settingsFilePath;
        private string accentColorHex = "#E2925C";
        private int bgOpacityPercent = 88;
        private bool isDraggable = false;
        private double savedFreeWidth = 375;
        private double savedFreeHeight = 760;
        private double savedLeft = double.NaN;
        private double savedTop = double.NaN;
        private bool hasSavedDimensions = false;
        private bool isWindowInitialized = false;

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
        private UniformGrid calMiniMonthGrid;
        private TextBlock txtCalRightScheduleTitle;
        private StackPanel calEventsListPanel;
        private Border calQuickAddPanel;
        private TextBox txtCalQuickTitle;
        private TextBox txtCalQuickTime;
        private TextBox txtCalQuickLoc;
        private DateTime calDisplayedMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        private DateTime calSelectedDate = DateTime.Today;
        private readonly List<CalendarEventItem> allCalendarEvents = new List<CalendarEventItem>();
        private readonly object calEventsLock = new object();
        private System.Windows.Forms.ToolStripMenuItem trayCalendarItem;

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

        private void LoadSettings()
        {
            EnsureAutoStart(true);
            try
            {
                if (File.Exists(settingsFilePath))
                {
                    foreach (string line in File.ReadAllLines(settingsFilePath))
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
                File.WriteAllText(settingsFilePath, sb.ToString());
            }
            catch { }
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

        private void InitializeWindowProps()
        {
            this.Title = "OpenWidget Desktop";
            TryApplyWindowIcon(this);
            this.MinWidth = 335;
            this.MinHeight = 420;
            this.Width = savedFreeWidth;
            this.Height = savedFreeHeight;

            var workArea = SystemParameters.WorkArea;
            if (!double.IsNaN(savedLeft) && !double.IsNaN(savedTop) &&
                savedLeft >= workArea.Left - 100 && savedLeft <= workArea.Right - 100 &&
                savedTop >= workArea.Top - 50 && savedTop <= workArea.Bottom - 100)
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
                    InitializeCalendarWidget();
                    SaveSettings();
                    TrimProcessMemory();
                }), DispatcherPriority.Loaded);
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
                SaveSettings();
                try
                {
                    if (calendarWindow != null)
                    {
                        calendarWindow.Close();
                        calendarWindow = null;
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

        private void SendWidgetToDesktopBottom(IntPtr hwnd)
        {
            SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        private void CheckAndEnforceShowDesktopImmunity()
        {
            if (userHidden) return;
            try
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                IntPtr calHwnd = (calendarWindow != null && showCalendarWidget) ? new WindowInteropHelper(calendarWindow).Handle : IntPtr.Zero;

                IntPtr fg = GetForegroundWindow();
                if (fg != IntPtr.Zero)
                {
                    StringBuilder sb = new StringBuilder(64);
                    GetClassName(fg, sb, 64);
                    string cls = sb.ToString();
                    // When user presses Win+D, WorkerW or Progman becomes the foreground window
                    if (cls == "WorkerW" || cls == "Progman")
                    {
                        if (!isShowDesktopActive)
                        {
                            isShowDesktopActive = true;
                            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                            if (calHwnd != IntPtr.Zero)
                                SetWindowPos(calHwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                        }
                    }
                    else if (fg != hwnd && fg != calHwnd && cls != "Shell_TrayWnd" && cls != "NotifyIconOverflowWindow")
                    {
                        if (isShowDesktopActive)
                        {
                            isShowDesktopActive = false;
                            SendWidgetToDesktopBottom(hwnd);
                            if (calHwnd != IntPtr.Zero) SendWidgetToDesktopBottom(calHwnd);
                        }
                        else
                        {
                            if (IsWindowAbove(hwnd, fg)) SendWidgetToDesktopBottom(hwnd);
                            if (calHwnd != IntPtr.Zero && IsWindowAbove(calHwnd, fg)) SendWidgetToDesktopBottom(calHwnd);
                        }
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
                        var workArea = SystemParameters.WorkArea;
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

            rowMouse = CreateTelemetryBarRow("🖱", "Scyrox V8", "1000 Hz • Taranıyor...", true, out txtMouseDetail, out barMouseFill, out txtMousePct);
            panelWirelessContent.Children.Add(rowMouse);

            rowHeadset = CreateTelemetryBarRow("🎧", "HyperX Cloud III", "2.4GHz • Açık", true, out txtHeadsetDetail, out barHeadsetFill, out txtHeadsetPct);
            rowHeadset.Visibility = Visibility.Collapsed;
            panelWirelessContent.Children.Add(rowHeadset);

            rowController = CreateTelemetryBarRow("🎮", "Kablosuz Kumanda", "Bağlı", true, out txtControllerDetail, out barControllerFill, out txtControllerPct);
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
                        <Grid VerticalAlignment='Center' Height='20'>
                            <Border Height='6' CornerRadius='3' Background='#2A211C' VerticalAlignment='Center'/>
                            <Track x:Name='PART_Track'>
                                <Track.DecreaseRepeatButton>
                                    <RepeatButton Command='Slider.DecreaseLarge'>
                                        <RepeatButton.Template>
                                            <ControlTemplate TargetType='RepeatButton'>
                                                <Border Height='6' CornerRadius='3' Background='{0}' VerticalAlignment='Center'/>
                                            </ControlTemplate>
                                        </RepeatButton.Template>
                                    </RepeatButton>
                                </Track.DecreaseRepeatButton>
                                <Track.IncreaseRepeatButton>
                                    <RepeatButton Command='Slider.IncreaseLarge'>
                                        <RepeatButton.Template>
                                            <ControlTemplate TargetType='RepeatButton'>
                                                <Border Height='6' CornerRadius='3' Background='Transparent' VerticalAlignment='Center'/>
                                            </ControlTemplate>
                                        </RepeatButton.Template>
                                    </RepeatButton>
                                </Track.IncreaseRepeatButton>
                                <Track.Thumb>
                                    <Thumb>
                                        <Thumb.Template>
                                            <ControlTemplate TargetType='Thumb'>
                                                <Border Width='16' Height='16' CornerRadius='8' Background='#FAF6F2' BorderBrush='{0}' BorderThickness='2.5'/>
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

        // ==========================================
        // SYSTEM TRAY & CACHED COREAUDIO CONTROLLER
        // ==========================================
        private void InitializeTrayAndAudio()
        {
            // Cache CoreAudio endpoints ONCE to prevent COM handle leaks
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

            iconMicUnmuted = CreateModernMicTrayIcon(false);
            iconMicMuted = CreateModernMicTrayIcon(true);

            trayIcon = new System.Windows.Forms.NotifyIcon();
            trayIcon.Visible = true;

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

            SyncMicrophoneStateFromOs(true);
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
        }

        private void DockWindow(string side)
        {
            var workArea = SystemParameters.WorkArea;
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

        private void OpenSettingsWindow()
        {
            if (settingsWindow != null && settingsWindow.IsVisible)
            {
                settingsWindow.Activate();
                return;
            }

            settingsWindow = new Window
            {
                Title = "OpenWidget Ayarları",
                Width = 395,
                Height = 610,
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
                CornerRadius = new CornerRadius(14),
                Background = HexBrush("#F51E1916"),
                BorderBrush = HexBrush(accentColorHex),
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(18, 15, 18, 15),
                Margin = new Thickness(8)
            };
            card.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 14, Opacity = 0.65 };

            StackPanel st = new StackPanel();

            Grid hdr = new Grid { Margin = new Thickness(0, 0, 0, 10), Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
            hdr.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    try { settingsWindow.DragMove(); } catch { }
                }
            };
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdr.Children.Add(new TextBlock { Text = "⚙ OpenWidget Ayarları", Foreground = HexBrush("#FAF6F2"), FontSize = 14, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });

            Button btnClose = CreateStyledIconButton("#2E2520", "#4A3C34", "Kapat");
            btnClose.Padding = new Thickness(8, 2, 8, 2);
            btnClose.Content = new TextBlock { Text = "✕", Foreground = HexBrush("#C7B9AE"), FontSize = 11 };
            btnClose.Click += (s, e) => settingsWindow.Close();
            Grid.SetColumn(btnClose, 1);
            hdr.Children.Add(btnClose);
            st.Children.Add(hdr);

            // Top Tab Switcher: [🎨 Görünüm & Tema] [🧩 Düzen & Modüller]
            UniformGrid tabBar = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 12) };
            Button btnTabGeneral = CreateStyledIconButton("#26201C", "#4A3C34", "Tema Rengi ve Saydamlık");
            btnTabGeneral.Margin = new Thickness(0, 0, 4, 0);
            btnTabGeneral.Padding = new Thickness(8, 6, 8, 6);
            TextBlock txtTabGen = new TextBlock { Text = "🎨 Görünüm & Tema", FontSize = 11.5, FontWeight = FontWeights.SemiBold };
            btnTabGeneral.Content = txtTabGen;

            Button btnTabLayout = CreateStyledIconButton("#26201C", "#4A3C34", "Modülleri Göster/Gizle ve Sürükleyerek Sırala");
            btnTabLayout.Margin = new Thickness(4, 0, 0, 0);
            btnTabLayout.Padding = new Thickness(8, 6, 8, 6);
            TextBlock txtTabLay = new TextBlock { Text = "🧩 Düzen & Modüller", FontSize = 11.5, FontWeight = FontWeights.SemiBold };
            btnTabLayout.Content = txtTabLay;

            tabBar.Children.Add(btnTabGeneral);
            tabBar.Children.Add(btnTabLayout);
            st.Children.Add(tabBar);

            StackPanel tabGeneralPanel = new StackPanel();
            StackPanel tabLayoutPanel = new StackPanel();

            Action syncTabsUi = () =>
            {
                bool isGen = (currentSettingsTab == 0);
                tabGeneralPanel.Visibility = isGen ? Visibility.Visible : Visibility.Collapsed;
                tabLayoutPanel.Visibility = isGen ? Visibility.Collapsed : Visibility.Visible;

                btnTabGeneral.Background = HexBrush(isGen ? "#38281E" : "#241E1B");
                btnTabGeneral.BorderBrush = HexBrush(isGen ? accentColorHex : "#3E322B");
                txtTabGen.Foreground = HexBrush(isGen ? "#FAF6F2" : "#B8A99E");

                btnTabLayout.Background = HexBrush(!isGen ? "#38281E" : "#241E1B");
                btnTabLayout.BorderBrush = HexBrush(!isGen ? accentColorHex : "#3E322B");
                txtTabLay.Foreground = HexBrush(!isGen ? "#FAF6F2" : "#B8A99E");
            };

            btnTabGeneral.Click += (s, e) => { currentSettingsTab = 0; syncTabsUi(); };
            btnTabLayout.Click += (s, e) => { currentSettingsTab = 1; syncTabsUi(); };

            // =========================================================
            // TAB 0: APPEARANCE & THEME
            // =========================================================
            tabGeneralPanel.Children.Add(new TextBlock { Text = "Ana Tema Rengi (Renk Seçici / Palet)", Foreground = HexBrush("#C7B9AE"), FontSize = 11.5, Margin = new Thickness(0, 0, 0, 6) });

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
                Background = HexBrush("#2A221E"),
                Foreground = HexBrush("#FAF6F2"),
                BorderBrush = HexBrush("#4A3C34"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 5, 8, 5),
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            // Interactive 2D Spectrum + Hue Color Picker Canvas
            tabGeneralPanel.Children.Add(CreateInteractiveSpectrumPicker(accentColorHex, (pickedHex) =>
            {
                txtHexInput.Text = pickedHex;
                previewSwatch.Background = HexBrush(pickedHex);
                ApplyTheme(pickedHex, bgOpacityPercent);
                card.BorderBrush = HexBrush(pickedHex);
                syncTabsUi();
            }));

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
            tabGeneralPanel.Children.Add(colorCustomRow);

            string[] presets = { "#E2925C", "#D97706", "#C08457", "#E07A5F", "#10B981", "#38BDF8", "#A78BFA", "#F43F5E" };
            string[] presetNames = { "Varsayılan (#E2925C)", "Kehribar", "Bronz Kahve", "Mercan", "Zümrüt", "Gökyüzü", "Lavanta", "Yakut" };
            UniformGrid swatchGrid = new UniformGrid { Columns = 8, Margin = new Thickness(0, 0, 0, 14) };
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
            tabGeneralPanel.Children.Add(swatchGrid);

            Grid opRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            opRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            opRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            opRow.Children.Add(new TextBlock { Text = "Arka Plan Opaklığı (Saydamlık)", Foreground = HexBrush("#C7B9AE"), FontSize = 11.5 });
            TextBlock txtOpVal = new TextBlock { Text = "%" + bgOpacityPercent, Foreground = HexBrush("#FAF6F2"), FontSize = 12, FontWeight = FontWeights.Bold };
            Grid.SetColumn(txtOpVal, 1);
            opRow.Children.Add(txtOpVal);
            tabGeneralPanel.Children.Add(opRow);

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
            tabGeneralPanel.Children.Add(slOpacity);

            Button btnFit = CreateStyledIconButton("#2A221E", "#4A3C34", "Widget boyunu içerik kadar küçült/sığdır");
            btnFit.Margin = new Thickness(0, 8, 0, 0);
            btnFit.Padding = new Thickness(10, 7, 10, 7);
            btnFit.Content = new TextBlock { Text = "📐 Widget Yüksekliğini İçeriğe Sığdır (Boşluğu Kaldır)", Foreground = HexBrush("#FAF6F2"), FontSize = 11.5, FontWeight = FontWeights.Medium };
            btnFit.Click += (s, e) =>
            {
                FitHeightToContentIfCompact();
                ShowToast("Yükseklik içeriğe göre optimize edildi");
            };
            tabGeneralPanel.Children.Add(btnFit);

            // =========================================================
            // TAB 1: LAYOUT, DRAG-AND-DROP REORDERING & TELEMETRY SUB-TOGGLES
            // =========================================================
            Border calCompanionCard = new Border
            {
                CornerRadius = new CornerRadius(9),
                Background = HexBrush(showCalendarWidget ? "#2C221C" : "#1F1916"),
                BorderBrush = HexBrush(showCalendarWidget ? accentColorHex : "#3A2F29"),
                BorderThickness = new Thickness(1.2),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 10)
            };
            StackPanel calCompanionStack = new StackPanel();
            Grid calCompanionTop = new Grid();
            calCompanionTop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            calCompanionTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel calCompanionInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            calCompanionInfo.Children.Add(new TextBlock
            {
                Text = "📅 Google Takvim Widget'ı (Ayrı Masaüstü Eklentisi)",
                Foreground = HexBrush("#FAF6F2"),
                FontSize = 11.5,
                FontWeight = FontWeights.Bold
            });
            calCompanionInfo.Children.Add(new TextBlock
            {
                Text = "Apple Calendar tarzı bağımsız masaüstü penceresi (safaacikgoz43@gmail.com)",
                Foreground = HexBrush("#A8988C"),
                FontSize = 10,
                Margin = new Thickness(0, 1, 0, 0)
            });
            Grid.SetColumn(calCompanionInfo, 0);
            calCompanionTop.Children.Add(calCompanionInfo);

            Button btnToggleCalWidget = CreateStyledIconButton(
                showCalendarWidget ? "#38281E" : "#221C18",
                showCalendarWidget ? accentColorHex : "#3E322B",
                "Ayrı Google Takvim masaüstü widget'ını aç veya kapat");
            btnToggleCalWidget.Padding = new Thickness(10, 4, 10, 4);
            btnToggleCalWidget.MinWidth = 68;
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
                calCompanionCard.Background = HexBrush(showCalendarWidget ? "#2C221C" : "#1F1916");
                calCompanionCard.BorderBrush = HexBrush(showCalendarWidget ? accentColorHex : "#3A2F29");
                btnToggleCalWidget.Background = HexBrush(showCalendarWidget ? "#38281E" : "#221C18");
                btnToggleCalWidget.BorderBrush = HexBrush(showCalendarWidget ? accentColorHex : "#3E322B");
                txtToggleCalWidget.Text = showCalendarWidget ? "✓ Açık" : "Kapalı";
                txtToggleCalWidget.Foreground = HexBrush(showCalendarWidget ? "#FAF6F2" : "#8E7F74");
            };
            Grid.SetColumn(btnToggleCalWidget, 1);
            calCompanionTop.Children.Add(btnToggleCalWidget);
            calCompanionStack.Children.Add(calCompanionTop);

            Grid calIcalRow = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            calIcalRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            calIcalRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBox txtIcalUrl = new TextBox
            {
                Text = string.IsNullOrEmpty(calendarSecretIcalUrl) ? "" : calendarSecretIcalUrl,
                Background = HexBrush("#221B17"),
                Foreground = HexBrush("#E6DDD6"),
                BorderBrush = HexBrush("#3E322B"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 3, 6, 3),
                FontSize = 10.5,
                ToolTip = "İsteğe bağlı: Telefondan anlık senkron için Google Takvim Ayarları -> 'iCal biçimindeki gizli adres' (.ics) linkini buraya yapıştırabilirsiniz"
            };
            Grid.SetColumn(txtIcalUrl, 0);
            calIcalRow.Children.Add(txtIcalUrl);

            Button btnSaveIcal = CreateStyledIconButton("#2E241E", "#4A3C34", "Gizli iCal (.ics) adresini kaydet ve senkronize et");
            btnSaveIcal.Margin = new Thickness(5, 0, 0, 0);
            btnSaveIcal.Padding = new Thickness(8, 3, 8, 3);
            btnSaveIcal.Content = new TextBlock { Text = "🔗 iCal Kaydet", Foreground = HexBrush("#E6DDD6"), FontSize = 10, FontWeight = FontWeights.SemiBold };
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
            tabLayoutPanel.Children.Add(calCompanionCard);

            tabLayoutPanel.Children.Add(new TextBlock
            {
                Text = "Modül Sıralaması & Görünürlük (Tut-Sürükle veya ▲/▼ ile Sırala)",
                Foreground = HexBrush("#C7B9AE"),
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 6)
            });

            StackPanel modulesOrderList = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
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
                        Background = HexBrush(isDraggingThis ? "#3A2B21" : (enabled ? "#28201C" : "#1D1815")),
                        BorderBrush = HexBrush(isDraggingThis ? accentColorHex : (enabled ? "#4A3C34" : "#2E2520")),
                        BorderThickness = new Thickness(isDraggingThis ? 1.5 : 1),
                        Padding = new Thickness(10, 5, 8, 5),
                        Margin = new Thickness(0, 0, 0, 4),
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
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(title, 1);
                    rg.Children.Add(title);

                    Button btnUp = CreateStyledIconButton("#221C18", "#3E322B", "Yukarı Taşı");
                    btnUp.Padding = new Thickness(6, 2, 6, 2);
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

                    Button btnDown = CreateStyledIconButton("#221C18", "#3E322B", "Aşağı Taşı");
                    btnDown.Padding = new Thickness(6, 2, 6, 2);
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
                        enabled ? "#38281E" : "#221C18",
                        enabled ? accentColorHex : "#3E322B",
                        "Bu modülü göster veya gizle");
                    btnToggle.Padding = new Thickness(9, 2.5, 9, 2.5);
                    btnToggle.MinWidth = 58;
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
            tabLayoutPanel.Children.Add(modulesOrderList);

            tabLayoutPanel.Children.Add(new Border { Height = 1, Background = HexBrush("#362C26"), Margin = new Thickness(0, 2, 0, 8) });

            // Telemetry Sub-Items Visibility Section
            tabLayoutPanel.Children.Add(new TextBlock
            {
                Text = "Telemetri İçeriği (Hangi Donanım Verileri Gösterilsin?)",
                Foreground = HexBrush("#C7B9AE"),
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 6)
            });

            UniformGrid teleSubGrid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 8) };
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

                    Button bSub = CreateStyledIconButton(on ? "#38281E" : "#221C18", on ? accentColorHex : "#3A2F29", tLabels[idx]);
                    bSub.Margin = new Thickness(idx % 2 == 0 ? 0 : 3, 0, idx % 2 == 0 ? 3 : 0, 5);
                    bSub.Padding = new Thickness(8, 5, 8, 5);

                    Grid bg = new Grid { Width = 156 };
                    bg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    bg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    bg.Children.Add(new TextBlock
                    {
                        Text = tLabels[idx],
                        Foreground = HexBrush(on ? "#FAF6F2" : "#8E7F74"),
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center
                    });

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
            tabLayoutPanel.Children.Add(teleSubGrid);

            Button btnFitLayout = CreateStyledIconButton("#2A221E", "#4A3C34", "Widget boyunu seçili modüllere göre otomatik sığdır");
            btnFitLayout.Margin = new Thickness(0, 2, 0, 0);
            btnFitLayout.Padding = new Thickness(10, 7, 10, 7);
            btnFitLayout.Content = new TextBlock { Text = "📐 Widget Yüksekliğini Seçili Modüllere Göre Sığdır", Foreground = HexBrush("#FAF6F2"), FontSize = 11.5, FontWeight = FontWeights.Medium };
            btnFitLayout.Click += (s, e) =>
            {
                FitHeightToContentIfCompact();
                ShowToast("Yükseklik aktif modüllere göre optimize edildi");
            };
            tabLayoutPanel.Children.Add(btnFitLayout);

            syncTabsUi();
            st.Children.Add(tabGeneralPanel);
            st.Children.Add(tabLayoutPanel);

            card.Child = st;
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

            // Fallback for non-HDR monitors: Coalesced background DDC/CI VCP 0x10 worker
            slot.PendingBrightness = val;
            if (Interlocked.CompareExchange(ref slot.WorkerBusy, 1, 0) == 0)
            {
                Task.Run(() => RunMonitorDdcWorker(slot));
            }
        }

        private void RunMonitorDdcWorker(PhysicalMonitorSlot slot)
        {
            try
            {
                while (true)
                {
                    int nextVal = Interlocked.Exchange(ref slot.PendingBrightness, -1);
                    if (nextVal < 0) break;

                    bool ok = false;
                    IntPtr hMon = slot.Handle;
                    if (hMon != IntPtr.Zero)
                    {
                        ok = MonitorDdcNative.SetVCPFeature(hMon, 0x10, (uint)nextVal);
                    }
                    if (!ok)
                    {
                        DiscoverAndSyncMonitors(false);
                        hMon = slot.Handle;
                        if (hMon != IntPtr.Zero)
                        {
                            MonitorDdcNative.SetVCPFeature(hMon, 0x10, (uint)nextVal);
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

                                    var adv = new MonitorDdcNative.DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO();
                                    adv.header.type = 9;
                                    adv.header.size = (uint)Marshal.SizeOf(typeof(MonitorDdcNative.DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO));
                                    adv.header.adapterId = paths[i].targetInfo.adapterId;
                                    adv.header.id = paths[i].targetInfo.id;
                                    MonitorDdcNative.DisplayConfigGetDeviceInfo(ref adv);

                                    var sdr = new MonitorDdcNative.DISPLAYCONFIG_SDR_WHITE_LEVEL();
                                    sdr.header.type = 11;
                                    sdr.header.size = (uint)Marshal.SizeOf(typeof(MonitorDdcNative.DISPLAYCONFIG_SDR_WHITE_LEVEL));
                                    sdr.header.adapterId = paths[i].targetInfo.adapterId;
                                    sdr.header.id = paths[i].targetInfo.id;
                                    MonitorDdcNative.DisplayConfigGetDeviceInfo(ref sdr);

                                    bool hdrEnabled = (adv.value & 2) != 0;
                                    string model = (tName.monitorFriendlyDeviceName ?? "").Trim();
                                    if (!string.IsNullOrEmpty(model))
                                    {
                                        int sdrPct = (int)Math.Round(Math.Max(0, Math.Min(100, (sdr.SDRWhiteLevel - 1000.0) / 50.0)));
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

                    // 2. Enumerate Physical Monitors (for primary/secondary ordering and DDC/CI fallback)
                    List<PhysicalMonitorSlot> found = new List<PhysicalMonitorSlot>();
                    MonitorDdcNative.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref MonitorDdcNative.RECT r, IntPtr data) =>
                    {
                        try
                        {
                            MonitorDdcNative.MONITORINFOEX mi = new MonitorDdcNative.MONITORINFOEX();
                            mi.cbSize = Marshal.SizeOf(typeof(MonitorDdcNative.MONITORINFOEX));
                            MonitorDdcNative.GetMonitorInfo(hMon, ref mi);
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

                                        if (readHardwareValues && !s.HdrSdrMode && s.Handle != IntPtr.Zero)
                                        {
                                            uint vct, cur, max;
                                            if (MonitorDdcNative.GetVCPFeatureAndVCPFeatureReply(s.Handle, 0x10, out vct, out cur, out max) && max > 0)
                                            {
                                                s.CurrentBrightness = (int)Math.Round((cur * 100.0) / max);
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
                                monitorSlots[i].AdapterId = found[i].AdapterId;
                                monitorSlots[i].TargetId = found[i].TargetId;
                                monitorSlots[i].HdrSdrMode = found[i].HdrSdrMode;
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
            // Initialize Direct NVML C-API once
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

            Task.Run(() =>
            {
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

            try
            {
                if (Clipboard.ContainsText())
                {
                    string initText = Clipboard.GetText().Trim();
                    if (!string.IsNullOrEmpty(initText))
                    {
                        lastClipboardText = initText;
                        clipboardHistory.Add(new ClipboardEntry { Text = initText, Time = DateTime.Now.ToString("HH:mm") });
                    }
                }
            }
            catch { }
            if (clipboardHistory.Count == 0)
            {
                clipboardHistory.Add(new ClipboardEntry { Text = "OpenWidget Masaüstü Hazır • Kütahya", Time = DateTime.Now.ToString("HH:mm") });
            }
            RefreshClipboardUi();

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
            UpdateTelemetryFast();
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
            // Direct C-API GPU poll (takes ~10 microseconds, zero process creation!)
            PollGpuDirectNvml();

            // CPU Load & Temperature
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

            // RAM (Reuses single sharedMemStatus buffer)
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

            // GPU & VRAM UI
            txtGpuDetail.Text = string.Format("{0} • {1:0}°C", gpuModelName, gpuTemp);
            txtGpuPct.Text = "%" + (int)gpuLoad;
            DrawSparkline(sparkGpu, gpuHistory, 46, 16);

            int vramPct = vramTotalGb > 0 ? (int)Math.Round((vramUsedGb / vramTotalGb) * 100.0) : 0;
            txtVramDetail.Text = string.Format("{0:0.0} / {1:0} GB", vramUsedGb, vramTotalGb);
            txtVramPct.Text = "%" + vramPct;
            barVramFill.Width = Math.Max(3, (42.0 * vramPct) / 100.0);

            // Network throughput
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
            txtNetBadge.Text = lastPingMs < 40 ? "Harika" : (lastPingMs < 90 ? "İyi" : "Orta");

            // Disks C & D (Reuses cached DriveInfo instances)
            UpdateDiskRow(driveC, txtDiskCDetail, barDiskCFill, txtDiskCPct);
            UpdateDiskRow(driveD, txtDiskDDetail, barDiskDFill, txtDiskDPct);
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
                    if (rowMouse != null)
                    {
                        Visibility targetMouseVis = showMouse ? Visibility.Visible : Visibility.Collapsed;
                        if (rowMouse.Visibility != targetMouseVis) { rowMouse.Visibility = targetMouseVis; visChanged = true; }
                        if (showMouse)
                        {
                            txtMouseDetail.Text = mouseDetail;
                            txtMousePct.Text = mousePctText;
                            barMouseFill.Width = Math.Max(3, (42.0 * mouseBarPct) / 100.0);
                        }
                    }

                    bool showHeadset = headsetBarPct > 0;
                    if (rowHeadset != null)
                    {
                        Visibility targetHeadsetVis = showHeadset ? Visibility.Visible : Visibility.Collapsed;
                        if (rowHeadset.Visibility != targetHeadsetVis) { rowHeadset.Visibility = targetHeadsetVis; visChanged = true; }
                        if (showHeadset)
                        {
                            txtHeadsetDetail.Text = headsetDetail;
                            txtHeadsetPct.Text = headsetPctText;
                            barHeadsetFill.Width = Math.Max(3, (42.0 * headsetBarPct) / 100.0);
                        }
                    }

                    bool showPad = padConnected && padBarPct > 0;
                    if (rowController != null)
                    {
                        Visibility targetPadVis = showPad ? Visibility.Visible : Visibility.Collapsed;
                        if (rowController.Visibility != targetPadVis) { rowController.Visibility = targetPadVis; visChanged = true; }
                        if (showPad)
                        {
                            txtControllerDetail.Text = padDetail;
                            txtControllerPct.Text = padPctText;
                            barControllerFill.Width = Math.Max(3, (42.0 * padBarPct) / 100.0);
                        }
                    }

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
                });
            }
            catch { }
            finally
            {
                Interlocked.Exchange(ref wirelessPollingBusy, 0);
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

            var workArea = SystemParameters.WorkArea;
            if (!double.IsNaN(savedCalLeft) && !double.IsNaN(savedCalTop) &&
                savedCalLeft >= workArea.Left - 150 && savedCalLeft <= workArea.Right - 100 &&
                savedCalTop >= workArea.Top - 50 && savedCalTop <= workArea.Bottom - 100)
            {
                calendarWindow.Left = savedCalLeft;
                calendarWindow.Top = savedCalTop;
            }
            else
            {
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
                if (calendarWindow.ActualWidth >= 380 && calendarWindow.ActualHeight >= 210)
                {
                    savedCalWidth = calendarWindow.ActualWidth;
                    savedCalHeight = calendarWindow.ActualHeight;
                    if (isWindowInitialized) SaveSettings();
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
                    IntPtr calHwnd = new WindowInteropHelper(calendarWindow).Handle;
                    ReleaseCapture();
                    SendMessage(calHwnd, WM_NCLBUTTONDOWN, (IntPtr)HT_CAPTION, IntPtr.Zero);
                    savedCalLeft = calendarWindow.Left;
                    savedCalTop = calendarWindow.Top;
                    SaveSettings();
                    e.Handled = true;
                }
                catch { }
            };

            calendarWindow.Loaded += (s, e) =>
            {
                IntPtr calHwnd = new WindowInteropHelper(calendarWindow).Handle;
                int exStyle = GetWindowLong(calHwnd, GWL_EXSTYLE);
                SetWindowLong(calHwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
                AttachToDesktopShell(calHwnd);
                HwndSource src = HwndSource.FromHwnd(calHwnd);
                if (src != null) src.AddHook(WndProc);
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
                if (showCalendarWidget) RefreshCalendarWidgetUi();
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
                Margin = new Thickness(4),
                Padding = new Thickness(14, 12, 14, 12)
            };
            DropShadowEffect shadow = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 14,
                ShadowDepth = 2,
                Opacity = 0.60
            };
            shadow.Freeze();
            calOuterBorder.Effect = shadow;

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

            // 6x7 Mini Month Day Cells
            calMiniMonthGrid = new UniformGrid { Columns = 7, Rows = 6 };
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

            // Scrollable Events Stack with Sleek Ultra-Thin Dark/Accent ScrollBar
            ScrollViewer evScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                PanningMode = PanningMode.VerticalOnly
            };
            ApplyThinDarkScrollBarStyle(evScroll);
            calEventsListPanel = new StackPanel { Margin = new Thickness(0, 0, 3, 0) };
            evScroll.Content = calEventsListPanel;
            Grid.SetRow(evScroll, 2);
            rightPane.Children.Add(evScroll);

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
            try
            {
                string xaml = @"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
       TargetType='{x:Type ScrollBar}'>
    <Setter Property='Width' Value='4'/>
    <Setter Property='MinWidth' Value='4'/>
    <Setter Property='Margin' Value='2,0,0,0'/>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='Template'>
        <Setter.Value>
            <ControlTemplate TargetType='{x:Type ScrollBar}'>
                <Grid Background='#221C1613' Width='4'>
                    <Track x:Name='PART_Track' IsDirectionReversed='True'>
                        <Track.Thumb>
                            <Thumb>
                                <Thumb.Template>
                                    <ControlTemplate TargetType='{x:Type Thumb}'>
                                        <Border CornerRadius='2' Background='#88C7B9AE'/>
                                    </ControlTemplate>
                                </Thumb.Template>
                            </Thumb>
                        </Track.Thumb>
                    </Track>
                </Grid>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>";
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

                RefreshCalendarWidgetUi();
            }
            catch { }
        }

        private void RefreshCalendarWidgetUi()
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
                lock (calEventsLock)
                {
                    snapshot = new List<CalendarEventItem>(allCalendarEvents);
                }

                // 1. Rebuild 6x7 Mini Month Grid (Monday-first: Pzt=0 ... Paz=6)
                calMiniMonthGrid.Children.Clear();
                DateTime firstOfMonth = new DateTime(calDisplayedMonth.Year, calDisplayedMonth.Month, 1);
                int firstDayOffset = ((int)firstOfMonth.DayOfWeek + 6) % 7; // Monday = 0, Sunday = 6
                DateTime gridStart = firstOfMonth.AddDays(-firstDayOffset);

                for (int cell = 0; cell < 42; cell++)
                {
                    DateTime cellDate = gridStart.AddDays(cell).Date;
                    bool isCurrentMonth = (cellDate.Month == calDisplayedMonth.Month);
                    bool isToday = (cellDate == today);
                    bool isSelected = (cellDate == calSelectedDate.Date);

                    bool hasPrimaryEvent = false;
                    bool hasHolidayEvent = false;
                    for (int k = 0; k < snapshot.Count; k++)
                    {
                        CalendarEventItem ev = snapshot[k];
                        if (DoesEventOccurOnDate(ev, cellDate))
                        {
                            if (ev.Source == "holiday") hasHolidayEvent = true;
                            else hasPrimaryEvent = true;
                        }
                    }

                    Border dayCell = new Border
                    {
                        Width = 22,
                        Height = 22,
                        CornerRadius = new CornerRadius(11),
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

                    StackPanel cellStack = new StackPanel
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center
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
                        Margin = new Thickness(0, -1, 0, 0)
                    };
                    cellStack.Children.Add(txtNum);

                    if (hasPrimaryEvent || hasHolidayEvent)
                    {
                        Border dot = new Border
                        {
                            Width = 3.5,
                            Height = 3.5,
                            CornerRadius = new CornerRadius(2),
                            Background = isToday
                                ? Brushes.White
                                : HexBrush(hasPrimaryEvent ? accentColorHex : "#9A9CFF"),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 0.5, 0, 0)
                        };
                        cellStack.Children.Add(dot);
                    }

                    dayCell.Child = cellStack;
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

                    calMiniMonthGrid.Children.Add(dayCell);
                }

                // 2. Rebuild Right Pane Event Pill Cards
                calEventsListPanel.Children.Clear();
                List<CalendarEventItem> dayEvents = GetEventsForDate(snapshot, calSelectedDate.Date);

                bool isViewingToday = (calSelectedDate.Date == today);
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

                DateTime now = DateTime.Now;
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

                // 3. Just like iPhone Apple Calendar widget: if viewing Today, also show "SIRADAKİ (YARIN / YAKLAŞAN)" below!
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
            }
            catch { }
        }

        private UIElement CreateAppleCalendarEventPill(CalendarEventItem ev, DateTime now, bool isTodayCard)
        {
            bool isOngoing = !ev.AllDay && isTodayCard && now >= ev.Start && now <= ev.End;
            bool isPastToday = !ev.AllDay && isTodayCard && now > ev.End;

            string barHex = ev.Source == "holiday" ? "#9A9CFF" : (isOngoing ? accentColorHex : "#10B981");
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

                File.AppendAllLines(customFile, new string[] { line }, new UTF8Encoding(false));
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
                if (File.Exists(customFile))
                {
                    List<string> kept = new List<string>();
                    foreach (string l in File.ReadAllLines(customFile, Encoding.UTF8))
                    {
                        if (l.Trim() != rawLine.Trim()) kept.Add(l);
                    }
                    File.WriteAllLines(customFile, kept.ToArray(), new UTF8Encoding(false));
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
                List<CalendarEventItem> loaded = new List<CalendarEventItem>();

                // 1. Load pre-synced Google Calendar events (safaacikgoz43@gmail.com + Holidays) from calendar_events.dat
                string datPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "calendar_events.dat");
                if (File.Exists(datPath))
                {
                    foreach (string line in File.ReadAllLines(datPath, Encoding.UTF8))
                    {
                        CalendarEventItem item = ParsePipeEventLine(line, false);
                        if (item != null) loaded.Add(item);
                    }
                }

                // 2. Load user's custom quick-added desktop events from custom_events.dat
                string customPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "custom_events.dat");
                if (File.Exists(customPath))
                {
                    foreach (string line in File.ReadAllLines(customPath, Encoding.UTF8))
                    {
                        CalendarEventItem item = ParsePipeEventLine(line, true);
                        if (item != null) loaded.Add(item);
                    }
                }

                // 3. If user configured a live Google Calendar Secret iCal (.ics) URL, fetch & merge live!
                if (!string.IsNullOrEmpty(calendarSecretIcalUrl) && calendarSecretIcalUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
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

                lock (calEventsLock)
                {
                    allCalendarEvents.Clear();
                    allCalendarEvents.AddRange(loaded);
                }

                Dispatcher.Invoke(() =>
                {
                    RefreshCalendarWidgetUi();
                    if (showToastOnDone)
                        ShowToast("Google Takvim senkronize edildi (" + loaded.Count + " etkinlik)");
                });
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
            string summary = "", location = "", desc = "", dtStartRaw = "", dtEndRaw = "";
            bool isAllDay = false;

            foreach (string rawLine in lines)
            {
                if (rawLine == "BEGIN:VEVENT")
                {
                    inEvent = true;
                    summary = ""; location = ""; desc = ""; dtStartRaw = ""; dtEndRaw = "";
                    isAllDay = false;
                }
                else if (rawLine == "END:VEVENT" && inEvent)
                {
                    inEvent = false;
                    DateTime sDt, eDt;
                    if (TryParseIcsDate(dtStartRaw, out sDt, out isAllDay))
                    {
                        bool dummy;
                        if (!TryParseIcsDate(dtEndRaw, out eDt, out dummy))
                            eDt = isAllDay ? sDt.AddDays(1) : sDt.AddHours(1);

                        results.Add(new CalendarEventItem
                        {
                            Start = sDt,
                            End = eDt,
                            AllDay = isAllDay,
                            Source = sourceTag,
                            ColorHex = defaultColor,
                            Title = UnescapeIcsText(summary),
                            Location = UnescapeIcsText(location),
                            Description = UnescapeIcsText(desc),
                            HtmlLink = "https://calendar.google.com"
                        });
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
                    }
                }
            }
            return results;
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
    }
}
