# コードレビュー依頼: v0.0.2-alpha → v0.0.3

## 概要

ShogiDroid（将棋ドロイドの .NET 9 Android リビルド）の v0.0.3 リリース前レビューです。
v0.0.2-alpha から 21 コミット、C# ファイル 48 本で +2,950 / -514 行の差分があります。

差分の取得:
```bash
git diff v0.0.2-alpha..develop -- '*.cs'
```

## プロジェクト背景

- 配布終了した将棋アプリのデコンパイル→ .NET 9 Android 再ビルド
- USI プロトコルで将棋エンジンを制御（ローカル実行 + vast.ai クラウド GPU）
- MVP アーキテクチャ（Game → Presenter → Activity）

## 変更の分類と重点レビュー箇所

### 1. vast.ai クラウドエンジン管理（最重要）

**対象ファイル:**
- `ShogiGUI.Engine/VastAiWatchdog.cs` — 全面書き換え。解析終了 5 分後にインスタンスを自動 Stop
- `ShogiGUI.Engine/VastAiManager.cs` — API レスポンス解析の修正、`intended_status` ベースの状態判定
- `Activities/VastAiActivity.cs` — interruptible(bid) モード対応、インスタンス起動待ち改善
- `Activities/MainActivity.cs` の `AutoBootVastAiAsync` 周辺 (2360〜2560 行付近) — エンジン接続失敗時のインスタンス自動起動

**レビュー観点:**
- `VastAiWatchdog`: シングルトンの `Timer` 管理、`lock` のデッドロックリスク、`isAnalyzing_` / `shutdownInFlight_` の状態遷移の正しさ
- `VastAiInstance.IsStopped` / `IsLoading` / `HasStartupFailure`: vast.ai API の `actual_status` は遅延し `intended_status` が先行する仕様での判定ロジック
- `AutoBootVastAiAsync`: 非同期フローの例外ハンドリング、UI スレッド安全性、`VastAiManager` (IDisposable) の寿命管理

### 2. エンジン状態管理・イベント（重要）

**対象ファイル:**
- `ShogiGUI.Models/Game.cs` — `VastAiBootRequired` イベント発火（6 箇所を `RequestVastAiBoot` ヘルパーに集約）、`ConsiderEnd` / `EngineTerminate` への `AnalyzeEnd` イベント追加、`ResumeAfterVastAiBoot`
- `ShogiGUI.Engine/EnginePlayer.cs` — 詰めろ解析対応、`HandleEngineFailure` パターン、USI デバッグログ追加
- `ShogiGUI.Engine/ThreatmateAnalyzer.cs` — 新規。バックグラウンドで Komoring Heights を使った詰めろ判定
- `ShogiGUI.Engine/KomoringEnginePlayer.cs` — 新規。Komoring Heights 専用のエンジン管理
- `ShogiGUI.Events/GameEventId.cs` — `VastAiBootRequired`, `ThreatmateUpdated` 追加

**レビュー観点:**
- `ThreatmateAnalyzer`: エンジンプロセスのライフサイクル管理、`Dispose` パターン、局面変更時の排他制御
- `Game.cs` の `pendingEngineMode_` 状態遷移: vast.ai 起動後の復帰パスが全モード（Analyze/Hint/Mate/Play）で正しいか
- `EnginePlayer` の `HandleEngineFailure`: エンジンプロセス死亡検知から UI 通知までのスレッド安全性

### 3. 並列解析（中）

**対象ファイル:**
- `ShogiGUI.Engine/ParallelAnalysisTaskRunner.cs` — 新規。複数エンジンインスタンスでの並列棋譜解析
- `ShogiGUI.Engine/ParallelAnalyzer.cs` — SSH 経由のリモートエンジン並列起動
- `Activities/MainActivity.cs` の `RunParallelAnalysis` 周辺 — バックグラウンドサービスからフォアグラウンド実行に移行

**レビュー観点:**
- 複数 SSH セッションの同時管理とクリーンアップ
- `CancellationToken` の伝搬
- `ParallelAnalysisService` 削除に伴う既存参照の残存がないか

### 4. UI / テーマ（軽）

**対象ファイル:**
- `ShogiGUI/ThemeHelper.cs` — 新規。ダーク/ライト/システム連動テーマ切替
- `Activities/ThemedActivity.cs` — 新規。テーマ適用の基底 Activity
- `Activities/SettingsHomeActivity.cs` — 設定画面のバックアップ/復元 UI、内蔵エンジン非表示トグル
- `Resources/values/colors.xml`, `Resources/values-night/colors.xml` — カラー定義の全面追加
- `Resources/layout/maina.xml` — 全画面時の `fitsSystemWindows` / 背景の修正

**レビュー観点:**
- `ThemedActivity` と既存 Activity の継承関係
- ダークモード切替時のリソースリーク

### 5. 棋譜読み込み（軽）

**対象ファイル:**
- `ShogiGUI/WebKifuFile.cs` — 6shogi 対応、棋神アナリティクス Deep Link 対応

**レビュー観点:**
- HTML パース時の正規表現: ReDoS リスク
- `WebUtility.HtmlDecode` の使用箇所

### 6. 設定管理（軽）

**対象ファイル:**
- `ShogiGUI/Settings.cs` — XML シリアライズによるバックアップ/復元
- `ShogiGUI/AppSettings.cs` — `HideInternalEngine` 追加
- `ShogiGUI/EngineSettings.cs` — vast.ai 関連設定フィールド

**レビュー観点:**
- `ImportFromFile`: 信頼できないファイルからの XML デシリアライズの安全性
- `Settings.Save()` が複数スレッドから呼ばれる可能性

## レビュー対象外

- `Resources/drawable/*.xml` — デザイントークン定義のみ
- `Assets/KomoringHeights-*` — 外部バイナリ（レビュー不可）
- `docs/`, `scripts/`, `CLAUDE.md` — ドキュメント・ビルドスクリプト
- `.github/workflows/build.yml` — CI 設定の軽微な修正

## 既知の問題（レビューで指摘不要）

- ステータス文字列 (`"running"`, `"exited"` 等) が定数化されていない — 現時点では保留判断済み
- `Game.cs` 全体のコード品質 — デコンパイル由来のため今回のスコープ外
