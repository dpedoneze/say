# Say — offline dictation for Windows

Say is a speech-to-text app that runs **100% offline** once a model is downloaded. Press a
hotkey (or click the on-screen pill), speak, and the transcription is pasted into the app
you were typing in — even if you switched windows in the meantime.

Built with C# / .NET 8 (WPF) and [Whisper.net](https://github.com/sandrohanea/whisper.net)
(whisper.cpp). Models are the free, open-source OpenAI Whisper GGML models.

## Features

- **Model picker with downloads** — Tiny → Large v3 (plus quantized variants) from Hugging Face,
  with progress, cancel, and delete. After that, everything is local.
- **Hands-free toggle** — click the pill or tap the hotkey to start; click/tap again to stop and paste.
- **Push-to-talk** — press and hold the pill or hotkey; release to stop and paste.
- **Cancel** — `Esc` discards the recording without pasting.
- **Live preview** — the pill shows a real-time waveform, timer, and a growing transcription preview.
  (Whisper is not a streaming model: the preview lags ~1s and may revise itself; the final pasted text
  is always computed from the full recording.)
- **Bottom-center overlay** — a frameless, always-on-top pill that *never* steals focus from the
  app you are dictating into.
- **Remembers the target** — the window/control focused when you started is restored before pasting,
  so you can switch applications mid-dictation.
- **Flexible pasting** — clipboard + `Ctrl+V` (restores your previous clipboard afterwards) or a
  type-it-out fallback for apps where `Ctrl+V` doesn't work.
- **Tray icon** — Settings, show/hide overlay, exit. F9 always brings the overlay back.
- **Settings** — hotkey rebinding, language (auto or fixed), CPU threads, input device,
  paste behavior, run at startup.

## Requirements

- Windows 10 (1903+) or Windows 11, x64.
- A microphone.
- Internet **only** for downloading models (one time).
- To build: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
  (`winget install Microsoft.DotNet.SDK.8`). The published app needs no runtime installed.

## Build and run

```powershell
dotnet run --project src/Say          # development run
dotnet test Say.sln                   # unit tests
dotnet publish src/Say/Say.csproj -c Release -r win-x64 --self-contained true
```

The published, self-contained app lands in
`src/Say/bin/Release/net8.0-windows/win-x64/publish/` — run `Say.exe` from there.

## First run

1. Say opens Settings and asks you to download a model. **Small** is the recommended default;
   on slower machines try **Tiny** or **Base**, and for maximum accuracy **Large v3 Turbo (Q5)**.
2. Models are stored in `%LOCALAPPDATA%\Say\models`. Downloads resume only by retrying.
3. Press **F9** (default) or click the pill and speak.

## Usage

| Action | How |
|---|---|
| Hands-free start/stop | Tap `F9` or click the pill (tap again to finish and paste) |
| Push-to-talk | Hold `F9` or press-and-hold the pill (release to paste) |
| Cancel | `Esc` |
| Rebind hotkey | Settings → Dictation → Hotkey |
| Show/hide overlay | Tray icon → Show / hide overlay (F9 always shows it again) |
| Exit | Tray icon → Exit |

## Command line

```
Say.exe --download <model-id>     download a model headlessly (e.g. tiny, small)
Say.exe --selftest                synthesize speech via Windows TTS and transcribe it end-to-end
Say.exe --record <seconds>        record from the microphone, print levels, save a WAV, transcribe it
Say.exe --model <path>            override the model for --selftest / --record
Say.exe --wav <path>              use an existing WAV for --selftest
```

`--selftest` exits 0 on success, 1 on failure, 2 when no model is available. Output is printed and
also written to `%TEMP%\say-selftest.log`.

## Data locations

- Settings: `%LOCALAPPDATA%\Say\settings.json`
- Models: `%LOCALAPPDATA%\Say\models\ggml-*.bin`
- Crash log: `%LOCALAPPDATA%\Say\crash.log`

## Known limitations

- Global hotkeys don't reach Say while an **elevated** (administrator) app has focus — a Windows
  hook restriction. Run Say as administrator if you need to dictate there.
- Whisper is not a streaming model; the live preview lags and can revise. The pasted result is
  computed from the complete recording.
- CPU-only by default. The GTX-class GPU CUDA backend (`Whisper.net.Runtime.Cuda`) can be added
  later.
- The executable is unsigned, so SmartScreen may warn on first launch.

## Project layout

```
src/Say/
  Views/          Overlay pill + Settings window (WPF)
  ViewModels/     Overlay, Settings, model list
  Services/       Audio capture, Whisper transcription, model manager,
                  global hotkey hook, focus capture/restore, paste, controller
  Interop/        Win32 P/Invoke (hooks, SendInput, foreground window, monitors)
src/Say.Tests/    xUnit tests for gesture logic, normalization, settings, catalog
```
