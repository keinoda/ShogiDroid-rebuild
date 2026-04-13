# 自律ループ用タスクリスト

Claude が自律ループで暇なときに、ビルド不要・単独で完結できる作業の一覧。
優先度順に並べてある。完了したらチェックを入れる。

---

## コード品質チェック・修正

### デッドコード除去
- [x] `ShogiLib/MoveEvalExtension.cs` の `GetMoveValuel()` — タイポで誰からも呼ばれていない未使用メソッド。削除済み
- [x] `Activities/Util.cs` の `IsGooglePhotosUri()` — private かつクラス内でも未使用。削除済み
- [x] `Resources/values/ids.xml` の `adView` ID — 広告機能は除外済み。削除済み

### 空 catch ブロックにログ追加
以下の空 catch は障害調査を困難にしている。最低限 `AppDebug.Log` を入れる：
- [x] `ShogiGUI/Settings.cs:38, 73` — 設定の Load/Save 失敗が完全に無視されている（最重要）
- [x] `ShogiGUI.Engine/EngineOptions.cs:56, 70` — エンジンオプションの Save/Load 失敗が無視
- [x] `ShogiGUI/LocalFile.cs:37` — フォルダ作成エラーの無視
- [x] `ShogiGUI.Engine/USIEngine.cs:490` — ファイルヘッダ確認失敗の無視
- [ ] `Activities/MainActivity.cs:2291, 2384` — intent 起動失敗の無視（progressDialog.Dismiss の空 catch は Android パターンとして意図的）
- [x] `ShogiGUI.Engine/VastAiManager.cs:258, 327, 428` — ラベル削除・残高取得・SSH到達確認の失敗
- [x] `ShogiGUI.Engine/GcpSpotManager.cs:536, 681` — SSH 接続確認・価格パース失敗の無視

### タイポ修正（識別子）
リソース ID とコード側の両方を同時に直す必要がある（片方だけだとビルドエラー）：
- [ ] `main_manu_lsist_view` → `main_menu_list_view`（ids.xml + MainActivity.cs）
- [ ] `notaiton_list_view` → `notation_list_view`（ids.xml + MainActivity.cs 複数箇所）
- [ ] `notaiton_branch_list_view` → `notation_branch_list_view`（ids.xml）
- [ ] `notaton_text` → `notation_text`（ids.xml + MainActivity.cs）
- [ ] `RequestWriteExternalStoragePermittion()` → `RequestWriteExternalStoragePermission()`（MainActivity.cs、呼び出し元も修正）

### 未使用 using の整理
- [x] `ShogiLib/Csa.cs` の `using ShogiDroid;` — `Resource.String.*` で使用中。削除不可（確認済み）
- [x] `ShogiGUI.Engine/PvInfo.cs` の `using ShogiDroid;` — `Resource.String.*` で使用中。削除不可（確認済み）

---

## ドキュメント整合性

- [ ] `CLAUDE.md` のプロジェクト構成から `ShogiLib.Kifu/` を削除（確認済み: 実ディレクトリは存在しない。CLAUDE.md 修正はユーザー確認後に実施）

---

## 静的解析・構造的チェック

- [ ] `EngineSettingsWrapperActivity.cs` のハードコード日本語文字列を `strings.xml` リソースに移動すべきか検討・リスト化
- [ ] エンジンオプション画面の不安定性について、`EngineWakeup` と `InitializeEnd` の二重ハンドラー登録の影響を調査・レポート作成
- [ ] `MainActivity.cs`（3,302行）の責務を分析し、将来の分割案をコメントとしてまとめる
- [ ] `CloudActivity.cs`（2,557行）の責務を分析し、同上

---

## 不要ファイル・リソースの確認

- [ ] `Shinden3-YO9-Android/` ディレクトリが空であることを確認し、削除提案
- [ ] `Resources/drawable/icon.png` が実際に不使用か確認（`shogidroid_icon.png` が正式アイコン）
- [ ] Classic 版と通常版でリソースの差分が意図通りか確認

---

## テスト基盤

- [ ] `ShogiLib/` の純粋ロジック（駒の移動判定、棋譜パース等）に対するユニットテストプロジェクトの雛形を設計（作成はユーザー確認後）
- [ ] 棋譜パーサー（KIF, CSA, SFEN, KI2）のテストケース案をリストアップ

---

## 注意事項

- 上記はすべて **ビルド・実機テスト不要** で Claude 単独で実施可能な作業
- リソース ID のリネームなど、ビルド確認が望ましいものはユーザーに確認してから適用する
- ファイル削除は CLAUDE.md の禁止事項に従い、必ず事前確認を取る
