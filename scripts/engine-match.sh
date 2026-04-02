#!/bin/bash
# 将棋エンジン連続対局スクリプト（vast.ai / AWS スポットインスタンス用）
# Docker コンテナ内で実行する想定
#
# 使い方:
#   # vast.ai or AWS にSSH接続後:
#   bash engine-match.sh [対局数] [並列数] [持ち時間]
#
#   例: bash engine-match.sh 100 8 "byoyomi 1000"
#       → 100局、8並列、1秒秒読みで Suisho11beta vs Suisho10

set -e

GAMES=${1:-100}
PARALLEL=${2:-8}
TIME_SETTING=${3:-"byoyomi 1000"}

ENGINE1="/workspace/Suisho11beta/Suisho11beta-YaneuraOu-tournament-zen3"
ENGINE2="/workspace/Suisho10/Suisho10-YaneuraOu-tournament-zen3"
HASH=1024
THREADS=4

echo "=== 将棋エンジン連続対局 ==="
echo "Engine1: $ENGINE1"
echo "Engine2: $ENGINE2"
echo "対局数: $GAMES, 並列: $PARALLEL"
echo "持ち時間: $TIME_SETTING"
echo "Hash: ${HASH}MB, Threads: $THREADS"
echo ""

# Ayane のインストール
if [ ! -d /tmp/Ayane ]; then
    echo "Ayane をダウンロード中..."
    apt-get update -qq && apt-get install -y -qq python3 python3-pip git > /dev/null 2>&1 || true
    git clone --depth 1 https://github.com/yaneurao/Ayane.git /tmp/Ayane 2>/dev/null
fi

cd /tmp/Ayane/source

# 対局実行
python3 ayaneru-colosseum.py \
    --engine1 "$ENGINE1" \
    --engine2 "$ENGINE2" \
    --hash1 $HASH \
    --hash2 $HASH \
    --thread1 $THREADS \
    --thread2 $THREADS \
    --time "$TIME_SETTING" \
    --loop $GAMES \
    --flip_turn \
    --server_num $PARALLEL
