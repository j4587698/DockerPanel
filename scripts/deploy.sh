#!/bin/bash

# DockerPanel 部署脚本
# 用法:
#   本地仓库内:  ./scripts/deploy.sh [production|development] [port]
#   单文件自举:  curl -fsSL https://raw.githubusercontent.com/j4587698/DockerPanel/main/scripts/deploy.sh | bash -s -- [production|development] [port]
#
# 生产环境会从 Docker Hub 拉取 j4587698/dockerpanel:latest 镜像，无需本地构建。
set -e

echo "🚀 开始部署 DockerPanel 应用..."

# 检查参数
ENVIRONMENT=${1:-production}
PORT=${2:-80}

if [ "$ENVIRONMENT" != "production" ] && [ "$ENVIRONMENT" != "development" ]; then
    echo "❌ 环境参数错误，请使用: production 或 development"
    echo "用法: $0 [environment] [port]"
    echo "示例: $0 production 80"
    exit 1
fi

echo "📋 部署配置:"
echo "  环境: $ENVIRONMENT"
echo "  端口: $PORT"
echo ""

# 检查 Docker 是否运行
if ! docker info > /dev/null 2>&1; then
    echo "❌ Docker 未运行，请先启动 Docker"
    exit 1
fi

# 定位脚本所在目录；若通过 curl | bash 运行则不存在本地仓库，标记为自举模式
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" 2>/dev/null && pwd || true)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." 2>/dev/null && pwd || true)"
SELF_CONTAINED=0
if [ -z "$SCRIPT_DIR" ] || [ ! -f "$REPO_ROOT/docker-compose.yml" ]; then
    SELF_CONTAINED=1
fi

# 开发环境必须基于本地仓库（需要源码构建），不支持自举
if [ "$ENVIRONMENT" = "development" ]; then
    if [ "$SELF_CONTAINED" -eq 1 ]; then
        echo "❌ 开发环境需要本地仓库源码，请先 git clone 后再运行 ./scripts/deploy.sh development"
        exit 1
    fi
    COMPOSE_FILE="$REPO_ROOT/docker-compose.dev.yml"
    echo "🛠️ 使用开发环境配置: $COMPOSE_FILE"

    if [ ! -f "$COMPOSE_FILE" ]; then
        echo "❌ 开发环境配置文件 $COMPOSE_FILE 不存在"
        exit 1
    fi

    echo "🏗️ 构建开发环境..."
    docker compose -f "$COMPOSE_FILE" build

    echo "🚀 启动开发环境..."
    docker compose -f "$COMPOSE_FILE" up -d

    echo ""
    echo "✅ 开发环境部署成功！"
    echo "🌐 前端地址: http://localhost:3000"
    echo "🔗 后端API: http://localhost:5000"
    exit 0
fi

# ---------- 生产环境 ----------
# 自举模式下从 GitHub raw 下载 compose 文件到临时目录；否则使用本地文件
if [ "$SELF_CONTAINED" -eq 1 ]; then
    COMPOSE_DIR="$(mktemp -d)"
    COMPOSE_FILE="$COMPOSE_DIR/docker-compose.yml"
    RAW_BASE="https://raw.githubusercontent.com/j4587698/DockerPanel/main"
    echo "📥 自举模式：从 $RAW_BASE 下载生产 compose 文件..."
    if command -v curl > /dev/null 2>&1; then
        curl -fsSL "$RAW_BASE/docker-compose.hub.yml" -o "$COMPOSE_FILE"
    elif command -v wget > /dev/null 2>&1; then
        wget -qO "$COMPOSE_FILE" "$RAW_BASE/docker-compose.hub.yml"
    else
        echo "❌ 需要 curl 或 wget 来下载 compose 文件"
        rm -rf "$COMPOSE_DIR"
        exit 1
    fi
    echo "✅ 已下载到 $COMPOSE_FILE"
else
    COMPOSE_FILE="$REPO_ROOT/docker-compose.yml"
    echo "🏭 使用本地生产环境配置: $COMPOSE_FILE"
fi

export DOCKERPANEL_HTTP_HOST_PORT="$PORT"

# 生产镜像由 GitHub Actions 构建并推送到 Docker Hub，这里只拉取并运行
echo "📥 拉取最新镜像..."
docker compose -f "$COMPOSE_FILE" pull

echo "🛑 停止旧容器..."
docker compose -f "$COMPOSE_FILE" down 2>/dev/null || true

echo "🚀 启动生产环境..."
docker compose -f "$COMPOSE_FILE" up -d

# 自举模式清理临时下载文件
if [ "$SELF_CONTAINED" -eq 1 ]; then
    rm -rf "$COMPOSE_DIR"
fi

# 等待服务启动
echo "⏳ 等待服务启动..."
sleep 10

# 健康检查
echo "🔍 执行健康检查..."
for i in $(seq 1 30); do
    if curl -f http://localhost:$PORT/health/live > /dev/null 2>&1; then
        echo "✅ 服务健康检查通过！"
        break
    fi

    if [ "$i" -eq 30 ]; then
        echo "⚠️ 健康检查超时，但服务可能仍在启动中"
    fi

    echo "⏳ 等待服务启动... ($i/30)"
    sleep 2
done

echo ""
echo "✅ 生产环境部署成功！"
echo "🌐 访问地址: http://localhost:$PORT"
if [ "$PORT" != "80" ]; then
    echo "⚠️  ACME HTTP-01 证书申请需要公网 80 端口，请确保反向代理或端口映射转发到本服务。"
fi

echo ""
echo "📋 管理命令（请在已下载 compose 文件的目录执行，或重新运行本脚本）:"
echo "  查看日志: docker compose -f $COMPOSE_FILE logs -f"
echo "  停止服务: docker compose -f $COMPOSE_FILE down"
echo "  重启服务: docker compose -f $COMPOSE_FILE restart"
echo "  查看状态: docker compose -f $COMPOSE_FILE ps"

# 显示容器状态
echo ""
echo "📦 容器状态:"
docker compose -f "$COMPOSE_FILE" ps
