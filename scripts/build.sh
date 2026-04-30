#!/bin/bash

# DockerPanel 构建脚本
set -e

echo "🔨 开始构建 DockerPanel 应用..."

# 检查Docker是否运行
if ! docker info > /dev/null 2>&1; then
    echo "❌ Docker未运行，请先启动Docker"
    exit 1
fi

# 项目根目录
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../" && pwd)"
cd "$PROJECT_ROOT"

echo "📁 项目目录: $PROJECT_ROOT"

# 清理旧的镜像和容器
echo "🧹 清理旧的容器和镜像..."
docker-compose down --remove-orphans 2>/dev/null || true
docker system prune -f

# 构建生产版本
echo "🏗️ 构建生产版本..."
docker-compose build --no-cache

# 检查构建结果
if [ $? -eq 0 ]; then
    echo "✅ 构建成功！"
    echo ""
    echo "📋 可用命令:"
    echo "  启动生产环境: docker-compose up -d"
    echo "  查看日志:     docker-compose logs -f"
    echo "  停止服务:     docker-compose down"
    echo "  开发环境:     docker-compose -f docker-compose.dev.yml up"
    echo ""
    echo "🌐 默认访问地址: http://localhost"
else
    echo "❌ 构建失败！"
    exit 1
fi