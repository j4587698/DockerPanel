import { createApp } from 'vue'
import { createPinia } from 'pinia'
import ElementPlus from 'element-plus'
import zhCn from 'element-plus/dist/locale/zh-cn.mjs'
import 'element-plus/dist/index.css'
import 'element-plus/theme-chalk/dark/css-vars.css'
import './style.css'
import App from './App.vue'
import router from './router'
import i18n, { preloadCommonModules } from './i18n'
import DialogMaximizePlugin from './plugins/dialogMaximize'

async function bootstrap() {
  const app = createApp(App)

  app.use(createPinia())
  app.use(router)
  app.use(i18n)
  app.use(ElementPlus, { locale: zhCn })
  app.use(DialogMaximizePlugin)

  // 预加载常用翻译模块
  await preloadCommonModules()

  app.mount('#app')
}

bootstrap()

