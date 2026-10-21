# Agent notes

AK980 Studio is a replacement for the AJAZZ AK980 V2 driver, built from its reverse-engineered USB protocol. README.md has the features and the full protocol table.

## Build

- `dotnet build` (.NET 8 SDK, Windows only: WPF + WinForms tray + GDI+)
- Publish: `dotnet publish src/AK980Studio -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/AK980Studio`
- CLI for hardware testing: `src/ak980` (`ak980 list`, `lcd`, `light`, `custom`, `options`, `raw`)

## Reverse engineering

- Source binaries come from the stock installer (`AJAZZ AK980 V2.exe`, installs to `C:\Program Files (x86)\AJAZZ AK980\`). Copy `DeviceDriver.exe`, `mui.dll` and `layouts\rgb-keyboard.xml` into `re/bin/`. That folder and `re/out/` are git-ignored: never commit vendor binaries or decompiler output.
- [rea](https://github.com/morluto/rea) with Ghidra 12.1.x + JDK 21: `re/rea.ps1` (set `REA_TOOLS`, default `%USERPROFILE%\tools`). Each rea CLI query re-imports the binary (~3 min). For bulk work, run `re/ghidra/DumpAll.java` through `analyzeHeadless`.
- Quick helpers (pefile + capstone): `re/disasm.py`, `re/xref.py`, `re/callers.py`, `re/opcodes.py`, `re/fieldwrites.py`, `re/strings.py`.

## Hardware testing rules

- Never send unknown opcodes to the keyboard. Some may trigger firmware reset or bootloader mode.
- Prefer no-op tests (re-send the current state) before visible changes, and restore the user's lighting afterwards.
- The logo LED (`Fn+Z`) and the charging indicator are firmware-only; no software command exists.

## UI checks

`scripts/shot.ps1 <out.png>` screenshots the running app; `scripts/nav.ps1 "<page name>"` switches pages via UI Automation.
