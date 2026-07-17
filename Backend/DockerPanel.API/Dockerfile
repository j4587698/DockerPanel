FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 复制项目文件
COPY ["DockerPanel.API.csproj", "./"]
RUN dotnet restore "DockerPanel.API.csproj"

# 复制源代码
COPY . .
RUN dotnet build "DockerPanel.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DockerPanel.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

# 安装必要的运行时依赖
RUN apt-get update && apt-get install -y \
    curl \
    && rm -rf /var/lib/apt/lists/*

# 从发布阶段复制文件
COPY --from=publish /app/publish .

# 设置环境变量
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=80
ENV ASPNETCORE_HTTPS_PORTS=443
ENV HTTP_PORT=80
ENV HTTPS_PORT=443
ENV ENABLE_HTTPS=true

# 健康检查
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost/health/live || exit 1

ENTRYPOINT ["dotnet", "DockerPanel.API.dll"]