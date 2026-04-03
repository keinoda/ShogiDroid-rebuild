# クラウドエンジン接続設定ガイド

ShogiDroid からクラウド上の将棋エンジンに接続するための設定手順。
設定が消えた場合の復旧にも使える。

## 共通設定

### SSH 秘密鍵

アプリの「クラウド」画面 →「SSH秘密鍵パス」に設定する。

- ファイルピッカーで選択すると、アプリ内部ストレージにコピーされ `.pub` も自動コピーされる
- 手動入力の場合: `/sdcard/.ssh/id_ed25519` 等のパスを直接入力
- **公開鍵（.pub）が秘密鍵と同じディレクトリに存在すること**（AWS/GCP のインスタンスに公開鍵を注入するため）

### Docker イメージ

各プロバイダーの設定欄で指定する。

- 開発用: `keinoda/shogi:v9.21nnue`
- リリース用: `keinoda/shogi:AobaNNUE`

---

## vast.ai

### 必要な認証情報

| 項目 | 取得方法 | アプリ内の設定欄 |
|---|---|---|
| API キー | https://cloud.vast.ai/account → API Key | vast.ai セクション →「APIキー」 |

### 設定手順

1. https://cloud.vast.ai でアカウント作成、クレジットチャージ
2. Account → API Key をコピー
3. アプリの「クラウド」画面 → vast.ai セクションを展開
4. 「APIキー」に貼り付け
5. 検索条件を設定してオファー検索 → インスタンス起動 → エンジン選択

### 設定値の控え

| 項目 | 値 |
|---|---|
| APIキー | （vast.ai Account ページで確認） |
| Dockerイメージ | `keinoda/shogi:v9.21nnue` |
| 起動コマンド | `env >> /etc/environment; touch ~/.no_auto_tmux;` |
| 検索条件 | GPU: RTX 4090/5090, 最小CPUコア数: 32, 最大単価: $0.5/h |

### 備考

- インスタンスは SSH.NET 経由で直接コンテナに SSH 接続
- エンジンは `/workspace/` 以下に配置されている
- Threads/Hash は接続時にインスタンススペックから自動設定される

---

## AWS スポットインスタンス

### 必要な認証情報

| 項目 | 取得方法 | アプリ内の設定欄 |
|---|---|---|
| アクセスキー | AWS Console → IAM → ユーザー → セキュリティ認証情報 → アクセスキーを作成 | AWS セクション →「アクセスキー」 |
| シークレットキー | 上記と同時に発行される（一度しか表示されない） | AWS セクション →「シークレットキー」 |

### 必要な IAM ポリシー

ユーザーに以下のポリシーをアタッチ:
- `AmazonEC2FullAccess`（または必要最小限の EC2 権限）

### 設定手順

1. AWS Console で IAM ユーザー作成、アクセスキー発行
2. アプリの「クラウド」画面 → AWS セクションを展開
3. アクセスキー、シークレットキーを入力
4. インスタンスタイプをドロップダウンで選択（Spot 価格が AZ 別に表示される）
5. 希望の AZ の「起動」ボタンを押す
6. 起動完了後、インスタンス一覧の「接続」でエンジン選択

### 設定値の控え

| 項目 | 値 |
|---|---|
| リージョン | `eu-north-1`（コード内固定） |
| インスタンスタイプ | `c7a.metal-48xl`（192vCPU 384GB）推奨、ドロップダウンで変更可 |
| Dockerイメージ | `keinoda/shogi:v9.21nnue` |

### インフラの自動作成

アプリが初回起動時に自動作成するリソース:
- **キーペア** `shogidroid-key`: SSH 公開鍵をインポート（既存のキーペアがあればそれを使用）
- **セキュリティグループ** `shogidroid-ssh`: SSH (22) のインバウンド許可

これらはリージョンに残り続けるが、料金はかからない。

### 備考

- Spot インスタンスは `InstanceInitiatedShutdownBehavior=terminate` で起動される
  - インスタンス内から `shutdown` するとインスタンス自体が終了（課金停止）
- 自動シャットダウン: 起動後 60 分
- root SSH は ForceCommand で Docker コンテナに自動転送される
- アクセスキーを紛失した場合: AWS Console で新しいアクセスキーを発行すればよい

