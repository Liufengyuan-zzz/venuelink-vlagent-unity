# VLAgent SDK 集成指南

面向第一次对接 VenueLink 的展项开发者。看完按步骤做，就能让自己的程序出现在中控里，并收到播放指令。

协议细节、SDK 内部实现见 `VLAgent开发文档.md`；第三方设备不改代码、只收 HTTP/TCP 指令时见 `第三方设备接入说明.md`。这两份在 VenueLink 主仓，需要时向中控方索取。

---

## 1. 这套系统在干什么

展厅里通常有三块：


| 角色                 | 是什么                             | 你要不要改它         |
| ------------------ | ------------------------------- | -------------- |
| **VLServer（中控）**   | 一台 Windows 程序，管设备列表、场景、给讲解平板发界面 | 不用改，现场已经在跑     |
| **VLClient（讲解平板）** | 讲解员点按钮的界面                       | 不用改            |
| **你的展项程序**         | Unity / Electron / Node 播放内容    | **要把 SDK 嵌进去** |


SDK 的名字叫 **VLAgent**。嵌进去之后：

1. 中控能发现这台机器（名称、IP、在不在线）
2. 管理员在中控里点一次「确认」，这台展项就能被场景和按钮选中
3. 讲解员点按钮时，中控把指令发到**你自己开的 TCP/HTTP 端口**
4. **解析指令、播放视频，还是你自己的代码**。SDK 不播片、不听业务指令

```
讲解平板  →  中控 VLServer  →  你的展项（TCP/HTTP）
                 ↑
           SDK 只负责：我是谁、我在不在线
```

---

## 2. 选哪份 SDK


| 你的程序            | 用这个                                                              | 版本    |
| --------------- | ---------------------------------------------------------------- | ----- |
| Unity 2022.3    | git URL：`github.com/Liufengyuan-zzz/venuelink-vlagent-unity.git` | 0.3.4 |
| Electron 或 Node | npm：`@venuelink/vlagent`                                         | 0.1.2 |


两份配置文件、字段、行为相同。下面先写公共步骤，再分语言。

---

## 3. 开始前准备

- 中控 VLServer 已启动，和展项电脑在同一局域网
- 问现场要 **中控的局域网 IP**（例如 `192.168.1.10`）。不是展项自己的 IP
- 给这台展项起一个不会重复的编号，例如 `exhibit-japan-01`
- 展项程序里已经有（或准备加）一个 **TCP 监听端口**，用来收指令，例如 `9000`

本机自己电脑上试：中控 IP 填 `127.0.0.1`，先起 VLServer 再起展项。

---

## 4. 只写一份配置：`vlagent.json`

SDK 和你收指令的代码 **必须读同一份文件**，不要在代码里再写一套 IP/端口。

```json
{
  "deviceId": "exhibit-japan-01",
  "brokerHost": "192.168.1.10",
  "brokerPort": 1883,
  "heartbeatIntervalMs": 3000,
  "reconnectDelayMs": 2000,
  "advertisePort": 9000
}
```


| 字段              | 填什么                                 |
| --------------- | ----------------------------------- |
| `deviceId`      | 这台展项的唯一编号，馆内不能重复                    |
| `brokerHost`    | **中控**的 IP。本机联调填 `127.0.0.1`        |
| `brokerPort`    | 一般 `1883`，不用改                       |
| `advertisePort` | **你的程序**真正 Listen 的端口。SDK 不会帮你开这个端口 |
| 后两个             | 可保持默认                               |


IP、MAC **不用写**。SDK 启动时会自动探测能连到中控的那张网卡。

---

## 5. 接到你的程序里

### Electron / Node

```powershell
pnpm add @venuelink/vlagent
```

把 `vlagent.json` 放到程序能读到的位置。只在 **Electron 主进程或 Node** 里调用，不要在渲染进程 `import`。

```js
import { loadConfig, VLAgentClient } from "@venuelink/vlagent";
import { join } from "node:path";
import { app } from "electron";

const config = loadConfig(join(app.getAppPath(), "vlagent.json"));
const agent = new VLAgentClient(config);

agent.onConnectionChanged = (ok) => console.log(ok ? "已连上中控" : "与中控断开");
await agent.start();

app.on("before-quit", async (e) => {
  e.preventDefault();
  await agent.stop();
  app.exit(0);
});
```

程序退出时调用 `stop()`，中控会显示正常下线。崩溃没走到 `stop()` 时，中控过几秒也会显示离线。

### Unity（2022.3 LTS）

1. Package Manager → 左上 `+` → **Add package from git URL** → 填
   `https://github.com/Liufengyuan-zzz/venuelink-vlagent-unity.git#v0.3.4`
   （`#` 后是版本 tag，现场交付建议锁定；不写则取最新）
