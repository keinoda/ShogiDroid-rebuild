FROM ubuntu:22.04

SHELL ["/bin/bash", "-c"]

# install dependencies
RUN \
    apt-get update && \
    apt-get install -y --no-install-recommends \
    python3 \
    python3-pip \
    build-essential \
    clang \
    lld \
    libopenblas-dev \
    unzip \
    htop \
    wget \
    socat \
    zip \
    p7zip-full \
    git \
    ca-certificates && \
    rm -rf /var/lib/apt/lists/*

# Install gdown for Google Drive downloads
RUN pip3 install --no-cache-dir gdown

WORKDIR /workspace

# Configure git
RUN \
    git config --global http.sslCAInfo /etc/ssl/certs/ca-certificates.crt && \
    git config --global http.sslVerify true

# Clone YaneuraOu repository (公式リポジトリを使用)
RUN \
    echo "shallow clone : YaneuraOu master" && \
    git clone https://github.com/yaneurao/YaneuraOu.git ~/YaneuraOu --depth 1

# Generate HALFKP_768X2_16_64 architecture header
RUN \
    echo "generate : HALFKP_768X2_16_64 architecture header" && \
    cd ~/YaneuraOu/source && \
    python3 eval/nnue/architectures/nnue_arch_gen.py YANEURAOU_ENGINE_NNUE_HALFKP_768X2_16_64 eval/nnue/architectures

# Download AobaNNUE from Google Drive and extract nn.bin
RUN \
    echo "download : AobaNNUE eval files from Google Drive" && \
    mkdir -p /workspace/AobaNNUE/eval /workspace/AobaNNUE/book && \
    gdown "https://drive.google.com/uc?id=1iogV4SOtRlv6VltLxHGqNoMCy6r1Dei3" -O /tmp/AobaNNUE.zip && \
    unzip -o /tmp/AobaNNUE.zip "AobaNNUE/eval/nn.bin" -d /tmp/aoba && \
    cp /tmp/aoba/AobaNNUE/eval/nn.bin /workspace/AobaNNUE/eval/nn.bin && \
    rm -rf /tmp/AobaNNUE.zip /tmp/aoba

# Create eval_options.txt (FV_SCALE=40 for AobaNNUE)
RUN echo "FV_SCALE 40" > /workspace/AobaNNUE/eval/eval_options.txt

# Build AobaNNUE for AVX2
RUN \
    echo "build : AobaNNUE (AVX2)" && \
    cd ~/YaneuraOu/source && \
    make -j$(nproc) YANEURAOU_EDITION=YANEURAOU_ENGINE_NNUE_HALFKP_768X2_16_64 PYTHON=python3 TARGET_CPU=AVX2 TARGET=/usr/local/bin/AobaNNUE-YaneuraOu-tournament-avx2 tournament >& >(tee ~/AobaNNUE-YaneuraOu-tournament-avx2.log)

# Build AobaNNUE for ZEN2
RUN \
    echo "build : AobaNNUE (ZEN2)" && \
    cd ~/YaneuraOu/source && \
    make YANEURAOU_EDITION=YANEURAOU_ENGINE_NNUE_HALFKP_768X2_16_64 clean && \
    make -j$(nproc) YANEURAOU_EDITION=YANEURAOU_ENGINE_NNUE_HALFKP_768X2_16_64 PYTHON=python3 TARGET_CPU=ZEN2 TARGET=/usr/local/bin/AobaNNUE-YaneuraOu-tournament-zen2 tournament >& >(tee ~/AobaNNUE-YaneuraOu-tournament-zen2.log)

# Build AobaNNUE for ZEN3
RUN \
    echo "build : AobaNNUE (ZEN3)" && \
    cd ~/YaneuraOu/source && \
    make YANEURAOU_EDITION=YANEURAOU_ENGINE_NNUE_HALFKP_768X2_16_64 clean && \
    make -j$(nproc) YANEURAOU_EDITION=YANEURAOU_ENGINE_NNUE_HALFKP_768X2_16_64 PYTHON=python3 TARGET_CPU=ZEN3 TARGET=/usr/local/bin/AobaNNUE-YaneuraOu-tournament-zen3 tournament >& >(tee ~/AobaNNUE-YaneuraOu-tournament-zen3.log)

# Copy binaries to workspace
RUN \
    cp /usr/local/bin/AobaNNUE-YaneuraOu-tournament-avx2 /workspace/AobaNNUE/AobaNNUE-YaneuraOu-tournament-avx2 && \
    cp /usr/local/bin/AobaNNUE-YaneuraOu-tournament-zen2 /workspace/AobaNNUE/AobaNNUE-YaneuraOu-tournament-zen2 && \
    cp /usr/local/bin/AobaNNUE-YaneuraOu-tournament-zen3 /workspace/AobaNNUE/AobaNNUE-YaneuraOu-tournament-zen3
