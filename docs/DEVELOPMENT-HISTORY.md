# Disc Workshop for Windows 11

Version 1.8.0 fixes frame stepping after browsing to another part of the film. Optional opening and ending silent black screens apply to every disc, each with a configurable duration defaulting to four seconds. Ending black follows any disc-break fade; added time is included in bitrate fitting and disc capacity checks.

Version 1.7.0 adds **Left/Right source-frame stepping**, **Up/Down preview volume**, and optional **picture/audio fade-out and fade-in at internal disc breaks** with a chosen duration. Low vision now stays readable during conversion and can be toggled from the fixed top bar while a job runs. The footer says **4K and HD** are downscaled, with the creator credit in smaller text.

Version 1.6.0 displays and accepts break times as **HH:MM:SS**, and **Space** plays/pauses video with sound in the break editor. Keep `ffplay.exe` beside FFmpeg for playback. Choose **XVCD (MPEG-1)** or **SVCD (MPEG-2)** for either one-disc fitting or maximum-bitrate multi-disc output. Standard VCD retains its fixed profile unless fitting/maximization is enabled. **Encoding parameters…** exposes picture dimensions, frame rate, video bitrate/fitting ceiling, MP2 audio bitrate/sample rate/channels, aspect ratio and GOP length. Saved custom settings are enabled by the advanced-parameters checkbox. Disc capacity checks remain active.

Install **release\DiscWorkshop-Setup-1.8.3.exe**, or open the portable **DiscWorkshop-1.8.3.exe**. The public installer contains the application and offline guide; FFmpeg/FFprobe, VCDImager and ImgBurn are separate prerequisites. See **docs/USER-GUIDE.html** for complete setup and website-ready documentation. Earlier versions are retained separately.

Version 1.5.0 adds a remembered **Low vision** mode with 15-point bold text and high contrast, **Maximum bitrate per disc** with a chosen disc count, subtitle burning from embedded tracks or external text subtitle files, and **Preview / choose breaks** with timeline scrubbing and exact-second entry. Choose 80-minute media for typical 700 MB blanks. Maximum mode caps video at 2500 kb/s and reserves capacity for audio and authoring; it switches VCD to XVCD. See `DISC-BITRATES.csv` for each volume's rate. Manual cuts round to an output frame and replace automatic cuts. Subtitle burning requires one assembled movie file. Maximum/manual jobs retain lossless SD intermediates and need extra disk space. The preview displays source frames without final subtitle burn-in.

Version 1.3.2 fixes a crash when closing or selecting an item in the donation menu. The menu can now be reused safely until the window closes.

Version 1.3.1 adds a green **Please donate** button and an animated arrow pointing toward it. The PayPal and Venmo choices remain available.

**Made by Albert Mark Burdge.** This app converts selected unencrypted DVD, Blu-ray, UHD and video files into VCD, SVCD or XVCD images, then opens ImgBurn for burning. It does not remove copy protection.

Version 1.3 adds a format selector, custom XVCD bitrate, SVCD encoding/authoring, an embedded icon, window logo, shortcut creation, and PayPal/Venmo donations. Black-screen splits, the progress bar, optional one-CD fitting and anamorphic squeeze remain available.

## Formats

| Format | Video | NTSC / PAL size | Video bitrate |
| --- | --- | --- | --- |
| VCD (default) | MPEG-1 | 352×240 / 352×288 | 1150 kb/s |
| SVCD | MPEG-2 | 480×480 / 480×576 | 2040 kb/s |
| XVCD | MPEG-1 | 352×240 / 352×288 | Custom 200–2500 kb/s; starts at 1800 |

All profiles use 224 kb/s, 44.1 kHz stereo MP2 audio. Choose **Output format** before converting. XVCD is nonstandard; higher or lower bitrate playback depends on your player. SVCD also requires player support. Higher bitrates fit less video per CD; disc timing is calculated from the selected bitrate and 74/80-minute media, with a capacity check after authoring. XVCD here customizes bitrate, not resolution.

## Logo, shortcuts and donations

The disc/play logo is drawn in the window and embedded as a multi-resolution Windows icon. Use **Create shortcut…**, choose Desktop (or another folder), and save. Windows shortcuts made manually from the executable also use its icon. Keep the executable at the same location after making a shortcut. Artwork files are in `assets`.

