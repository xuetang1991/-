# 泰拉瑞亚 MOD 代码规范 · L0 入口（唯一事实源）

> 适用对象：本工作区后续新建的 tModLoader MOD 工程（旧工程不追溯）。
> 本文件是唯一事实源；细则与正反例见 `docs/code-style.md`。Agent 每次任务**先读本文件**再动手。

## 1. 版本基线（MUST）

| 项 | 值 |
|---|---|
| tModLoader | stable 1.4.4.9 / v2026.08.3.0（2026-10 核实） |
| .NET | net8.0（SDK 8.0.423，8.0.x 同基线） |
| C# | LangVersion 12.0（禁止 1.4.5-dev 的 net10 / C#14 特性） |
| dotnet 路径 | `C:\Program Files\dotnet\dotnet.exe`（在 PATH） |
| tModLoader 安装 | `E:\steam\steamapps\common\tModLoader`（脚本从 `..\tModLoader.targets` 自动解析） |
| 平台 | Windows |

升级以上任何一项 = 「必问」。

## 2. 门禁（每次改动完成前必须通过）

```powershell
pwsh -File tools/verify.ps1   # 必须退出码 0
```

`verify.ps1` 依次执行：无头构建（tModLoader 进程内编译，解析 `modReferences`；`.tmod` 打包到临时目录，不碰已安装的 `Mods\`）→ `dotnet format` 校验 → 贴图检查 → 本地化键检查 → 静态状态登记检查 → 禁用模式与编码检查。
游戏内 Build 只用于运行验证，**不能代替门禁**。
**旧代码基线**：本工程接手时已有一批不符合规范的既有代码（用户决定「旧的代码不用改」）。门禁对该部分的放行只认 `docs/legacy-baseline.txt` 里**逐条登记**的项（目前仅 `warning CS0672` 一条编译警告 + 旧本地化文件整文件跳过）；新代码不得往该文件里加项，写完该文件对应旧代码后必须删行恢复全量检查。

## 3. 必问（停下来问人，不得自行决定）

1. 引入第三方库 / NuGet
2. 修改 `*.csproj`、`build.txt` 或任何构建配置
3. 引入 MonoMod / IL Hook / 反射
4. 删除或修改已有 public API
5. 跨文件大重构（>3 个文件或 >200 行）
6. 修改本规范、门禁脚本或 `.editorconfig`
7. 新增 / 删除 / 移动文件或目录（含新增一级玩法域）
8. 新增静态可变状态（须登记 `docs/state-registry.md`）
9. 网络消息号或协议格式变更
10. 破坏存档兼容
11. 新增贴图 `Texture` override 例外
12. 增加 `#pragma` / 抑制警告 / 任何例外登记
13. 变更版本基线

## 4. 自主（直接做，完工报告）

- 在既定目录内新增 ModType 及其同名 `.png`
- 实现既定行为；补 XML/注释/测试；修「有复现步骤且已定位根因」的 bug
- 局部重命名、格式化（只动任务相关文件）
- 完工必须跑门禁并在报告里贴结果

## 5. 禁止（默认不做）

降低/删除门禁、反射/线程/unsafe、绕过失败的测试、删除带说明的既有代码、改版本基线、与任务无关的格式化/重命名。

## 6. 规则优先级

用户当前指令 > 本文件 > `docs/code-style.md`（L2 细则）> `.editorconfig`（机器格式）。
机器格式以 `.editorconfig` + 门禁为准；语义冲突先问人。所有例外登记到 `docs/style-exceptions.md`。

## 7. 指针表

| 文件 | 作用 |
|---|---|
| `docs/code-style.md` | L2 规则正文（规则 ID + MUST/SHOULD/MAY + 正反例） |
| `docs/state-registry.md` | 静态可变状态登记表（唯一白名单） |
| `docs/style-exceptions.md` | 例外登记表（贴图 / 规则 / 全局降级） |
| `docs/legacy-baseline.txt` | 旧代码基线豁免清单（门禁逐条比对的唯一来源） |
| `docs/anti-patterns.md` | 反例库（AI 坏模式 + 历史踩坑） |
| `tools/verify.ps1` | 总门禁 |
| `tools/build.ps1` | 无头构建（不打包 `.tmod`） |
| `tools/CommonTools.ps1` | 脚本公共函数（dotnet / 工程定位、无头构建） |
| `tools/verify-textures.ps1` | 贴图命名 / override 例外检查 |
| `tools/check-localization.ps1` | 双语键双向齐全检查 |
| `.editorconfig` | L1 格式与命名机器规则 |

## 8. 开工流程（MUST）

1. 读本文件 + `docs/code-style.md` 对应章节 + 相关登记表。
2. 列「将改哪些文件、为什么」；命中必问清单 → 先停下来问。
3. 最小 diff 实现，不顺手改无关文件。
4. `pwsh -File tools/verify.ps1` 通过。
5. 按模板报告：**改了什么 / 为什么 / 验证结果 / 遗留 / 需确认**。

## 9. Top 硬规则速查（细则见 L2）

- 4 空格缩进、全 Allman 大括号、file-scoped namespace、UTF-8 **带 BOM** + CRLF + 末尾空行。
- 禁用 `var`；私有字段 `_camelCase`；其余 PascalCase/camelCase；ModType 强制后缀 + 一类一文件 + 同名 png。
- 贴图一律自动推断，禁止 `Texture` override（仅四类白名单 + 例外登记）。
- 静态可变状态默认禁止；确需使用必须登记 `docs/state-registry.md` 并在对应钩子复位。
- 逻辑计时用 tick / `Main.GameUpdateCount`；随机：表现层 `Main.rand`，权威逻辑服务端决定或独立 `UnifiedRandom`。
- 自定义包：`Mod.GetPacket()` + `HandlePacket` switch + 集中 `NetMessageType` 枚举；单机路径不发包。
- 日志只用 `Mod.Logger`，双语单行「中文 / English」；禁空 catch、禁每帧日志、禁 `Console.WriteLine`。
- 注释：实现内中文单语；public 契约 XML = `<summary>` 中文 + `<remarks>` 英文；禁记录式标记（`【V\d】` / `ASSUMPTION-nn` / 复述已删实现）。
- 文本一律本地化（`en-US` + `zh-Hans` 双向齐全），禁止代码内硬编码。
