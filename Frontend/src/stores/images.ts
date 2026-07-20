import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import { ElMessage } from 'element-plus'
import { imageApi } from '@/api/image'
import type {
  ImageInfo,
  ImageDetailInfo,
  ImageSearchResult,
  ImageHistoryEntry,
  ImageLayersInfo,
  ImageStatistics,
  BuildImageRequest,
  TagImageRequest,
  PushImageRequest,
  BatchRemoveImagesRequest,
  BatchOperationResult,
  PruneResult,
  PruneImagesRequest
} from '@/types/image'

// 镜像状态接口
interface ImageState {
  images: ImageInfo[]
  selectedImage: ImageDetailInfo | null
  loading: boolean
  error: string | null
  filter: {
    search: string
    nodeId: string | null
    showDangling: boolean
    showAll: boolean
  }
  statistics: ImageStatistics | null
  refreshing: boolean
  searchResults: ImageSearchResult[]
}

// 创建镜像状态管理
export const useImagesStore = defineStore('images', () => {
  // 状态
  const state = ref<ImageState>({
    images: [],
    selectedImage: null,
    loading: false,
    error: null,
    filter: {
      search: '',
      nodeId: null,
      showDangling: false,
      showAll: false
    },
    statistics: null,
    refreshing: false,
    searchResults: []
  })

  // 计算属性
  const loading = computed(() => state.value.loading)
  const images = computed(() => state.value.images)
  const selectedImage = computed(() => state.value.selectedImage)
  const error = computed(() => state.value.error)
  const statistics = computed(() => state.value.statistics)
  const refreshing = computed(() => state.value.refreshing)
  const searchResults = computed(() => state.value.searchResults)

  // 过滤后的镜像列表
  const filteredImages = computed(() => {
    const images = state.value.images || []
    const filter = state.value.filter || {}
    let filtered = images

    // 搜索过滤
    if (filter.search) {
      const searchTerm = filter.search.toLowerCase()
      filtered = filtered.filter(image =>
        image.repository.toLowerCase().includes(searchTerm) ||
        image.id.toLowerCase().includes(searchTerm) ||
        image.tags.some((tag: string) => tag.toLowerCase().includes(searchTerm))
      )
    }

    // 节点过滤
    if (filter.nodeId) {
      filtered = filtered.filter(image => image.nodeId === filter.nodeId)
    }

    // 悬空镜像过滤
    if (!filter.showDangling) {
      filtered = filtered.filter(image => !image.tags.includes('<none>:<none>'))
    }

    return filtered
  })

  // 镜像统计信息
  const imageStats = computed(() => {
    const images = state.value.images || []
    const stats = {
      total: images.length,
      dangling: 0,
      used: 0,
      totalSize: 0,
      byRepository: {} as Record<string, number>
    }

    images.forEach(image => {
      // 悬空镜像统计 - 使用repoTags字段
      const tags = image.repoTags || image.tags || []
      if (tags.some(tag => tag.includes('<none>:<none>'))) {
        stats.dangling++
      } else {
        stats.used++
      }

      // 大小统计
      stats.totalSize += image.size || 0

      // 按仓库统计 - 安全处理repository字段
      if (image.repository) {
        const repo = image.repository.split(':')[0]
        stats.byRepository[repo] = (stats.byRepository[repo] || 0) + 1
      }
    })

    return stats
  })

  // 扩展统计信息 - 兼容前端页面期望的字段名
  const extendedStats = computed(() => {
    const baseStats = imageStats.value
    const images = state.value.images || []

    // 计算官方镜像和自定义镜像
    let officialCount = 0
    let customCount = 0

    images.forEach(image => {
      if (image.repository) {
        // 与详情页面保持一致的判断逻辑
        if (image.repository.startsWith('library/') || image.repository === 'scratch' || !image.repository.includes('/')) {
          officialCount++
        } else {
          customCount++
        }
      }
    })

    return {
      totalImages: baseStats.total,
      officialImages: officialCount,
      customImages: customCount,
      totalSize: baseStats.totalSize,
      dangling: baseStats.dangling,
      used: baseStats.used,
      byRepository: baseStats.byRepository
    }
  })

  // 获取镜像列表
  const fetchImages = async (params?: { nodeId?: string }) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await imageApi.getImages(params)
      // 响应拦截器已解包，response 即为数据
      let images = []
      if (Array.isArray(response)) {
        images = response
      } else {
        console.warn('Images Store - 意外的响应结构:', response)
        images = []
      }
      state.value.images = images
    } catch (error: any) {
      state.value.error = error.message || '获取镜像列表失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 刷新镜像列表
  const refreshImages = async () => {
    state.value.refreshing = true
    try {
      await fetchImages({ nodeId: state.value.filter.nodeId || undefined })
      await fetchStatistics({ nodeId: state.value.filter.nodeId || undefined })
    } finally {
      state.value.refreshing = false
    }
  }

  // 获取镜像详情
  const fetchImageDetails = async (imageId: string, nodeId?: string) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await imageApi.getImage(imageId, { nodeId })
      state.value.selectedImage = response
    } catch (error: any) {
      state.value.error = error.message || '获取镜像详情失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 拉取镜像
  const pullImage = async (imageName: string, tag?: string, connectionId?: string, mirrorId?: string | null) => {
    state.value.error = null

    try {
      await imageApi.pullImage({ 
        imageName, 
        tag, 
        connectionId,
        registry: mirrorId || undefined
      })
      // 刷新镜像列表
      await refreshImages()
    } catch (error: any) {
      // 优先使用后端返回的错误信息
      const errorMsg = error.userMessage || error.response?.data?.error || error.response?.data?.message || error.message || '拉取镜像失败'
      state.value.error = errorMsg
      throw new Error(errorMsg)
    }
  }

  // 删除镜像
  const removeImage = async (imageId: string, force = false, nodeId?: string) => {
    state.value.loading = true
    state.value.error = null

    try {
      await imageApi.removeImage(imageId, { force, nodeId })
      state.value.images = state.value.images.filter(image => image.id !== imageId)
      if (state.value.selectedImage?.id === imageId) {
        state.value.selectedImage = null
      }
      ElMessage.success('镜像删除成功')
    } catch (error: any) {
      state.value.error = error.message || '删除镜像失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 标记镜像
  const tagImage = async (sourceImageId: string, data: {
    targetRepository: string
    targetTag?: string
    nodeId?: string
  }) => {
    state.value.loading = true
    state.value.error = null

    try {
      await imageApi.tagImage(sourceImageId, data)
      ElMessage.success('镜像标记成功')

      // 刷新镜像列表
      await refreshImages()
    } catch (error: any) {
      state.value.error = error.message || '标记镜像失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 推送镜像
  const pushImage = async (imageName: string, data: {
    tag?: string
    nodeId?: string
  }) => {
    state.value.loading = true
    state.value.error = null

    try {
      await imageApi.pushImage(imageName, data)
      ElMessage.success('镜像推送成功')

      // 刷新镜像列表
      await refreshImages()
    } catch (error: any) {
      state.value.error = error.message || '推送镜像失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 搜索镜像
  const searchImages = async (term: string, limit = 25) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await imageApi.searchImages({ term, limit })
      state.value.searchResults = response
    } catch (error: any) {
      state.value.error = error.message || '搜索镜像失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 获取镜像构建历史
  const getImageHistory = async (imageId: string, nodeId?: string): Promise<ImageHistoryEntry[]> => {
    try {
      const response = await imageApi.getImageHistory(imageId, { nodeId })
      let history = []
      if (Array.isArray(response)) {
        history = response
      }
      return history
    } catch (error: any) {
      state.value.error = error.message || '获取镜像构建历史失败'
      ElMessage.error(state.value.error)
      throw error
    }
  }

  // 构建镜像
  const buildImage = async (data: BuildImageRequest) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await imageApi.buildImage(data)
      ElMessage.success('镜像构建成功')

      // 刷新镜像列表
      await refreshImages()
      return response
    } catch (error: any) {
      state.value.error = error.message || '构建镜像失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 获取镜像分层信息
  const getImageLayers = async (imageId: string, nodeId?: string): Promise<ImageLayersInfo> => {
    try {
      const response = await imageApi.getImageLayers(imageId, { nodeId })
      return response
    } catch (error: any) {
      state.value.error = error.message || '获取镜像分层信息失败'
      ElMessage.error(state.value.error)
      throw error
    }
  }

  // 批量删除镜像
  const batchRemoveImages = async (data: BatchRemoveImagesRequest): Promise<BatchOperationResult> => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await imageApi.batchRemoveImages(data)
      ElMessage.success(`成功删除 ${response.successCount} 个镜像`)

      // 刷新镜像列表
      await refreshImages()

      return response
    } catch (error: any) {
      state.value.error = error.message || '批量删除镜像失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 获取统计信息
  const fetchStatistics = async (params?: { nodeId?: string }) => {
    try {
      const response = await imageApi.getImageStatistics(params)
      state.value.statistics = response
    } catch (error: any) {
      state.value.error = error.message || '获取镜像统计信息失败'
      ElMessage.error(state.value.error)
      throw error
    }
  }

  // 清理未使用的镜像
  const pruneImages = async (data: PruneImagesRequest): Promise<PruneResult> => {
    state.value.loading = true
    state.value.error = null

    try {
      const result = await imageApi.pruneImages(data)
      const spaceMB = (result.spaceReclaimed / 1024 / 1024).toFixed(2)
      ElMessage.success(`成功清理 ${result.imagesDeleted} 个镜像，释放 ${spaceMB} MB 空间`)

      // 刷新镜像列表
      await refreshImages()

      return result
    } catch (error: any) {
      state.value.error = error.message || '清理镜像失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 搜索镜像
  const searchImagesFilter = (searchTerm: string) => {
    state.value.filter.search = searchTerm
  }

  // 更新过滤器
  const updateFilter = (filter: Partial<ImageState['filter']>) => {
    state.value.filter = { ...state.value.filter, ...filter }

    // 如果节点ID改变，重新获取数据
    if ('nodeId' in filter && filter.nodeId !== state.value.filter.nodeId) {
      fetchImages({ nodeId: filter.nodeId || undefined })
      fetchStatistics({ nodeId: filter.nodeId || undefined })
    }
  }

  // 选择镜像
  const selectImage = (image: ImageInfo) => {
    fetchImageDetails(image.id, image.nodeId)
  }

  // 清除选择
  const clearSelection = () => {
    state.value.selectedImage = null
  }

  // 清除错误
  const clearError = () => {
    state.value.error = null
  }

  // 重置过滤器
  const resetFilter = () => {
    state.value.filter = {
      search: '',
      nodeId: null,
      showDangling: false,
      showAll: false
    }
    fetchImages()
    fetchStatistics()
  }

  return {
    // 状态
    state,
    loading,
    images,
    selectedImage,
    error,
    statistics,
    refreshing,
    searchResults,

    // 计算属性
    filteredImages,
    imageStats,
    extendedStats,

    // 方法
    fetchImages,
    refreshImages,
    fetchImageDetails,
    pullImage,
    removeImage,
    tagImage,
    pushImage,
    searchImages,
    getImageHistory,
    buildImage,
    getImageLayers,
    batchRemoveImages,
    fetchStatistics,
    pruneImages,
    searchImagesFilter,
    updateFilter,
    selectImage,
    clearSelection,
    clearError,
    resetFilter
  }
})

export default useImagesStore