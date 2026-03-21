# ShogiDroid Rebuild (将棋ドロイドR)

配布終了した将棋ドロイド改造版をデコンパイルし、.NET 9 Android で再ビルドしたフォーク。

## ビルド方法

```bash
# /usr/local/share/dotnet を使用（android workload 必要）
# Debug は arm64 単一アーキテクチャでビルド（高速化）
DOTNET_ROOT=/usr/local/share/dotnet /usr/local/share/dotnet/dotnet build -c Debug

# Release ビルド（実機にはこちらを使用）
DOTNET_ROOT=/usr/local/share/dotnet /usr/local/share/dotnet/dotnet build -c Release
```

### 前提条件
- .NET 9 SDK (`/usr/local/share/dotnet/dotnet` 9.0.200)
- Android workload: `sudo /usr/local/share/dotnet/dotnet workload install android`
- Homebrew 版 dotnet (9.0.104) は android workload 非対応なので使わない
- `global.json` で SDK バージョンを 9.0.200 に固定済み

## エミュレータでのテスト

```bash
# インストール
adb install -r ShogiDroid/bin/Debug/net9.0-android/android-arm64/com.siganus.ShogiDroid.rebuild-Signed.apk

# 起動
adb shell am start -n com.siganus.ShogiDroid.rebuild/crc64721063ab64a94a2e.MainActivity

# ログ確認
adb logcat -s ShogiDroid

# ストレージ権限付与（API 30+）
adb shell appops set com.siganus.ShogiDroid.rebuild MANAGE_EXTERNAL_STORAGE allow

# デバッグコマンド（Debug ビルドのみ）
adb shell am broadcast -a com.siganus.ShogiDroid.rebuild.DEBUG --es cmd analyze
adb shell am broadcast -a com.siganus.ShogiDroid.rebuild.DEBUG --es cmd next
adb shell am broadcast -a com.siganus.ShogiDroid.rebuild.DEBUG --es cmd prev
adb shell am broadcast -a com.siganus.ShogiDroid.rebuild.DEBUG --es cmd first
adb shell am broadcast -a com.siganus.ShogiDroid.rebuild.DEBUG --es cmd last
adb shell am broadcast -a com.siganus.ShogiDroid.rebuild.DEBUG --es cmd reverse
adb shell am broadcast -a com.siganus.ShogiDroid.rebuild.DEBUG --es cmd menu
adb shell am broadcast -a com.siganus.ShogiDroid.rebuild.DEBUG --es cmd stop
adb shell am broadcast -a com.siganus.ShogiDroid.rebuild.DEBUG --es cmd screenshot
adb shell am broadcast -a com.siganus.ShogiDroid.rebuild.DEBUG --es cmd book_load --es path /sdcard/book.db
```

## プロジェクト構成

```
ShogiDroid/
├── Activities/          # Android Activity（画面）
├── AppDebug/            # ログ出力（Android Logcat, tag: ShogiDroid）
├── Resources/           # Android リソース（layout, values, drawable 等）
├── ShogiDroid.Controls/ # カスタム UI コントロール
├── ShogiDroid.Controls.ShogiBoard/  # 将棋盤の描画・操作
├── ShogiGUI/            # アプリ設定、ドメインロジック
├── ShogiGUI.Engine/     # USI エンジン通信
├── ShogiGUI.Events/     # イベント定義
├── ShogiGUI.Models/     # ゲームモデル
├── ShogiGUI.Presenters/ # MVP Presenter
├── ShogiLib/            # 将棋ルール・棋譜管理の基盤ライブラリ
└── ShogiLib.Kifu/       # 棋譜形式パーサー（KIF, CSA, SFEN, KI2）
```

## 重要な技術的注意点

### .NET Android 固有の問題
- **Activation Constructor**: XML からインフレートされるカスタム View / Preference は `protected ClassName(IntPtr, JniHandleOwnership)` コンストラクタが必須
- **EmbedAssembliesIntoApk**: `true` にしないとアセンブリ読み込み失敗でクラッシュ
- **BinaryFormatter 廃止**: .NET 9 では使えない。ポリモーフィックな型の DeepCopy は `Clone()` メソッドで対応

### エンジン起動
- Android 10+ (API 29+) では SELinux の `execute_no_trans` 拒否のため、アプリデータ領域のバイナリを直接実行できない
- `/system/bin/linker64` をラッパーとして使用して回避
- chmod 値: 484 (10進) = 0744 (8進) = rwxr--r--

### アプリ識別
- ApplicationId: `com.siganus.ShogiDroid.rebuild`（元の `com.siganus.ShogiDroid` と共存可能）
- ContentProvider authority: `com.siganus.ShogiDroid.rebuild.provider`

### vast.ai クラウドエンジン接続
- SSH.NET ライブラリによる SSH 接続（平文 TCP から移行済み）
- エンジンはSSH経由で `/workspace` 内のバイナリを直接起動
- socat によるポート公開は不要（セキュリティ改善）
- `VastAiActivity` でインスタンス管理、エンジン自動検出・選択
- NNUE/DEEP の自動判別（USI オプションの `UCT_NodeLimit` / `DNN_Batch_Size` の有無）
- Threads / Hash / UCT_NodeLimit をインスタンススペックに基づき自動設定
- RemoteMonitor で CPU/GPU 利用率をSSH経由で取得・表示

### 棋神アナリティクス連携
- `kishin-analytics.heroz.jp` の Deep Link 対応
- 将棋ウォーズのAIボタンからShogiDroidで棋譜を直接読み込み可能
- 端末側で「デフォルトで開く」設定が必要（Android 12+ のドメイン検証制約）

## 元のアプリとの差分
- 広告関連クラス（MyInterstitialAd 等）は意図的に除外
- SSH Engine は SSH.NET ベースで再実装済み（旧アセットは不要）
- 文字列はすべて日本語に統一済み

## 既知の問題
- エンジンオプション画面が不安定な場合がある
