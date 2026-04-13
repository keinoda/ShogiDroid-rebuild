FROM nvidia/cuda:13.0.0-cudnn-devel-ubuntu22.04

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

# === FukauraOu (TensorRT) ===
# ponkotsu モデル
RUN \
    wget https://huggingface.co/ngs436/ponkotsu/resolve/main/model.onnx -O /root/model.onnx && \
    wget https://huggingface.co/ngs436/ponkotsu/resolve/main/eval_options.txt -O /root/eval_options.txt

# TensorRT インストール（ビルドに必要なヘッダー・ライブラリ）
RUN \
    wget https://developer.download.nvidia.com/compute/tensorrt/10.13.3/local_installers/nv-tensorrt-local-repo-ubuntu2204-10.13.3-cuda-13.0_1.0-1_amd64.deb && \
    dpkg -i nv-tensorrt-local-repo-ubuntu2204-10.13.3-cuda-13.0_1.0-1_amd64.deb && \
    cp /var/nv-tensorrt-local-repo-ubuntu2204-10.13.3-cuda-13.0/*-keyring.gpg /usr/share/keyrings/ && \
    apt-get update && \
    apt-get install -y --no-install-recommends tensorrt && \
    rm nv-tensorrt-local-repo-ubuntu2204-10.13.3-cuda-13.0_1.0-1_amd64.deb && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

# FukauraOu ZEN3 のみビルド
RUN \
    echo "build : FukauraOu (ZEN3)" && \
    cd ~/YaneuraOu/source && \
    make YANEURAOU_EDITION=YANEURAOU_ENGINE_DEEP_TENSOR_RT_UBUNTU clean && \
    make -j$(nproc) YANEURAOU_EDITION=YANEURAOU_ENGINE_DEEP_TENSOR_RT_UBUNTU TARGET_CPU=ZEN3 TARGET=/usr/local/bin/FukauraOu-zen3 normal >& >(tee ~/FukauraOu-zen3.log)

RUN \
    mkdir -p /workspace/FukauraOu/book /workspace/FukauraOu/eval && \
    cp /usr/local/bin/FukauraOu-zen3 /workspace/FukauraOu/FukauraOu-zen3 && \
    cp /root/eval_options.txt /workspace/FukauraOu/eval/eval_options.txt && \
    cp /root/model.onnx /workspace/FukauraOu/eval/model.onnx
