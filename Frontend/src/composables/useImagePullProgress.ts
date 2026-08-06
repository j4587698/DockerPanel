import { ref, onMounted, onBeforeUnmount } from 'vue'
import { signalrService } from '@/services/signalr'

export interface PullLayerProgress {
  layerId: string
  status: string
  current: number
  total: number
  progress: number
}

/**
 * 订阅后端广播的 image-pull-progress 事件，把「按层聚合后的单调进度」映射为响应式状态。
 * 用于重建容器、自动更新、回滚、批量升级、镜像拉取等需要展示进度的场景。
 *
 * @param match 判断某条 pullId 是否属于当前视图的谓词，例如 pullId => pullId.includes(containerId)
 * @param autoStart 是否在挂载后立即开始订阅（默认 true）
 */
export function useImagePullProgress(
  match: (pullId: string) => boolean,
  autoStart = true
) {
  const progress = ref(0)
  const step = ref('')
  const detail = ref('')
  const imageName = ref('')
  const layer = ref<PullLayerProgress | null>(null)
  const indeterminate = ref(false)

  /** 是否收到过任何进度事件（未收到则维持 indeterminate 展示） */
  const hasData = ref(false)

  let unsubscribe: (() => void) | null = null

  const clear = () => {
    progress.value = 0
    step.value = ''
    detail.value = ''
    imageName.value = ''
    layer.value = null
    hasData.value = false
    indeterminate.value = false
  }

  const start = () => {
    stop()
    clear()
    unsubscribe = signalrService.subscribe('image-pull-progress', (message: any) => {
      const data = message?.data
      if (!data?.pullId || !match(data.pullId)) return

      hasData.value = true
      if (data.progress != null) progress.value = data.progress
      if (data.step) step.value = data.step
      if (data.detail != null && String(data.detail).trim() !== '') {
        detail.value = data.detail
      }
      if (data.imageName) imageName.value = data.imageName
      layer.value = data.layer || null

      // 进入完成/失败时关闭 indeterminate 动画
      if (data.step === '完成' || data.step === '失败') {
        indeterminate.value = false
        stop()
      }
    })
  }

  const stop = () => {
    if (unsubscribe) {
      unsubscribe()
      unsubscribe = null
    }
  }

  if (autoStart) {
    onMounted(() => start())
  }
  onBeforeUnmount(() => stop())

  return {
    progress,
    step,
    detail,
    imageName,
    layer,
    hasData,
    indeterminate,
    start,
    stop,
    clear
  }
}