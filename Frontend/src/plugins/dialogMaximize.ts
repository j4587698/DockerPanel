/**
 * 对话框最大化插件
 * 自动为所有 Element Plus el-dialog 添加最大化按钮
 */
import type { App } from 'vue'

// 最大化按钮 SVG 图标
const MAXIMIZE_ICON = `<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M8 3H5a2 2 0 0 0-2 2v3m18 0V5a2 2 0 0 0-2-2h-3m0 18h3a2 2 0 0 0 2-2v-3M3 16v3a2 2 0 0 0 2 2h3"></path></svg>`

// 还原按钮 SVG 图标
const RESTORE_ICON = `<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M8 3v3a2 2 0 0 1-2 2H3m18 0h-3a2 2 0 0 1-2-2V3m0 18v-3a2 2 0 0 1 2-2h3M3 16h3a2 2 0 0 1 2 2v3"></path></svg>`

// 存储已处理的对话框
const processedDialogs = new WeakSet<Element>()

// 存储对话框的最大化状态
const dialogStates = new WeakMap<Element, boolean>()

/**
 * 创建最大化按钮
 */
function createMaximizeButton(dialog: Element): HTMLButtonElement {
    const btn = document.createElement('button')
    btn.className = 'el-dialog__maximizebtn'
    btn.setAttribute('aria-label', '最大化')
    btn.setAttribute('type', 'button')
    btn.innerHTML = MAXIMIZE_ICON

    btn.addEventListener('click', (e) => {
        e.preventDefault()
        e.stopPropagation()
        toggleMaximize(dialog, btn)
    })

    return btn
}

/**
 * 切换最大化状态
 */
function toggleMaximize(dialog: Element, btn: HTMLButtonElement): void {
    const isMaximized = dialogStates.get(dialog) || false
    const newState = !isMaximized

    dialogStates.set(dialog, newState)

    if (newState) {
        dialog.classList.add('is-maximized')
        btn.innerHTML = RESTORE_ICON
        btn.setAttribute('aria-label', '还原')
    } else {
        dialog.classList.remove('is-maximized')
        btn.innerHTML = MAXIMIZE_ICON
        btn.setAttribute('aria-label', '最大化')
    }

    // 触发窗口 resize 事件，让内部组件（如终端）调整大小
    window.dispatchEvent(new Event('resize'))

    // 发送自定义事件，供组件监听
    dialog.dispatchEvent(new CustomEvent('dialog-maximize-change', {
        detail: { maximized: newState },
        bubbles: true
    }))
}

/**
 * 处理对话框，添加最大化按钮
 */
function processDialog(dialog: Element): void {
    if (processedDialogs.has(dialog)) return

    const header = dialog.querySelector('.el-dialog__header')
    if (!header) return

    // 检查是否已经有关闭按钮
    const closeBtn = header.querySelector('.el-dialog__headerbtn')
    if (!closeBtn) return

    // 创建并插入最大化按钮
    const maximizeBtn = createMaximizeButton(dialog)
    header.insertBefore(maximizeBtn, closeBtn)

    processedDialogs.add(dialog)
    dialogStates.set(dialog, false)
}

/**
 * 初始化 MutationObserver
 */
function initObserver(): MutationObserver {
    const observer = new MutationObserver((mutations) => {
        for (const mutation of mutations) {
            // 检查新增的节点
            for (const node of mutation.addedNodes) {
                if (node instanceof Element) {
                    // 检查是否是 el-overlay（对话框的遮罩层）
                    if (node.classList.contains('el-overlay')) {
                        const dialog = node.querySelector('.el-dialog')
                        if (dialog) {
                            // 延迟处理以确保对话框完全渲染
                            requestAnimationFrame(() => processDialog(dialog))
                        }
                    }
                    // 直接检查是否是 el-dialog
                    if (node.classList.contains('el-dialog')) {
                        requestAnimationFrame(() => processDialog(node))
                    }
                    // 检查子节点中的对话框
                    const dialogs = node.querySelectorAll('.el-dialog')
                    dialogs.forEach(dialog => {
                        requestAnimationFrame(() => processDialog(dialog))
                    })
                }
            }
        }
    })

    // 观察 body 下的变化（因为 Element Plus 使用 teleport）
    observer.observe(document.body, {
        childList: true,
        subtree: true
    })

    return observer
}

/**
 * Vue 插件
 */
export const DialogMaximizePlugin = {
    install(_app: App): void {
        // 在 DOM 准备好后初始化
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => initObserver())
        } else {
            initObserver()
        }
    }
}

export default DialogMaximizePlugin
