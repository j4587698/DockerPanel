/**
 * Tasks API
 * 后台任务管理相关的API调用
 */

import api from "./index"

export interface BackgroundTask {
  id: string
  type: 'image-build' | 'image-pull' | 'image-push' | 'image-import' | 'compose-deploy' | 'volume-archive' | 'volume-restore' | 'other'
  title: string
  status: 'pending' | 'running' | 'completed' | 'failed'
  progress: number
  detail?: string
  stream?: string
  error?: string
  createdAt: string
  updatedAt: string
  metadata?: Record<string, unknown>
}

export const tasksApi = {
  // 获取所有任务
  getTasks: () => {
    return api.get<BackgroundTask[]>("/tasks")
  },

  // 获取进行中的任务
  getActiveTasks: () => {
    return api.get<BackgroundTask[]>("/tasks/active")
  },

  // 获取特定任务
  getTask: (id: string) => {
    return api.get<BackgroundTask>(`/tasks/${id}`)
  },

  // 删除任务
  removeTask: (id: string) => {
    return api.delete(`/tasks/${id}`)
  },

  // 清理已完成的任务
  clearCompleted: () => {
    return api.post("/tasks/clear-completed")
  }
}

export default tasksApi
