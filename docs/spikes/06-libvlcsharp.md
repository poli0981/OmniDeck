# Spike 06 — LibVLCSharp in WPF (airspace, fullscreen)

**Question:** does LibVLCSharp's `VideoView` work inside a WPF-UI `FluentWindow` with overlay controls and a fullscreen toggle?
**Decision:** GO with conditions. Inside a Mica `FluentWindow`, `VideoView` plays with hardware decoding, and WPF overlay controls and subtitles draw correctly above the video. Fullscreen works by resizing the shell window over the monitor. FlyleafLib is not needed. Three conditions:
- moving a playing player to another window (pop-out) needs a video-output restart plus a seek;
- the pop-out's overlay was clipped once;
- with the SPEC plugin set, libVLC adds about 39 MB compressed to the installer.

**Measured:** 2026-10-30 on `spike/p0` at `2a87046` (`spikes/MediaLab`): the `--auto` and `--auto-move` runs on the trimmed plugin build, with every screen capture checked by eye. Issue [#7](https://github.com/poli0981/OmniDeck/issues/7).

## Environment

- LibVLCSharp.WPF 3.10.2 and VideoLAN.LibVLC.Windows 3.0.24 (libVLC 3.0.24 "Vetinari"); WPF-UI 4.3.0; .NET 10.0.14.
- One 2560×1440 monitor at 100 % scale; RTX 5080 (D3D11VA); Windows Insider build 29648.
- Test media, generated locally with ffmpeg so nothing was downloaded:
  - H.264/AAC 1080p30, 60 s;
  - H.264/AAC 2160p60, 20 s;
  - an external `.srt` with Vietnamese and Japanese text.

## Method

MediaLab is a `FluentWindow` (Mica, `ui:TitleBar`) holding a `VideoView`. The view's WPF content is a badge in the top-right corner and a control bar (`ui:Button`, `Slider`, time).

The `--auto` run:
1. plays the 1080p clip and adds the `.srt`;
2. toggles fullscreen in place;
3. opens a fullscreen window and a pop-out, each with and without restarting the video output;
4. hides and shows the main window;
5. plays the 2160p60 clip.

At every step it captures the screen as DWM composited it, because the video and the overlay are separate windows and only the screen shows how they combine. It also logs libVLC warnings, frame statistics, CPU and private commit. `--auto-move` repeats the hand-over to another window with a seek, capturing at 0.5 s and 2 s.

## Results

| Check | Result |
|---|---|
| WPF overlay above the video (airspace) | OK. LibVLCSharp's overlay window draws the WPF controls above the video, translucent backgrounds included |
| External subtitles (`.srt`, UTF-8, Vietnamese and Japanese) | OK |
| Fullscreen in place: shell window resized to the monitor, chrome hidden, corners square | OK, playback never interrupted |
| Hand-over to another window, as is | **No.** The video keeps drawing into the original window, and the new window stays black |
| … after re-selecting the video track | The new window shows video, corrupted until the next keyframe (several seconds) |
| … after re-selecting the track and seeking to the current time | **OK.** A clean picture within 0.5 s, in both directions |
| Pop-out into a second `FluentWindow` | Video OK. The overlay button was clipped on its right side once |
| Hide and show (tray) | Overlay and playback come back |
| Hardware decoding (D3D11VA) | 1080p30 and 2160p60 with 0 lost frames, at under 1 % CPU (of 28 logical cores) |

**Memory** (private commit; libVLC runs inside the shell process):

| Moment | Private commit |
|---|---|
| before libVLC loads | 45 MB |
| libVLC loaded | 116 MB |
| playing 1080p30 | 284 MB |
| playing 2160p60 | 522 MB |
| after `Stop` | 324 MB |

**Size**, x64 only. The package also copies its x86 and ARM64 builds unless `VlcWindowsX86Enabled` and `VlcWindowsArm64Enabled` are set to `false`.

| Plugin set | Raw | Zip (deflate, level 9) |
|---|---|---|
| Full (321 plugins) | 106 MB | 48 MB |
| SPEC set (267 plugins) | 86 MB | 39 MB |

The SPEC set keeps the plugin folders access, audio filter, audio mixer, audio output, codec, demux, packetizer, stream filter, text renderer, spu, video chroma, video filter, video output and misc. The largest files are `libavcodec` (18 MB), `access/srt` (4 MB; network streaming only, removable for local files), `libass` (3.4 MB) and `freetype` (3 MB).

## Consequences

- **Engine:** LibVLCSharp with libVLC 3.0.x, as [SPEC](../SPEC.md#36-media-player) §3.6 says. FlyleafLib stays unused.
- **Fullscreen:** the module's own window (main or pop-out) covers the monitor; the player never moves to a separate fullscreen window. This passes the fullscreen part of [WPFUI-CONVENTIONS](../WPFUI-CONVENTIONS.md#8-hard-rules) §8.
- **Pop-out:** move the player to the new `VideoView`, re-select the video track, and seek to the current time. Pop-out ships only after P2 fixes the overlay's sizing in a newly created window and re-tests it, as §8 requires.
- **Build:**
  - set `VlcWindowsX86Enabled` and `VlcWindowsArm64Enabled` to `false` and use the SPEC include list;
  - consider dropping the network access plugins (srt and others), since playback is local-file only;
  - libVLC is the largest single component in the installer, at about 39 MB compressed. THIRD-PARTY-NOTICES gets its entry in P2, with approval.
- **Governor:** playback holds 280–520 MB committed in the shell process, and `Stop` alone kept 324 MB. `SuspendAsync` therefore stops and disposes the `MediaPlayer`, and `HibernateAsync` disposes the `LibVLC` instance as well.
- **Not tested:** a second monitor and per-monitor DPI changes, because only one monitor was available. Check them in P2 once the media module exists.
- **Log noise:** filter these harmless libVLC messages out of the app log:
  - `option quiet does not exist`;
  - `SetThumbNailClip failed` (a fullscreen window without a taskbar button);
  - `not enough decoding slices`.

## Reproduce

On `spike/p0`, with the test clips from `spikes/README.md`:

```powershell
dotnet build spikes/MediaLab -c Release -p:TrimVlc=true
spikes\MediaLab\bin\Release\net10.0-windows\win-x64\MediaLab.exe --auto
spikes\MediaLab\bin\Release\net10.0-windows\win-x64\MediaLab.exe --auto-move --seek-on-move
```
