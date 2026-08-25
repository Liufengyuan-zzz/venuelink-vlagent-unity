# Changelog

## 0.3.4 - 2026-08-24

- `VLAgentBehaviour` 默认 `DontDestroyOnLoad`；切场景不销毁、不发正常下线。后续场景若再挂一份则销毁重复物体。Inspector 可关 `persistAcrossScenes`。
- 改为经公开仓库 `venuelink-vlagent-unity` 以 git URL 安装，不再要求 Add package from disk。
- 集成指南移入包内 `Documentation~/`，随包分发。

## 0.3.3 - 2026-08-19

- 未入库空密走 `agent-pending-{sessionId}` 与 `exhibit/pending/{sessionId}/…`；确认后写回正式 `deviceId` 与口令并重连。
- 默认 `vlagent.json` 的 `deviceId` 留空，由中控签发。

## 0.3.2 - 2026-08-18

- 可选 `mqttPassword`；未入库可空密 register，确认后经 `exhibit/{id}/cred` 自动写入配置并重连。
- `VLAgentClient` / `VLAgentBehaviour`：确认后经 `cred` 自动写入配置并重连。
- `clientId` / `username` 仍为 `agent-{deviceId}`。

## 0.3.1 - 2026-08-13

- 导入 SDK 或打开工程时，若缺少 `Assets/StreamingAssets/vlagent.json` 则自动创建（已有文件不覆盖）。
- 菜单：`VenueLink / VLAgent / 补全 vlagent.json`。

## 0.3.0 - 2026-07-27

- Add `ReportStateAsync` / `ClearStateAsync` for business telemetry on retained `status`.
- Document recommended `state` fields: mode, media, audio, content, controls, fault.
- Basic Sample: `FakeTelemetryDemo` fake progress/volume reporting.
- Heartbeat and reconnect keep the latest reported state.

## 0.2.1 - 2026-07-27

- Remove MQTT `username`/`password` config; identity is always `agent-{deviceId}` with empty password.
- Align with VLServer passwordless MQTT auth (role by username convention + Topic ACL).

## 0.2.0 - 2026-07-27

- Stage 2: publish `exhibit/{deviceId}/register` after MQTT connect/reconnect.
- Capture broker-route IPv4, matching NIC MAC, hostname/OS/version.
- Require `advertisePort` (exhibit TCP listen port candidate; SDK does not listen).
- Suggest `fixedTcp` access mode for VLServer confirmation.

## 0.1.0 - 2026-07-23

- Initial Unity 2022.3 LTS package.
- MQTT TCP connection and reconnect.
- Retained status heartbeat, LWT and graceful offline status.
- StreamingAssets JSON configuration and Basic sample.
