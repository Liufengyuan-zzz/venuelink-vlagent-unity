# VenueLink VLAgent Unity SDK

适用于 Unity 2022.3 LTS。第一次对接请看 [`VLAgent SDK集成指南`](Documentation~/VLAgent%20SDK集成指南.md)。

当前 **0.3.8**：连中控、上报在线、Inspector 配指令并导出给中控追加导入。

SDK **不**开业务端口、**不**订阅 MQTT `cmd`。收指令用 Sample 的 TCP/UDP，或自己 Listen。

## 安装

Package Manager → 左上 `+` → Add package from git URL：

```text
https://github.com/Liufengyuan-zzz/venuelink-vlagent-unity.git#v0.3.8
```

现场交付请带 `#v0.3.8`。不写 tag 则跟 `main`。改 SDK 源码时才 Add package from disk，选本目录 `package.json`。

也可写进工程 `Packages/manifest.json`：

```json
{
  "dependencies": {
    "com.venuelink.vlagent": "https://github.com/Liufengyuan-zzz/venuelink-vlagent-unity.git#v0.3.8"
  }
}
```

## 接入中控

1. 导入后会自动创建 `Assets/StreamingAssets/vlagent.json`（已有不覆盖）。菜单：`VenueLink → VLAgent → 补全 vlagent.json`。
2. 改 `brokerHost`（本机联调 `127.0.0.1`，现场填中控局域网 IP）、`advertisePort`（展项自己 Listen 的端口）。
3. 独立空物体挂 `VLAgentBehaviour`（默认切场景不销毁，不要和会卸场景的业务物体绑在一起）。
4. 先开 VLServer，再 Play。中控「设备管理 → 待确认 Agent」里确认入库，默认 FixedTcp。

`deviceId` / `mqttPassword` 由中控签发，写到 `persistentDataPath`，不改 StreamingAssets。IP / MAC 不用填，启动时自动采。

待确认列表里的 IP 只是候选；多网卡时要改成展项真正收指令的那张网，再点确认。

```json
{
  "deviceId": "",
  "identityAssigned": false,
  "brokerHost": "127.0.0.1",
  "brokerPort": 1883,
  "heartbeatIntervalMs": 3000,
  "reconnectDelayMs": 2000,
  "advertisePort": 9000,
  "mqttPassword": ""
}
```

| 字段 | 谁填 | 含义 |
|---|---|---|
| `deviceId` | 中控签发 | 未入库可空。确认后写入。 |
| `identityAssigned` | SDK 维护 | 是否已入库。不要手改。 |
| `mqttPassword` | 确认入库后 | 中控签发。 |
| `brokerHost` / `brokerPort` | 人工 | **中控**地址，不是展项自己的 IP。端口默认 `1883`。 |
| `advertisePort` | 人工 | 展项 Listen 的端口。SDK 不会帮你开。 |
| 后两个 | 一般不用改 | 心跳 / 重连间隔，单位 ms。 |

TCP/UDP 监听必须读同一份 `advertisePort`，不要在 Inspector 另写端口。

中控删掉该设备后再开展项：SDK 清掉本地密钥，重新出现在待确认。不必手改 `vlagent.json`。

## 指令表与导出

`vlagent.json` 不用改。在收指令的**同一物体**上挂 `VLCommandTable`，和 Sample 的 `FixedTcpCommandServer`（或 `FixedUdpCommandServer`）一起。

Inspector 每条填：

| 字段 | 填什么 |
|---|---|
| id | 留空会自动生成，生成后不要改。追加导入按它判重。 |
| label | 中控里的显示名，如「播放开场」 |
| payload | 中控**原样下发**的内容。纯文本即可：`play`、`stop`、`left/001`。不是必须 JSON。 |
| ackRequired | 对应中控「等回执」。TCP 示例开了回执才会回 ACK。 |
| 收到后 | 拖展项方法：播视频、切场景等 |

导出固定为中控的「文本」格式，不用填十六进制。灯控/Modbus 那种二进制帧导入后再到中控改格式。

菜单 **`VenueLink → VLAgent → 导出指令配置包`**，得到 `.vlconfig`。中控：设置 → 备份与迁移 → **追加导入**。

TCP/UDP 示例会自动 `Dispatch`。自写接收时，主线程调用 `commandTable.Dispatch(line)`，payload 对得上就 Invok「收到后」。

## 代码接入（可选）

不挂 Behaviour 也可以：

```csharp
var config = AgentConfigLoader.Load();
var agent = new VLAgentClient(config);
await agent.StartAsync();

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

`ReportState` 全部可选，变化时上报即可，建议不超过约 1Hz。Sample 的 `FakeTelemetryDemo` 可看假进度。

## 兼容性

- Unity 2022.3 LTS，API Compatibility Level：.NET Standard 2.1
- MQTT TCP，默认 1883
- MQTTnet 4.3.7.1207（MIT，见 Third Party Notices）
