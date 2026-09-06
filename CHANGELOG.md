# Changelog

## 0.3.7 - 2026-09-06

- 中控删除已入库设备后，再用旧密钥连接会被拒绝。SDK 此时清除本地 `mqttPassword` / 正式 `deviceId`，回到待确认并重新 register。展项不用再手工删配置里的密钥。

## 0.3.6 - 2026-08-31

- 重连等待增加有界随机抖动，批量断线后的 Agent 会分散重连；极端延迟配置保持在安全上限内。
- 单次心跳发布异常不再终止心跳循环，后续周期会继续发布在线状态。
- 已确认身份与 MQTT 凭据使用原子写盘持久化；配置恢复保留正式 `deviceId` 与 `identityAssigned` 状态。
- 强化心跳、重连与身份配置校验，并补充对应回归测试。

## 0.3.5 - 2026-08-27

- 解析中控 `cred` JSON 时解码 `\uXXXX`。Base64 口令里的 `+` 被写成 `\u002B` 时不再写坏 `vlagent.json`，二次打开可正常重连。

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
