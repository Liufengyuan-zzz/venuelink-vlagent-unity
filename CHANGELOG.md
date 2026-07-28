# Changelog

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
