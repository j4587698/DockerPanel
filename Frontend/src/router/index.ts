import { createRouter, createWebHistory } from 'vue-router'
import Layout from '../layout/Layout.vue'
import { loadLocaleForRoute } from '../i18n'
import { useAuthStore } from '../stores/auth'

// 所有页面懒加载 - 提升首屏加载性能
const Dashboard = () => import('../views/Dashboard.vue')
const Login = () => import('../views/Login.vue')

// 路由懒加载 - 提升首屏加载性能
const Containers = () => import('../views/Containers.vue')
const ContainerDetail = () => import('../views/ContainerDetail.vue')
const Images = () => import('../views/Images.vue')
const Networks = () => import('../views/Networks.vue')
const Volumes = () => import('../views/Volumes.vue')
const NodeList = () => import('../views/nodes/NodeList.vue')
const CertificateList = () => import('../views/certificates/CertificateList.vue')
const ComposeList = () => import('../views/compose/ComposeList.vue')
const RegistryList = () => import('../views/registries/RegistryList.vue')
const RealtimeLogs = () => import('../views/RealtimeLogs.vue')
const AuditLogs = () => import('../views/AuditLogs.vue')
const YarpManagement = () => import('../views/YarpManagement.vue')
const SshManagement = () => import('../views/SshManagement.vue')
const Settings = () => import('../views/Settings.vue')
const UserManagement = () => import('../views/UserManagement.vue')

const routes = [
  {
    path: '/setup',
    name: 'Setup',
    component: Login,
    meta: { public: true, setup: true, title: '首次安装' }
  },
  {
    path: '/login',
    name: 'Login',
    component: Login,
    meta: { public: true, title: '登录' }
  },
  {
    path: '/',
    component: Layout,
    redirect: '/dashboard',
    children: [
      {
        path: 'dashboard',
        name: 'Dashboard',
        component: Dashboard,
        meta: { title: '仪表盘', icon: 'Monitor' }
      },
      {
        path: 'containers',
        name: 'Containers',
        component: Containers,
        meta: { title: '容器管理', icon: 'Box' }
      },
      {
        path: 'containers/:id',
        name: 'ContainerDetail',
        component: ContainerDetail,
        meta: { title: '容器详情', icon: 'Box' }
      },
      {
        path: 'images',
        name: 'Images',
        component: Images,
        meta: { title: '镜像管理', icon: 'Picture' }
      },
      {
        path: 'networks',
        name: 'Networks',
        component: Networks,
        meta: { title: '网络管理', icon: 'Connection' }
      },
      {
        path: 'volumes',
        name: 'Volumes',
        component: Volumes,
        meta: { title: '存储卷管理', icon: 'Folder' }
      },
      {
        path: 'yarp',
        name: 'YarpManagement',
        component: YarpManagement,
        meta: { title: 'YARP网关', icon: 'Link', roles: ['Admin', 'Operator'] }
      },
      {
        path: 'nodes',
        name: 'Nodes',
        component: NodeList,
        meta: { title: '节点管理', icon: 'Platform', roles: ['Admin'] }
      },
      {
        path: 'compose',
        name: 'Compose',
        component: ComposeList,
        meta: { title: 'Compose管理', icon: 'Grid', roles: ['Admin', 'Operator'] }
      },
      {
        path: 'registries',
        name: 'Registries',
        component: RegistryList,
        meta: { title: '镜像仓库', icon: 'Connection', roles: ['Admin'] }
      },
      {
        path: 'certificates',
        name: 'Certificates',
        component: CertificateList,
        meta: { title: '证书管理', icon: 'Document', roles: ['Admin', 'Operator'] }
      },
      {
        path: 'realtime-logs',
        name: 'RealtimeLogs',
        component: RealtimeLogs,
        meta: { title: '实时日志', icon: 'Document' }
      },
      {
        path: 'audit',
        name: 'AuditLogs',
        component: AuditLogs,
        meta: { title: '操作审计', icon: 'Document', roles: ['Admin'] }
      },
      {
        path: 'ssh',
        name: 'SshManagement',
        component: SshManagement,
        meta: { title: 'SSH管理', icon: 'Monitor', roles: ['Admin'] }
      },
      {
        path: 'settings',
        name: 'Settings',
        component: Settings,
        meta: { title: '系统设置', icon: 'Setting', roles: ['Admin'] }
      },
      {
        path: 'users',
        name: 'Users',
        component: UserManagement,
        meta: { title: '用户管理', icon: 'User', roles: ['Admin'] }
      }
    ]
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

// 路由名称到模块名称的映射
const routeToModuleMap: Record<string, string> = {
  'Dashboard': 'dashboard',
  'Containers': 'containers',
  'ContainerDetail': 'container-detail',
  'Images': 'images',
  'Networks': 'networks',
  'Volumes': 'volumes',
  'YarpManagement': 'yarp',
  'Nodes': 'nodes',
  'Compose': 'compose',
  'Registries': 'registries',
  'Certificates': 'certificates',
  'RealtimeLogs': 'dashboard',
  'AuditLogs': 'audit',
  'SshManagement': 'ssh',
  'Settings': 'settings',
  'Users': 'users'
}

// 路由导航守卫 - 按需加载翻译
router.beforeEach(async (to, _from, next) => {
  const routeName = to.name as string
  const moduleName = routeToModuleMap[routeName]
  
  if (moduleName) {
    await loadLocaleForRoute(moduleName)
  }

  const authStore = useAuthStore()
  const isPublicRoute = Boolean(to.meta.public)
  const redirectTarget = typeof to.query.redirect === 'string' ? to.query.redirect : '/dashboard'

  if (!authStore.isAuthenticated) {
    try {
      const status = authStore.status ?? await authStore.loadStatus()
      if (status.requiresSetup) {
        if (to.name !== 'Setup') {
          next({ path: '/setup', query: { redirect: to.name === 'Login' ? redirectTarget : to.fullPath } })
          return
        }

        next()
        return
      }

      if (to.name === 'Setup') {
        next({ path: '/login', query: to.query })
        return
      }
    } catch {
      // 认证状态接口异常时继续走常规登录流程，登录页会显示错误提示。
    }
  }

  if (isPublicRoute) {
    if ((to.name === 'Login' || to.name === 'Setup') && authStore.token) {
      const valid = await authStore.loadCurrentUser()
      if (valid) {
        next(redirectTarget)
        return
      }
    }

    next()
    return
  }

  if (!authStore.isAuthenticated) {
    authStore.clearAuth()
    next({ path: '/login', query: { redirect: to.fullPath } })
    return
  }

  if (!authStore.initialized || !authStore.user) {
    const valid = await authStore.loadCurrentUser()
    if (!valid) {
      next({ path: '/login', query: { redirect: to.fullPath } })
      return
    }
  }

  const allowedRoles = to.meta.roles as string[] | undefined
  if (allowedRoles?.length && !allowedRoles.includes(authStore.effectiveRole)) {
    next('/dashboard')
    return
  }
  
  next()
})

export default router
