<template>
  <span class="app-logo" :class="[`app-logo--${size}`, { 'app-logo--mark-only': !showText }]">
    <span class="app-logo__mark" aria-hidden="true">
      <svg viewBox="0 0 64 64" fill="none" class="app-logo__svg">
        <rect x="8" y="10" width="48" height="44" rx="14" fill="url(#app-logo-bg)" />
        <path
          d="M18 25.5C18 23.57 19.57 22 21.5 22H42.5C44.43 22 46 23.57 46 25.5V27.5C46 29.43 44.43 31 42.5 31H21.5C19.57 31 18 29.43 18 27.5V25.5Z"
          fill="white"
          fill-opacity="0.94"
        />
        <path
          d="M18 37.5C18 35.57 19.57 34 21.5 34H28.5C30.43 34 32 35.57 32 37.5V44.5C32 46.43 30.43 48 28.5 48H21.5C19.57 48 18 46.43 18 44.5V37.5Z"
          fill="white"
          fill-opacity="0.94"
        />
        <path
          d="M36 37.25C36 35.45 37.45 34 39.25 34H42.75C44.55 34 46 35.45 46 37.25V37.75C46 39.55 44.55 41 42.75 41H39.25C37.45 41 36 39.55 36 37.75V37.25Z"
          fill="white"
          fill-opacity="0.94"
        />
        <path
          d="M36 45.75C36 44.78 36.78 44 37.75 44H44.25C45.22 44 46 44.78 46 45.75V46.25C46 47.22 45.22 48 44.25 48H37.75C36.78 48 36 47.22 36 46.25V45.75Z"
          fill="white"
          fill-opacity="0.94"
        />
        <defs>
          <linearGradient id="app-logo-bg" x1="14" y1="13" x2="52" y2="54" gradientUnits="userSpaceOnUse">
            <stop stop-color="#38BDF8" />
            <stop offset="0.55" stop-color="#2563EB" />
            <stop offset="1" stop-color="#1E40AF" />
          </linearGradient>
        </defs>
      </svg>
    </span>
    <span v-if="showText" class="app-logo__text">{{ name }}</span>
  </span>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
  name?: string
  showText?: boolean
  size?: 'sm' | 'md' | 'lg'
}>(), {
  name: 'DockerPanel',
  showText: true,
  size: 'md'
})
</script>

<style scoped>
.app-logo {
  display: inline-flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
  color: var(--text-main);
  line-height: 1;
}

.app-logo__mark {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  border-radius: 12px;
  background: rgba(37, 99, 235, 0.12);
  box-shadow: 0 10px 24px rgba(37, 99, 235, 0.18);
}

.app-logo__svg {
  display: block;
  width: 100%;
  height: 100%;
}

.app-logo__text {
  min-width: 0;
  overflow: hidden;
  color: var(--text-main);
  font-weight: 800;
  letter-spacing: -0.02em;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.app-logo--sm .app-logo__mark {
  width: 32px;
  height: 32px;
  border-radius: 9px;
}

.app-logo--sm .app-logo__text {
  font-size: 16px;
}

.app-logo--md .app-logo__mark {
  width: 40px;
  height: 40px;
}

.app-logo--md .app-logo__text {
  font-size: 17px;
}

.app-logo--lg {
  gap: 12px;
}

.app-logo--lg .app-logo__mark {
  width: 46px;
  height: 46px;
  border-radius: 14px;
}

.app-logo--lg .app-logo__text {
  font-size: 18px;
}

.app-logo--mark-only {
  gap: 0;
}

html.dark .app-logo__mark {
  background: rgba(96, 165, 250, 0.16);
  box-shadow: 0 10px 28px rgba(37, 99, 235, 0.24);
}
</style>
