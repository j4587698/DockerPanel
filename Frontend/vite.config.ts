import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'

const normalizePath = (id: string) => id.replace(/\\/g, '/')

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
    sourcemap: false,
    minify: 'esbuild',
    target: 'es2020',
    chunkSizeWarningLimit: 1200,
    rolldownOptions: {
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
        codeSplitting: {
          groups: [
            {
              name: 'vue-vendor',
              test: (id) => {
                const nid = normalizePath(id)
                return nid.includes('node_modules/vue/') ||
                  nid.includes('node_modules/vue-router/') ||
                  nid.includes('node_modules/pinia/') ||
                  nid.includes('node_modules/vue-i18n/') ||
                  nid.includes('node_modules/element-plus/') ||
                  nid.includes('node_modules/@element-plus/icons-vue/')
              },
              priority: 30
            },
            {
              name: 'codemirror',
              test: (id) => {
                const nid = normalizePath(id)
                return nid.includes('node_modules/@codemirror/') || nid.includes('node_modules/vue-codemirror/')
              },
              priority: 25
            },
            {
              name: 'xterm',
              test: (id) => {
                const nid = normalizePath(id)
                return nid.includes('node_modules/xterm/') || nid.includes('node_modules/@xterm/')
              },
              priority: 25
            },
            {
              name: 'zrender',
              test: (id) => normalizePath(id).includes('node_modules/zrender/'),
              priority: 25
            },
            {
              name: (id) => {
                const nid = normalizePath(id)
                if (nid.includes('/lib/chart/')) return 'echarts-charts'
                if (nid.includes('/lib/component/')) return 'echarts-components'
                if (nid.includes('/lib/renderer/')) return 'echarts-renderers'
                if (nid.includes('/lib/util/') || nid.includes('/lib/data/') || nid.includes('/lib/scale/')) return 'echarts-utils'
                return 'echarts-core'
              },
              test: (id) => normalizePath(id).includes('node_modules/echarts/'),
              priority: 20
            },
            {
              name: 'signalr',
              test: (id) => normalizePath(id).includes('node_modules/@microsoft/signalr/'),
              priority: 20
            },
            {
              name: 'utils',
              test: (id) => normalizePath(id).includes('node_modules/axios/'),
              priority: 20
            },
            {
              name: (id) => {
                const nid = normalizePath(id)
                const match = nid.match(/\/locales\/modules\/([^/]+)\/([^/]+)\.ts/)
                return match ? `locale-${match[1]}-${match[2]}` : null
              },
              test: (id) => normalizePath(id).includes('/locales/modules/'),
              priority: 10
            },
            {
              name: 'app-api',
              test: (id) => normalizePath(id).includes('/src/api/'),
              priority: 5
            },
            {
              name: 'app-stores',
              test: (id) => normalizePath(id).includes('/src/stores/'),
              priority: 5
            },
            {
              name: 'app-services',
              test: (id) => normalizePath(id).includes('/src/services/'),
              priority: 5
            },
            {
              name: 'app-composables',
              test: (id) => normalizePath(id).includes('/src/composables/'),
              priority: 5
            },
            {
              name: 'app-utils',
              test: (id) => normalizePath(id).includes('/src/utils/'),
              priority: 5
            }
          ]
        },
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
