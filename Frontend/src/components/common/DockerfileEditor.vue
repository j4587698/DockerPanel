<template>
  <div class="dockerfile-editor" :class="{ 'is-dark': isDark }">
    <codemirror
      v-model="content"
      :extensions="extensions"
      :style="{ height: height + 'px' }"
      @change="handleChange"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { Codemirror } from 'vue-codemirror'
import { lineNumbers, highlightActiveLine, highlightActiveLineGutter } from '@codemirror/view'
import { history, historyKeymap } from '@codemirror/commands'
import { defaultKeymap } from '@codemirror/commands'
import { keymap } from '@codemirror/view'
import { syntaxHighlighting, defaultHighlightStyle, HighlightStyle } from '@codemirror/language'
import { tags as t } from '@lezer/highlight'

const props = defineProps<{
  modelValue: string
  height?: number
  readonly?: boolean
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
}>()

const isDark = ref(document.documentElement.classList.contains('dark'))

// 监听主题变化
const observer = new MutationObserver(() => {
  isDark.value = document.documentElement.classList.contains('dark')
})
observer.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] })

const content = ref(props.modelValue || '')

watch(() => props.modelValue, (val) => {
  content.value = val || ''
})

const handleChange = (value: string) => {
  emit('update:modelValue', value)
}

// Dockerfile 关键字
const dockerfileKeywords = [
  'FROM', 'RUN', 'CMD', 'LABEL', 'MAINTAINER', 'EXPOSE', 'ENV', 'ADD',
  'COPY', 'ENTRYPOINT', 'VOLUME', 'USER', 'WORKDIR', 'ARG', 'ONBUILD',
  'STOPSIGNAL', 'HEALTHCHECK', 'SHELL'
]

// 自定义 Dockerfile 高亮样式
const dockerfileHighlightStyle = HighlightStyle.define([
  { tag: t.keyword, color: '#3b82f6', fontWeight: 'bold' },
  { tag: t.string, color: '#22c55e' },
  { tag: t.comment, color: '#6b7280', fontStyle: 'italic' },
  { tag: t.number, color: '#f59e0b' },
  { tag: t.operator, color: '#8b5cf6' },
])

const extensions = computed(() => [
  lineNumbers(),
  highlightActiveLine(),
  highlightActiveLineGutter(),
  history(),
  keymap.of([...defaultKeymap, ...historyKeymap]),
  syntaxHighlighting(dockerfileHighlightStyle),
  syntaxHighlighting(defaultHighlightStyle),
])
</script>

<style scoped>
.dockerfile-editor {
  border: 1px solid var(--border-color);
  border-radius: 8px;
  overflow: hidden;
  background: var(--bg-surface);
}

.dockerfile-editor.is-dark {
  background: #1e1e1e;
}

.dockerfile-editor :deep(.cm-editor) {
  font-family: 'Fira Code', 'Consolas', 'Monaco', monospace;
  font-size: 13px;
}

.dockerfile-editor :deep(.cm-gutters) {
  background: var(--bg-glass-dark);
  border-right: 1px solid var(--border-color);
}

.dockerfile-editor.is-dark :deep(.cm-gutters) {
  background: #252526;
}

.dockerfile-editor :deep(.cm-activeLineGutter),
.dockerfile-editor :deep(.cm-activeLine) {
  background: rgba(59, 130, 246, 0.1);
}

.dockerfile-editor :deep(.cm-content) {
  padding: 8px 0;
}

.dockerfile-editor :deep(.cm-line) {
  padding: 0 8px;
}
</style>