2. 导入后会自动生成 `Assets/StreamingAssets/vlagent.json`（已有文件不覆盖）
3. 改这个 JSON 里的 `deviceId`、`brokerHost`、`advertisePort`
4. **独立空物体**上挂 `VLAgentBehaviour`（默认切场景不销毁），运行即可

也可以自己写：

```csharp
var config = AgentConfigLoader.LoadFromStreamingAssets();
var agent = new VLAgentClient(config);
await agent.StartAsync();
```

退出时 `StopAsync()`，不用了再 `Dispose()`。

可导入包内 **Basic Agent Sample**，里面有配置示例和 TCP 收指令示例。

---

## 6. 你还要自己收指令

默认接入方式是 **FixedTcp**：中控连到「确认时的 IP + `advertisePort`」，发 **一行 UTF-8 文本 + 换行** `\n`，然后断开。内容由中控「指令管理」里配置，SDK **原样不改**。

下面是最小 Node 示例（端口必须和 JSON 里的 `advertisePort` 相同）：

```js
import { createServer } from "node:net";
import { loadConfig } from "@venuelink/vlagent";

const { advertisePort } = loadConfig("./vlagent.json");

createServer((socket) => {
  let buf = "";
  socket.setEncoding("utf8");
  socket.on("data", (chunk) => {
    buf += chunk;
    const n = buf.indexOf("\n");
    if (n < 0) return;
    const line = buf.slice(0, n).trim();
    console.log("收到指令", line);
    // 在这里播放 / 切场景
    socket.end();
  });
}).listen(advertisePort, "0.0.0.0");
```

Unity 可参考 Sample 里的 `FixedTcpCommandServer`：同样读 `vlagent.json` 的端口再 `Listen`。

指令内容长什么样，问现场中控怎么配。常见是一行 JSON，例如 `{"action":"play","target":"japan"}`，也可能是普通文本。你按自己程序能懂的格式解析即可。

若要用 HTTP 收指令，中控确认入库时把接入方式改成 FixedHttp，并填 Path。详见主仓的 `第三方设备接入说明.md`。

---

## 7. 到中控确认入库

1. 先开 VLServer，再开你的展项
2. 中控打开 **设备管理 → 待确认 Agent**
3. 应看到你的 `deviceId`、自动采到的 IP、端口
4. 填显示名，核对接下去的 IP（见下一节），点确认
5. 默认接入方式是 FixedTcp。确认后这台设备会出现在设备列表、场景编排、按钮目标里

没出现在待确认：看展项日志是否连上；检查 `brokerHost` 是不是中控 IP、防火墙是否挡住 1883。

---

## 8. 现场必须核对 IP

待确认列表里的 IP **只是 SDK 猜的**，多网卡、VPN、虚拟网卡时经常猜错。

确认前问清楚：

- 展项 TCP 实际绑在哪张网、哪个 IP？
- 中控能不能访问这个 IP + `advertisePort`？

对不上，在确认界面改成真正收指令的 IP，再点确认。改晚了要到设备档案里改。

---

## 9. 怎样算接好了

- [ ] 展项启动后，待确认列表出现对应 `deviceId`
- [ ] 确认后设备在线（程序关掉会变离线）
- [ ] 中控对该设备发一条测试指令，你的 TCP/HTTP 能收到原文
- [ ] 收到后展项有实际反应（播放、切页等）

---

## 10. 可选：上报播放进度

不报也能被发现、被控。若希望中控侧看到进度/音量，在状态变化时调用（建议不超过每秒 1 次）：

Electron / Node：

```js
await agent.reportState({
  mode: "manual",
  media: { id: "intro", playing: true, positionMs: 125000, durationMs: 600000 },
  audio: { volume: 0.6, muted: false }
});
```

Unity：`ReportStateAsync(...)`，字段相同。全部可选，有什么报什么。

当前中控主要用于看在不在线，进度转发到讲解平板尚未做。

---

## 11. 常见问题

**SDK 会不会帮我开 9000 端口？**  
不会。`advertisePort` 只是告诉中控「去这个端口找我」。你必须自己 Listen。

**SDK 会不会帮我收播放指令？**  
不会。指令走你自己的 TCP/HTTP。SDK 只上报身份和在线。

**要不要密码？**  
不要。身份是 `agent-{deviceId}`，所以 `deviceId` 馆内不能重复。

**本机能通、现场不通？**  
本机 `brokerHost` 用了 `127.0.0.1`，现场必须改成中控局域网 IP。另外核对第 8 节的 IP。

**改了 JSON 没生效？**  
要重启展项程序。Unity 确认读的是 `StreamingAssets/vlagent.json`，不是另一份拷贝。