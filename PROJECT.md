# Disc Workshop

Standalone Windows 11 WinForms application made by Albert Mark Burdge.

This folder is the project root. Add this folder as a local project named **Disc Workshop** in Codex. It is independent of the sibling Video RentALL website.

## Current state

- Current release: `DiscWorkshop-1.8.3.exe`; installer: `release/DiscWorkshop-Setup-1.8.3.exe`. Version 1.3.2 was previously confirmed working by the user.
- Build: run `build.ps1` with Windows PowerShell and the .NET Framework compiler.
- Sources: `DiscWorkshop.cs`, `DiscPlanning.cs`, `MovieOptions.cs`, `WorkshopUI.cs`, `BreakEditor.cs`, `Branding.cs`.
- Version 1.7 adds `PreviewFrames.cs` for bounded FFprobe frame-timestamp reads and `RevisionTest.cs` for source-frame stepping, picture/audio fades without frame loss, and live low-vision toggles during a real conversion. Keep high-precision internal preview times even though HH:MM:SS shows milliseconds. Low vision is in a fixed bar outside config; lock input with BusyInputFilter instead of disabling whole panels. Fade only internal boundaries and retain duration/frame-count and capacity checks.
- Version 1.6 adds `PreviewPlayback.cs` (embedded FFplay and HH:MM:SS control), `EncodingOptions.cs` (custom parameters and dialog), and `UpdateTest.cs` (playback/encoding/UI conversion integration checks). Preview playback requires FFplay beside FFmpeg or in tools/PATH. Keep the native space key scan code when forwarding play/pause. One-disc fitting, custom encoding, max-rate and manual jobs use lossless intermediates; audio bitrate and fitting ceiling must be included in capacity calculations. Preserve standard VCD behavior and explicit MPEG-1/MPEG-2 selection.
- Donation-menu regression: `DonationMenuTest.cs`.
- Usage and supported formats: `README.md`.
- Keep `tools` alongside the executable. FFmpeg/FFprobe and ImgBurn are external dependencies configured in the application.

## Requirements to preserve

- Unencrypted DVD, Blu-ray, UHD video files and ordinary video inputs.
- VCD, SVCD and XVCD conversion, disc images and ImgBurn handoff.
- Conversion progress display; anamorphic widescreen squeeze into a 4:3 frame.
- Prefer black-screen points for multi-disc splits.
- Optional lower bitrate to fit one CD, disabled by default.
- Application logo and shortcut icon; credit to Albert Mark Burdge.
- Green Please donate button with animated arrow and PayPal/Venmo choices.
- PayPal: https://paypal.me/albie
- Venmo: https://account.venmo.com/u/Albie_Burdge

Version 1.3.2 fixes premature ContextMenuStrip disposal. The donation menu belongs to the window and is disposed only when its owner is disposed. Keep this lifetime behavior.

Version 1.4.0 added sustained quiet-audio ranking for disc breaks. Version 1.5.0 adds remembered low-vision styling modeled on MovieSeeD, per-disc bitrate maximization, embedded/external subtitle burn-in, and frame-aligned manual breaks with a source-frame scrubber. Subtitle burning requires one assembled source. Maximum/manual jobs normalize to lossless SD intermediates before encoding each volume; preserve the capacity checks and cancellation behavior. FeatureTest.cs exercises these pipelines and the UI; --self-test retains the previous conversion regression suite. Website/offline documentation: docs/USER-GUIDE.html. Build installer with build-installer.ps1. The public installer excludes third-party media tools; the guide explains prerequisites.




1.8.0: fixed stale frame-window reuse after seeking; added independent opening/ending black-screen options (4 seconds by default), with silence and capacity-aware encoding on all discs.

1.8.1: replaced preview debounce timer with cancellable-by-generation asynchronous refresh. Reproduced stale opening image with correct late timeline position; verified actual picture and Left/Right after live playback pause on long-GOP MP4 and timestamp-offset transport streams.

1.8.2: editor-scoped message filter plus foreground-scoped keyboard hook for cross-process SDL focus. Real injected keys tested with native player deliberately focused: volume Down/Up, Space pause and Left frame step; rendered image/time checked on long-GOP MP4 and offset TS. Control focus tests cover timeline, numeric fields and play button.

1.8.3: removed preview arrow shortcuts, frame-step queue/cache and native keyboard hook. Kept Space and mouse/time navigation; black padding/fades remain available.
