// ==================== 状态管理中心 ====================
// 统一导出所有store，提供清晰的状态管理结构

// 应用核心状态
export { useAppStore } from './app'
export { useAuthStore } from './auth'

// Docker相关状态管理
export { useContainersStore } from './containers'
export { useImagesStore } from './images'
export { useVolumesStore } from './volumes'
export { useNetworksStore } from './networks'

// 证书管理
export { useCertificateStore } from './certificate'

// 系统和节点管理
export { useSystemStore } from './system'
export { useNodesStore } from './nodes'

// WebSocket连接管理
export { useWebSocketStore } from './websocket'

// ==================== Store使用指南 ====================
//
// 已识别的问题和解决方案：
// 1. 重复的证书store：certificate.ts 和 certificates.ts
//    - 解决方案：使用 certificate.ts（功能更完整，包含ACME和SSL证书管理）
//    - certificates.ts 将被弃用并迁移到 certificate.ts
//
// 2. 状态管理结构优化：
//    - 每个store都遵循统一的模式：状态、计算属性、方法
//    - 使用TypeScript严格类型检查
//    - 统一的错误处理和消息提示
//
// 3. 未来优化方向：
//    - 添加状态持久化
//    - 实现store间的数据共享
//    - 优化API调用缓存策略
//
// ==================== 迁移说明 ====================
//
// 如果你的代码正在使用 certificates store，请按以下方式迁移：
//
// 旧代码：
// import { useCertificatesStore } from '@/stores/certificates'
//
// 新代码：
// import { useCertificateStore } from '@/stores'
//
// 主要差异：
// - useCertificatesStore -> useCertificateStore
// - certificates -> sslCertificates
// - stats -> certificateStats
// - API方法名称保持一致，但功能更强大