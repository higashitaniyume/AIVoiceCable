# AIVoiceCable

AIVoiceCable 是一个基于 .NET 8 / WPF 的桌面工具，用于把 AI 生成的语音播放到 VB-CABLE 的虚拟麦克风链路中。

典型使用方式是：应用把 TTS 音频播放到 `CABLE Input`，其他软件把 `CABLE Output` 设置为麦克风后，就能听到 AI 的语音回复。

## 功能概览

- 自定义回复：输入文本，调用 Fish Audio 生成语音，试听或发送到 VB-CABLE。
- 完全 AI 回复：监听系统播放声音，经过 ASR -> LLM -> TTS 后自动播放到 VB-CABLE。
- 声色管理：支持查看、新建、编辑、删除声色，并内置预置声色“永雏塔菲”。
- API 设置：支持 Fish Audio、AssemblyAI、DeepSeek 或任意 OpenAI-compatible LLM。
- 音频设备设置：枚举播放/录音设备，检测 VB-CABLE，选择 TTS 输出和本机监听设备。
- 日志调试：界面实时日志，同时按日期写入本地日志文件。
- 本地持久化：配置、声色、历史记录、日志和缓存保存在 `%AppData%/AIVoiceCable/`。
- 敏感信息保护：API Key 使用 Windows DPAPI 加密保存，设置界面使用密码框隐藏。

## 技术栈

- C# / .NET 8
- WPF
- MVVM
- CommunityToolkit.Mvvm
- Microsoft.Extensions.DependencyInjection
- NAudio
- System.Security.Cryptography.ProtectedData

## 环境要求

- Windows 10 或更高版本
- Visual Studio 2022，需安装“.NET 桌面开发”工作负载
- .NET 8 SDK
- VB-Audio Virtual Cable
- Fish Audio API Key
- AssemblyAI API Key，用于完全 AI 回复的实时转录
- OpenAI-compatible LLM API Key，例如 DeepSeek

## 快速开始

1. 安装 VB-Audio Virtual Cable。
2. 打开解决方案：

   ```powershell
   AIVoiceCable.sln
   ```

3. 在 Visual Studio 中启动 `AIVoiceCable` 项目。
4. 打开“API 设置”页面，填写并保存：
   - Fish Audio API Key
   - AssemblyAI API Key
   - LLM 服务商 Base URL、模型名和 API Key
5. 打开“音频设备设置”页面，点击“刷新设备列表”。
6. 将 TTS 输出设备设置为 `CABLE Input`。
7. 如果需要自己听到 AI 语音，开启“同时在本机监听 AI 语音”，并选择真实耳机或音响。
8. 在“自定义回复”页面输入文本，点击“试听”或“发送到 VB-CABLE”。

## 从命令行构建

```powershell
dotnet restore AIVoiceCable.sln
dotnet build AIVoiceCable.sln
dotnet build AIVoiceCable.sln -c Release
```

## 配置和本地文件

应用首次启动会自动创建：

```text
%AppData%/AIVoiceCable/
  config.json
  voices.json
  history.json
  secrets.json
  logs/
  cache/
```

说明：

- `config.json`：普通配置，例如模型、Base URL、音频设备 ID。
- `voices.json`：声色配置。
- `history.json`：自定义回复历史记录。
- `secrets.json`：API Key 加密后的密文。
- `logs/`：按日期滚动的日志文件。
- `cache/`：TTS 生成的临时音频缓存。

如果配置文件损坏，应用会自动备份为 `.bad.<timestamp>` 文件并使用默认配置继续启动。

## API Key 加密说明

API Key 不会明文写入配置文件。

应用使用 Windows DPAPI：

```csharp
ProtectedData.Protect(..., DataProtectionScope.CurrentUser)
```

这意味着密钥由当前 Windows 用户上下文保护。通常只有同一台机器上的同一个 Windows 用户可以解密 `secrets.json` 中的内容。

注意：应用运行时为了调用 API，仍然需要把 API Key 解密到进程内存中。设置界面使用 `PasswordBox` 隐藏输入内容，但这不等同于内存级机密隔离。

## 页面说明

### 自定义回复

- 输入多行纯文本。
- 选择声色和 Fish Audio 模型。
- 支持试听、发送到 VB-CABLE、保存音频文件、停止播放和清空文本。
- 成功生成的内容会写入历史记录，重启后仍可复用。

### 完全 AI 回复

流程：

```text
系统声音 -> WASAPI loopback -> AssemblyAI ASR -> LLM -> Fish Audio TTS -> CABLE Input
```

播放 TTS 时会暂停处理 ASR final transcript，避免 AI 自己的声音被再次识别后形成循环。

### 声色管理

- 内置“永雏塔菲”预置声色。
- 支持自定义 Fish Audio `reference_id` / voice id。
- 支持设置默认声色。

### API 设置

- Fish Audio：API Key、Base URL、默认模型、回退模型。
- AssemblyAI：API Key、WebSocket Endpoint。
- LLM：支持多个 OpenAI-compatible 服务商，可新增、编辑、删除和设置默认。

### 音频设备设置

- 枚举系统播放设备和录音设备。
- 自动标记 VB-CABLE 相关设备。
- 支持应用内双路播放：一份到 `CABLE Input`，一份到真实监听设备。

### 日志 / 调试

- 实时显示应用日志。
- 写入 `%AppData%/AIVoiceCable/logs/`。
- 日志会对常见 API Key / Authorization 内容做脱敏处理。

## 项目结构

```text
AIVoiceCable/
  AIVoiceCable.sln
  src/
    AIVoiceCable/
      App.xaml
      MainWindow.xaml
      Behaviors/
      Converters/
      Interfaces/
      Models/
      Services/
      ViewModels/
      Views/
```

核心分层：

- `Views/`：WPF 页面。
- `ViewModels/`：页面状态、命令和流程编排。
- `Services/`：配置、日志、API、音频设备、播放、捕获、历史记录。
- `Interfaces/`：TTS、LLM、ASR、音频播放/捕获等可替换接口。
- `Models/`：配置、声色、历史、日志、设备等数据模型。

## 重要实现边界

- 当前使用设备级 WASAPI loopback 监听系统声音。
- 按具体应用程序捕获音频未作为稳定主流程实现。
- VB-CABLE 的 Windows 控制面板“侦听此设备”未强行程序化修改；应用提供更稳定的本机同步监听方案。
- Fish Audio、AssemblyAI、LLM 的接口参数集中在配置和服务中，后续可按官方 API 调整。

## 安全建议

- 不要把 `%AppData%/AIVoiceCable/secrets.json` 提交到仓库。
- 不要在日志、截图或 issue 中暴露完整 API Key。
- 如果怀疑 API Key 泄露，应到对应服务商后台立即轮换密钥。

## 开发备注

提交前建议运行：

```powershell
dotnet build AIVoiceCable.sln
dotnet build AIVoiceCable.sln -c Release
```
