# OpenWidget for Windows (`openwidget-windows`)

> **Ultra-Lightweight, Native Frosted-Glass Desktop Widget for Windows 10 & 11**

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-0078D6?style=for-the-badge&logo=windows)](https://github.com/safeapricot/openwidget-windows)
[![Language](https://img.shields.io/badge/Language-C%23%20%2F%20WPF-68217A?style=for-the-badge&logo=csharp)](https://github.com/safeapricot/openwidget-windows)
[![RAM Usage](https://img.shields.io/badge/RAM%20Footprint-~23%20MB-10B981?style=for-the-badge)](https://github.com/safeapricot/openwidget-windows)
[![CPU Idle](https://img.shields.io/badge/Idle%20CPU-0.0%25-38BDF8?style=for-the-badge)](https://github.com/safeapricot/openwidget-windows)
[![Localization](https://img.shields.io/badge/Localization-English%20%7C%20T%C3%BCrk%C3%A7e-E2925C?style=for-the-badge)](https://github.com/safeapricot/openwidget-windows)

## One-Click Download

[![Download OpenWidget ZIP](https://img.shields.io/badge/DOWNLOAD%20ZIP-OpenWidget%20v2.2.0-E2925C?style=for-the-badge&logo=windows)](https://github.com/safeapricot/openwidget-windows/releases/download/v2.2.0/OpenWidget-v2.2.0.zip)

> **Quick Setup:** Click the orange button above to immediately download the `.zip` package, extract it to a folder, and run **`OpenWidget.exe`**.

## Live Preview

<p align="center">
  <img src="preview.png" alt="OpenWidget Windows Desktop Preview" width="1100" />
</p>

---

## Overview

**OpenWidget** is a single-file, zero-dependency native Windows desktop widget written in C# & WPF with direct Win32, DisplayConfig SDR/DDC, USB HID, CoreAudio COM, and NVIDIA NVML C-API calls. Unlike Electron or web-based widgets that consume 300–500 MB of RAM, OpenWidget is compiled into a **~332 KB native executable** (including embedded multi-resolution icons) and uses minimal RAM with **0.0% idle CPU**.

### Key Features
- **Interactive First-Run Setup Wizard:**
  - Automatically launches on first run (or via `--setup`).
  - **Weather Location Choice:** Auto-detect via **GPS / Location Services**, or type any **City / District** worldwide (e.g., `London`, `New York`, `Istanbul / Kadikoy`).
  - **Optional Companion Widgets, Theme Color & Shortcuts:** Pick your favorite accent color, optionally enable Calendar/Timer widgets (off by default), create a Desktop shortcut, and enable Instant Windows Startup.
- **Optional Calendar & Timer Companion Widgets (Default Off):**
  - **Calendar Widget:** Floating mini-month calendar & daily agenda with private iCal (`.ics`) feed support and quick event entry. Toggle anytime via **Settings -> Widgets** or System Tray.
  - **Timer Widgeti:** Includes Stopwatch (`Default`), Gaming/Entertainment mode (with optional automatic game detection), customizable Pomodoro focus/break mode with completion notifications, daily counters, and an accent-themed yearly activity heatmap.
- **Modern 3-Tab Scrollable Settings Window (`Appearance`, `Widgets`, `Modules`):**
  - Redesigned with a sticky header, 3 clean tabs, responsive non-overlapping cards, and sleek theme-colored thin scrollbars and sliders.
- **Instant Windows Boot & Cold-Launch Optimization:**
  - Uses multi-core background JIT (`ProfileOptimization`), asynchronous CoreAudio/NVML/hardware initialization, and disables Windows Explorer's 10-second `Serialize` startup throttle when Windows auto-start is enabled.
- **Auto-Detected Monitor Brightness Control (HDR SDR White Level & DDC/CI):**
  - Automatically detects connected monitors and their friendly model names, adding smooth brightness sliders (`0%–100%`), mouse-wheel step control (`±5%`), **`Linked / Separate`** multi-monitor sync toggle, and instant presets (`15%`, `40%`, `75%`, `100%`).
  - **HDR Lock Bypass:** Automatically uses Windows native **SDR Content Brightness (`DisplayConfigSetDeviceInfo`)** on HDR-enabled displays (where hardware VCP `0x10` is locked by monitor firmware) and hardware **DDC/CI (`dxva2.dll`)** on standard SDR displays — replacing 140 MB Electron brightness utilities with zero background overhead.
- **Customizable Layout, Drag-and-Drop Reordering & Granular Visibility (`Layout & Modules`):**
  - Open **Settings -> Layout & Modules** to individually toggle **Weather**, **Monitor Brightness**, **Wireless Devices**, **Telemetry**, and **Clipboard History** on or off.
  - **Drag & Drop Reordering:** Hold and drag any module row (or use the up/down buttons) to customize the exact vertical order of sections on your widget.
  - **Granular Telemetry Toggles:** Individually show or hide **CPU**, **RAM**, **GPU**, **VRAM**, **Network & Ping**, and **Drives** inside the Telemetry card.
- **Live Weather & 3-Day Forecast (°C / °F):** Current temperature, feels-like, humidity, wind speed, precipitation, and 3-day min/max forecast powered by Open-Meteo (no API key required), with instant **Celsius (`°C`) / Fahrenheit (`°F`)** switching in Settings.
- **Auto-Detected Wireless Peripherals Battery & Polling Rate Telemetry:**
  - Automatically detects connected wireless **Mice**, **Keyboards**, **Headsets**, and **Controllers/Gamepads** along with their real product names.
  - Supports **Windows Native Bluetooth / BLE Battery Service (`DEVPKEY_Device_BatteryLife`)**, **XInput / DirectInput Wireless Gamepads**, **CompX 4K/8K Wireless Gaming Mice** (Scyrox, VXE, ATK, etc. — including real hardware **Polling Rate `125 Hz – 8000 Hz`** and battery voltage), and **HyperX Wireless Headsets** directly via non-blocking Overlapped USB HID I/O — zero manufacturer bloatware needed.
  - Automatically hides turned-off devices (and hides the entire section if no wireless peripherals are active).
- **Zero-Overhead Hardware Telemetry (Auto-Detects Your PC):**
  - **CPU:** Automatically detects your processor model name, live utilization, sparkline graph, and temperature (`°C` shown ahead of processor name for laptop compatibility).
  - **RAM:** Live physical memory usage & progress bar via Win32 `GlobalMemoryStatusEx`.
  - **GPU & VRAM:** Direct `nvml.dll` C-API integration for NVIDIA GPUs (zero `nvidia-smi.exe` subprocess spawning).
  - **Network & Ping:** Real-time download/upload bitrate (`Mbps` / `Kbps`) and live latency (`ms`) badge.
  - **Disks:** Automatically detects all fixed drives (`C:`, `D:`, etc.) with free/total capacity and usage bars.
- **System Tray One-Click Microphone Mute (`CoreAudio`):**
  - Left-click the microphone icon in the System Tray (or press **`Ctrl + Shift + M`** anywhere, or click the mic button in the widget header) to instantly mute/unmute your default Windows microphone.
- **Event-Driven Clipboard History:**
  - Listens to OS clipboard events via `AddClipboardFormatListener` (zero polling). Click any recent entry to copy it again, or clear the list with a single click.
- **2D Interactive Spectrum Color Picker & Opacity Control:**
  - Open Settings to drag across a live 2D HSV color spectrum, enter a custom `#HEX` code, open the full color palette dialog, or adjust frosted-glass background opacity (`30%–100%`).
- **`Win + D` (Show Desktop) Immunity & High-DPI Sharpness:**
  - Stays pinned to your desktop even when pressing `Win + D`. Per-Monitor DPI V2 aware for razor-sharp ClearType text on 1080p, 2K (1440p), and 4K displays.

### Quick Start (Installation)
1. Download **`OpenWidget-Public-Release.zip`** (or clone this repository).
2. Pick your language, weather location mode (GPS or Manual City), and theme color, then click **Complete Setup & Launch OpenWidget**.

### Building from Source (No Visual Studio Required)
OpenWidget compiles out-of-the-box using the native .NET Framework 64-bit compiler built into every Windows 10/11 machine:
```powershell
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /platform:x64 /win32icon:app.ico /lib:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF" /r:PresentationCore.dll /r:PresentationFramework.dll /r:WindowsBase.dll /r:System.Xaml.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Device.dll /out:OpenWidget.exe OpenWidget.cs
```

---

## Genel Bakış

**OpenWidget**, gemini kankimle yaptığım ultra hafif bir masaüstü widget uygulamasıdır.

### Öne Çıkan Özellikler
- **Etkileşimli Kurulum Yardımcısı:**
  - İlk açılışta otomatik olarak karşınıza çıkar (`--setup`):
  - **Hava Durumu Konumu:** İster **Otomatik Algıla (GPS / Konum Servisi)** seçin, ister **Sabit İl / İlçe** (Örn: `İstanbul / Kadıköy`, `Ankara`, `Kütahya / Merkez`) yazın.
  - **İsteğe Bağlı Ek Widget'lar, Tema ve Kısayollar:** Başlangıç tema renginizi seçin, isterseniz Takvim ve Timer widget'larını açın (varsayılan kapalıdır), Masaüstü kısayolu ve hızlı Windows başlangıç açılışını tek tıkla tamamlayın.
- **İsteğe Bağlı Takvim & Timer Widget'ları (Varsayılan Kapalı):**
  - **Takvim Widget'ı:** Aylık mini takvim görünümü, günlük ajanda, `.ics` / iCal abonelik desteği ve hızlı etkinlik ekleme — istediğiniz zaman **Ayarlar → Ek Widget'lar** veya Sistem Tepsisi'nden açıp kapatabilirsiniz.
  - **Timer Widgeti:** Normal zamanlayıcı, otomatik oyun algılamalı Eğlence/Oyun modu, bildirimli ve özelleştirilebilir Pomodoro Ders modu, günlük sayaçlar ve tema rengine uyumlu yıllık aktivite ısı haritası.
- **Yenilenmiş 3 Sekmeli Ayarlar Menüsü & İnce Tema Renginde Slider/Scrollbar:**
  - Sabit üst başlık, `Görünüm`, `Ek Widget'lar` ve `Modül Düzeni` sekmeleri, kaydırılabilir kart yapısı ve tema rengiyle güncellenen ince slider & kaydırma çubukları.
- **Hızlı Windows Başlangıcı Optimizasyonu:**
  - Çok çekirdekli arka plan JIT (`ProfileOptimization`), açılışı kilitlemeyen asenkron sürücü/ses/telemetri yüklemesi ve Windows otomatik başlatma aktifken Explorer başlangıç gecikmesini sıfırlama desteği.
- **Monitör Parlaklığı Kontrolü:**
  - Bağlı monitörlerinizi ve gerçek model isimlerini otomatik algılayarak her monitör için parlaklık slider'ı (`%0–%100`), fare tekerleği ile `±%5` hassas kontrol, **`Birlikte / Ayrı`** senkronizasyon modu ve hızlı mod butonları (`%15`, `%40`, `%75`, `%100`) ekler.
  - **HDR Kilit Çözümü:** HDR açıkken donanımsal parlaklığı kilitleyen monitörlerde Windows'un yerel **SDR İçerik Parlaklığı (`DisplayConfigSetDeviceInfo`)** motorunu, standart SDR ekranlarda ise doğrudan **DDC/CI (`dxva2.dll`)** protokolünü sıfır gecikmeyle kullanır.
- **Özelleştirilebilir Widget Düzeni & Sürükle-Bırak Sıralama (`Düzen & Modüller`):**
  - **Ayarlar -> Düzen & Modüller** sekmesinden **Hava Durumu**, **Monitör Parlaklığı**, **Kablosuz Cihazlar**, **Telemetri** ve **Pano Geçmişi** bölümlerinden hangilerinin gösterileceğini tek tıkla seçebilirsiniz.
  - **Tut-Bırak (Drag & Drop) Sıralama:** Modülleri basılı tutup yukarı/aşağı sürükleyerek (veya yukarı/aşağı butonlarıyla) widget üzerindeki sıralamasını istediğiniz gibi değiştirebilirsiniz.
  - **Detaylı Telemetri Seçimi:** Telemetri kartı içindeki **İşlemci (CPU)**, **Bellek (RAM)**, **Ekran Kartı (GPU)**, **Video Belleği (VRAM)**, **Ağ Trafiği** ve **Diskler** satırlarını ayrı ayrı açıp kapatabilirsiniz.
- **Canlı Hava Durumu & 3 Günlük Tahmin (°C / °F):** Anlık sıcaklık, hissedilen sıcaklık, nem, rüzgar, yağış ve 3 günlük tahmin (Ayarlar'dan **Celsius `°C` / Fahrenheit `°F`** seçimi ile).
- **Otomatik Kablosuz Cihaz (Fare, Klavye, Kulaklık, Kumanda) Şarj & Hz Takibi:**
  - Bilgisayarınıza bağlı kablosuz **Fare**, **Klavye**, **Kulaklık** ve **Oyun Kumandalarını** türünü ve gerçek cihaz ismini algılayarak otomatik listeler.
  - **Bluetooth Cihazlar (`DEVPKEY_Device_BatteryLife`)**, **XInput / DirectInput Kablosuz Kumandalar**, **Kablosuz Oyuncu Fareleri** (Scyrox, VXE, ATK vb. — gerçek donanım **Polling Rate `125 Hz – 8000 Hz`**, voltaj ve şarj durumu dahil) doğrudan Windows çekirdeğinden okur; hiçbir üretici yazılımı gerektirmez.
  - Kapalı olan cihazları otomatik gizler; aktif kablosuz cihaz yoksa bölüm tamamen gizlenerek sıfır kalabalık yaratır.
- **Her Bilgisayara Uyumlu Canlı Donanım Telemetrisi:**
  - Bilgisayarınızdaki **İşlemci (CPU)** adını, **Ekran Kartı (GPU)** modelini (`nvml.dll` doğrudan C-API), **RAM / VRAM** kullanımını, **Ağ Trafiği & Ping** değerlerini ve takılı olan sabit **Disklerinizi (`C:`, `D:` vb.)** otomatik algılar.
- **Sistem Tepsisi (System Tray) Tek Tık Mikrofon Susturma:**
  - Sağ alttaki mikrofon ikonuna sol tıklayarak, widget üzerindeki mikrofon butonuna basarak veya klavyeden **`Ctrl + Shift + M`** kısayoluyla mikrofonunuzu anında susturup açabilirsiniz.
- **Olay Güdümlü (Event-Driven) Pano Geçmişi:**
  - Kopyaladığınız son 8 metni sıfır işlemci yüküyle (`WM_CLIPBOARDUPDATE`) listeler; üzerine tıkladığınız metni tekrar panoya kopyalar.
- **Canlı Spektrum Renk Seçici & Saydamlık Ayarı:**
  - Ayarlar menüsünden 2 boyutlu renk spektrumu üzerinden istediğiniz rengi sürükleyerek seçebilir, `#HEX` kodu girebilir ve arka plan saydamlığını (`%30–%100`) değiştirebilirsiniz.
- **`Win + D` (Masaüstünü Göster) Koruması:**
  - Masaüstünü göster'e bastığınızda gitmez. Ana sayfadaki herhangi bir widget gibi.

### Kurulum ve Kullanım
1. **`OpenWidget-Public-Release.zip`** dosyasını indirin ve bir klasöre çıkartın.
2. Konum tercihinizi ve tema renginizi seçip başlatın.

---

## License
MIT License — Created by [@safeapricot](https://github.com/safeapricot).
