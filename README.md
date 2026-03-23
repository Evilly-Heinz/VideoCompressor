# VideoCompressor

Công cụ nén video Windows 11 — chuột phải vào video → **"Compress this video"**

Output: `{tên_video}_compressed.mp4` cùng thư mục với file gốc.

---

## Cấu trúc solution

```
VideoCompressor.sln                ← mở bằng Visual Studio 2022
│
├── Compressor\                    ← C++ project (Console, x64)
│   ├── VideoCompressor.vcxproj
│   └── VideoCompressor.cpp        ← gọi ffmpeg, stream progress
│
├── VideoCompressorUI\             ← C# WPF project (.NET 8, x64)
│   ├── VideoCompressorUI.csproj
│   ├── app.manifest               ← PerMonitorV2 DPI, Win11
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml            ← Fluent dark UI, custom titlebar
│   ├── MainWindow.xaml.cs
│   └── Themes\Styles.xaml         ← Win11 Fluent color tokens + styles
│
├── scripts\
│   └── install_context_menu.reg
│
├── install.bat     ← đăng ký context menu (cần Admin)
└── uninstall.bat   ← gỡ context menu
```

Output build: `bin\Release\` (cả hai exe cùng thư mục).

---

## Build với Visual Studio 2022

### Yêu cầu
| Thành phần | Ghi chú |
|---|---|
| Visual Studio 2022 (v17+) | Community/Pro/Enterprise đều được |
| Workload: **Desktop development with C++** | cho C++ project |
| Workload: **.NET desktop development** | cho WPF project |
| .NET 8 SDK | thường đi kèm VS2022 |
| **ffmpeg.exe** | download riêng, xem bên dưới |

### Các bước
1. Mở `VideoCompressor.sln` bằng Visual Studio 2022
2. Chọn configuration **Release | x64**
3. **Build → Build Solution** (`Ctrl+Shift+B`)
4. Output nằm ở `bin\Release\`

> Debug build cũng hoạt động để dev/test.

### Tải ffmpeg.exe
1. Vào https://github.com/BtbN/FFmpeg-Builds/releases
2. Tải `ffmpeg-master-latest-win64-gpl.zip`
3. Giải nén, lấy `bin\ffmpeg.exe`
4. Copy `ffmpeg.exe` vào `bin\Release\`

---

## Cài đặt context menu

```
Chuột phải vào install.bat → "Run as administrator"
```

Sau đó: chuột phải bất kỳ `.mp4`, `.mov`, `.avi`, `.mkv`... → **"Compress this video"**

---

## Gỡ cài đặt

```
Chuột phải uninstall.bat → "Run as administrator"
```

---

## Ghi chú kỹ thuật

- **C++ project**: toolset v143 (VS2022), C++17, Unicode, x64 only
- **WPF project**: .NET 8, net8.0-windows, PerMonitorV2 DPI aware
- **UI**: custom titlebar với WindowChrome, Win11 Fluent dark palette,
  Segoe UI Variable Text font, caption buttons (min/max/close)
- **IPC**: VideoCompressorUI đọc stdout của VideoCompressor.exe theo format:
  - `PROGRESS:N` — tiến độ 0–100
  - `STATUS: STARTING / DONE / ERROR`
  - `SIZE_IN:<bytes>` / `SIZE_OUT:<bytes>`
  - `DURATION:<ms>`
