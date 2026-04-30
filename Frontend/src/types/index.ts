// ==================== 统一类型定义中心 ====================
// 这里集中导出所有类型定义，供整个项目使用

// 基础通用类型
export * from './common'
export * from './api'

// 容器相关类型
export * from './container'

// 镜像相关类型
export * from './image'

// 网络相关类型
export * from './network'

// 存储卷相关类型
export * from './volume'

// ACME证书相关类型
export * from './certificate'

// 代理相关类型
export * from './proxy'

// SSH相关类型
export * from './ssh'

// 节点相关类型
export * from './node'

// 系统相关类型
export * from './system'

// UI组件相关类型
export * from './ui'

// ==================== 类型导出完成 ====================
//
// 已创建的类型定义文件：
// - common.ts: 通用类型定义（API响应、分页、表单、图表等）
// - api.ts: API相关类型定义（HTTP请求、认证、监控等）
// - container.ts: 容器管理相关类型定义
// - image.ts: 镜像管理相关类型定义
// - network.ts: 网络管理相关类型定义
// - volume.ts: 存储卷管理相关类型定义
// - certificate.ts: ACME证书管理相关类型定义
// - proxy.ts: 代理管理相关类型定义
// - ssh.ts: SSH管理相关类型定义
// - node.ts: 节点管理相关类型定义
// - system.ts: 系统管理相关类型定义
// - ui.ts: UI组件相关类型定义
//
// 所有类型定义都遵循以下原则：
// 1. 使用TypeScript严格类型检查
// 2. 提供详细的接口定义和注释
// 3. 支持扩展性和可维护性
// 4. 遵循最佳实践和命名规范
// 5. 提供完整的类型覆盖范围
//
// 使用方式：
// import { Container, Image, Network } from '@/types'
// 或者
// import * as Types from '@/types'
// const container: Types.Container = { ... }