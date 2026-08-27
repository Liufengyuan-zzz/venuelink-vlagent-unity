# VenueLink VLAgent Unity SDK

适用于 Unity 2022.3 LTS 的无界面展项接入 SDK。第一次对接请先看 [`VLAgent SDK集成指南`](Documentation~/VLAgent%20SDK集成指南.md)。

当前 `0.3.5` 提供：

- MQTT TCP 连接与自动重连
- 连接后发布 `register`（IP / MAC / 端口 / hostname）
- retained 在线心跳
- `ReportState` / `ClearState`：上报业务运行态（进度、音量等）
- 正常退出 `online:false`
- 异常退出 LWT `online:false`
- `StreamingAssets/vlagent.json` 配置（导入后自动创建，已有不覆盖）

不提供：业务 `cmd/ack`、TCP/HTTP Server、设置页面、Server→Pad 转发。

## 引用

Unity Package Manager → 左上 `+` → Add package from git URL，填：

```text
https://github.com/Liufengyuan-zzz/venuelink-vlagent-unity.git#v0.3.5
```

`#` 后面是版本 tag，不写则取最新 `main`。现场交付建议锁定 tag，避免升级带来意外。

也可直接写进工程的 `Packages/manifest.json`，团队成员打开工程即自动拉取：

```json
{
  "dependencies": {
    "com.venuelink.vlagent": "https://github.com/Liufengyuan-zzz/venuelink-vlagent-unity.git#v0.3.5"
  }
}
```

升级换 tag 即可。要改 SDK 源码时才用 Add package from disk 选 `packages/VLAgent.Unity/package.json`。

## 配置

导入本 SDK 后，Unity 会自动创建 `Assets/StreamingAssets/`（没有就建）和 `vlagent.json`。**已有文件不会覆盖。** 也可菜单：`VenueLink → VLAgent → 补全 vlagent.json`。

SDK 读这份文件连中控；**展项自己的通信代码（TCP/HTTP 监听、回执）也必须读同一份文件**，不要在 Inspector 或代码里另写一套 IP/端口。创建后请立刻改 `brokerHost`、`advertisePort`。

```json
{
  "deviceId": "",
  "brokerHost": "192.168.1.10",
  "brokerPort": 1883,
  "heartbeatIntervalMs": 3000,
  "reconnectDelayMs": 2000,
  "advertisePort": 9000
}
```

| 字段 | 谁填 | 含义 |
|---|---|---|
| `deviceId` | 中控签发 | 未入库可留空（SDK 生成临时 sessionId）。确认后写入正式 id。身份：待确认 `agent-pending-{sessionId}`，已入库 `agent-{deviceId}`。 |
| `mqttPassword` | 确认入库后 | 中控签发；未入库留空。确认后与 `deviceId` 一并写回。 |
| `brokerHost` / `brokerPort` | 人工 | **中控 VLServer 的地址**，不是展项自己的地址。本机联调用 `127.0.0.1`；现场填中控局域网 IP（如 `192.168.1.10`），端口默认 `1883`。 |
| `advertisePort` | 人工 | **展项程序真正监听、收中控指令的 TCP 端口**。SDK **不会**帮你开这个端口。展项通信代码必须读这个字段再 `Listen`。 |
| `heartbeatIntervalMs` / `reconnectDelayMs` | 一般不用改 | 心跳间隔、断线重连等待。 |

**IP / MAC 不用写进 JSON。** SDK 启动时会自动探测：选一张「能访问到 `brokerHost`」的本机网卡，把该网卡的 IPv4 和 MAC 上报给中控。

### 开发时怎么用这份配置

1. SDK：`AgentConfigLoader.LoadFromStreamingAssets()`（或挂 `VLAgentBehaviour`）。
2. 展项通信：同样读 `vlagent.json` 的 `advertisePort` 再监听。可参考 Sample 的 `FixedTcpCommandServer`。
3. 中控设备档案若是 FixedTcp：目标就是「确认时的 IP + `advertisePort`」。两边不一致，指令发不到展项。

### 现场必须核对 IP

中控「待确认 Agent」里看到的 IP **只是 SDK 自动采到的候选**，不是最终答案。

机器有多块网卡、VPN、虚拟网卡时，采到的 IP 可能 **不是** 展项程序对外收指令用的那张网。管理员确认前要问清楚：

- 展项 TCP/HTTP 实际绑在哪张网、哪个 IP？
- 中控能否访问这个 IP + `advertisePort`？

对不上就在确认界面改成通信用的 IP，再点确认。确认后中控才按这个地址发指令。

## 使用

挂载 `VLAgentBehaviour`（请用独立空物体）。默认切场景不销毁；重复挂载会丢掉后进场景的那份。不要和会随场景卸载的业务物体绑在一起。也可：

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
