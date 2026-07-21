# 前端构建阶段
FROM --platform=$BUILDPLATFORM node:20-alpine AS frontend-build

WORKDIR /src

COPY Frontend/package*.json ./Frontend/
WORKDIR /src/Frontend
RUN npm ci

COPY Frontend/ ./
RUN npm run build

# 后端发布阶段
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
ARG TARGETARCH

WORKDIR /src

# 将 Docker 的 TARGETARCH (amd64, arm64等) 转换为 .NET 识别的架构标识 (x64, arm64等)
RUN if [ "$TARGETARCH" = "amd64" ]; then \
      echo "x64" > /tmp/arch; \
    else \
      echo "$TARGETARCH" > /tmp/arch; \
    fi

COPY Backend/DockerPanel.API/DockerPanel.API.csproj ./Backend/DockerPanel.API/
RUN dotnet restore ./Backend/DockerPanel.API/DockerPanel.API.csproj -a $(cat /tmp/arch)

COPY Backend/DockerPanel.API/ ./Backend/DockerPanel.API/
COPY --from=frontend-build /src/Backend/DockerPanel.API/wwwroot ./Backend/DockerPanel.API/wwwroot

RUN dotnet publish ./Backend/DockerPanel.API/DockerPanel.API.csproj -c Release -o /app/publish -a $(cat /tmp/arch) /p:UseAppHost=false

# 后端运行阶段
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS backend-runtime

WORKDIR /app

# 安装必要的运行时依赖
RUN apk add --no-cache \
    icu-libs \
    curl \
    && rm -rf /var/cache/apk/*

COPY --from=backend-build /app/publish ./

# 设置环境变量
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80
ENV HTTP_PORT=80
ENV HTTPS_PORT=443
ENV ENABLE_HTTPS=true

# 健康检查
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost/health/live || exit 1

# 暴露端口
EXPOSE 80

# 启动应用
ENTRYPOINT ["dotnet", "DockerPanel.API.dll"]