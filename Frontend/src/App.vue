<script setup lang="ts">
import { computed, watch } from 'vue'
import { RouterView, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useSettingsStore } from '@/stores/settings'
import { updateDocumentTitle } from '@/utils/branding'

const route = useRoute()
const { t, locale } = useI18n()
const settingsStore = useSettingsStore()
const currentPageTitle = computed(() => {
  if (typeof route.meta.titleKey === 'string') {
    return t(route.meta.titleKey)
  }

  return typeof route.meta.title === 'string' ? route.meta.title : ''
})

watch(
  [currentPageTitle, () => settingsStore.systemName, locale],
  ([pageTitle, systemName]) => updateDocumentTitle(pageTitle, systemName),
  { immediate: true }
)
</script>

<template>
  <RouterView />
</template>

<style>
#app {
  height: 100vh;
  width: 100vw;
}

* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

body {
  font-family: 'Inter', 'Helvetica Neue', Helvetica, 'PingFang SC', 'Hiragino Sans GB', 'Microsoft YaHei', Arial, sans-serif;
  background: var(--bg-app);
  color: var(--text-main);
}
</style>
