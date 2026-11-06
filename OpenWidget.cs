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

        [STAThread]
        public static void Main(string[] args)
        {
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
        private RadioButton rbAutoGps;
        private RadioButton rbManualCity;
        private TextBox txtCityInput;
        private Button btnTestCity;
        private TextBlock txtStatusFeedback;
        private Button btnCustomWizardColor;
        private CheckBox chkDesktopShortcut;
        private CheckBox chkStartupRun;
        private string selectedAccent = "#E2925C";
        private GeoLookupResult resolvedLocation;
        private Button btnFinish;

        public SetupWizardWindow(string cfgPath)
        {
            this.configFilePath = cfgPath;
            this.Title = "OpenWidget Setup / Kurulum Yardımcısı";
            this.Width = 460;
            this.Height = 575;
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
                Padding = new Thickness(24, 18, 24, 20),
                Margin = new Thickness(10)
            };
            card.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 18, Opacity = 0.7 };
            card.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    try { this.DragMove(); } catch { }
                }
            };

            StackPanel root = new StackPanel();

            // Header
            Grid hdr = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titleStack = new StackPanel();
            txtWizTitle = new TextBlock { Text = "✨ OpenWidget Kurulum Yardımcısı", Foreground = HexBrush("#FAF6F2"), FontSize = 16.5, FontWeight = FontWeights.Bold };
            txtWizSubtitle = new TextBlock { Text = "Masaüstü widget'ınızı kullanmaya başlamadan önce tercihlerinizi seçin.", Foreground = HexBrush("#B8A99E"), FontSize = 11.5, Margin = new Thickness(0, 3, 0, 0) };
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
            root.Children.Add(hdr);

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
                    }
                }
            };
            Grid.SetColumn(btnCustomWizardColor, 1);
            themeSelectRow.Children.Add(btnCustomWizardColor);
            root.Children.Add(themeSelectRow);

            // Step 3: System Integration Options
            txtStep3 = new TextBlock { Text = "3. Sistem ve Kısayol Seçenekleri", Foreground = HexBrush("#E2925C"), FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
            root.Children.Add(txtStep3);

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
                Margin = new Thickness(0, 0, 0, 16),
                Cursor = Cursors.Hand
            };
            root.Children.Add(chkDesktopShortcut);
            root.Children.Add(chkStartupRun);

            // Finish Button
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
            root.Children.Add(btnFinish);

            card.Child = root;
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
            txtStep3.Text = en ? "3. System & Shortcut Options" : "3. Sistem ve Kısayol Seçenekleri";
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
                File.WriteAllText(configFilePath, sb.ToString());

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
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

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

        // Weather UI
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

        // Telemetry UI
        private TextBlock txtTelemetrySectionTitle;
        private StackPanel panelTelemetryContent;
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
                        rawCpu = Regex.Replace(rawCpu, @"\b(AMD|Intel\(R\)|Core\(TM\)|Processor|\d+-Core)\b", "", RegexOptions.IgnoreCase);
                        rawCpu = Regex.Replace(rawCpu, @"\s+", " ").Trim();
                        if (!string.IsNullOrEmpty(rawCpu)) cpuShortName = rawCpu;
                    }
                }
            }
            catch { }

            // Initialize Direct NVML C-API if user has an NVIDIA GPU
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
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsFilePath))
                {
                    foreach (string line in File.ReadAllLines(settingsFilePath))
                    {
                        int idx = line.IndexOf('=');
                        if (idx > 0)
                        {
                            string k = line.Substring(0, idx).Trim();
                            string v = line.Substring(idx + 1).Trim();
                            if (k == "Language" && (v.Equals("EN", StringComparison.OrdinalIgnoreCase) || v.Equals("TR", StringComparison.OrdinalIgnoreCase)))
                                appLanguage = v.ToUpperInvariant();
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
                sb.AppendLine("Language=" + appLanguage);
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
            }
            catch { }
        }

        private void InitializeWindowProps()
        {
            this.Title = "OpenWidget Desktop";
            this.MinWidth = 335;
            this.MinHeight = 380;
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
                    IntPtr hwnd = new WindowInteropHelper(this).Handle;
                    RemoveClipboardFormatListener(hwnd);
                    UnregisterHotKey(hwnd, HOTKEY_ID_MIC);
                }
                catch { }
                try { if (nvmlInitialized) NvmlNative.nvmlShutdown(); } catch { }
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
                IntPtr progman = FindWindow("Progman", null);
                IntPtr defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (defView == IntPtr.Zero)
                {
                    EnumWindows((topHwnd, lParam) =>
                    {
                        IntPtr dv = FindWindowEx(topHwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                        if (dv != IntPtr.Zero)
                        {
                            defView = dv;
                            return false;
                        }
                        return true;
                    }, IntPtr.Zero);
                }
                if (defView != IntPtr.Zero)
                {
                    SetWindowLongPtr64(hwnd, GWLP_HWNDPARENT, defView);
                }
            }
            catch { }
        }

        private void CheckAndEnforceShowDesktopImmunity()
        {
            if (userHidden) return;
            try
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                IntPtr fg = GetForegroundWindow();
                if (fg != IntPtr.Zero)
                {
                    StringBuilder sb = new StringBuilder(64);
                    GetClassName(fg, sb, 64);
                    string cls = sb.ToString();
                    if (cls == "WorkerW" || cls == "Progman")
                    {
                        if (!isShowDesktopActive)
                        {
                            isShowDesktopActive = true;
                            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                        }
                    }
                    else if (isShowDesktopActive && fg != hwnd)
                    {
                        isShowDesktopActive = false;
                        SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
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
                        double targetH = Math.Min(workArea.Height - 28, Math.Max(390, desiredH));
                        this.Height = targetH;
                        savedFreeHeight = targetH;
                    }
                }
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_MINIMIZE = 0xF020;
            const int WM_WINDOWPOSCHANGING = 0x0046;
            const uint SWP_HIDEWINDOW = 0x0080;

            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID_MIC)
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
                    if ((wp.flags & SWP_HIDEWINDOW) != 0)
                    {
                        wp.flags &= ~SWP_HIDEWINDOW;
                        wp.flags |= SWP_SHOWWINDOW;
                        Marshal.StructureToPtr(wp, lParam, true);
                    }
                }
                catch { }
            }
            return IntPtr.Zero;
        }

        private Brush HexBrush(string hex)
        {
            SolidColorBrush b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex);
            if (b.CanFreeze) b.Freeze();
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

                UpdateDraggableButtonUi();

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

            // 2. WEATHER SECTION
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
            rootStack.Children.Add(weatherTitleGrid);

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
            rootStack.Children.Add(weatherCard);

            // 3. TELEMETRY SECTION (No Smart Bulb bloat in Public Edition!)
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
            rootStack.Children.Add(teleHeader);

            panelTelemetryContent.Children.Add(CreateTelemetrySparkRow("⚙", L("İşlemci (CPU)", "Processor (CPU)"), cpuShortName + " • 46°C", true, out txtCpuRowLabel, out txtCpuDetail, out sparkCpu, out txtCpuPct));
            panelTelemetryContent.Children.Add(CreateTelemetryBarRow("📟", L("Bellek (RAM)", "Memory (RAM)"), "8.0 / 16 GB", true, out txtRamRowLabel, out txtRamDetail, out barRamFill, out txtRamPct));
            panelTelemetryContent.Children.Add(CreateTelemetrySparkRow("🎮", L("Ekran Kartı (GPU)", "Graphics (GPU)"), gpuModelName + " • 45°C", true, out txtGpuRowLabel, out txtGpuDetail, out sparkGpu, out txtGpuPct));
            panelTelemetryContent.Children.Add(CreateTelemetryBarRow("🎞", L("Video Belleği (VRAM)", "Video Memory (VRAM)"), "1.5 / 8 GB", true, out txtVramRowLabel, out txtVramDetail, out barVramFill, out txtVramPct));

            // Network Row
            Grid netRow = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            netRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            netRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            netRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            netRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel netLeft = new StackPanel { Orientation = Orientation.Horizontal };
            netLeft.Children.Add(CreateMiniIconBadge("⇄"));
            txtNetRowLabel = new TextBlock { Text = L("Ağ Trafiği", "Network"), Foreground = HexBrush("#F5EFEA"), FontSize = 12, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center };
            netLeft.Children.Add(txtNetRowLabel);
            Grid.SetColumn(netLeft, 0);
            netRow.Children.Add(netLeft);

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
            txtNetBadge = new TextBlock { Text = L("Harika", "Great"), FontSize = 10, FontWeight = FontWeights.SemiBold };
            accentTextBlocks.Add(txtNetBadge);
            netBadgeBorder.Child = txtNetBadge;
            Grid.SetColumn(netBadgeBorder, 2);
            netRow.Children.Add(netBadgeBorder);

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
            netRow.Children.Add(txtNetPing);
            panelTelemetryContent.Children.Add(netRow);

            // Dynamic Fixed Drives (Automatically adapts to whatever disks C:, D:, E: the user has!)
            for (int i = 0; i < fixedDrives.Count; i++)
            {
                DriveInfo d = fixedDrives[i];
                string dName = d.Name.TrimEnd('\\');
                TextBlock rowLbl;
                TextBlock det;
                Border fill;
                TextBlock pct;
                panelTelemetryContent.Children.Add(CreateTelemetryBarRow(i == 0 ? "💾" : "💿", dName + L(" Diski", " Drive"), L("... GB Boş", "... GB Free"), true, out rowLbl, out det, out fill, out pct));
                diskRowLabels.Add(rowLbl);
                diskDetailBlocks.Add(det);
                diskFillBars.Add(fill);
                diskPctBlocks.Add(pct);
            }

            rootStack.Children.Add(panelTelemetryContent);

            // 4. CLIPBOARD HISTORY SECTION
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
            rootStack.Children.Add(clipHeader);

            panelClipItems = new StackPanel();
            rootStack.Children.Add(panelClipItems);

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
            string xaml = string.Format(@"
            <Slider xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                    Minimum='30' Maximum='100' Value='{1}' IsMoveToPointEnabled='True' Cursor='Hand' Margin='0,2,0,10'>
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
            </Slider>", accentHex, (int)Math.Round(initialVal));
            return (Slider)System.Windows.Markup.XamlReader.Parse(xaml);
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
            trayMuteItem = new System.Windows.Forms.ToolStripMenuItem(L("🎙 Mikrofonu Sustur / Aç (Sol Tık)", "🎙 Mute / Unmute Microphone (Left Click)"));
            trayMuteItem.Click += (s, e) => ToggleMicrophoneMute();
            menu.Items.Add(trayMuteItem);

            traySettingsItem = new System.Windows.Forms.ToolStripMenuItem(L("⚙ Widget Ayarları (Dil, Konum & Tema)...", "⚙ Widget Settings (Language, Location & Theme)..."));
            traySettingsItem.Font = new System.Drawing.Font(traySettingsItem.Font, System.Drawing.FontStyle.Bold);
            traySettingsItem.Click += (s, e) => OpenSettingsWindow();
            menu.Items.Add(traySettingsItem);

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
                if (btnSettingsHeader != null) btnSettingsHeader.ToolTip = L("Widget Ayarları (Dil, Konum, Tema Rengi & Opaklık)", "Widget Settings (Language, Location, Theme & Opacity)");

                if (trayMuteItem != null) trayMuteItem.Text = L("🎙 Mikrofonu Sustur / Aç (Sol Tık)", "🎙 Mute / Unmute Microphone (Left Click)");
                if (traySettingsItem != null) traySettingsItem.Text = L("⚙ Widget Ayarları (Dil, Konum & Tema)...", "⚙ Widget Settings (Language, Location & Theme)...");
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
                Task.Run(() => FetchWeatherAsync());
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
                if (trayLockItem != null) trayLockItem.Text = L("📌 Konumu ve Boyutu Kilitle (Sabit Mod)", "📌 Lock Position & Size (Fixed Mode)");
                ShowToast(L("Taşınabilir Mod Açık: Sürükleyebilir veya kenarlardan boyutlandırabilirsiniz", "Unlocked Mode: Drag anywhere or resize from edges"));
            }
            else
            {
                this.ResizeMode = ResizeMode.NoResize;
                if (windowChrome != null) windowChrome.ResizeBorderThickness = new Thickness(0);
                if (resizeGripIndicator != null) resizeGripIndicator.Visibility = Visibility.Collapsed;
                if (trayLockItem != null) trayLockItem.Text = L("🔓 Taşınabilir & Boyutlandırılabilir Modu Aç", "🔓 Enable Draggable & Resizable Mode");
                SaveSettings();
                ShowToast(L("Konum ve boyut kilitlendi", "Position and size locked"));
            }
            UpdateDraggableButtonUi();
        }

        private void UpdateDraggableButtonUi()
        {
            if (btnLockDrag == null) return;
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

        private void OpenSettingsWindow()
        {
            if (settingsWindow != null && settingsWindow.IsVisible)
            {
                settingsWindow.Activate();
                return;
            }

            settingsWindow = new Window
            {
                Title = L("OpenWidget Ayarları", "OpenWidget Settings"),
                Width = 390,
                Height = 605,
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

            Border card = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = HexBrush("#F51E1916"),
                BorderBrush = HexBrush(accentColorHex),
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(18, 16, 18, 16),
                Margin = new Thickness(8)
            };
            card.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 14, Opacity = 0.65 };

            card.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    try { settingsWindow.DragMove(); } catch { }
                }
            };

            StackPanel st = new StackPanel();

            Grid hdr = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdr.Children.Add(new TextBlock { Text = L("⚙ Widget & Konum Ayarları", "⚙ Widget & Location Settings"), Foreground = HexBrush("#FAF6F2"), FontSize = 14, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });

            Button btnClose = CreateStyledIconButton("#2E2520", "#4A3C34", L("Kapat", "Close"));
            btnClose.Padding = new Thickness(8, 2, 8, 2);
            btnClose.Content = new TextBlock { Text = "✕", Foreground = HexBrush("#C7B9AE"), FontSize = 11 };
            btnClose.Click += (s, e) => settingsWindow.Close();
            Grid.SetColumn(btnClose, 1);
            hdr.Children.Add(btnClose);
            st.Children.Add(hdr);

            // 0. Language Selector Row (TR / EN)
            Grid langRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            langRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            langRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            langRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            langRow.Children.Add(new TextBlock { Text = L("🌐 Arayüz Dili / Language", "🌐 Interface Language / Dil"), Foreground = HexBrush("#C7B9AE"), FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });

            Button btnSetTr = CreateStyledIconButton(IsEn ? "#26201C" : "#38281E", IsEn ? "#4A3C34" : accentColorHex, "Türkçe");
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

            Button btnSetEn = CreateStyledIconButton(IsEn ? "#38281E" : "#26201C", IsEn ? accentColorHex : "#4A3C34", "English");
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
            st.Children.Add(langRow);

            // 1. Weather Location Setting
            st.Children.Add(new TextBlock { Text = L("Hava Durumu Konumu (GPS veya Şehir/İlçe Adı)", "Weather Location (GPS or City/District Name)"), Foreground = HexBrush("#C7B9AE"), FontSize = 11.5, Margin = new Thickness(0, 0, 0, 6) });
            Grid locRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            locRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            locRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            locRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBox txtCity = new TextBox
            {
                Text = locationName,
                Background = HexBrush("#2A221E"),
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
            btnSaveCity.Content = new TextBlock { Text = L("Şehir Ara", "Search City"), Foreground = HexBrush("#FAF6F2"), FontSize = 11, FontWeight = FontWeights.SemiBold };
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
            st.Children.Add(locRow);

            st.Children.Add(new Border { Height = 1, Background = HexBrush("#362C26"), Margin = new Thickness(0, 6, 0, 10) });

            // 2. Theme Accent Color Selection (Interactive Spectrum Picker + Palette Dialog + Swatches)
            st.Children.Add(new TextBlock { Text = L("Ana Tema Rengi (Renk Seçici / Palet)", "Primary Theme Color (Color Picker / Palette)"), Foreground = HexBrush("#C7B9AE"), FontSize = 11.5, Margin = new Thickness(0, 0, 0, 6) });

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

            st.Children.Add(CreateInteractiveSpectrumPicker(accentColorHex, (pickedHex) =>
            {
                txtHexInput.Text = pickedHex;
                previewSwatch.Background = HexBrush(pickedHex);
                ApplyTheme(pickedHex, bgOpacityPercent);
                card.BorderBrush = HexBrush(pickedHex);
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
                    }
                }
            };
            Grid.SetColumn(btnWinPalette, 3);
            colorCustomRow.Children.Add(btnWinPalette);
            st.Children.Add(colorCustomRow);

            string[] presets = { "#E2925C", "#D97706", "#C08457", "#E07A5F", "#10B981", "#38BDF8", "#A78BFA", "#F43F5E" };
            string[] presetNames = IsEn
                ? new string[] { "Default (#E2925C)", "Amber", "Bronze Brown", "Coral", "Emerald", "Sky Blue", "Lavender", "Ruby" }
                : new string[] { "Varsayılan (#E2925C)", "Kehribar", "Bronz Kahve", "Mercan", "Zümrüt", "Gökyüzü", "Lavanta", "Yakut" };
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
                };
                swatchGrid.Children.Add(sw);
            }
            st.Children.Add(swatchGrid);

            // 3. Background Opacity Slider
            Grid opRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            opRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            opRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            opRow.Children.Add(new TextBlock { Text = L("Arka Plan Opaklığı (Saydamlık)", "Background Opacity (Transparency)"), Foreground = HexBrush("#C7B9AE"), FontSize = 11.5 });
            TextBlock txtOpVal = new TextBlock { Text = IsEn ? (bgOpacityPercent + "%") : ("%" + bgOpacityPercent), Foreground = HexBrush("#FAF6F2"), FontSize = 12, FontWeight = FontWeights.Bold };
            Grid.SetColumn(txtOpVal, 1);
            opRow.Children.Add(txtOpVal);
            st.Children.Add(opRow);

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
            st.Children.Add(slOpacity);

            Button btnFit = CreateStyledIconButton("#2A221E", "#4A3C34", L("Widget boyunu içerik kadar küçült/sığdır", "Auto-fit widget height to content"));
            btnFit.Margin = new Thickness(0, 6, 0, 0);
            btnFit.Padding = new Thickness(10, 7, 10, 7);
            btnFit.Content = new TextBlock { Text = L("📐 Widget Yüksekliğini İçeriğe Sığdır (Boşluğu Kaldır)", "📐 Auto-Fit Widget Height to Content"), Foreground = HexBrush("#FAF6F2"), FontSize = 11.5, FontWeight = FontWeights.Medium };
            btnFit.Click += (s, e) =>
            {
                FitHeightToContentIfCompact();
                ShowToast(L("Yükseklik içeriğe göre optimize edildi", "Height optimized to fit content"));
            };
            st.Children.Add(btnFit);

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
        // ULTRA-LIGHT TIMERS, NVML GPU & TELEMETRY
        // ==========================================
        private void InitializeTimers()
        {
            Task.Run(() =>
            {
                try
                {
                    cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    cpuCounter.NextValue();
                }
                catch { }
                FetchWeatherAsync();
                UpdatePingAsync();
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
                clipboardHistory.Add(new ClipboardEntry { Text = L("OpenWidget Masaüstü Hazır", "OpenWidget Desktop Ready"), Time = DateTime.Now.ToString("HH:mm") });
            }
            RefreshClipboardUi();

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

            DispatcherTimer mediumTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            mediumTimer.Tick += (s, e) => Task.Run(() => UpdatePingAsync());
            mediumTimer.Start();

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
            txtCpuDetail.Text = string.Format("{0} • {1:0}°C", cpuShortName, cTemp);
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

            txtGpuDetail.Text = string.Format("{0} • {1:0}°C", gpuModelName, gpuTemp);
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
        // WEATHER FETCHER (DYNAMIC USER LOCATION)
        // ==========================================
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

                    Dispatcher.Invoke(() =>
                    {
                        txtLocationBadge.Text = locationName;
                        txtWeatherTemp.Text = Math.Round(temp) + "°";
                        txtWeatherFeels.Text = IsEn
                            ? string.Format("Feels like {0:0}°C", feels)
                            : string.Format("Hissedilen {0:0}°C", feels);
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
    }
}
