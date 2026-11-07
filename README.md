# 泰拉瑞亚 MOD 代码规范 · 落地包（手册 v1.0，2026-10-17）

本目录是「接下来要做的泰拉瑞亚 MOD」的**代码规范落地包**，设计目标：给 AI Agent（DSH）一份可执行、
可机器验证的规范，而不只是一份文档。

> 规范内容与工程细则分开：这里只管**代码规范 + Agent 行为规则**；MOD 内容（玩法、数值、工程计划）不在本包范围。

## 一、包里有什么

| 文件 | 作用 |
|---|---|
| [AGENTS.md](AGENTS.md) | **唯一事实源**（≤150 行）。Agent 每次开工先读它：版本基线、门禁命令、必问清单、自主边界、规则指针 |
| [.editorconfig](.editorconfig) | L1 机器格式规则（4 空格、CRLF、UTF-8 BOM、Allman、file-scoped、命名规则等），`dotnet format` 强制执行 |
| [docs/code-style.md](docs/code-style.md) | **规范正文 v1.0**（约 540 行）：规则 ID + 中文正文 + MUST/SHOULD/MAY + 正反例，17 个主题章节 + 附录 A/B/C |
| [docs/state-registry.md](docs/state-registry.md) | 静态可变状态登记表（CS-STATE-003 的唯一合法出口） |
| [docs/style-exceptions.md](docs/style-exceptions.md) | 例外登记表（含贴图 override 四类白名单、全局降级流程） |
| [docs/anti-patterns.md](docs/anti-patterns.md) | 反例库：AI 生成痕迹、已踩坑清单、注释坏模式、速查表 |
| [tools/CommonTools.ps1](tools/CommonTools.ps1) | 脚本公共函数（找 dotnet、找工程、找源文件、无头构建） |
| [tools/build.ps1](tools/build.ps1) | 无头构建（0 warning，不打包 .tmod） |
| [tools/verify.ps1](tools/verify.ps1) | **总门禁**：构建 + `dotnet format` + 贴图 + 本地化 + 静态状态 + 禁用模式 + 编码 |
| [tools/check-localization.ps1](tools/check-localization.ps1) | 双语本地化文件键集双向齐全检查 |
| [tools/verify-textures.ps1](tools/verify-textures.ps1) | ModType 同名贴图 + Texture override 登记检查 |

## 二、怎么用（新 MOD 工程）

1. 新建模组工程（tModLoader 模板，名字换成你的，如 `MyMod`），把本目录下 **AGENTS.md、.editorconfig、
   docs\、tools\** 复制到工程根目录（与 `.csproj` 同级）。
2. 按 [docs/code-style.md](docs/code-style.md) 第 3 章改工程根 `*.csproj`（`net8.0`、`LangVersion 12.0`、
   `Nullable enable`、`TreatWarningsAsErrors`、`EnforceCodeStyleInBuild`、`AllowUnsafeBlocks false`、`AssemblyName`），
   并补 `build.txt` / `description.txt` / `icon.png`。
3. 在工程根执行总门禁：

   ```powershell
   pwsh -File tools/verify.ps1
   ```

   通过后退出码为 0；任何一步失败都会列出具体文件与行号，先修根因，**不要绕过门禁**。
4. 只编译不打包（游戏没关也能跑）：

   ```powershell
   pwsh -File tools/build.ps1
   ```

## 三、前置条件

- **tModLoader**：stable 1.4.4.9 / tML v2026.08.3.0（本机 `D:\Steam\steamapps\common\tModLoader`）。
- **.NET SDK 8.0.x**：本机为 `C:\Users\Alsac\.dotnet\dotnet.exe`（8.0.425，**不在 PATH**）；脚本会自动找它，
  找不到再退回 PATH 上的 `dotnet`。没有 8.x SDK 时 `dotnet format` 不可用。
- **PowerShell 7（pwsh）**：`.ps1` 文件必须 **UTF-8 with BOM**（否则中文在 PowerShell 5.1 下会乱码/报错）。
- `tools/verify.ps1` 里的 `dotnet format` 命令需要工程能成功 restore；首次运行会稍慢。

## 四、门禁哲学（为什么这么设计）

- 规范分三层：**L0 AGENTS.md**（Agent 入口与纪律）、**L1 .editorconfig + csproj**（机器强制）、
  **L2 docs/**（人读细则）。三层互相指向，不重复抄写。
- 强制走**无头 CLI 通道**：`dotnet build -warnaserror` + `dotnet format --verify-no-changes`。
  游戏内 Build 用的是 Roslyn Preview 且不读 `.editorconfig`、不跑分析器，**不能当门禁**。
- 静态可变状态、贴图、本地化这些编译器抓不到的坑，用 `tools/` 里的粗检脚本兜底；检不了的部分写进 L2 文档，
  由 Agent 三档规则（必问 / 自主 / 禁止）约束。

## 五、模板占位符

本包按「新 MOD」设计，落地到具体工程时替换：

| 占位符 | 含义 | 示例 |
|---|---|---|
| `<ModName>` | 模组内部名（= `.csproj` 的 `AssemblyName`） | `MyMod` |
| `<域>` | 玩法领域目录名（PascalCase 单数或复数按领域习惯） | `Content\Combat\` |
| `<Author>` | build.txt 作者 | `Alsac` |

## 六、版本与维护

- 手册版本：**v1.0**（2026-10-17），配套 tML `v2026.08.3.0` / `net8.0` / C# 12。
- 升级 tML 或 .NET 时：先读本包 AGENTS.md「必问清单」第 13 条（工程配置变更先问），同步更新
  `docs/code-style.md` 版本章节、`.editorconfig`、`tools/` 脚本与本文档，并在变更说明里登记。
- `tools/verify.ps1` 的静态状态检查是**粗检**（误报白名单见 `docs/state-registry.md`），
  真正权威仍是 `docs/state-registry.md` 的人工登记。

## 七、本工程落地状态（2026-10-25）

- 规范包已落地到工程根：`AGENTS.md`、`.editorconfig`、`docs/`、`tools/`；`sea.csproj` 已替换为 CS-PROJ-001 模板。
- 本机环境：tML `E:\steam\steamapps\common\tModLoader`（v2026.08.3.0）、.NET SDK 8.0.423、PowerShell 7.4.6（`pwsh` 已在 PATH）。
- 门禁：`pwsh -File tools/verify.ps1`；当前基线 = 构建 0 error + 1 个**已登记** warning（CS0672，见下）。
- 构建链路：本工程引用 `SubworldLibrary`，**纯 `dotnet build` 无法解析 mod 引用**（`tMLMod.targets` 不含 modReferences 管道），因此门禁改为 tML 进程内编译：`dotnet tModLoader.dll -server -build <工程目录>\ -tmlsavedirectory <临时目录> -define "TML_2026_08;JETBRAINS_ANNOTATIONS" -unsafe false`。`-tmlsavedirectory` 让 `.tmod` 打包到临时目录，**游戏开着也能跑门禁**，且不会覆盖已安装的 `Mods\sea.tmod`。
- 旧代码基线：`docs/legacy-baseline.txt`（配合 `docs/style-exceptions.md` 的 LEGACY-001~004），只放行「接手前就存在」的问题；新代码不得往里加项，旧代码重写后必须删行恢复全量检查。
- 已知限制：本 tML 版本不读取 `build.txt` 的 `buildIgnores`，`docs/`、`tools/`、`AGENTS.md`、`README.md`、`.editorconfig` 会被一起打进 `.tmod`（约 +34 KB，不影响运行）。
