# YaneuraOu NNUE SFNNwoP1536 ARM64 SIGSEGV 報告

## 概要

YaneuraOu NNUE SFNNwoP1536 ビルド（Suisho11beta同梱）が Android ARM64 環境で `go infinite` 直後に SIGSEGV でクラッシュする。同一端末・同一設定で HALFKP_512X2_8_64 ビルド（Suisho10同梱）は正常動作する。

## 環境

- 端末: Samsung Galaxy S24 Ultra (SM-S948Q), Android 16, ARM64
- RAM: 約 11GB
- カーネル: `6.12.30-android16-5`
- ホストアプリ: ShogiDroid (com.ngs43.shogidroid)
- エンジン起動方法: `/system/bin/linker64` ラッパー経由

## 再現手順

1. ShogiDroid でエンジンに Suisho11beta（YaneuraOu_NNUE_SFNNwoP1536_arm64-v8a）を選択
2. USI Hash 2048MB, Threads 4（デフォルト）で解析開始
3. `go infinite` 送信後 1〜2 秒で SIGSEGV 発生

## USI 通信ログ

```
>> usi
<< id name YaneuraOu_NNUE_SFNNwoP1536 NNUE 9.21git 64noSSE
<< usiok
>> setoption name USI_Hash value 2048
>> setoption name EnteringKingRule value CSARule27
>> isready
<< readyok                          ← 正常（nn.bin ロード・Hash 確保成功）
>> usinewgame
>> position startpos
>> go infinite
   (1.8秒後にプロセス死亡)
```

## クラッシュ情報

```
Fatal signal 11 (SIGSEGV), code 2 (SEGV_ACCERR), fault addr 0x73eecf4de8 (write)
Process uptime: 3s
pid: 12229, tid: 12247, name: linker64
```

- `SEGV_ACCERR` (書き込み権限エラー) は典型的なスタックオーバーフローのシグネチャ
- fault addr がスタック領域付近

## バックトレース

```
#00 pc 0x110450  Suisho11beta
#01 pc 0x11114c  Suisho11beta
#02 pc 0x11114c  Suisho11beta   ← 同一アドレスの再帰
#03 pc 0x11114c  Suisho11beta
    (54 frames total)
```

同一アドレス `0x11114c` が繰り返し出現しており、深い再帰によるスタック消費を示す。

## 比較: Suisho10 (HALFKP_512X2_8_64) は正常動作

| | Suisho11beta | Suisho10 |
|---|---|---|
| アーキテクチャ | SFNNwoP1536 | HALFKP_512X2_8_64 |
| バイナリ | YaneuraOu_NNUE_SFNNwoP1536_arm64-v8a | YaneuraOu_ENGINE_NNUE_HALFKP_512X2_8_64_arm64-v8a |
| nn.bin サイズ | 108MB | 128MB |
| 同一設定での動作 | SIGSEGV | 正常 |

Hash 2048MB / Threads 4 は両者共通。Hash 使用率はクラッシュ時点で 1.1% 程度であり、メモリ確保自体は成功している。OOM Kill ではない。

## 推定原因

SFNNwoP1536 アーキテクチャの NNUE 評価関数計算で、探索スレッドのデフォルトスタックサイズ（Android の pthread デフォルトは約 1MB）を超過している可能性がある。HALFKP_512X2_8_64 よりもアキュムレータ構造が大きいため、再帰的な探索でスタックフレームが積み上がりやすい。

## 想定される対策案

1. **探索スレッドのスタックサイズ拡大**: `pthread_create` 時の `stacksize` を増やす（例: 8MB）
2. **コンパイルオプション**: `-Wl,-z,stacksize=8388608` でデフォルトスタックを拡大
3. **再帰の深さ制限**: 探索の再帰深度に上限を設ける
4. **スタック使用量の削減**: SFNNwoP1536 の差分計算でスタック上の大きな配列をヒープに移す
