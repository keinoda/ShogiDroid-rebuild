FROM ubuntu:22.04

SHELL ["/bin/bash", "-c"]

# 依存パッケージ
RUN \
    apt-get update && \
    apt-get install -y --no-install-recommends \
    python3 \
    build-essential \
    clang \
    lld \
    libopenblas-dev \
    unzip \
    htop \
    wget \
    zip \
    p7zip-full \
    git \
    ca-certificates && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /workspace

RUN \
    git config --global http.sslCAInfo /etc/ssl/certs/ca-certificates.crt && \
    git config --global http.sslVerify true

# やねうら王（最新版）
RUN \
    echo "shallow clone : YaneuraOu master" && \
    git clone https://github.com/yaneurao/YaneuraOu.git ~/YaneuraOu --depth 1

# === Suisho11beta ===
RUN \
    wget https://storage.yaneu.com/yaneuraou/suisho11beta-suisho_concerto202512.7z

RUN \
    7z x suisho11beta-suisho_concerto202512.7z -o/root/ && \
    rm suisho11beta-suisho_concerto202512.7z

# ZEN3 のみビルド
RUN \
    echo "build : Suisho11beta (ZEN3)" && \
    cd ~/YaneuraOu/source && \
    make -j$(nproc) YANEURAOU_EDITION=YANEURAOU_ENGINE_NNUE_SFNNwoP1536 PYTHON=python3 TARGET_CPU=ZEN3 TARGET=/usr/local/bin/Suisho11beta-YaneuraOu-tournament-zen3 tournament >& >(tee ~/Suisho11beta-zen3.log)

RUN \
    mkdir -p /workspace/Suisho11beta/book /workspace/Suisho11beta/eval && \
    cp /usr/local/bin/Suisho11beta-YaneuraOu-tournament-zen3 /workspace/Suisho11beta/ && \
    cp /root/eval/nn.bin /workspace/Suisho11beta/eval/nn.bin

# === Suisho10 ===
RUN \
    wget https://storage.yaneu.com/yaneuraou/suisho10_250413.7z

RUN \
    7z x suisho10_250413.7z -o/root/ && \
    rm suisho10_250413.7z

RUN \
    echo "build : Suisho10 (ZEN3)" && \
    cd ~/YaneuraOu/source && \
    make YANEURAOU_EDITION=YANEURAOU_ENGINE_NNUE_SFNNwoP1536 clean && \
    make -j$(nproc) YANEURAOU_EDITION=YANEURAOU_ENGINE_NNUE_HALFKP_512X2_8_64 PYTHON=python3 TARGET_CPU=ZEN3 TARGET=/usr/local/bin/Suisho10-YaneuraOu-tournament-zen3 tournament >& >(tee ~/Suisho10-zen3.log)

RUN \
    mkdir -p /workspace/Suisho10/book /workspace/Suisho10/eval && \
    cp /usr/local/bin/Suisho10-YaneuraOu-tournament-zen3 /workspace/Suisho10/ && \
    cp /root/suisho10_250413/nn.bin /workspace/Suisho10/eval/nn.bin
