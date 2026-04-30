#!/bin/bash

# DockerPanel 部署脚本
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

# 项目根目录
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../" && pwd)"
cd "$PROJECT_ROOT"

# 检查Docker是否运行
if ! docker info > /dev/null 2>&1; then
    echo "❌ Docker未运行，请先启动Docker"
    exit 1
fi

# 检查Docker Compose文件
if [ "$ENVIRONMENT" = "development" ]; then
    COMPOSE_FILE="docker-compose.dev.yml"
    echo "🛠️ 使用开发环境配置: $COMPOSE_FILE"

    # 检查开发环境配置文件是否存在
    if [ ! -f "$COMPOSE_FILE" ]; then
        echo "❌ 开发环境配置文件 $COMPOSE_FILE 不存在"
        exit 1
    fi

    # 构建开发环境
    echo "🏗️ 构建开发环境..."
    docker-compose -f "$COMPOSE_FILE" build

    # 启动开发环境
    echo "🚀 启动开发环境..."
    docker-compose -f "$COMPOSE_FILE" up -d

    echo ""
    echo "✅ 开发环境部署成功！"
    echo "🌐 前端地址: http://localhost:3000"
    echo "🔗 后端API: http://localhost:5000"

else
    COMPOSE_FILE="docker-compose.yml"
    echo "🏭 使用生产环境配置: $COMPOSE_FILE"
    export DOCKERPANEL_HTTP_HOST_PORT="$PORT"

    # 检查生产环境配置文件是否存在
    if [ ! -f "$COMPOSE_FILE" ]; then
        echo "❌ 生产环境配置文件 $COMPOSE_FILE 不存在"
        exit 1
    fi

    # 构建生产环境
    echo "🏗️ 构建生产环境..."
    docker-compose -f "$COMPOSE_FILE" build --no-cache

    # 停止旧容器
    echo "🛑 停止旧容器..."
    docker-compose -f "$COMPOSE_FILE" down 2>/dev/null || true

    # 启动生产环境
    echo "🚀 启动生产环境..."
    docker-compose -f "$COMPOSE_FILE" up -d

    # 等待服务启动
    echo "⏳ 等待服务启动..."
    sleep 10

    # 健康检查
    echo "🔍 执行健康检查..."
    for i in {1..30}; do
        if curl -f http://localhost:$PORT/health/live > /dev/null 2>&1; then
            echo "✅ 服务健康检查通过！"
            break
        fi

        if [ $i -eq 30 ]; then
            echo "⚠️ 健康检查超时，但服务可能仍在启动中"
        fi

        echo "⏳ 等待服务启动... ($i/30)"
        sleep 2
    done

    echo ""
    echo "✅ 生产环境部署成功！"
    echo "🌐 访问地址: http://localhost:$PORT"
    echo "📊 Swagger文档: http://localhost:$PORT/swagger"
    if [ "$PORT" != "80" ]; then
        echo "⚠️  ACME HTTP-01 证书申请需要公网 80 端口，请确保反向代理或端口映射转发到本服务。"
    fi
fi

echo ""
echo "📋 管理命令:"
echo "  查看日志: docker-compose -f $COMPOSE_FILE logs -f"
echo "  停止服务: docker-compose -f $COMPOSE_FILE down"
echo "  重启服务: docker-compose -f $COMPOSE_FILE restart"
echo "  查看状态: docker-compose -f $COMPOSE_FILE ps"

# 显示容器状态
echo ""
echo "📦 容器状态:"
docker-compose -f "$COMPOSE_FILE" ps