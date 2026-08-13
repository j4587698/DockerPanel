# 前端构建阶段
FROM --platform=$BUILDPLATFORM node:20-alpine AS frontend-build

WORKDIR /src

COPY Frontend/package*.json ./Frontend/
WORKDIR /src/Frontend
RUN npm ci

COPY Frontend/ ./
RUN npm run build

# 后端发布阶段（Native AOT，alpine SDK 原生 musl 环境，无需交叉工具链）
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS backend-build
ARG TARGETARCH

# AOT 编译工具链（clang 编译器 + zlib 链接库 + llvm-objcopy 符号剥离 + binutils(GNU bfd 链接器)）
RUN apk add --no-cache clang zlib-dev llvm binutils

# 将 Docker 的 TARGETARCH (amd64, arm64等) 转换为 .NET 识别的架构标识 (x64, arm64等)
RUN if [ "$TARGETARCH" = "amd64" ]; then \
      echo "x64" > /tmp/arch; \
    else \
      echo "$TARGETARCH" > /tmp/arch; \
    fi

# 运行时层为 Alpine (musl)，publish 使用 linux-musl RID 以匹配
RUN echo "linux-musl-$(cat /tmp/arch)" > /tmp/rid

WORKDIR /src

COPY Backend/DockerPanel.API/DockerPanel.API.csproj ./Backend/DockerPanel.API/
RUN dotnet restore ./Backend/DockerPanel.API/DockerPanel.API.csproj -r $(cat /tmp/rid)

COPY Backend/DockerPanel.API/ ./Backend/DockerPanel.API/
COPY --from=frontend-build /src/Backend/DockerPanel.API/wwwroot ./Backend/DockerPanel.API/wwwroot

RUN dotnet publish ./Backend/DockerPanel.API/DockerPanel.API.csproj -c Release -o /app/publish -r $(cat /tmp/rid) /p:PublishAot=true /p:StripSymbols=true /p:DebugType=None /p:DebugSymbols=false

# 后端运行阶段（纯 Alpine + AOT 单文件可执行）
FROM alpine:3.21 AS backend-runtime

# CA 证书：alpine 默认不含，AOT 程序 HTTPS 出站（ACME/Docker API 等）需要
RUN apk add --no-cache ca-certificates

WORKDIR /app

COPY --from=backend-build /app/publish ./
RUN chmod +x ./DockerPanel.API

# 设置环境变量
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80
ENV HTTP_PORT=80
ENV HTTPS_PORT=443
ENV ENABLE_HTTPS=true

# 健康检查
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD wget -q -O /dev/null http://localhost/health/live || exit 1

# 暴露端口
EXPOSE 80

# 启动应用（AOT 原生可执行文件，无 dotnet 运行时依赖）
ENTRYPOINT ["./DockerPanel.API"]
