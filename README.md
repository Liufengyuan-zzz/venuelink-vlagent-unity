# VenueLink VLAgent Unity SDK

适用于 Unity 2022.3 LTS 的无界面展项接入 SDK。

当前 `0.3.0` 提供：

- MQTT TCP 连接与自动重连
- 连接后发布 `register`（IP / MAC / 端口 / hostname）
- retained 在线心跳
- `ReportState` / `ClearState`：上报业务运行态（进度、音量等）
- 正常退出 `online:false`
- 异常退出 LWT `online:false`
- `StreamingAssets/vlagent.json` 配置

不提供：业务 `cmd/ack`、TCP/HTTP Server、设置页面、Server→Pad 转发。

## 引用

Unity Package Manager → Add package from disk，选择：

```text
packages/VLAgent.Unity/package.json
```

## 配置

在展项工程创建 `Assets/StreamingAssets/vlagent.json`：

```json
{
  "deviceId": "exhibit-unity-01",
  "brokerHost": "192.168.1.10",
  "brokerPort": 1883,
  "heartbeatIntervalMs": 3000,
  "reconnectDelayMs": 2000,
  "advertisePort": 9000
}
```

- `deviceId` 决定 MQTT 身份 `agent-{deviceId}`（username 与 clientId 相同）与 Topic，不可重复；无需密码。
- `advertisePort` 是展项自己的 TCP 监听端口候选，SDK **不会**监听。
- `ip/mac` 由 SDK 采集：优先取能路由到 Broker 的本机网卡。
- 本机联调可用 `brokerHost=127.0.0.1`；现场应填中控局域网 IP。

## 使用

挂载 `VLAgentBehaviour`，或：

```csharp
var config = AgentConfigLoader.LoadFromStreamingAssets();
var agent = new VLAgentClient(config);
await agent.StartAsync();

// 网卡切换后可手动重报
await agent.ReportRegisterAsync();

// 业务运行态（进度 / 音量等）
await agent.ReportStateAsync(new AgentState
{
    mode = "manual",
    media = new MediaState
    {
        id = "intro",
        playing = true,
        positionMs = 125000,
        durationMs = 600000
    },
    audio = new AudioState { volume = 0.6f, muted = false }
});

await agent.StopAsync();
agent.Dispose();
```

推荐 `state` 字段见开发文档 §5.4；全部可选，建议变化时上报且不超过约 1Hz。Basic Sample 的 `FakeTelemetryDemo` 可演示假进度上报。

## 传输与兼容性

- Unity：2022.3 LTS
- API Compatibility Level：.NET Standard 2.1
- MQTT：TCP，默认端口 1883
- MQTTnet：4.3.7.1207（MIT，见 Third Party Notices）
