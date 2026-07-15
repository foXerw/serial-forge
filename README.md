# SerialForge

**[English](#english)** · **[中文](#中文)**

A data-driven serial-protocol debugging tool for Windows (.NET 8 + WPF).

---

## English

Describe your custom serial protocol **once** — visually in-app or as a JSON
file — and SerialForge auto-generates a per-command field form, frames your
application data into bytes, sends/receives it over a COM port, and shows a
timestamped hex log with per-field decode. The same definition drives both
encoding (TX) and decoding (RX).

> The UI is in Simplified Chinese.

### Features

- **Visual protocol editor** — a frame is an ordered list of **segments**;
  each segment has a `role` (`fixed` / `value` / `length` / `checksum`) and a
  bit `width`. Bitfields are just narrow segments (e.g. a high-nibble +
  low-nibble pair fills one byte). Commands are named presets of `value`
  segments and may carry a `payload` sub-template that structures the variable
  payload. Add / delete / reorder segments, edit the raw JSON, and **apply
  changes live** without restarting. Invalid drafts are reported inline and
  never disturb the running session.
- **Data-driven engine** — one protocol definition powers TX encode and RX
  decode; packing/parsing are each a forward walk (no constraint solver). The
  decoder is robust to malformed/bad-CRC frames (never crashes the read loop).
- **Command panel** — pick a command, fill the editable fields, send; `fixed` /
  `length` / `checksum` segments are filled automatically. Encode errors
  surface in the log.
- **Hex RX/TX log** — timestamped, per-field decode overlay, red highlight for
  bad frames.
- **Save / load** protocols as JSON; ship and share definitions.
- **Firmware upgrade (Phase 2)** — drive a full upgrade over the live link
  (`start` → chunked `transfer` with per-packet ACK + timeout/retry → `end` +
  whole-image CRC32) with a progress bar and cancel. Built on a hardened
  `FrameDispatcher.Await` + a minimal send/expect/retry step runner.

### Run

```bash
dotnet run --project SerialForge.App
```

Top-right buttons: 「📖 使用说明」 (help), 「✎ 编辑协议」 (edit protocol),
「⬆ 固件升级」 (firmware upgrade).

### Define a protocol

Two ways:

- **In-app:** open the visual editor (「✎ 编辑协议」), edit framing / fields /
  commands, then 「应用」 to apply live or 「另存为…」 to export JSON. A 「原始 JSON」
  tab gives direct text editing with bi-directional sync.
- **JSON file:** see `SerialForge.Protocols/demo-mcu.json`. Demo frame format:

  ```
  AA 55 | len:u16le | cmd:u8 | payload | crc16:u16le
  ```

  Schema covers `frame` (ordered segments: name / role / width / byte order /
  value / default / counts / algo / over) and `commands` (preset `values` +
  optional `payload` sub-template). Framing is derived from the segments: the
  first `fixed` segment is the preamble, the `length` segment gives the frame size.

### Test

```bash
dotnet test      # 81 tests — segment engine, framer, transport, editor VMs, upgrade flow
```

### Architecture

| Project | Responsibility |
|---|---|
| `SerialForge.Core` | Protocol model (sealed records), compute algorithms, `FrameEngine` (Pack/Parse), `ProtocolLoader` / `ProtocolSaver` (JSON ↔ model, `SegmentModel` namespace) |
| `SerialForge.Transport` | Serial I/O (`SerialTransport`), `Framer`, `FrameDispatcher` (`Await` seam), `FlowRunner`, `FirmwareImage`, `UpgradeFlow` |
| `SerialForge.App` | WPF / MVVM (CommunityToolkit.Mvvm): connection bar, command panel, hex log, **protocol editor**, **firmware upgrade**, help |

Core has zero WPF and zero serial-port dependencies, so the engine and framer
are fully unit-testable without hardware or UI.

### Roadmap

- General `FlowDefinition` JSON (send/expect/branch/loop DSL) for arbitrary
  multi-step exchanges; resume/interruption; non-upgrade flows.
- Phase 3 (optional): manual raw-byte send, session save/load, scripting,
  TCP/file-replay transports.

### Docs

- Phase 1 design — `docs/superpowers/specs/2026-07-11-serialforge-design.md`
- Segment-model rewrite design — `docs/superpowers/specs/2026-07-15-serialforge-flat-segment-model-design.md`
- Protocol editor design — `docs/superpowers/specs/2026-07-11-serialforge-protocol-editor-design.md`
- Firmware upgrade design — `docs/superpowers/specs/2026-07-11-serialforge-firmware-upgrade-design.md`

---

## 中文

SerialForge 是一款 Windows 桌面（.NET 8 + WPF）的**数据驱动**串口协议调试工具。
只需把自定义串口协议**定义一次**——在应用内可视化编辑或写成 JSON 文件——工具就会自动
生成按命令的字段表单，把你的应用数据封装成帧字节，经 COM 口收发，并在带时间戳的十六进制
日志里按字段解析。同一份定义同时驱动发送 (TX) 编码与接收 (RX) 解码。

### 功能特性

- **可视化协议编辑器** —— 一帧就是一组有序**位段（segment）**；每段有 `role`
  （`fixed`/`value`/`length`/`checksum`）与位 `width`。位域就是窄一点的段
  （如高 4 位 + 低 4 位 两个段自然拼成一个字节）。命令是 `value` 段的命名预设，可带
  `payload` 子模板来描述变长 payload 的结构。可增删 / 重排段、编辑原始 JSON，改动
  **即时生效**、无需重启。非法草稿内联报错，绝不动正在运行的现场。
- **数据驱动引擎** —— 一份协议定义同时驱动 TX 编码与 RX 解码；打包/解析都是正向遍历
  （无约束求解器）。解码对坏帧 / 错 CRC 健壮（绝不崩溃读线程）。
- **命令面板** —— 选命令、填可编辑字段、发送；`fixed`/`length`/`checksum` 段自动填充。
  编码错误会显示到日志。
- **十六进制收发日志** —— 带时间戳、按字段解析叠加、坏帧红色高亮。
- **存 / 读**协议为 JSON，方便分发与复用。
- **固件升级（Phase 2）** —— 在已连接的链路上驱动完整升级
  （`start` → 分块 `transfer` 逐包 ACK + 超时重试 → `end` + 整包 CRC32 校验），
  带进度条与取消。建在加固后的 `FrameDispatcher.Await` + 最小 send/expect/重试 步骤运行器之上。

### 运行

```bash
dotnet run --project SerialForge.App
```

右上角按钮：「📖 使用说明」、「✎ 编辑协议」、「⬆ 固件升级」。

### 定义协议

两种方式：

- **应用内**：打开可视化编辑器（「✎ 编辑协议」），编辑成帧 / 字段 / 命令，点「应用」即时生效，
  或点「另存为…」导出 JSON。「原始 JSON」标签页可直接编辑文本并与可视化页双向同步。
- **JSON 文件**：见 `SerialForge.Protocols/demo-mcu.json`。示例帧格式：

  ```
  AA 55 | len:u16le | cmd:u8 | payload | crc16:u16le
  ```

  Schema 涵盖 `frame`（有序位段：name / role / width / 字节序 / value / default /
  counts / algo / over）与 `commands`（预设 `values` + 可选 `payload` 子模板）。成帧由段
  自动派生：第一个 `fixed` 段是前导，`length` 段给出帧总长。

### 测试

```bash
dotnet test      # 81 个测试 —— 位段引擎、成帧、传输、编辑器 VM、升级流程
```

### 架构

| 项目 | 职责 |
|---|---|
| `SerialForge.Core` | 协议模型（sealed record）、计算算法、`FrameEngine`（Pack/Parse）、`ProtocolLoader`/`ProtocolSaver`（JSON ↔ 模型，`SegmentModel` 命名空间） |
| `SerialForge.Transport` | 串口 I/O（`SerialTransport`）、`Framer`、`FrameDispatcher`（`Await` 接缝）、`FlowRunner`、`FirmwareImage`、`UpgradeFlow` |
| `SerialForge.App` | WPF / MVVM（CommunityToolkit.Mvvm）：连接栏、命令面板、收发日志、**协议编辑器**、**固件升级**、使用说明 |

Core 层零 WPF、零串口依赖，引擎与成帧层可脱离硬件与 UI 完整单测。

### 路线图

- 通用 `FlowDefinition` JSON（send/expect/branch/loop DSL）支撑任意多步交互；断点续传；非升级类 Flow。
- Phase 3（可选）：手动原始字节发送、会话保存/加载、脚本自动化、TCP/文件回放传输。

### 文档

- Phase 1 设计 —— `docs/superpowers/specs/2026-07-11-serialforge-design.md`
- 位段模型重写设计 —— `docs/superpowers/specs/2026-07-15-serialforge-flat-segment-model-design.md`
- 协议编辑器设计 —— `docs/superpowers/specs/2026-07-11-serialforge-protocol-editor-design.md`
- 固件升级设计 —— `docs/superpowers/specs/2026-07-11-serialforge-firmware-upgrade-design.md`
