# 前端构建阶段
FROM node:20-alpine AS frontend-build

WORKDIR /src

COPY Frontend/package*.json ./Frontend/
WORKDIR /src/Frontend
RUN npm ci

COPY Frontend/ ./
RUN npm run build

# 后端发布阶段
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build

WORKDIR /src

COPY Backend/DockerPanel.API/DockerPanel.API.csproj ./Backend/DockerPanel.API/
RUN dotnet restore ./Backend/DockerPanel.API/DockerPanel.API.csproj

COPY Backend/DockerPanel.API/ ./Backend/DockerPanel.API/
COPY --from=frontend-build /src/Backend/DockerPanel.API/wwwroot ./Backend/DockerPanel.API/wwwroot

RUN dotnet publish ./Backend/DockerPanel.API/DockerPanel.API.csproj -c Release -o /app/publish /p:UseAppHost=false

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
# .NET 10 默认注入 ASPNETCORE_HTTP_PORTS=8080，优先级高于 ConfigureKestrel 的 ListenAnyIP，
# 会覆盖我们想绑定的 80/443。显式设回正确端口值即可。
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=80
ENV ASPNETCORE_HTTPS_PORTS=443
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