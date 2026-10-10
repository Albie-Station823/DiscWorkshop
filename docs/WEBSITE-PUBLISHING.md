# Disc Workshop 1.8.3 website handoff

## Files to upload

- `release/DiscWorkshop-Setup-1.8.3.exe`: Windows installer.
- `release/DiscWorkshop-Setup-1.8.3.exe.sha256`: installer checksum.
- `docs/USER-GUIDE.html`: complete, self-contained help page. No external scripts, fonts or styles are required. It also works offline and is included in the installer.

The files are prepared locally; no website upload or public download URL has been created. Link your website's download button to the uploaded installer. Link a nearby **User guide and setup requirements** button to the HTML guide. Keep this documentation accessible before download.

## Suggested download-page copy

**Disc Workshop 1.8.3 for Windows 11**

Made by Albert Mark Burdge.

Convert readable, unencrypted movie files from DVD, Blu-ray, UHD or ordinary video files into VCD, SVCD or XVCD images for compatible CD players. Includes conversion progress, widescreen squeeze, automatic multi-disc breaks that prefer sustained quiet audio and black screens, and optional lower-bitrate one-CD fitting.

**Download Windows installer**

**Before you start:** FFmpeg/FFprobe, FFplay for preview playback, VCDImager and ImgBurn are separate prerequisites and are not included. Output is standard definition. This app does not remove copy protection. Quiet detection does not guarantee dialogue-free breaks. Playback depends on your player and chosen format. Read the user guide for setup and limitations.

**New in 1.8.3:** Removed arrow-key preview shortcuts and their keyboard hook. Keeps Space play/pause, HH:MM:SS entry, timeline scrubbing, configurable silent black screens, disc-break fades, and accessibility controls.

Please donate: https://paypal.me/albie or https://account.venmo.com/u/Albie_Burdge

## Release verification

Automated media regression checks passed for VCD, SVCD and XVCD. Focused tests passed for quiet/black ranking, capacity and search limits, short-pause rejection, real audio/video detection, actual segment timing and decoding. The 1.8.3 installer is built locally and has a SHA-256 checksum. Physical burning and player compatibility require testing on the target hardware.

The installer is unsigned. Physical optical-disc reading, CD burning and playback in standalone players have not been verified. Separate third-party media tools are not redistributed in this installer.

## Rebuild

Run `build-installer.ps1` from PowerShell. It builds the application, compiles `installer.iss` with Inno Setup in `vendor/inno`, and writes the SHA-256 file. The Inno Setup compiler is a development tool, not part of the distributed installer. Follow its license terms for your use.