**Please donate** opens a choice of [PayPal](https://paypal.me/albie) and [Venmo](https://account.venmo.com/u/Albie_Burdge). Selecting a provider opens that exact link in your default browser. The app does not collect payment details or initiate a payment itself.

## Make a Video CD

1. Insert your readable source disc and click **Add files**.
   - DVD: browse to `VIDEO_TS`. Select a single movie title's numbered files, for example `VTS_01_1.VOB`, `VTS_01_2.VOB`, etc. Exclude `_0.VOB` menu files. The title is not always `01`.
   - Blu-ray/UHD: browse to `BDMV\STREAM`. Select the movie's M2TS clips, then arrange them in playback order. File-number order is not necessarily movie order.
   - For seamless branching, multi-angle discs or complex titles, use an already assembled unencrypted movie file. This version does not parse IFO titles or MPLS playlists. Separately encoded VOB boundaries may have small audio/video discontinuities.
2. Choose the output folder and **NTSC** or **PAL** for your player. Audio stream 0 means the first audio track; choose another number if necessary. Each source needs that audio stream. Choose subtitle burn-in for one assembled movie file; disc menus are not retained.
3. For HDR10/HLG sources, enable **HDR → SDR**. Leave this off for ordinary SDR sources. Dolby Vision conversion is not supported explicitly.
4. Optionally enable **Keep copies of source files** to retain the selected source files locally as well as the conversion. This is a file copy, not a full-disc backup.
5. Leave **Anamorphic squeeze for 16:9 TV** enabled for squeeze-style widescreen playback, or turn it off for the original 4:3 letterbox behavior. Leave **Fit whole film on one CD** unchecked for normal-quality multi-disc output. Click **Create Video CDs**. The progress bar shows the current clip's estimated percentage and clip number, not an overall job percentage. It uses an activity indicator if the input duration is unavailable and during copying, splitting, and authoring. Keep several GB free, plus the source size if retaining copies.
6. Select a completed disc in the results list and click **Burn selected CD**. ImgBurn opens with the CUE selected and verification enabled. Insert a blank CD-R, choose your writer, and click ImgBurn's **Write** button. Repeat for each volume. The app itself does not report physical burn completion.

Keep each `.cue` and matching `.bin` together. Burn the **CUE image**, not the MPEG file or a data CD containing BIN/CUE files. You can reopen finished images with **Open existing CUE**.

## Quiet-audio and black-screen disc breaks (default)

VCD mode keeps its conservative 70-minute maximum per volume. SVCD and XVCD calculate their volume duration from bitrate and selected media capacity. The app balances running time between discs and searches within five minutes of each proposed boundary for an almost completely black screen lasting at least 0.35 seconds. It selects an independently decodable video keyframe inside the black interval, keeping every disc within capacity. For example, a two-hour standard VCD film normally gets a break near one hour, rather than filling the first CD to 70 minutes.

Version 1.4.0 also scans the selected, converted stereo audio for passages below -35 dB lasting at least three seconds. Preferred cuts leave at least one second of quiet on each side. Ranking: black plus quiet, quiet without black, black without quiet, then a balanced keyframe fallback. All candidates respect capacity and the search window. The log and DISC-PLAN.txt identify each choice and mark fallbacks. actual-splits.csv records actual boundaries. This is volume detection, not speech recognition: whispered dialogue may be missed, and dialogue-free music or effects may be too loud. Preview and listen before burning. Use Preview / choose breaks for manual boundaries. The scan adds processing time.

## Fit whole film on one CD (optional, off by default)

Check **Fit whole film on one CD** and select **74-minute** or **80-minute** media. The app estimates total duration, calculates a lower bitrate with capacity/authoring headroom, retains stereo audio, and produces one unsplit volume. VCD selection switches to nonstandard XVCD for this job; SVCD keeps MPEG-2. Empty XVCD padding sectors are removed after remuxing without changing media packets or timestamps. Padded intermediates are retained as `padded-*.mpg`; burn only the final authored CUE/BIN. Lower-bitrate output loses picture quality and player compatibility varies.

The automatic bitrate is capped at the selected profile's bitrate (1150 for VCD, 2040 for SVCD, or the custom XVCD setting), with a 200 kb/s minimum. Extremely long movies are rejected if they cannot fit within that range. Finished MPEG size and, when authoring is enabled, actual BIN size are checked against the selected media. If inaccurate duration or excessive overhead causes overflow, the job fails instead of marking an oversized disc ready. Choose multi-disc mode in that case. One-CD mode skips the black-screen scan. The format and bitrate are recorded in `DISC-PLAN.txt` and the log.

## Widescreen squeeze playback

With squeeze enabled, a 16:9 source fills the encoded 4:3 frame without adding letterbox bars. It looks horizontally compressed when displayed as 4:3. Set your TV to **Full / Wide / 16:9 stretch**, not zoom, to restore the original proportions. The VCD remains marked 4:3; do not depend on automatic widescreen switching. Avoid applying two stretches in both the player and TV.

Films wider than 16:9 retain top/bottom bars so their original shape is preserved. 4:3 sources are pillarboxed within the 16:9 canvas. Existing black bars baked into a source are not automatically removed. Correct source aspect-ratio metadata is required. Each completed job includes `PLAYBACK.txt` recording the chosen mode.

## Tools

- **FFmpeg**: detected from PATH, `tools\ffmpeg.exe`, or the existing AutoPod installation on this machine. If moved to another machine, use **Locate .exe**. Official download links: https://ffmpeg.org/download.html
- **FFprobe**: required for duration and keyframe timing. Detected beside FFmpeg, in `tools\ffprobe.exe`, or on PATH. It is already available beside FFmpeg on this machine.
- **VCDImager**: included locally in `tools` with its Cygwin runtime, from the cdrtfe 1.5.9.1 portable package. It identifies as VCDImager 0.7.13, an old development build. Local NTSC/PAL authoring tests passed, but player compatibility still requires testing. Project: https://www.gnu.org/software/vcdimager/ . Package: https://sourceforge.net/projects/cdrtfe/files/cdrtfe/cdrtfe%201.5.9/
- **ImgBurn**: detected from its existing installation on this machine; not bundled. Official site: https://www.imgburn.com/ . Burning requires a working CD writer and blank media.

External programs run locally. No video is uploaded. Tool paths are saved in `%LOCALAPPDATA%\DiscWorkshop\tools.txt`. Each job creates its own folder and `job.log`. Failed/cancelled jobs retain partial files; do not burn them. No source files are deleted or modified.

## Quality and limitations

All profiles downscale HD/4K to SD. Choose anamorphic squeeze for manual 16:9 playback or conventional 4:3 letterbox. Your player must support the selected VCD/SVCD/XVCD format and CD-R media.

Requires Windows 11 with .NET Framework 4.x, a compatible source-disc reader, and supported unencrypted files. UHD reading depends on your drive. There is no automatic title detection, disc menu creation, hardware encoding, or direct integrated burn engine. Selecting an audio stream that does not exist produces a visible error.

## Validation and rebuilding

Run `build.ps1` in PowerShell to generate the icon and compile using the Windows .NET Framework C# compiler. Sources are `DiscWorkshop.cs`, `DiscPlanning.cs`, `Branding.cs` and the asset-generation entry point `MakeAssets.cs`.

Run `DiscWorkshop-1.4.0.exe --self-test` and wait for it to exit. Tests cover the prior VCD pipeline plus NTSC/PAL SVCD and custom XVCD encoding, codec/dimensions/audio, splitting, authoring, strict decoding, capacity calculations, embedded icon extraction, shortcut creation, and donation URLs. Reports are saved in `test-results`; the current result is `PASS.txt` or `FAIL.txt`.

The desktop interface was inspected on this Windows machine. **No physical DVD/Blu-ray/UHD read, CD burn, or standalone-player playback has been verified.**

## Third-party notices

VCDImager and Cygwin remain subject to their respective GPL licenses. See `tools\licenses`. The original downloaded cdrtfe archive and extracted package are kept in `vendor` for provenance. Source packages are available from https://sourceforge.net/projects/cdrtfe/files/tools/source/ . FFmpeg and ImgBurn are separate pre-existing installations and are not distributed with this app. Before redistributing third-party binaries, comply with their licenses and corresponding-source requirements.









Version 1.8.1 fixes the stale opening picture after live preview playback is paused and stepped. Preview refreshes now use the latest seek request directly; regression checks examine the rendered picture as well as its time.

Version 1.8.2 routes Space and arrow keys consistently when the embedded native player, timeline slider, time fields, or buttons have focus. The keyboard hook applies only while the preview editor is the foreground window and is removed when it closes.

Version 1.8.3 removes Left/Right frame stepping, Up/Down volume shortcuts and the preview keyboard hook at the user request. Use the timeline or HH:MM:SS to choose breaks; Space still plays/pauses.
