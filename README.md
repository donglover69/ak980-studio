# AK980 Studio

A replacement for the AJAZZ AK980 V2 driver software (`DeviceDriver.exe`), built from its reverse-engineered USB protocol.

The stock app re-sends the LCD clock **and an LCD slot number** every time it starts (and at every boot, if autostart is on). The slot it sends is "whichever animation is selected in its list + 1", so the screen jumps to a different slot on every launch. AK980 Studio never sends anything on startup unless you turn that on.

## Run it

- App: `publish\AK980Studio\AK980Studio.exe` (needs the .NET 8 Desktop Runtime, already installed here)
- CLI: `publish\cli\ak980.exe` (`list`, `lcd`, `preview`, `time`, `light`, `custom`, `options`, `side`, `raw`)
- Build: `dotnet build` / `dotnet publish src/AK980Studio -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/AK980Studio`

Don't open the stock AJAZZ app afterwards: it will reset the screen again.

## Features

| Page | What it does |
|---|---|
| Screen | Library of animations (first run imports the stock app's saved ones, with their timings). Fit/Fill/Stretch, zoom + pan, mirror/flip, brightness/contrast/saturation, speed, fixed delay, trim, frame step, text overlay, dithering. The preview shows the exact 16-bit colors the panel will display. Clock sync is opt-in. |
| Lighting | All 19 effects plus off. Each effect only shows the controls it supports (brightness, speed, direction, color, rainbow). Live apply, presets, optional re-apply on connect. |
| Per-key colors | Paint the real key layout, eyedropper, rainbow/gradient fills, WASD preset. |
| Keyboard | Win / Alt+Tab / Alt+F4 locks, Fn swap, sleep timer, debounce level (with the stock latency figures). |
| App settings | Dark/light theme, accent color, UI size, start with Windows (hidden), close to tray, command gap. |

Firmware-only lights (no software command exists, checked against every opcode in the stock driver):

- `Fn+Z`: logo LED next to the arrow keys
- `Fn+Del`: LCD on/off
- `Fn+X`: all lights
- The red Fn key while charging and the Num Lock highlight are drawn by the firmware and can't be switched off.

Data lives in `%LOCALAPPDATA%\AK980Studio` (`settings.json`, `library\`).

## Protocol (wired, VID 0C45 PID 8009)

Sources: `DeviceDriver.exe` and `mui.dll` from the stock install, analysed with [rea](https://github.com/morluto/rea) + Ghidra 12.1.4 and capstone. The verified items were tested on the keyboard.

| Interface | Usage page | Reports | Role |
|---|---|---|---|
| MI_03 | FF13 | 65-byte feature (ID 0 + 64) | commands |
| MI_02 | FF68 | 4097-byte output (ID 0 + 4096) | LCD pages |

Each command is a 64-byte packet sent with `HidD_SetFeature` (report ID 0 prepended), followed by a `HidD_GetFeature` read-back. The reply echoes the packet, and byte 4 of the reply is a status. The stock app waits 35 ms before each packet.

A transaction looks like this: `04 18` (begin), then `04 OP ARG 00 00 00 00 00 COUNT_LO COUNT_HI`, then COUNT data packets, then `04 02` (commit), and for lighting `04 F0` (apply).

| Op | Data | Meaning | Verified |
|---|---|---|---|
| `72` | ARG = slot, COUNT = 4 KiB pages, sent on MI_02, one ack per page | LCD upload | yes |
| `28` | COUNT = 1: `00 SLOT 5A YY MM DD hh mm ss 00 DOW … [62]=AA [63]=55` | LCD clock and selected slot | yes |
| `13` | `MODE R G B 0 0 0 0 RAINBOW BRIGHT SPEED DIR 0 0 AA 55` | backlight effect, modes 1–19, 0 = off | yes |
| `13` | `80 … [9]=BRIGHT … AA 55` | switch to per-key mode | yes |
| `23` | COUNT = 9: 576 bytes of `[idx R G B]` × 144, last two bytes `AA 55` | per-key colors | yes |
| `13` | `20..29` | "side light" (hidden in the stock UI, no visible effect on this board) | sent, no effect |
| `17` | ARG = 1, COUNT = 1: `00 01 ALTTAB ALTF4 WIN FNSWAP SLEEP 00 RESPONSE … [62]=AA [63]=55` | firmware options | sent unchanged values only |
| `F5` | read back the per-key table | read | not used |
| `11` / `27` | COUNT = 9 | key remap, layer 0 / Fn layer | not implemented |
| `15` / `19` | | macros | not implemented |
| `20` | | real-time lighting stream (music/ambilight) | not implemented |

LCD upload blob: `[0]` = frame count, `[1..n]` = per-frame delay in 2 ms units, then from offset 256 the frames as RGB565 little-endian, 240×135, top-down rows. The blob is padded with `FF` to a whole number of 4 KiB pages; the keyboard holds up to 140 frames. Each page takes about 145 ms, because the keyboard acks every flash write.

The 2.4 GHz dongle (PID FEFE) uses a different framing on MI_03 (32-byte output reports): the clock is `0C 10 00 00 SLOT 5A …`, and LCD data goes out as 28-byte chunks after a `7F 03 00 SLOT COUNT24` header. Only the USB path is implemented.

## Repo layout

- `src/Ajazz.Keyboard`: HID transport (P/Invoke, no dependencies), protocol, LCD rendering and encoding
- `src/AK980Studio`: WPF app
- `src/ak980`: CLI
- `re/`: reverse-engineering helpers (rea wrapper, Ghidra dump script, capstone scripts). `re/bin` and `re/out` are git-ignored because they hold vendor binaries and decompiler output.
- `scripts/`: dev helpers that screenshot the running app and switch its pages through UI Automation
