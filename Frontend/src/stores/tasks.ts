/**
 * 后台任务 Store
 * 管理所有后台任务状态
 */

import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { tasksApi, type BackgroundTask as ApiBackgroundTask } from '@/api/tasks'

const debugLog = (...args: unknown[]) => {
  if (import.meta.env.DEV) {
    console.debug(...args)
  }
}

export interface BackgroundTask {
  id: string
  type: 'image-build' | 'image-pull' | 'image-push' | 'image-import' | 'compose-deploy' | 'volume-archive' | 'volume-restore' | 'other'
  title: string
  status: 'pending' | 'running' | 'completed' | 'failed'
  progress: number
  detail?: string
  stream?: string
  error?: string
  createdAt: Date
  updatedAt: Date
}

export const useTasksStore = defineStore('tasks', () => {
  const tasks = ref<BackgroundTask[]>([])
  const isInitialized = ref(false)

  // 进行中的任务数量
  const runningCount = computed(() => 
    tasks.value.filter(t => t.status === 'running' || t.status === 'pending').length
  )

  // 从后端同步任务（应用启动时调用）
  const syncFromBackend = async () => {
    try {
      const response = await tasksApi.getActiveTasks()
      const backendTasks = response as ApiBackgroundTask[]
      
      // 转换后端任务格式到前端格式
      tasks.value = backendTasks.map(t => ({
        id: t.id,
        type: t.type,
        title: t.title,
        status: t.status,
        progress: t.progress,
        detail: t.detail,
        stream: t.stream,
        error: t.error,
        createdAt: new Date(t.createdAt),
        updatedAt: new Date(t.updatedAt)
      }))
      
      isInitialized.value = true
      debugLog(`已从后端同步 ${tasks.value.length} 个进行中的任务`)
    } catch (error) {
      console.error('从后端同步任务失败:', error)
    }
  }

  // 添加任务（后端已创建任务后调用）
  const addTask = (task: Omit<BackgroundTask, 'createdAt' | 'updatedAt'>) => {
    const newTask: BackgroundTask = {
      ...task,
      createdAt: new Date(),
      updatedAt: new Date()
    }
    tasks.value.unshift(newTask)
    return newTask
  }

  // 更新任务
  const updateTask = (id: string, updates: Partial<BackgroundTask>) => {
    const index = tasks.value.findIndex(t => t.id === id)
    if (index !== -1) {
      tasks.value[index] = {
        ...tasks.value[index],
        ...updates,
        updatedAt: new Date()
      }
    }
  }

  // 移除任务
  const removeTask = (id: string) => {
    const index = tasks.value.findIndex(t => t.id === id)
    if (index !== -1) {
      tasks.value.splice(index, 1)
    }
  }

  // 清除已完成的任务
  const clearCompleted = () => {
    tasks.value = tasks.value.filter(t => t.status === 'running' || t.status === 'pending')
  }

  // 清除所有任务
  const clearAll = () => {
    tasks.value = []
  }

  return {
    tasks,
    runningCount,
    isInitialized,
    syncFromBackend,
    addTask,
    updateTask,
    removeTask,
    clearCompleted,
    clearAll
  }
})
