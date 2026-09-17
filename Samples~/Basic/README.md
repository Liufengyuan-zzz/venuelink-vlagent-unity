# Basic Agent Sample

1. 在 Unity Package Manager 中导入 `Basic Agent` Sample。
2. 导入 SDK 后会自动创建 `Assets/StreamingAssets/vlagent.json`（没有 StreamingAssets 会一并创建；已有文件不覆盖）。也可把 Sample 中的 `vlagent.example.json` 复制过去覆盖模板。
3. 修改：
   - `deviceId`：每个展项唯一，例如 `exhibit-hall-a-01`（MQTT username/clientId 自动为 `agent-{deviceId}`，无需密码）
   - `brokerHost`：VLServer 局域网地址（现场勿用 `127.0.0.1`）
   - `advertisePort`：展项自身收指令的监听端口（TCP 或 UDP，SDK 不监听）
4. 在空场景中新建独立 GameObject，挂载 `VLAgentBootstrap`（内部的 `VLAgentBehaviour` 默认切场景不销毁）。
5. （可选）同物体再挂 `FakeTelemetryDemo`，演示 `ReportState` 假进度/音量。
6. 同物体挂收指令脚本，二者按设备档案的接入方式**选一个**，都按 `advertisePort` 监听、Console 打印原文：
   - `FixedTcpCommandServer`：档案为 FixedTcp。短连接一行文本 + `\n`，可回 ACK。
   - `FixedUdpCommandServer`：档案为 FixedUdp。一包一条、**无换行**、不回执，适合原本就只听 UDP 的老展项。
   两个都挂且端口相同会启动失败（UDP/TCP 端口互不冲突，但同类重复监听会冲突）。
7. 同物体再挂 `VLCommandTable`，在 Inspector 填指令（显示名 + payload），把展项方法拖到「收到后」。菜单 `VenueLink → VLAgent → 导出指令配置包`，把 `.vlconfig` 拿到中控追加导入。
8. 启动 VLServer 后进入 Play Mode。

验证：

- Console 出现 `[VLAgent] MQTT 已连接`。
- VLServer「设备管理」出现待确认设备（含 IP / 端口 / MAC）。
- 管理员确认后（默认 FixedTcp）进入正式设备列表。
- Dashboard / 状态监控显示在线。
- 若挂了 `FakeTelemetryDemo`，MQTT `exhibit/{deviceId}/status` 的 `state` 含 `media` / `audio`。
- 停止 Play Mode 应发布 retained `online:false`。
- 强制结束 Unity Player 时由 Broker LWT 发布 `online:false`。

SDK 不订阅 `cmd`。业务指令用 `FixedTcpCommandServer` / `FixedUdpCommandServer`（或展项自写 TCP/UDP）接收；设备档案的接入方式与端口须与实际监听一致（端口取 `advertisePort`）。自写接收时调用 `VLCommandTable.Dispatch(line)` 即可接到同一张指令表。

UDP 是无连接通道：中控只能确认「包已发出」，不能确认展项收到或执行。要确认送达请用 TCP 或 HTTP。
