# DockerPanel

DockerPanel 是一个基于 **ASP.NET Core 10** 和 **Vue 3** 构建的高性能现代化 Docker 可视化管理与反向代理网关面板。

后端通过 Docker Socket 全面接管并调度 Docker 容器生命周期，前端通过静态资源一体化嵌入后端容器中，支持**单镜像开箱即用、一键部署与无缝平滑自升级**。

当前版本：`0.11.1`

Docker 镜像发布于 Docker Hub：[`j4587698/dockerpanel`](https://hub.docker.com/r/j4587698/dockerpanel)，由 GitHub Actions 自动化构建与发布多架构镜像。

---

## ✨ 核心特性

- **自身一键平滑升级**：内置基于 Sidecar 容器与 Docker Registry Manifest 摘要比对的自升级引擎，数据卷与运行参数 100% 无损继承，一键完成更新重启与健康探活。
- **容器与 Compose 深度管理**：
  - 容器创建、停止、启动、重启、高级重建（Recreate，保留原有卷、网络与端口参数）、批量更新与回滚；
  - 实时日志流推送（SignalR）、Web 终端（xterm.js）、容器内文件浏览与管理、容器 CPU/内存/网络/磁盘实时监控；
  - 可选宿主机监控：通过只读挂载 `/proc`、`/sys` 额外展示宿主机 CPU/内存/负载/磁盘 I/O/网络，未挂载时面板明确提示而不是伪造数据；
  - Docker Compose 项目在线编辑、实时构建与编排部署。
- **镜像与仓库集成**：
  - 支持 Docker Hub、私有 Harbor、阿里云/腾讯云加速器等多种 Registry 认证；
  - 实时镜像拉取/推送进度按层聚合广播；
  - 基于镜像 Digest 的新版本自动检测与通知。
- **YARP 反向代理与网关**：
  - 基于微软高性能 YARP 反向代理核心，支持动态路由规则、负载均衡、SSL/TLS 卸载与路由健康检查。
- **ACME 自动化证书生命周期管理**：
  - 支持 Let's Encrypt / ZeroSSL 自动化证书申请（HTTP-01 与 DNS-01 校验）；
  - 证书自动续期、SNI 动态证书加载与通配符证书关联。
- **多节点调度与 SSH 管理**：
  - 支持管理本地 Docker 引擎与多远程节点；
  - 内置 SSH 密钥管理与基于 Web 的 Linux 服务器终端。
- **用户权限与安全审计**：
  - 多用户与细粒度角色权限管理、初始化向导与安全密码策略；
  - 全流程操作审计日志追踪。

---

## 🛠️ 技术栈

- **后端**：ASP.NET Core 10 (C# 14 / Native AOT JSON 源码生成)、SignalR 实时通信、YARP 反向代理、TinyDb 轻量持久化、AcmeForge ACME 客户端、Docker.DotNet。
- **前端**：Vue 3 (Composition API / `<script setup>`)、TypeScript、Vite、Pinia、Vue Router、Element Plus、ECharts、xterm.js、CodeMirror 6。
- **部署**：Docker / Docker Compose 单容器一体化部署。

---

## 🚀 快速部署

DockerPanel 以**单一镜像**发布，无需额外配置外部数据库。

镜像地址：`j4587698/dockerpanel`

可用 tag：
- `latest`：最新稳定发布版本
- `0.11` / `0.11.1`：对应指定语义化版本

---

### 方式一：使用 docker run 部署（推荐）

#### 1. 标准双端口模式（推荐，支持 HTTP 访问、ACME 证书申请及 HTTPS 反代）

```bash
docker run -d \
  --name dockerpanel \
  --restart unless-stopped \
  -p 80:80 \
  -p 443:443 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e HTTP_PORT=80 \
  -e HTTPS_PORT=443 \
  -e ENABLE_HTTPS=true \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v /opt/dockerpanel/data:/app/Data \
  -v /opt/dockerpanel/logs:/app/Logs \
  j4587698/dockerpanel:latest
```

#### 2. 自定义端口模式（如服务器 80/443 已被其他服务占用）

```bash
docker run -d \
  --name dockerpanel \
  --restart unless-stopped \
  -p 8080:80 \
  -p 8443:443 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e HTTP_PORT=80 \
  -e HTTPS_PORT=443 \
  -e ENABLE_HTTPS=true \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v /opt/dockerpanel/data:/app/Data \
  -v /opt/dockerpanel/logs:/app/Logs \
  j4587698/dockerpanel:latest
```

> **📌 端口与网络说明**：
> - **80 端口**：面板 HTTP 访问入口，同时用于 **Let's Encrypt / ACME HTTP-01 证书申请校验**。若要在面板中自动申请 SSL 证书，请确保宿主机公网 80 端口能直接访问或通过上游网关转发至本容器。
> - **443 端口**：面板 HTTPS 及 YARP 反代 SNI 动态证书访问入口。

---

### 方式二：使用 Docker Compose 部署

仓库提供 `docker-compose.yml`（或 `docker-compose.hub.yml`），直接引用官方镜像：

```yaml
services:
  dockerpanel:
    image: j4587698/dockerpanel:latest
    container_name: dockerpanel
    ports:
      - "${DOCKERPANEL_HTTP_HOST_PORT:-80}:80"
      - "${DOCKERPANEL_HTTPS_HOST_PORT:-443}:443"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - HTTP_PORT=80
      - HTTPS_PORT=443
      - ENABLE_HTTPS=true
      - Logging__LogLevel__Default=Information
      - Logging__LogLevel__Microsoft.AspNetCore=Warning
      # 可选：首次启动预置管理员用户名与密码（未设置时首次访问进入 /setup 向导创建）
      # - DOCKERPANEL_ADMIN_USERNAME=admin
      # - DOCKERPANEL_ADMIN_PASSWORD=YourStrongPassword!
      # 可选：显式指定 JWT 密钥（未配置时自动生成并保存到持久化数据目录）
      # - DOCKERPANEL_JWT_SECRET=your-custom-jwt-secret-key-at-least-32-chars
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - dockerpanel_data:/app/Data
      - dockerpanel_logs:/app/Logs
    networks:
      - dockerpanel-network
    extra_hosts:
      - "host.docker.internal:host-gateway"
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "wget", "-q", "-O", "/dev/null", "http://localhost/health/live"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s

volumes:
  dockerpanel_data:
    driver: local
  dockerpanel_logs:
    driver: local

networks:
  dockerpanel-network:
    driver: bridge
    name: dockerpanel-network
```

启动与管理：

```bash
# 启动
docker compose up -d

# 查看状态
docker compose ps

# 查看实时日志
docker compose logs -f

# 停止
docker compose down
```

如需自定义宿主机端口，可通过环境变量启动：
```bash
DOCKERPANEL_HTTP_HOST_PORT=8080 DOCKERPANEL_HTTPS_HOST_PORT=8443 docker compose up -d
```

---

### 可选：启用宿主机监控（只读挂载）

默认情况下 DockerPanel **只采集 Docker 容器指标**（每个容器的 CPU / 内存 / 网络，在"容器"页查看）。未启用宿主机监控时，"概览"页会回退显示**容器聚合**指标并明确标注口径，绝不把容器数据当成宿主机数据；启用宿主机监控后首页自动切换为宿主机指标（CPU / 内存 / 负载 / 磁盘 / 网络），并停止逐容器采样。

如果希望在同一个面板里看到 **宿主机本身** 的 CPU、内存、负载、磁盘 I/O 与网络，可额外添加以下只读挂载（无需 `--privileged`、无需 `--network=host`、无需 `--pid=host`）：

```yaml
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      # 宿主机监控（全部只读）
      - /proc:/host/proc:ro
      - /sys:/host/sys:ro
      - /etc/os-release:/host/etc/os-release:ro
      # 可选：仅用于读取宿主机各挂载点的磁盘容量
      - /:/host/root:ro
```

`docker run` 等价写法：

```bash
docker run -d \
  --name dockerpanel \
  --restart unless-stopped \
  -p 80:80 -p 443:443 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v /proc:/host/proc:ro \
  -v /sys:/host/sys:ro \
  -v /etc/os-release:/host/etc/os-release:ro \
  -v /:/host/root:ro \
  -v /opt/dockerpanel/data:/app/Data \
  j4587698/dockerpanel:latest
```

挂载路径也可用环境变量覆盖：`HOST_PROC`、`HOST_SYS`、`HOST_ROOT`、`HOST_OS_RELEASE`。

**能力与限制：**

| 指标 | 需要的挂载 | 说明 |
|------|-----------|------|
| CPU / 内存 / 负载 / 运行时长 | `/proc` | 来自 `/host/proc/stat`、`meminfo`、`loadavg`、`uptime` |
| 磁盘读写速率 | `/proc` | 来自 `/host/proc/diskstats`（与容量无关） |
| 网络速率与累计流量 | `/proc` | 来自 `/host/proc/1/net/dev`；若该文件不可读则标记为不可用 |
| 磁盘容量 / 各挂载点使用率 | `/proc` + `/` | 需要 `/host/root`，否则只显示不可用提示 |
| 发行版名称/版本 | `/etc/os-release` | 也可从 `/host/root/etc/os-release` 回退读取 |

- 网络指标刻意读取 `/host/proc/1/net/dev` 而不是 `/host/proc/net/dev`：后者在容器内会解析到**读取进程自己的网络命名空间**，会得出"把容器网卡当宿主机网卡"的错误结论。因此本功能**不依赖** `--network=host`。
- 未挂载时面板不会报错，只是在"概览"页显示"宿主机监控未启用"及上面的挂载示例。
- 首次采集的 CPU/网络/磁盘速率为 0，需要第二次采样（约一个推送周期）后才有速率值。
- `/proc` 与 `/` 的只读挂载会暴露宿主机进程列表与文件系统结构给容器内进程。DockerPanel 只读取上述固定文件，不会上传任何数据；但如对隔离性要求很高，请保持默认（不挂载），仅使用容器监控。

**平台支持（重要）：**

宿主机指标的数据源是 `/proc`、`/sys`，因此**只支持 Linux 内核**：

| 部署方式 | 宿主机指标 | 说明 |
|---|---|---|
| Linux + Docker / Compose | ✅ 可用 | 显示真实宿主机 CPU/内存/磁盘/网络 |
| Docker Desktop（Windows / macOS） | ⚠️ 显示的是 Linux 虚拟机 | 容器实际运行在 WSL2 / LinuxKit 虚拟机中，挂载到的是**该虚拟机**的 `/proc`，不是 Windows/macOS 宿主本身；CPU/内存/磁盘均为虚拟机口径 |
| Windows 上直接运行可执行文件 | ❌ 不可用 | 没有 `/proc`，面板回退显示容器聚合指标并标注口径，不会展示伪造的宿主机数据 |
| 远程节点的资源采集 | ✅ 仅 Linux 节点 | 通过 SSH 执行 `top` / `free` / `df` / `uname`，Windows 远程节点不会被正确采集 |

容器指标（"容器"页的每个容器 CPU / 内存、容器监控弹窗）不受此限制：它走 Docker Engine API，Linux 容器与 Windows 容器均可采集。

---

### 方式三：一键部署脚本（单文件自举）

无需手动 clone 仓库，直接在终端执行脚本即可全自动部署：

```bash
# 默认部署 (HTTP: 80, HTTPS: 443)
curl -fsSL https://raw.githubusercontent.com/j4587698/DockerPanel/main/scripts/deploy.sh | bash -s -- production 80 443

# 自定义端口部署 (如 HTTP 8080, HTTPS 8443)
curl -fsSL https://raw.githubusercontent.com/j4587698/DockerPanel/main/scripts/deploy.sh | bash -s -- production 8080 8443
```

---

## 🔄 面板版本升级

DockerPanel 支持多种平滑升级方式：

### 1. Web 界面一键自升级（最便捷）
登录面板后，进入 **【系统设置】 $\to$ 【系统镜像与升级】**：
- 系统会自动基于镜像 Digest 对比远程仓库是否有新版本构建；
- 点击 **「一键升级系统」**，面板将自动预拉取新镜像，并通过短暂的 Sidecar Helper 容器平滑完成停旧建新与健康探活，**所有挂载数据与配置 100% 完整保留**。

### 2. Docker Compose 终端升级
```bash
docker compose pull
docker compose up -d
```

### 3. Docker CLI 终端升级
```bash
docker pull j4587698/dockerpanel:latest
docker stop dockerpanel && docker rm dockerpanel
# 重新执行您的 docker run 命令启动即可（数据在挂载目录中不受影响）
```

---

## ⚙️ 环境变量配置参考

| 变量名 | 默认值 | 说明 |
|--------|--------|------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | 运行环境（`Production` / `Development`） |
| `HTTP_PORT` | `80` | 容器内 HTTP 监听端口 |
| `HTTPS_PORT` | `443` | 容器内 HTTPS 监听端口 |
| `ENABLE_HTTPS` | `true` | 是否启用 Kestrel HTTPS 监听与 SNI 证书动态加载 |
| `DOCKERPANEL_HTTP_HOST_PORT` | `80` | Compose 部署映射到宿主机的 HTTP 端口 |
| `DOCKERPANEL_HTTPS_HOST_PORT` | `443` | Compose 部署映射到宿主机的 HTTPS 端口 |
| `DOCKERPANEL_ADMIN_USERNAME` | - | 首次启动预置管理员用户名 |
| `DOCKERPANEL_ADMIN_PASSWORD` | - | 首次启动预置管理员密码 |
| `DOCKERPANEL_JWT_SECRET` | 自动持久化 | JWT 签名密钥（建议显式设置 32 位以上强密钥） |
| `HOST_PROC` | `/host/proc` | 宿主机 `/proc` 只读挂载路径（未挂载时不启用宿主机监控） |
| `HOST_SYS` | `/host/sys` | 宿主机 `/sys` 只读挂载路径 |
| `HOST_ROOT` | `/host/root` | 宿主机根目录只读挂载路径（仅用于磁盘容量） |
| `HOST_OS_RELEASE` | `/host/etc/os-release` | 宿主机 `os-release` 只读挂载路径 |
| `TinyDb__Path` | `Data/DockerPanel.db` | TinyDb 数据库持久化路径 |

---

## 💻 本地开发指南

如需参与 DockerPanel 的开发或本地构建：

```bash
# 1. 克隆代码库
git clone https://github.com/j4587698/DockerPanel.git
cd DockerPanel

# 2. 启动后端 (ASP.NET Core 10)
cd Backend/DockerPanel.API
dotnet run

# 3. 启动前端 (Vue 3 + Vite) - 另开终端
cd Frontend
npm install
npm run dev
```

- 前端开发服务：`http://localhost:3000`
- 后端开发接口：`http://localhost:5000`
- Swagger 文档仅在 Development 环境开启：`http://localhost:5000/swagger`

前端类型检查与打包：
```bash
cd Frontend
npm run build
```

---

## 🚢 自动化镜像构建与发布

本项目通过 GitHub Actions 自动化工作流（`.github/workflows/docker-publish.yml`）进行多架构镜像构建与发布：
1. 每次合并到 `main` 分支时，自动提取 `Backend/DockerPanel.API/DockerPanel.API.csproj` 中的 `<Version>`；
2. 自动打包前端并与 .NET 11 后端一同编译为单一轻量化镜像；
3. 推送至 Docker Hub（`j4587698/dockerpanel:latest`、`j4587698/dockerpanel:0.11.1`、`j4587698/dockerpanel:0.11`）及 GHCR。

---

## 📄 开源许可证

本项目基于 [MIT License](LICENSE) 开源。
