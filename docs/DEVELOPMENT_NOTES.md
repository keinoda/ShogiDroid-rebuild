# 開発ノート — ShogiDroid Rebuild

デコンパイル → .NET 9 Android 移植で得られた知見をまとめる。

## 1. Xamarin → .NET 9 Android 移行の落とし穴

### 1.1 アセンブリの埋め込み
- **問題**: Fast Deployment が有効だとエミュレータでアセンブリが見つからず SIGABRT
- **対策**: `<EmbedAssembliesIntoApk>true</EmbedAssembliesIntoApk>` を csproj に追加

### 1.2 Activation Constructor
- **問題**: XML レイアウトからインフレートされるカスタム View / Preference に `(IntPtr, JniHandleOwnership)` コンストラクタがないと `NotSupportedException` でクラッシュ
- **対策**: すべてのカスタム View/Preference クラスに以下を追加:
```csharp
protected ClassName(IntPtr javaReference, JniHandleOwnership transfer)
    : base(javaReference, transfer) { }
```
- **該当クラス**: `SeekBarPreference`, `ListIntPreference`, `EvalGraph`, `ShogiBoard`

### 1.3 BinaryFormatter の廃止
- **問題**: .NET 9 では `BinaryFormatter` が完全に削除されている。`[Serializable]` + `BinaryFormatter` によるディープコピーが動かない
- **対策**:
  - 単純型（List<string> 等）: `System.Text.Json.JsonSerializer` で代替
  - ポリモーフィック型（USIOption 階層）: 各サブクラスに `Clone()` メソッドを実装
  - `System.Text.Json` はコンストラクタのパラメータ名とプロパティ名が一致しないとデシリアライズ失敗する

### 1.4 Release ビルドの AOT 問題
- **問題**: SDK バージョンの違いにより `Xamarin.AndroidX.Core` の GUID が AOT コンパイル済みバイナリと不一致
- **状況**: 未解決。当面 Debug ビルドを使用
- **今後**: NuGet パッケージのバージョンをピン留めして解決を試みる

## 2. Android 固有の問題

### 2.1 SELinux によるバイナリ実行拒否
- **問題**: Android 10+ (API 29+) の SELinux が `execute_no_trans` を拒否し、アプリデータ領域にコピーしたネイティブバイナリを `Process.Start()` で実行できない
- **対策**: `/system/bin/linker64`（または `/system/bin/linker`）をプロセスのファイル名に指定し、実際の実行ファイルを引数として渡す
```csharp
process_.StartInfo.FileName = "/system/bin/linker64";
process_.StartInfo.Arguments = actualBinaryPath;
```

### 2.2 chmod の DllImport 問題
- **問題**: 元のコードは `[DllImport("libchmod.so")]` で chmod を呼んでいたが、ネイティブライブラリが存在しない
- **対策**: `Java.Lang.Runtime.GetRuntime().Exec()` で `chmod` コマンドを実行
- **注意**: `EngineFile.Chmod(path, 484)` の 484 は10進数で、内部で8進変換される (484₁₀ = 744₈ = rwxr--r--)

### 2.3 MANAGE_EXTERNAL_STORAGE 権限
- **問題**: API 30+ では `WRITE_EXTERNAL_STORAGE` では不十分で、`/sdcard/ShogiDroid/` にアクセスできない
- **対策**: `AndroidManifest.xml` に `MANAGE_EXTERNAL_STORAGE` を追加し、`Settings.ActionManageAllFilesAccessPermission` で許可をリクエスト
- **テスト時**: `adb shell appops set <package> MANAGE_EXTERNAL_STORAGE allow` で即時付与

### 2.4 ContentProvider の競合
- **問題**: 元のアプリと同じ authority だと `INSTALL_FAILED_CONFLICTING_PROVIDER` でインストールできない
- **対策**: authority を `com.siganus.ShogiDroid.rebuild.provider` に変更

## 3. デコンパイラの不具合

### 3.1 変数名の衝突
- **問題**: デコンパイラが生成した変数名がバグを含む場合がある（例: `text` という変数が `workingdirectory` と混同される）
- **対策**: 元の処理の意図を理解してから修正する。単純な置換ではなく論理の整合性を確認

## 4. 将棋エンジン（USI プロトコル）

### 4.1 エンジンの探索
- `EngineFile.FindEngine()` がディレクトリ内から実行バイナリを探索
- ARM64 バイナリ名に `arm64` や `aarch64` が含まれる場合に優先選択される

### 4.2 エンジンオプションの扱い
- USI エンジンは `option name ... type ...` 形式でオプションを通知
- `USIOptions` が辞書管理し、各型（check/spin/combo/string/button/filename）のサブクラスに分岐
- EngineOptions 画面はエンジンを起動してオプションを取得した後に UI を構築する

## 5. ビルド環境の注意

### 5.1 複数の .NET SDK
- Homebrew 版 (`/opt/homebrew/bin/dotnet`, 9.0.104): android workload なし
- System 版 (`/usr/local/share/dotnet/dotnet`, 9.0.200): android workload あり
- `global.json` で SDK 9.0.200 を指定して正しい SDK を使うように制御

### 5.2 NuGet パッケージバージョン
- csproj で `Version="1.*"` のフローティングバージョンを使用
- SDK バージョンが変わるとパッケージの解決結果が変わり AOT GUID 不一致の原因になりうる

### 5.3 現在の標準ビルド手順
- 使用 SDK: `/usr/local/share/dotnet/dotnet` 9.0.200
- `DOTNET_ROOT=/usr/local/share/dotnet` を付けて実行する
- Homebrew 版 `dotnet` は android workload 非対応なので使わない
- `ShogiDroid/global.json` で SDK 9.0.200 に固定済み

#### Debug ビルド
```bash
DOTNET_ROOT=/usr/local/share/dotnet /usr/local/share/dotnet/dotnet build -c Debug
```
- `android-arm64` 単一アーキテクチャでビルド
- 出力 APK: `ShogiDroid/bin/Debug/net9.0-android/android-arm64/com.siganus.ShogiDroid.rebuild-Signed.apk`

#### Release ビルド
```bash
DOTNET_ROOT=/usr/local/share/dotnet /usr/local/share/dotnet/dotnet build -c Release
```
- AOT コンパイルが走るため Debug より時間がかかる
- 目安時間は約2分
- 出力 APK: `ShogiDroid/bin/Release/net9.0-android/android-arm64/com.siganus.ShogiDroid.rebuild-Signed.apk`

#### 実機インストール
```bash
adb install -r ShogiDroid/bin/Release/net9.0-android/android-arm64/com.siganus.ShogiDroid.rebuild-Signed.apk
adb shell am start -n com.siganus.ShogiDroid.rebuild/crc64721063ab64a94a2e.MainActivity
```

#### ワイヤレスデバッグ接続
```bash
adb connect <IPアドレス>:<ポート>
adb pair <IPアドレス>:<ペアリングポート> <ペアリングコード>
```
- 端末側で `設定 > 開発者向けオプション > ワイヤレスデバッグ` を開き、IPアドレスとポートを確認する
- ペアリングは初回のみ必要
