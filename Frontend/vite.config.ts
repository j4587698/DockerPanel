import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'

const normalizePath = (id: string) => id.replace(/\\/g, '/')

const elementChunkGroups: Record<string, string[]> = {
  'element-form': [
    'form', 'input', 'input-number', 'select', 'select-v2', 'checkbox', 'radio', 'switch',
    'slider', 'color-picker', 'color-picker-panel', 'date-picker', 'time-picker', 'time-select',
    'cascader', 'cascader-panel', 'autocomplete', 'upload', 'rate', 'transfer', 'tree-select',
    'input-tag', 'mention'
  ],
  'element-data': [
    'table', 'table-v2', 'pagination', 'tree', 'tree-v2', 'tag', 'badge', 'progress',
    'descriptions', 'statistic', 'timeline', 'steps'
  ],
  'element-overlay': [
    'dialog', 'drawer', 'tooltip', 'popper', 'popover', 'popconfirm', 'message', 'message-box',
    'notification', 'loading', 'overlay', 'focus-trap', 'teleport'
  ],
  'element-layout': [
    'affix', 'alert', 'anchor', 'avatar', 'backtop', 'breadcrumb', 'button', 'calendar', 'card',
    'carousel', 'check-tag', 'col', 'collapse', 'collapse-transition', 'collection', 'config-provider',
    'container', 'countdown', 'divider', 'empty', 'icon', 'image', 'image-viewer', 'infinite-scroll',
    'link', 'menu', 'page-header', 'result', 'roving-focus-group', 'row', 'scrollbar', 'segmented',
    'skeleton', 'slot', 'space', 'splitter', 'tabs', 'text', 'tour', 'virtual-list', 'watermark'
  ]
}

const getElementComponentChunk = (componentName: string) => {
  for (const [chunkName, components] of Object.entries(elementChunkGroups)) {
    if (components.includes(componentName)) return chunkName
  }

  return 'element-components'
}

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
    chunkSizeWarningLimit: 500,
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
          // Vue core and ecosystem
          if (normalizedId.includes('node_modules/vue/') || 
              normalizedId.includes('node_modules/vue-router/') || 
              normalizedId.includes('node_modules/pinia/') || 
              normalizedId.includes('node_modules/vue-i18n/')) {
            return 'vue-vendor'
          }

          // Element Plus 按组件拆分，避免单个 UI chunk 过大
          const elementComponentMatch = normalizedId.match(/node_modules\/element-plus\/es\/components\/([^/]+)/)
          if (elementComponentMatch) {
            return getElementComponentChunk(elementComponentMatch[1])
          }
          if (normalizedId.includes('node_modules/element-plus/')) {
            return 'element-core'
          }

          // Element Plus icons (large package)
          if (normalizedId.includes('node_modules/@element-plus/icons-vue/')) {
            return 'element-icons'
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
