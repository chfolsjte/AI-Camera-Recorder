# AI Camera Recorder

Windows 10/11 原生 WinUI 3 錄影與本地 AI 4K 處理工具。

## 目前已實作

- C# / WinUI 3 原生桌面程式
- 非 MSIX、自包含 Windows x64 發行設定
- Windows Media Foundation USB Camera 自動列舉
- 1080P、1440P、2160P 與 30/60 FPS 錄影設定
- FFmpeg DirectShow 擷取
- NVIDIA NVENC H.265 / AV1 錄影與輸出
- MKV 安全錄影
- FFprobe 自動保留來源 FPS
- Real-ESRGAN 本地超解析與去噪流程
- 可選 GFPGAN 人臉修復流程
- FFmpeg 銳化濾鏡
- 多工作循序佇列
- 失敗、取消、完成狀態
- Windows GitHub Actions 編譯、測試、發布 artifact
- 不需要 Python
- 不需要 PowerShell

## 尚需完成與驗證

- MediaCapture 即時預覽
- 實際查詢每台鏡頭支援的格式，而不是只提供常用選項
- 鏡頭麥克風與外接麥克風選擇
- 工作暫停、取消、重新排序與失敗重試 UI
- TensorRT-RTX 原生執行提供者與 ONNX 模型封裝
- GFPGAN Windows runtime 與模型自動下載器
- FFmpeg、Real-ESRGAN、GFPGAN 的發行包下載與授權清單
- 分段/串流 AI，避免長影片產生大量 PNG
- RTX 5070 Ti 實機效能與 VRAM 測試
- AVerMedia PW315 實機格式與錄影測試

## 建置

需要 Windows 10 1809 以上與 .NET 10 SDK：

```powershell
dotnet restore AI.CameraRecorder.slnx
dotnet test tests/AI.CameraRecorder.Core.Tests/AI.CameraRecorder.Core.Tests.csproj -c Release
dotnet publish src/AI.CameraRecorder.App/AI.CameraRecorder.App.csproj -c Release -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true
```

GitHub Actions 成功後，可在工作流程的 Artifacts 下載 `AI-Camera-Recorder-win-x64`。

## 目錄

```text
src/AI.CameraRecorder.App/       WinUI 3 應用程式與核心服務
tests/AI.CameraRecorder.Core.Tests/  不依賴硬體的單元測試
.github/workflows/windows-ci.yml  Windows 編譯與發行
```

## Runtime 目錄規格

```text
AI.CameraRecorder.exe
tools/ffmpeg/ffmpeg.exe
tools/ffmpeg/ffprobe.exe
tools/realesrgan/realesrgan-ncnn-vulkan.exe
tools/gfpgan/gfpgan-ncnn-vulkan.exe
models/
```

第三方執行檔與模型不直接提交到 Git；正式發行工作流程會下載已鎖定版本、驗證 SHA-256，並附帶授權資訊。
