# Disc Workshop

![Disc Workshop logo](assets/DiscWorkshop.png)

**Disc Workshop 1.8.3 for Windows 11** — by Albert Mark Burdge.

Convert readable, unencrypted video files into VCD, SVCD, or XVCD disc images, then hand the completed CUE image to ImgBurn.

[Station 823](https://station823.com/) · [Donate with PayPal](https://www.paypal.com/paypalme/albie) · [Donate with Venmo](https://account.venmo.com/u/Albie_Burdge)

## Downloads

Visit [Disc Workshop at Station 823](https://station823.com/disc-workshop/) for downloads. This repository contains the application source and documentation. Third-party media tools and installers are not bundled.

## Features

- VCD, SVCD and custom XVCD output, with NTSC/PAL selection.
- Disc-capacity fitting and optional maximum bitrate per disc.
- Automatic breaks favoring black screens and sustained quiet; manual timeline/HH:MM:SS selection.
- Embedded/external subtitle burn-in, widescreen squeeze, optional fades and silent black padding.
- Remembered low-vision controls, conversion progress, and local processing.

Version 1.8.3 retains Space play/pause but removes arrow-key preview shortcuts and their keyboard hook. Older behavior described in the development history is not the current interface.

## Build

Use Windows with the .NET Framework C# compiler:

```powershell
.\build.ps1
```

The result is `DiscWorkshop-1.8.3.exe`. All source files used by the build and the asset generator are included.

## External prerequisites

- [FFmpeg](https://ffmpeg.org/download.html), FFprobe, and FFplay for video processing and preview.
- [VCDImager](https://www.gnu.org/software/vcdimager/) for authoring disc images.
- [ImgBurn](https://www.imgburn.com/) for the optional burn handoff.

Configure their paths in the application. These separate programs retain their own licenses. This repository contains no third-party executables, Cygwin DLLs, movies, or disc images.

For an installer build, `build-installer.ps1` expects a separately supplied Inno Setup compiler under `vendor/inno`; see `installer.iss` and the [publisher guide](docs/WEBSITE-PUBLISHING.md).

## Usage and limitations

Read the [offline user guide](docs/USER-GUIDE.html) and [setup notes](docs/SETUP.txt). Download the HTML guide and open it locally, or use the Station 823 page. The [development history](docs/DEVELOPMENT-HISTORY.md) contains historical testing and release notes; machine-local tool paths there are not bundled prerequisites.

HD/4K inputs are downscaled to SD. The app does not remove copy protection or preserve disc menus. XVCD is nonstandard, and player compatibility varies. Physical disc reading, burning, and standalone-player playback need testing on the intended hardware. A burn handoff is not proof of a completed burn.

## License and contributions

Copyright (C) 2026 Albert Mark Burdge. Application source is licensed under [GNU GPL version 2](LICENSE.txt), without warranty. See [contributing](CONTRIBUTING.md) and [support](SUPPORT.md).
