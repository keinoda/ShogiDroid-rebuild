FROM ubuntu:22.04

SHELL ["/bin/bash", "-c"]

# install dependencies
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
    socat \
    zip \
    p7zip-full \
    git \
    ca-certificates && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /workspace

#COPY patch_yaneuraou*.diff ~/
#COPY patch_dlshogi*.diff ~/

# Configure git to handle SSL certificates properly
RUN \
    git config --global http.sslCAInfo /etc/ssl/certs/ca-certificates.crt && \
    git config --global http.sslVerify true

# Clone YaneuraOu repository
RUN \
    echo "shallow clone : YaneuraOu master" && \
    git clone https://github.com/yaneurao/YaneuraOu.git ~/YaneuraOu --depth 1

# === Suisho11beta Section ===
# Download Suisho11beta model
RUN \
    wget https://storage.yaneu.com/yaneuraou/suisho11beta-suisho_concerto202512.7z

# Extract Suisho11beta model
RUN \
    7z x suisho11beta-suisho_concerto202512.7z -o/root/ && \
    rm suisho11beta-suisho_concerto202512.7z

#RUN if [ -e ~/patch_yaneuraou.diff ]; then \
#    echo "apply diff : YaneuraOu" &&\
#    cd ~/YaneuraOu &&\
#    git apply ~/patch_yaneuraou.diff;\
#    fi

#RUN if [ -e ~/patch_dlshogi.diff ]; then \
#    echo "apply diff : DeepLearningShogi" &&\
#    cd ~/DeepLearningShogi/ &&\
#    git apply ~/patch_dlshogi.diff;\
#    fi

# Build Suisho11beta for AVX2
RUN \
    echo "build : Suisho11beta (AVX2)" && \
    cd ~/YaneuraOu/source && \
    make -j$(nproc) YANEURAOU_EDITION=YANEURAOU_ENGINE_NNUE_SFNNwoP1536 PYTHON=python3 TARGET_CPU=AVX2 TARGET=/usr/local/bin/Suisho11beta-YaneuraOu-tournament-avx2 tournament >& >(tee ~/Suisho11beta-YaneuraOu-tournament-avx2.log)

# Build Suisho11beta for ZEN2
RUN \
    echo "build : Suisho11beta (ZEN2)" && \
    cd ~/YaneuraOu/source && \
    make YANEURAOU_EDITION=YANEURAOU_ENGINE_NNUE_SFNNwoP1536 clean && \
    make -j$(nproc) YANEURAOU_EDITION=YANEURAOU_ENGINE_NNUE_SFNNwoP1536 PYTHON=python3 TARGET_CPU=ZEN2 TARGET=/usr/local/bin/Suisho11beta-YaneuraOu-tournament-zen2 tournament >& >(tee ~/Suisho11beta-YaneuraOu-tournament-zen2.log)

# Build Suisho11beta for ZEN3
RUN \
    echo "build : Suisho11beta (ZEN3)" && \
    cd ~/YaneuraOu/source && \
    make YANEURAOU_EDITION=YANEURAOU_ENGINE_NNUE_SFNNwoP1536 clean && \
    make -j$(nproc) YANEURAOU_EDITION=YANEURAOU_ENGINE_NNUE_SFNNwoP1536 PYTHON=python3 TARGET_CPU=ZEN3 TARGET=/usr/local/bin/Suisho11beta-YaneuraOu-tournament-zen3 tournament >& >(tee ~/Suisho11beta-YaneuraOu-tournament-zen3.log)

# Create Suisho11beta directory structure
RUN \
    mkdir /workspace/Suisho11beta && \
    mkdir /workspace/Suisho11beta/book && \
    mkdir /workspace/Suisho11beta/eval

# Copy Suisho11beta binaries to workspace
RUN \
    cp /usr/local/bin/Suisho11beta-YaneuraOu-tournament-avx2 /workspace/Suisho11beta/Suisho11beta-YaneuraOu-tournament-avx2 && \
    cp /usr/local/bin/Suisho11beta-YaneuraOu-tournament-zen2 /workspace/Suisho11beta/Suisho11beta-YaneuraOu-tournament-zen2 && \
    cp /usr/local/bin/Suisho11beta-YaneuraOu-tournament-zen3 /workspace/Suisho11beta/Suisho11beta-YaneuraOu-tournament-zen3

# Copy Suisho11beta model weights (展開先ディレクトリを確認して配置)
RUN \
    find /root/ -name "nn.bin" -exec cp {} /workspace/Suisho11beta/eval/nn.bin \;
# === End of Suisho11beta Section ===
