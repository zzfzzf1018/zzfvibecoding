# 中国象棋 Android App

一款支持双人对战和人机对战的中国象棋应用。

## 功能特性

- **双人对战**：两位玩家在同一设备上轮流下棋
- **人机对战**：与 AI 对弈，支持三档难度（简单/中等/困难）
- **走法提示**：选中棋子后显示所有合法落点
- **悔棋功能**：支持悔棋操作
- **将军检测**：自动检测将军和将杀状态

## 项目结构

```
app/src/main/java/com/chess/chinese/
├── MainActivity.kt          # 主菜单界面
├── GameActivity.kt          # 游戏界面
├── game/
│   ├── Piece.kt            # 棋子定义
│   ├── Move.kt             # 走法定义
│   ├── ChessBoard.kt       # 棋盘逻辑
│   ├── ChessRules.kt       # 规则引擎（走法生成+合法性校验）
│   ├── ChessAI.kt          # AI引擎（Minimax + Alpha-Beta剪枝）
│   └── GameManager.kt      # 游戏管理器
└── ui/
    └── ChessBoardView.kt   # 棋盘绘制视图
```

## 编译打包

### 方法一：使用 Android Studio（推荐新手）

#### 前置条件
- 安装 [Android Studio](https://developer.android.com/studio)（会自动安装 JDK 和 Android SDK）

#### 步骤
1. 用 Android Studio 打开本项目目录
2. 等待 Gradle 同步完成
3. 点击菜单 `Build` → `Build Bundle(s) / APK(s)` → `Build APK(s)`
4. APK 输出在 `app/build/outputs/apk/debug/app-debug.apk`

### 方法二：纯命令行编译（不需要 Android Studio）

只需安装 JDK 17 和 Android SDK 命令行工具，无需安装几 GB 的 Android Studio。

#### 前置条件

1. **安装 JDK 17**（必须是 17，不支持 JDK 25 等更新版本）
   - Windows 一键安装：`winget install EclipseAdoptium.Temurin.17.JDK`
   - 或从 https://adoptium.net/ 下载
   - 安装路径通常为：`C:\Program Files\Eclipse Adoptium\jdk-17.0.18.8-hotspot`

2. **下载 Android SDK Command-Line Tools**
   - 下载地址：https://developer.android.com/studio#command-line-tools-only
   - 解压到一个目录，例如 `D:\android-sdk\cmdline-tools\latest\`

3. **安装必要 SDK 组件**
   ```powershell
   cd D:\android-sdk\cmdline-tools\latest\bin
   .\sdkmanager.bat --sdk_root=D:\android-sdk "platform-tools" "platforms;android-34" "build-tools;34.0.0"
   .\sdkmanager.bat --sdk_root=D:\android-sdk --licenses
   ```

4. **配置 local.properties**

   在项目根目录的 `local.properties` 文件中设置 SDK 路径：
   ```properties
   sdk.dir=D:\\android-sdk
   ```

#### 编译 Debug APK

```powershell
# 设置 JAVA_HOME（每次新开终端都需要）
$env:JAVA_HOME = "C:\Program Files\Eclipse Adoptium\jdk-17.0.18.8-hotspot"

# 进入项目目录
cd d:\zzf\flyingdev\zzfvibecoding\g\che

# 编译 Debug 版本
.\gradlew.bat assembleDebug
```

生成的 APK 位于：`app/build/outputs/apk/debug/app-debug.apk`（约 5.5MB）

#### 编译 Release APK（签名版，推荐）

Release 版本体积更小（约 4.5MB），已配置好签名。

```powershell
$env:JAVA_HOME = "C:\Program Files\Eclipse Adoptium\jdk-17.0.18.8-hotspot"
cd d:\zzf\flyingdev\zzfvibecoding\g\che
.\gradlew.bat assembleRelease
```

生成的 APK 位于：`app/build/outputs/apk/release/app-release.apk`

> **签名密钥信息**（已预生成 `chess-release-key.jks`）：
> - 密钥别名：chess
> - 密钥密码：chess123
> - 如需自定义签名，修改 `app/build.gradle.kts` 中的 `signingConfigs` 配置

### 常见问题

| 问题 | 解决方案 |
|------|---------|
| `gradlew.bat` 无法识别 | 使用 `.\gradlew.bat`（加 `.\` 前缀） |
| Gradle 下载超时 | 已配置腾讯云镜像，检查网络连接 |
| `What went wrong: 25.0.3` | JDK 版本太高，必须使用 JDK 17 |
| Maven 依赖下载慢 | 已配置阿里云镜像，首次构建需要几分钟 |
| JAVA_HOME 未设置 | 每次新终端需执行 `$env:JAVA_HOME = "..."` |

## 安装到手机

1. 在手机上开启「允许安装未知来源应用」
2. 将生成的 APK 文件传输到手机（USB/微信/网盘均可）
3. 点击 APK 文件安装即可

## AI 难度说明

| 难度 | 搜索深度 | 特点 |
|------|---------|------|
| 简单 | 2层 | 适合初学者，AI较弱 |
| 中等 | 3层 | 有一定棋力，适合普通玩家 |
| 困难 | 4层 | AI较强，需要一定棋力才能战胜 |

## 技术实现

- **语言**：Kotlin
- **UI**：自定义 Canvas 绘制棋盘和棋子
- **AI 算法**：Minimax 搜索 + Alpha-Beta 剪枝 + 位置评估表
- **最低支持**：Android 7.0 (API 24)
