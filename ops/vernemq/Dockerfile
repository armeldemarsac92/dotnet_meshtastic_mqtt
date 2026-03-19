# syntax=docker/dockerfile:1.7

FROM erlang:27-bookworm AS build
WORKDIR /src

ARG VERNEMQ_VERSION=2.1.1

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        build-essential \
        ca-certificates \
        curl \
        git \
        libsnappy-dev \
        libssl-dev \
        unzip && \
    rm -rf /var/lib/apt/lists/*

RUN curl -fsSL "https://github.com/vernemq/vernemq/archive/refs/tags/${VERNEMQ_VERSION}.tar.gz" \
    | tar -xz --strip-components=1

RUN make rel

FROM debian:bookworm-slim AS final
WORKDIR /vernemq

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        bash \
        ca-certificates \
        curl \
        iproute2 \
        jq \
        libsnappy1v5 \
        net-tools \
        openssl \
        procps && \
    rm -rf /var/lib/apt/lists/* && \
    addgroup --gid 10000 vernemq && \
    adduser --uid 10000 --system --ingroup vernemq --home /vernemq --disabled-password vernemq

COPY --from=build --chown=10000:10000 /src/_build/default/rel/vernemq /vernemq

RUN ln -s /vernemq/etc /etc/vernemq && \
    ln -s /vernemq/data /var/lib/vernemq && \
    ln -s /vernemq/log /var/log/vernemq

EXPOSE 1883 8080 8084 44053 4369 8888 9100 9101 9102 9103 9104 9105 9106 9107 9108 9109

VOLUME ["/vernemq/log", "/vernemq/data", "/vernemq/etc"]

USER vernemq

CMD ["/vernemq/bin/vernemq", "console"]
