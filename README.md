# OpenWidget Desktop — Personal Edition (`personal` branch)

> **Not:** Bu dal (`personal`), [OpenWidget](https://github.com/safeapricot/openwidget-windows) projesinin yerel ağdaki akıllı ev cihazları (Matter / WiZ ampul) ve özel donanım profilleri için özelleştirilmiş **Personal Edition** sürümünü içerir. Genel kullanıma uygun, çok dilli (TR/EN) ve otomatik donanım algılamalı sürüm için [`main` dalına](https://github.com/safeapricot/openwidget-windows/tree/main) göz atabilirsiniz.

**OpenWidget Personal**, Windows masaüstü için geliştirilmiş; sıfır harici DLL/çalışma zamanı bağımlılığına sahip (`~210 KB` tek `.exe`), hafif (`~9 MB RAM`), **2K / 4K High-DPI** uyumlu, yerel (native) bir **C# / WPF cam (frosted-glass) masaüstü widget ve kontrol panelidir**.

Hava durumu, canlı donanım telemetrisi, yerel ağ üzerinden **Matter / WiZ akıllı ampul** kontrolü (2D RGB renk spektrumu dahil), **HDR/SDR & DDC/CI monitör parlaklığı kontrolü**, **kablosuz ekipman batarya takibi**, **sürükle-bırak modül düzenleyici**, **pano geçmişi** ve sistem tepsisinden **tek tıkla mikrofon susturma** özelliklerini tek bir modern arayüzde birleştirir.

---

Bu branch benim kendi bilgisayarımda kendi akıllı ampülümü kontrol edebileceğim şekilde ayrıca yapılmış bir openwidget sürümü. Publicten farklı özellik olarak WiZ ampul kontrol desteği var.


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