---

## GCP Spot VM

### 必要な認証情報

| 項目 | 取得方法 | アプリ内の設定欄 |
|---|---|---|
| サービスアカウントキー (JSON) | GCP Console → IAM と管理 → サービスアカウント → 鍵を追加 → JSON | GCP セクション →「サービスアカウントキー」 |

### GCP プロジェクト側の事前準備

1. **プロジェクト作成**: https://console.cloud.google.com で作成、請求先アカウント紐付け
2. **API 有効化**（2つ必要）:
   - Compute Engine API: https://console.cloud.google.com/apis/api/compute.googleapis.com
   - Cloud Billing API（価格表示用）: https://console.cloud.google.com/apis/api/cloudbilling.googleapis.com
3. **サービスアカウント作成**:
   - IAM と管理 → サービスアカウント → 作成
   - ロール: **Compute 管理者** (`roles/compute.admin`)
   - 鍵タブ → 鍵を追加 → JSON でダウンロード
4. **クォータ確認**（大型インスタンスを使う場合）:
   - IAM と管理 → 割り当て で以下を検索・引き上げ:
     - `CPUS_ALL_REGIONS` → 360 以上
     - `CPUS_PER_VM_FAMILY` (対象リージョン) → 360 以上

### 設定手順

1. ダウンロードした JSON キーを端末に配置:
   ```bash
   adb push sa-key.json /sdcard/ShogiDroid/gcp/sa-key.json
   ```
2. アプリの「クラウド」画面 → GCP セクションを展開
3. 「サービスアカウントキー」に `/sdcard/ShogiDroid/gcp/sa-key.json` を入力
4. ゾーン、マシンタイプをドロップダウンで選択（Spot 価格が表示される）
5. 「Spot VM を起動」→ 確認ダイアログで「起動」
6. 起動完了後、インスタンス一覧の「接続」でエンジン選択

### 設定値の控え

| 項目 | 値 |
|---|---|
| プロジェクトID | `shogidriod` |
| サービスアカウント | `shogidroid-shogidroid-iam-gser@shogidriod.iam.gserviceaccount.com` |
| キーファイル配置先 | `/sdcard/ShogiDroid/gcp/sa-key.json` |
| ゾーン | `us-central1-a`（C3D 利用可能） |
| マシンタイプ | `c3d-highcpu-180`（180vCPU 354GB、Spot ~$1.02/h）推奨 |
| Dockerイメージ | `keinoda/shogi:v9.21nnue` |

### インフラの自動作成

- **ファイアウォールルール** `shogidroid-allow-ssh`: SSH (22) のインバウンド許可、タグ `shogidroid` 対象

### 備考

- Spot VM は `instanceTerminationAction=STOP` で起動される
  - GCP に Preempt されると停止状態になり再開可能（ただしディスク課金は継続）
- 自動シャットダウン: 起動後 60 分
- ディスク: pd-ssd 10GB（autoDelete=true、VM 削除時に一緒に消える）
- **使い終わったら「削除」推奨**（「停止」だとディスク課金が継続する）
- サービスアカウントキーを紛失した場合: GCP Console で同じサービスアカウントに新しい鍵を追加発行すればよい
- C4D インスタンスは hyperdisk-balanced 強制（IOPS 課金 ~$0.25/h）のため非対応

---

## 費用の目安（Spot 価格、2026年4月時点）

| プロバイダー | インスタンス | vCPU | Spot 価格 |
|---|---|---|---|
| vast.ai | RTX 4090 + 32cores | 32 | ~$0.2-0.4/h |
| AWS | c7a.metal-48xl | 192 | ~$1.0-2.3/h（リージョンによる） |
| AWS | c7a.8xlarge | 32 | ~$0.3-0.4/h |
| GCP | c3d-highcpu-30 | 30 | ~$0.17/h |
| GCP | c3d-highcpu-90 | 90 | ~$0.51/h |
| GCP | c3d-highcpu-180 | 180 | ~$1.02/h |
| GCP | c3d-highcpu-360 | 360 | ~$2.04/h |

GCP C3D は vCPU あたり単価が最も安い。
