# SerialForge

A data-driven serial protocol debugging tool for Windows. Describe your custom
serial protocol in a JSON file; the tool auto-generates a per-command field
form, frames your "application data" into bytes, sends/receives over a COM
port, and shows a hex log with per-field decode.

## Run
```bash
dotnet run --project SerialForge.App
```

## Define a protocol
Drop a JSON file (see `SerialForge.Protocols/demo-mcu.json`) describing framing,
layout (literal/value/computed fields), and commands. The same definition drives
both encoding (TX) and decoding (RX).

## Test
```bash
dotnet test
```

## Architecture
- `SerialForge.Core` — protocol model, codecs, compute algorithms, ProtocolEngine.
- `SerialForge.Transport` — serial I/O, framer, frame dispatcher.
- `SerialForge.App` — WPF/MVVM UI.

Phase 2 (firmware-upgrade Flow engine) plugs into `FrameDispatcher.Await`.
See `docs/superpowers/specs/2026-07-11-serialforge-design.md`.
