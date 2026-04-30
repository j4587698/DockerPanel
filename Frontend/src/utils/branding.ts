export const APP_NAME = 'DockerPanel'

export const normalizeAppName = (value?: string | null) => {
  const name = value?.trim()
  return name || APP_NAME
}

export const formatDocumentTitle = (pageTitle?: string | null, appName?: string | null) => {
  const normalizedAppName = normalizeAppName(appName)
  const normalizedPageTitle = pageTitle?.trim()
  return normalizedPageTitle ? `${normalizedPageTitle} - ${normalizedAppName}` : normalizedAppName
}

export const updateDocumentTitle = (pageTitle?: string | null, appName?: string | null) => {
  document.title = formatDocumentTitle(pageTitle, appName)
}
