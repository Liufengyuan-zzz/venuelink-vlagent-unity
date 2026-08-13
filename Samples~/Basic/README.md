# Basic Agent Sample

1. 在 Unity Package Manager 中导入 `Basic Agent` Sample。
2. 导入 SDK 后会自动创建 `Assets/StreamingAssets/vlagent.json`（没有 StreamingAssets 会一并创建；已有文件不覆盖）。也可把 Sample 中的 `vlagent.example.json` 复制过去覆盖模板。
3. 修改：
   - `deviceId`：每个展项唯一，例如 `exhibit-hall-a-01`（MQTT username/clientId 自动为 `agent-{deviceId}`，无需密码）
   - `brokerHost`：VLServer 局域网地址（现场勿用 `127.0.0.1`）
   - `advertisePort`：展项自身 TCP 监听端口（SDK 不监听）
4. 在空场景中新建 GameObject，挂载 `VLAgentBootstrap`。
5. （可选）同物体再挂 `FakeTelemetryDemo`，演示 `ReportState` 假进度/音量。
6. 同物体挂 `FixedTcpCommandServer`：按 `advertisePort` 监听，接收中控 FixedTcp 指令（Console 会打印原文）。
7. 启动 VLServer 后进入 Play Mode。

验证：

- Console 出现 `[VLAgent] MQTT 已连接`。
- VLServer「设备管理」出现待确认设备（含 IP / 端口 / MAC）。
- 管理员确认后（默认 FixedTcp）进入正式设备列表。
- Dashboard / 状态监控显示在线。
- 若挂了 `FakeTelemetryDemo`，MQTT `exhibit/{deviceId}/status` 的 `state` 含 `media` / `audio`。
- 停止 Play Mode 应发布 retained `online:false`。
- 强制结束 Unity Player 时由 Broker LWT 发布 `online:false`。

SDK 不订阅 `cmd`。业务指令用 `FixedTcpCommandServer`（或展项自写 TCP）接收；设备档案须为 FixedTcp，端口与 `advertisePort` 一致。
