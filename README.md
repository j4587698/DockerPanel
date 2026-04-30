# DockerPanel - Docker容器管理面板

一个基于ASP.NET Core和Vue.js的现代化Docker容器管理Web应用。

## 🏗️ 架构概览

- **后端**: ASP.NET Core 10.0 Web API
- **前端**: Vue.js 3 + TypeScript + Element Plus
- **数据库**: TinyDb (嵌入式NoSQL数据库)
- **部署**: Docker + Docker Compose

## 🚀 快速开始

### 前置要求

- Docker 20.0+
- Docker Compose 2.0+
- Node.js 18+ (仅开发环境)
- .NET 10.0 SDK (仅开发环境)

### 生产环境部署

1. **克隆项目**
   ```bash
   git clone <repository-url>
   cd DockerPanel
   ```

2. **一键部署**
   ```bash
   chmod +x scripts/deploy.sh
   ./scripts/deploy.sh production
   ```

3. **访问应用**
   - 应用地址: http://localhost
   - API文档: http://localhost/swagger
   - ACME HTTP-01 证书申请要求域名公网 80 端口能访问本服务。

4. **首次登录**
   - 首次访问会进入 `/setup` 首次安装向导，请自行设置管理员用户名和强密码。
   - 也可以通过环境变量预置管理员：`DOCKERPANEL_ADMIN_USERNAME`、`DOCKERPANEL_ADMIN_PASSWORD`。
   - JWT 密钥默认生成并持久化到数据目录；生产环境建议设置 `DOCKERPANEL_JWT_SECRET` 或 `Auth__JwtSecret`。

### 开发环境设置

1. **启动开发环境**
   ```bash
   ./scripts/deploy.sh development
   ```

2. **访问开发服务**
   - 前端开发服务: http://localhost:3000
   - 后端API服务: http://localhost:5000

## 📁 项目结构

```
DockerPanel/
├── Backend/                    # 后端API
│   └── DockerPanel.API/
│       ├── Controllers/        # API控制器
│       ├── Models/            # 数据模型
│       ├── Services/          # 业务服务
│       ├── DTOs/              # 数据传输对象
│       └── Program.cs         # 应用程序入口
├── Frontend/                  # 前端应用
│   ├── src/
│   │   ├── components/        # Vue组件
│   │   ├── views/             # 页面视图
│   │   ├── router/            # 路由配置
│   │   ├── stores/            # 状态管理
│   │   └── api/               # API调用
│   ├── package.json           # 前端依赖
│   └── vite.config.ts         # 构建配置
├── scripts/                   # 部署脚本
│   ├── build.sh              # 构建脚本
│   └── deploy.sh             # 部署脚本
├── docker-compose.yml         # 生产环境编排
├── docker-compose.dev.yml     # 开发环境编排
├── Dockerfile                 # 生产环境镜像
└── Frontend/nginx.conf        # 独立前端镜像的 Nginx 配置
```

## 🛠️ 开发指南

### 本地开发

1. **后端开发**
   ```bash
   cd Backend/DockerPanel.API
   dotnet run
   ```

2. **前端开发**
   ```bash
   cd Frontend
   npm install
   npm run dev
   ```

### 构建命令

```bash
# 构建生产镜像
./scripts/build.sh

# 部署到生产环境
./scripts/deploy.sh production

# 部署到开发环境
./scripts/deploy.sh development
```

### GitHub Actions 镜像发布

仓库包含 Docker 镜像发布工作流，推送 `main`/`master` 分支、推送 `v*.*.*` 标签或手动触发时，会构建根目录 `Dockerfile` 并推送到：

- Docker Hub：`docker.io/<DOCKERHUB_USERNAME>/dockerpanel`
- GitHub Container Registry：`ghcr.io/<owner>/dockerpanel`

发布前需要在 GitHub 仓库 `Settings` → `Secrets and variables` → `Actions` 中配置：

| Secret | 说明 |
|--------|------|
| `DOCKERHUB_USERNAME` | Docker Hub 用户名或组织名 |
| `DOCKERHUB_TOKEN` | Docker Hub Access Token |

`ghcr.io` 使用仓库内置 `GITHUB_TOKEN` 推送，工作流已声明 `packages: write` 权限。

## 📊 功能特性

### 核心功能

- ✅ **容器管理**: 启动、停止、重启、删除容器
- ✅ **镜像管理**: 拉取、构建、删除镜像
- ✅ **网络管理**: 创建、管理Docker网络
- ✅ **卷管理**: 管理数据卷和绑定挂载
- ✅ **节点管理**: 多节点集群管理
- ✅ **系统监控**: 实时系统资源监控
- ✅ **日志查看**: 容器日志实时查看
- ✅ **终端访问**: Web终端连接容器

### 技术特性

- 🔐 **安全认证**: JWT令牌认证
- 🔄 **实时通信**: SignalR实时更新
- 📱 **响应式设计**: 支持移动端访问
- 🎨 **现代UI**: Element Plus组件库
- 📈 **性能监控**: 应用性能指标
- 🐳 **容器化**: 完全容器化部署

## 🔧 配置说明

### 环境变量

| 变量名 | 默认值 | 说明 |
|--------|--------|------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | 运行环境 |
| `ASPNETCORE_URLS` | `http://+:80` | 容器内服务监听地址 |
| `HTTP_PORT` | `80` | 容器内 HTTP 监听端口 |
| `ENABLE_HTTPS` | `false` | 容器内是否启用 Kestrel HTTPS；证书申请使用 HTTP-01 时应保持关闭或确保 80 可达 |
| `DOCKERPANEL_HTTP_HOST_PORT` | `80` | Docker Compose 映射到宿主机的 HTTP 端口；ACME HTTP-01 应使用 80 |
| `Logging__LogLevel__Default` | `Information` | 日志级别 |
| `DOCKERPANEL_ADMIN_USERNAME` | `admin` | 首次启动时自动初始化的管理员用户名（需同时设置密码） |
| `DOCKERPANEL_ADMIN_PASSWORD` | - | 首次启动时自动初始化的管理员密码 |
| `DOCKERPANEL_JWT_SECRET` | 自动生成 | JWT 签名密钥，生产环境建议设置不少于 32 个字符 |

### 数据持久化

- **数据库**: `/var/lib/dockerpanel/data`
- **日志文件**: `/var/lib/dockerpanel/logs`
- **配置文件**: `/var/lib/dockerpanel/config`

## 🐛 故障排除

### 常见问题

1. **Docker权限问题**
   ```bash
   sudo usermod -aG docker $USER
   ```

2. **端口被占用**
   ```bash
   # 检查端口占用
   lsof -i :80
   # 修改端口
   ./scripts/deploy.sh production 9090
   ```
   > 注意：如果生产环境需要 ACME HTTP-01 自动签发证书，公网 80 端口仍必须转发到 DockerPanel。

3. **容器启动失败**
   ```bash
   # 查看日志
   docker-compose logs -f
   ```

### 健康检查

```bash
# 检查服务状态
curl http://localhost/health/live

# 检查容器状态
docker-compose ps
```

## 🤝 贡献指南

1. Fork项目
2. 创建功能分支
3. 提交代码变更
4. 推送到分支
5. 创建Pull Request

## 📝 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情。

## 📞 支持

如有问题或建议，请通过以下方式联系：

- 📧 邮箱: support@dockerpanel.com
- 🐛 问题反馈: [GitHub Issues](https://github.com/your-repo/issues)
- 📖 文档: [项目Wiki](https://github.com/your-repo/wiki)

---

⭐ 如果这个项目对你有帮助，请给我们一个星标！