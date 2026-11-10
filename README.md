# OpenWidget Desktop — Personal Edition (`personal` branch)

> **Not:** Bu dal (`personal`), [OpenWidget](https://github.com/safeapricot/openwidget-windows) projesinin yerel ağdaki akıllı ev cihazları (Matter / WiZ ampul) ve özel donanım profilleri için özelleştirilmiş **Personal Edition** sürümünü içerir. Genel kullanıma uygun, çok dilli (TR/EN) ve otomatik donanım algılamalı sürüm için [`main` dalına](https://github.com/safeapricot/openwidget-windows/tree/main) göz atabilirsiniz.

**OpenWidget Personal**, Windows masaüstü için geliştirilmiş; sıfır harici DLL/çalışma zamanı bağımlılığına sahip (`~210 KB` tek `.exe`), hafif (`~9 MB RAM`), **2K / 4K High-DPI** uyumlu, yerel (native) bir **C# / WPF cam (frosted-glass) masaüstü widget ve kontrol panelidir**.

Hava durumu, canlı donanım telemetrisi, yerel ağ üzerinden **Matter / WiZ akıllı ampul** kontrolü (2D RGB renk spektrumu dahil), **HDR/SDR & DDC/CI monitör parlaklığı kontrolü**, **kablosuz ekipman batarya takibi**, **sürükle-bırak modül düzenleyici**, **pano geçmişi** ve sistem tepsisinden **tek tıkla mikrofon susturma** özelliklerini tek bir modern arayüzde birleştirir.

---

## Temel Özellikler

### 1. Matter / WiZ Akıllı Ampul Kontrolü (Yerel LAN / UDP)
- **Bulutsuz & Gecikmesiz Kontrol:** Yerel ağdaki WiZ / Matter destekli akıllı ampul ile doğrudan `UDP 38899` portu üzerinden (`getPilot` / `setPilot`) haberleşir.
- **Canlı Parlaklık Slider'ı:** `%10` ile `%100` arasında parlaklığı gerçek zamanlı ve akıcı olarak değiştirir (`80ms` UDP debounce ile).
- **Hızlı Sahne Modları & 2D RGB Özel Renk Spektrumu (`Özel`):**
  - Tek tıkla `Gece %10 (2200K)`, `Sıcak (2700K)` ve `Gün Işığı (4200K)` modları arasında geçiş yapar.
  - **`Özel`** butonuna tıklandığında açılan **2D HSV/RGB Renk Spektrumu** ve hızlı renk paletleri ile ampulün rengini fareyle sürükleyerek anlık olarak değiştirebilir.
- **Çift Yönlü Senkronizasyon:** Telefon uygulamasından yapılan parlaklık, renk veya açma/kapama değişikliklerini otomatik algılayıp arayüzü günceller.

### 2. Monitör Parlaklığı Kontrolü (HDR SDR White Level & DDC/CI)
- **Çift Monitör Desteği (`Lenovo 27Q-11` & `ViewSonic VX2428`):** Twinkle Tray gibi yüksek RAM tüketen arka plan uygulamalarına gerek kalmadan monitör parlaklığını doğrudan kontrol eder.
- **HDR Monitör Kilitlenmesini Aşan Hibrit Mimari:**
  - **Windows DisplayConfig (`DisplayConfigSetDeviceInfo` — SDR White Level):** HDR açıkken kendini kilitleyen monitörlerde bile SDR içerik parlaklığını (`1000` – `6000` nits ölçeği) pürüzsüz ve gecikmesiz olarak değiştirir.
  - **Donanımsal DDC/CI (`dxva2.dll` VCP `0x10`) & WMI Fallback:** Donanım arka ışık parlaklığını destekleyen monitörlerle senkronize çalışır.
- **Birlikte / Ayrı Modu & Hızlı Ön Ayarlar:** Monitörleri tek slider ile senkronize (`Birlikte`) veya bağımsız (`Ayrı`) kontrol etme; `%15`, `%40`, `%75`, `%100` hızlı parlaklık butonları.

### 3. Kablosuz Cihazlar (HID / WMI / XInput Batarya Telemetrisi)
- **Scyrox V8 (2.4GHz Wireless Mouse):** Özel HID Feature/Input Report protokolü ve gerçek zamanlı USB paket zamanlaması analizi ile **batarya yüzdesi (`%`)**, **voltaj (`V`)** ve **aktif Polling Rate (`125Hz` – `8000Hz`)** değerlerini sürücüsüz okur.
- **HyperX Cloud III Wireless & Kablosuz Kontrolcüler:** Windows PnP `DEVPKEY_Device_BatteryPercentage` ve `XInput` API'leri üzerinden kulaklık ve gamepad batarya durumunu canlı listeler.

### 4. Canlı Sistem Telemetrisi
- **İşlemci (CPU):** Anlık kullanım yüzdesi, canlı akış grafiği (sparkline) ve işlemci sıcaklığı (`°C`).
- **Bellek (RAM):** Kullanılan / Toplam GB bilgisi, doluluk yüzdesi ve ilerleme çubuğu.
- **Ekran Kartı (GPU & VRAM):** Doğrudan **`nvml.dll`** (NVIDIA Management Library C-API) üzerinden sıfır konsol/süreç yüküyle anlık GPU sıcaklığı (`°C`), GPU kullanımı (`%`), canlı yük grafiği ve VRAM kullanımı (`GB` & `%`).
- **Ağ Trafiği & Ping:** Aktif ağ bağdaştırıcısı üzerinden anlık İndirme (`↓`) / Yükleme (`↑`) hızı, canlı gecikme süresi (`ms`) ve bağlantı kalitesi rozeti.
- **Disk Durumu:** Sabit disklerin boş/toplam kapasitesi ve doluluk çubukları.

### 5. Sürükle-Bırak Düzen & Modül Yönetimi (`Ayarlar → Düzen & Modüller`)
- **Modül Sıralama (Drag & Drop):** *Hava Durumu, Akıllı Ampul, Monitör Parlaklığı, Kablosuz Cihazlar, Telemetri* ve *Pano Geçmişi* kartlarını ayarlar penceresinde `⠿` tutamacından sürükleyip bırakarak masaüstündeki dizilimini anında değiştirebilirsiniz.
- **Modül & Alt-Satır Görünürlüğü:** İstemediğiniz ana modülleri veya Telemetri altındaki tekil satırları (*CPU, RAM, GPU, VRAM, Ağ, Diskler*) tek tıkla gizleyip açabilirsiniz.

### 6. Sistem Tepsisi (System Tray) Tek Tıkla Mikrofon Susturma
- **Native CoreAudio Entegrasyonu:** Windows `IMMDeviceEnumerator` ve `IAudioEndpointVolume` COM arayüzlerini kullanarak varsayılan mikrofonu donanım seviyesinde susturur veya açar.
- **3 Farklı Kontrol Yöntemi:** Sistem tepsisi ikonuna **Sol Tık**, üst bardaki mikrofon butonu veya her yerden çalışan **`Ctrl + Shift + M`** global klavye kısayolu.

### 7. Hava Durumu, Pano Geçmişi ve Masaüstü Koruması
- **Hava Durumu:** Open-Meteo API üzerinden anlık sıcaklık, hissedilen sıcaklık, nem, rüzgar, yağış ve 3 günlük tahmin.
- **Pano Geçmişi (Clipboard History):** Kopyalanan son metinleri saatiyle listeler; üzerine tıklandığında anında tekrar panoya kopyalar.
- **"Masaüstünü Göster" (`Win + D`) Koruması:** `WndProc` mesaj kancası sayesinde `Win + D` yapıldığında widget kaybolmaz, masaüstüne sabit kalır.

---

## Proje Yapısı

```text
OpenWidget/
├── OpenWidget.cs    # Tüm uygulama, UI, Win32 Interop, CoreAudio, NVML, DDC/SDR, HID, UDP kaynak kodu
├── OpenWidget.exe   # Derlenmiş 64-bit yerel Windows çalıştırılabilir dosyası (~210 KB, sıfır harici DLL)
├── app.ico          # Uygulama ikonu
└── README.md        # Proje dokümantasyonu
```

---

## Kaynak Koddan Derleme (Harici IDE Gerekmez)

Windows içerisinde yerleşik bulunan .NET Framework 64-bit C# derleyicisi (`csc.exe`) ile tek komutla derlenebilir:

```powershell
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wpfDir = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"

& $csc /target:winexe /out:OpenWidget.exe /win32icon:app.ico /optimize+ /platform:x64 `
    /lib:"$wpfDir" `
    /r:PresentationFramework.dll /r:PresentationCore.dll /r:WindowsBase.dll `
    /r:System.Xaml.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll `
    OpenWidget.cs
```
