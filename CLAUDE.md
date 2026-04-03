# ShogiDroid (ngs43 build)

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
adb install -r ShogiDroid/bin/Debug/net9.0-android/android-arm64/com.ngs43.shogidroid-Signed.apk

# 起動
adb shell am start -n com.ngs43.shogidroid/crc64721063ab64a94a2e.MainActivity

# ログ確認
adb logcat -s ShogiDroid

# ストレージ権限付与（API 30+）
adb shell appops set com.ngs43.shogidroid MANAGE_EXTERNAL_STORAGE allow

# デバッグコマンド（Debug ビルドのみ）
adb shell am broadcast -a com.ngs43.shogidroid.DEBUG --es cmd analyze
adb shell am broadcast -a com.ngs43.shogidroid.DEBUG --es cmd next
adb shell am broadcast -a com.ngs43.shogidroid.DEBUG --es cmd prev
adb shell am broadcast -a com.ngs43.shogidroid.DEBUG --es cmd first
adb shell am broadcast -a com.ngs43.shogidroid.DEBUG --es cmd last
adb shell am broadcast -a com.ngs43.shogidroid.DEBUG --es cmd reverse
adb shell am broadcast -a com.ngs43.shogidroid.DEBUG --es cmd menu
adb shell am broadcast -a com.ngs43.shogidroid.DEBUG --es cmd stop
adb shell am broadcast -a com.ngs43.shogidroid.DEBUG --es cmd screenshot
adb shell am broadcast -a com.ngs43.shogidroid.DEBUG --es cmd book_load --es path /sdcard/book.db
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
- `develop` / `main` の通常ビルド:
  - ApplicationId: `com.ngs43.shogidroid`
  - ContentProvider authority: `com.ngs43.shogidroid.provider`
  - 表示名: `ShogiDroid`
- `release/distribute` の通常版:
  - ApplicationId: `com.ngs436.ShogiDroidR`
  - ContentProvider authority: `com.ngs436.ShogiDroidR.provider`
  - 表示名: `将棋ドロイドR`
- `release/distribute` の Classic 版:
  - ApplicationId: `com.ngs436.ShogiDroidRClassic`
  - ContentProvider authority: `com.ngs436.ShogiDroidRClassic.provider`
  - 表示名: `将棋ドロイドR Classic`
- Classic 版は見た目のみ差し替える。機能差を作らず、Material 専用 UI/リソースを classic 側へ持ち込まない
- release の通常版と Classic 版は共存できるように、ApplicationId / authority / debug action を必ず分ける

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
- 将棋ウォーズのAIボタンから ShogiDroid で棋譜を直接読み込み可能
- 端末側で「デフォルトで開く」設定が必要（Android 12+ のドメイン検証制約）

## 元のアプリとの差分
- 広告関連クラス（MyInterstitialAd 等）は意図的に除外
- SSH Engine は SSH.NET ベースで再実装済み（旧アセットは不要）
- 文字列はすべて日本語に統一済み

## ブランチ運用
- 話題や機能が変わったと判断した場合、develop から新しい feature ブランチを自由に作成してよい
- 完成後に develop へマージし、feature ブランチは削除する
- GitHub 上で `release` ブランチを公開する際は、通常版に加えて、機能は同一で見た目のみ異なる Classic 版もあわせてリリースする

## リリース運用
- Release 作業は `release/distribute` worktree (`/Users/keinoda/ShogiDroid-rebuild-release`) で行う
- 先に `develop` 側の変更を内容単位で整理して commit し、その後 `release/distribute` へ取り込む
- `release/distribute` では以下の release 固有設定を入れる:
  1. `EngineSettings.VastAiDockerImage` のデフォルト値を `"keinoda/shogi:AobaNNUE"` にする
  2. `EngineSettings.DockerImageLockFingerprint` に開発用 SSH 鍵の SHA256 を設定する
     - 現状は `id_ed25519` をアプリ側で `id_rsa.pem` に rename して使う前提
  3. Dockerfile は `ShogiDroid/docker/` に置く。新規追加時は通常 GitHub 管理対象にしない
- Release ビルドは通常版と Classic 版の両方を必ず確認する:

```bash
dotnet build ShogiDroid/ShogiDroid.csproj -c Release
dotnet build ShogiDroid/ShogiDroid.csproj -c Release -p:ClassicUi=true
```

- 生成物として確認する signed APK:
  - 通常版: `ShogiDroid/bin/Release/net9.0-android/android-arm64/com.ngs436.ShogiDroidR-Signed.apk`
  - Classic 版: `ShogiDroid/bin/Release/net9.0-android/android-arm64/com.ngs436.ShogiDroidRClassic-Signed.apk`
- GitHub Release は tag から作成し、リリースノートは `RELEASE_NOTES_vX.Y.Z.md` をそのまま使う
- GitHub Release には signed APK を 2 本添付する:
  - `ShogiDroid-vX.Y.Z.apk`
  - `ShogiDroid-Classic-vX.Y.Z.apk`
- 公開手順の基本順序:
  1. `release/distribute` を最新化して通常版 / Classic 版をビルド確認
  2. `git tag vX.Y.Z`
  3. branch と tag を GitHub へ push
  4. `RELEASE_NOTES_vX.Y.Z.md` を使って GitHub Release を作成
  5. signed APK 2 本を Release asset としてアップロード
- GitHub には 100MB 制限がある。`ShogiDroid/Assets/AobaNNUE/eval/nn.bin` のような大きいファイルは Git LFS を使う
- APK は GitHub Release asset として配布し、通常の git 管理対象にはしない

## バグ修正の方針
- すべてのバグに対して、可能な限り即物的な対応・対処療法的な対応に終始するのではなく、**論理的におかしいものがないか根本原因を検討すること**
- 表面的な症状を抑えるだけでなく、設計やデータフローに構造的な問題がないかを考慮した上で修正を行う

## 禁止事項
- **クリーンインストール（adb uninstall → install）を勝手に実行しないこと**。設定・エンジンオプション等が全て消失する。必ず `adb install -r`（上書きインストール）を使う
- **release/distribute ブランチで `dotnet build` を実行しないこと**。release ブランチは ApplicationId が異なり（`com.ngs436.ShogiDroidR`）、ビルドすると署名キーの状態が汚染され、develop 側（`com.ngs43.shogidroid`）の上書きインストールが署名不一致で失敗し、端末からアプリが消失する恐れがある。Release ビルドは必ず専用 worktree (`/Users/keinoda/ShogiDroid-rebuild-release`) で行う
- **ブランチ切り替え時は `bin/` `obj/` を意識すること**。異なる ApplicationId のブランチでビルドした後は、`bin/Release/` に古い APK が混在する。正しい APK パスを必ず確認してからインストールする

## 既知の問題
- エンジンオプション画面が不安定な場合がある
