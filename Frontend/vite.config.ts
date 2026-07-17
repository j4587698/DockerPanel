import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'

const normalizePath = (id: string) => id.replace(/\\/g, '/')

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': resolve(__dirname, 'src')
    }
  },
  build: {
    outDir: '../Backend/DockerPanel.API/wwwroot',
    emptyOutDir: true,
    // Enable source maps for debugging in development builds
    sourcemap: false,
    // Minification settings
    minify: 'esbuild',
    // Target modern browsers for smaller bundles
    target: 'es2020',
    // Chunk size warning limit (500KB)
    chunkSizeWarningLimit: 800,
    rollupOptions: {
      input: {
        main: resolve(__dirname, 'index.html')
      },
      onwarn(warning, warn) {
        const id = normalizePath(String(warning.id ?? ''))
        const message = String(warning.message ?? '')
        if (
          warning.code === 'INVALID_ANNOTATION' &&
          id.includes('node_modules/@microsoft/signalr/') &&
          message.includes('/*#__PURE__*/')
        ) {
          return
        }

        warn(warning)
      },
      output: {
        // Manual chunk splitting for better caching
        manualChunks: (id) => {
          const normalizedId = normalizePath(id)
          // Vue 与 Element Plus 必须合并进同一个 chunk。
          // 二者存在跨模块的循环初始化依赖：若拆成 vue-vendor / element-vendor
          // 两个独立 chunk，浏览器按加载顺序求值时，被引用的 let/const 尚未
          // 初始化，会触发 "Cannot access 'X' before initialization" (TDZ)。
          // 合并后 Rollup 能在单一 chunk 内正确安排初始化顺序。
          if (normalizedId.includes('node_modules/vue/') ||
              normalizedId.includes('node_modules/vue-router/') ||
              normalizedId.includes('node_modules/pinia/') ||
              normalizedId.includes('node_modules/vue-i18n/') ||
              normalizedId.includes('node_modules/element-plus/') ||
              normalizedId.includes('node_modules/@element-plus/icons-vue/')) {
            return 'vue-vendor'
          }

          // 大型功能库按用途拆分
          if (normalizedId.includes('node_modules/@codemirror/') || normalizedId.includes('node_modules/vue-codemirror/')) {
            return 'codemirror'
          }
          if (normalizedId.includes('node_modules/xterm/') || normalizedId.includes('node_modules/@xterm/')) {
            return 'xterm'
          }
          if (normalizedId.includes('node_modules/zrender/')) {
            return 'zrender'
          }
          if (normalizedId.includes('node_modules/echarts/')) {
            if (normalizedId.includes('/lib/chart/')) return 'echarts-charts'
            if (normalizedId.includes('/lib/component/')) return 'echarts-components'
            if (normalizedId.includes('/lib/renderer/')) return 'echarts-renderers'
            if (normalizedId.includes('/lib/util/') || normalizedId.includes('/lib/data/') || normalizedId.includes('/lib/scale/')) return 'echarts-utils'
            return 'echarts-core'
          }
          if (normalizedId.includes('node_modules/@microsoft/signalr/')) {
            return 'signalr'
          }

          // Utility libraries
          if (normalizedId.includes('node_modules/axios/')) {
            return 'utils'
          }

          // i18n locale modules - 按需加载
          if (normalizedId.includes('/locales/modules/')) {
            const match = normalizedId.match(/\/locales\/modules\/([^/]+)\/([^/]+)\.ts/)
            if (match) {
              return `locale-${match[1]}-${match[2]}`
            }
          }

          // 业务代码按层拆分，组件交给路由/异步组件自然分包，避免首屏预加载所有功能组件
          if (normalizedId.includes('/src/api/')) return 'app-api'
          if (normalizedId.includes('/src/stores/')) return 'app-stores'
          if (normalizedId.includes('/src/services/')) return 'app-services'
          if (normalizedId.includes('/src/composables/')) return 'app-composables'
          if (normalizedId.includes('/src/utils/')) return 'app-utils'
        },
        // Naming patterns for better caching
        chunkFileNames: 'assets/js/[name]-[hash].js',
        entryFileNames: 'assets/js/[name]-[hash].js',
        assetFileNames: 'assets/[ext]/[name]-[hash].[ext]'
      }
    }
  },
  // Optimize dependencies
  optimizeDeps: {
    include: ['vue', 'vue-router', 'pinia', 'element-plus', 'axios'],
    exclude: ['@microsoft/signalr']
  },
  server: {
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:80',
        changeOrigin: true,
        configure: (proxy) => {
          proxy.on('error', (err) => {
            console.warn('proxy error', err)
          })
        }
      },
      // SignalR Hubs - 需要WebSocket支持
      '/dockerpanelHub': {
        target: 'http://127.0.0.1:80',
        changeOrigin: true,
        ws: true
      },
      '/sshTerminalHub': {
        target: 'http://127.0.0.1:80',
        changeOrigin: true,
        ws: true
      },
      '/containerTerminalHub': {
        target: 'http://127.0.0.1:80',
        changeOrigin: true,
        ws: true
      },
      // ACME挑战验证路径 - 代理到80端口供CA验证
      '/.well-known/acme-challenge': {
        target: 'http://127.0.0.1:80',
        changeOrigin: true,
        configure: (proxy) => {
          proxy.on('error', (err) => {
            console.warn('ACME challenge proxy error', err)
          })
        }
      }
    }
  }
})
