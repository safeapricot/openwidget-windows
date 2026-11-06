# ✨ OpenWidget for Windows (`openwidget-windows`)

> **Ultra-Lightweight, Native Frosted-Glass Desktop Widget for Windows 10 & 11**  
> **Windows 10 & 11 için Ultra Hafif, Yarı Saydam Buzlu Cam Masaüstü Widget'ı**

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-0078D6?style=for-the-badge&logo=windows)](https://github.com/safeapricot/openwidget-windows)
[![Language](https://img.shields.io/badge/Language-C%23%20%2F%20WPF-68217A?style=for-the-badge&logo=csharp)](https://github.com/safeapricot/openwidget-windows)
[![RAM Usage](https://img.shields.io/badge/RAM%20Footprint-~23%20MB-10B981?style=for-the-badge)](https://github.com/safeapricot/openwidget-windows)
[![CPU Idle](https://img.shields.io/badge/Idle%20CPU-0.0%25-38BDF8?style=for-the-badge)](https://github.com/safeapricot/openwidget-windows)
[![Localization](https://img.shields.io/badge/Localization-English%20%7C%20T%C3%BCrk%C3%A7e-E2925C?style=for-the-badge)](https://github.com/safeapricot/openwidget-windows)

---

## 🇬🇧 English Overview

**OpenWidget** is a single-file, zero-dependency native Windows desktop widget written in C# & WPF with direct Win32, CoreAudio COM, and NVIDIA NVML C-API calls. Unlike Electron or web-based widgets that consume 300–500 MB of RAM, OpenWidget is compiled into a **~44 KB native executable** and uses only **~23 MB of RAM** with **0.0% idle CPU**.

### 🌟 Key Features
- **🌐 Bilingual Interface (`EN` / `TR`):** Choose **English** or **Türkçe** during the interactive Setup Wizard or switch languages live anytime from the Settings (`⚙`) window.
- **🧙 Interactive First-Run Setup Wizard:**
  - Automatically launches on first run (or via `--setup`).
  - **Weather Location Choice:** Auto-detect via **GPS / Location Services**, or type any **City / District** worldwide (e.g., `London`, `New York`, `Istanbul / Kadikoy`).
  - **Custom Theme Color & Shortcuts:** Pick your favorite accent color, create a Desktop shortcut, and enable Windows Startup auto-launch.
- **☀ Live Weather & 3-Day Forecast:** Current temperature, feels-like, humidity, wind speed, precipitation, and 3-day min/max forecast powered by Open-Meteo (no API key required).
- **📊 Zero-Overhead Hardware Telemetry (Auto-Detects Your PC):**
  - **CPU:** Automatically detects your processor model name, live utilization, sparkline graph, and temperature.
  - **RAM:** Live physical memory usage & progress bar via Win32 `GlobalMemoryStatusEx`.
  - **GPU & VRAM:** Direct `nvml.dll` C-API integration for NVIDIA GPUs (zero `nvidia-smi.exe` subprocess spawning!).
  - **Network & Ping:** Real-time download/upload bitrate (`Mbps` / `Kbps`) and live latency (`ms`) badge.
  - **Disks:** Automatically detects all fixed drives (`C:`, `D:`, etc.) with free/total capacity and usage bars.
- **🎙 System Tray One-Click Microphone Mute (`CoreAudio`):**
  - Left-click the microphone icon in the System Tray (or press **`Ctrl + Shift + M`** anywhere, or click `🎙` in the widget header) to instantly mute/unmute your default Windows microphone.
- **📋 Event-Driven Clipboard History:**
  - Listens to OS clipboard events via `AddClipboardFormatListener` (zero polling). Click any recent entry to copy it again, or click `🗑` to clear.
- **🎨 2D Interactive Spectrum Color Picker & Opacity Control:**
  - Open Settings (`⚙`) to drag across a live 2D HSV color spectrum, enter a custom `#HEX` code, open the full color palette dialog, or adjust frosted-glass background opacity (`30%–100%`).
- **🛡 `Win + D` (Show Desktop) Immunity & High-DPI Sharpness:**
  - Stays pinned to your desktop even when pressing `Win + D`. Per-Monitor DPI V2 aware for razor-sharp ClearType text on 1080p, 2K (1440p), and 4K displays.

### 🚀 Quick Start (Installation)
1. Download **`OpenWidget-Public-Release.zip`** (or clone this repository).
2. Double-click **`OpenWidget.exe`** (or run `Kurulum_Yardimcisi.bat` to re-open the Setup Wizard anytime).
3. Select **🇬🇧 English** or **🇹🇷 Türkçe**, pick your weather location mode (GPS or Manual City), choose your theme color, and click **Complete Setup & Launch OpenWidget**!

### 🛠 Building from Source (No Visual Studio Required)
OpenWidget compiles out-of-the-box using the native .NET Framework 64-bit compiler built into every Windows 10/11 machine:
```powershell
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /platform:x64 /lib:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF" /r:PresentationCore.dll /r:PresentationFramework.dll /r:WindowsBase.dll /r:System.Xaml.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Device.dll /out:OpenWidget.exe OpenWidget.cs
```

---

## 🇹🇷 Türkçe Genel Bakış

**OpenWidget**, Windows 10 ve 11 için C# / WPF ve doğrudan Win32, CoreAudio COM ve NVIDIA NVML C-API çağrılarıyla sıfırdan geliştirilmiş, tek dosyalık (`OpenWidget.exe`), ultra hafif bir masaüstü widget uygulamasıdır.

### 🌟 Öne Çıkan Özellikler
- **🌐 Çift Dil Desteği (`🇹🇷 Türkçe` / `🇬🇧 English`):** Kurulum Yardımcısında veya Ayarlar (`⚙`) penceresinde tek tıkla Türkçe ve İngilizce arasında anında geçiş yapabilirsiniz.
- **🧙 Etkileşimli Kurulum Yardımcısı:**
  - İlk açılışta otomatik olarak karşınıza çıkar:
  - **Hava Durumu Konumu:** İster **📍 Otomatik Algıla (GPS / Konum Servisi)** seçin, ister **🏙 Sabit İl / İlçe** (Örn: `İstanbul / Kadıköy`, `Ankara`, `Kütahya / Merkez`) yazın.
  - **Tema ve Kısayollar:** Başlangıç tema renginizi seçin, Masaüstü kısayolu ve Windows başlangıcında otomatik açılma ayarlarını tek tıkla tamamlayın.
- **☀ Canlı Hava Durumu & 3 Günlük Tahmin:** Anlık sıcaklık, hissedilen sıcaklık, nem, rüzgar, yağış ve 3 günlük tahmin.
- **📊 Her Bilgisayara Uyumlu Canlı Donanım Telemetrisi:**
  - Bilgisayarınızdaki **İşlemci (CPU)** adını, **Ekran Kartı (GPU)** modelini (`nvml.dll` doğrudan C-API), **RAM / VRAM** kullanımını, **Ağ Trafiği & Ping** değerlerini ve takılı olan sabit **Disklerinizi (`C:`, `D:` vb.)** otomatik algılar.
- **🎙 Sistem Tepsisi (System Tray) Tek Tık Mikrofon Susturma:**
  - Sağ alttaki mikrofon ikonuna sol tıklayarak, widget üzerindeki `🎙` butonuna basarak veya klavyeden **`Ctrl + Shift + M`** kısayoluyla mikrofonunuzu anında susturup açabilirsiniz.
- **📋 Olay Güdümlü (Event-Driven) Pano Geçmişi:**
  - Kopyaladığınız son 8 metni sıfır işlemci yüküyle (`WM_CLIPBOARDUPDATE`) listeler; üzerine tıkladığınız metni tekrar panoya kopyalar.
- **🎨 Canlı Spektrum Renk Seçici & Saydamlık Ayarı:**
  - Ayarlar (`⚙`) menüsünden 2 boyutlu renk spektrumu üzerinden istediğiniz rengi sürükleyerek seçebilir, `#HEX` kodu girebilir ve arka plan saydamlığını (`%30–%100`) değiştirebilirsiniz.
- **🛡 `Win + D` (Masaüstünü Göster) Koruması & 2K/4K Netlik:**
  - `Win + D` tuşlarına bastığınızda masaüstünden kaybolmaz; son bıraktığınız konumu ve boyutu otomatik hatırlar.

### 🚀 Kurulum ve Kullanım
1. **`OpenWidget-Public-Release.zip`** dosyasını indirin ve bir klasöre çıkartın.
2. **`OpenWidget.exe`** dosyasına çift tıklayın (Kurulum ekranını istediğiniz zaman tekrar açmak için `Kurulum_Yardimcisi.bat` dosyasına çift tıklayabilirsiniz).
3. Dilinizi, konum tercihinizi ve tema renginizi seçip başlatın!

---

## 📄 License / Lisans
MIT License — Created by [@safeapricot](https://github.com/safeapricot).
