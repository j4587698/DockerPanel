const fs = require('fs')
const path = require('path')

const srcDir = path.join(__dirname, 'src')
const locales = ['zh-CN', 'en-US']

function findAllFiles(dir, fileList = []) {
  const files = fs.readdirSync(dir)
  for (const file of files) {
    const fullPath = path.join(dir, file)
    if (fullPath.includes(`${path.sep}locales${path.sep}`)) continue

    if (fs.statSync(fullPath).isDirectory()) {
      findAllFiles(fullPath, fileList)
    } else if (/\.(vue|ts)$/.test(fullPath)) {
      fileList.push(fullPath)
    }
  }
  return fileList
}

function collectUsedKeys() {
  const keys = new Set()
  const patterns = [
    /(?:^|[^\w$])(?:\$t|t)\(\s*(['"])([A-Za-z][A-Za-z0-9_.-]*\.[A-Za-z0-9_.-]+)\1/g,
    /\.t\(\s*(['"])([A-Za-z][A-Za-z0-9_.-]*\.[A-Za-z0-9_.-]+)\1/g
  ]

  for (const file of findAllFiles(srcDir)) {
    const content = fs.readFileSync(file, 'utf8')
    for (const regex of patterns) {
      regex.lastIndex = 0
      let match
      while ((match = regex.exec(content)) !== null) {
        keys.add(match[2])
      }
    }
  }

  return [...keys].sort()
}

function flattenLocaleObject(obj, prefix, output) {
  for (const [key, value] of Object.entries(obj)) {
    const currentPath = prefix ? `${prefix}.${key}` : key
    if (value && typeof value === 'object' && !Array.isArray(value)) {
      flattenLocaleObject(value, currentPath, output)
    } else {
      output.add(currentPath)
    }
  }
}

function parseLocaleFile(filePath, moduleName) {
  const root = {}
  const duplicates = []
  const stack = [{ path: moduleName, obj: root, keys: new Set() }]
  const lines = fs.readFileSync(filePath, 'utf8').split(/\r?\n/)

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i].trim()
    if (!line || line.startsWith('//') || line.startsWith('export default')) continue

    if (/^}\s*,?$/.test(line)) {
      if (stack.length > 1) stack.pop()
      continue
    }

    const keyMatch = line.match(/^['"]?([A-Za-z0-9_-]+)['"]?\s*:/)
    if (!keyMatch) continue

    const key = keyMatch[1]
    const parent = stack[stack.length - 1]
    const currentPath = `${parent.path}.${key}`
    if (parent.keys.has(key)) {
      duplicates.push({ key: currentPath, line: i + 1 })
    }
    parent.keys.add(key)

    if (/^['"]?[A-Za-z0-9_-]+['"]?\s*:\s*\{\s*,?$/.test(line)) {
      const child = {}
      parent.obj[key] = child
      stack.push({ path: currentPath, obj: child, keys: new Set() })
    } else {
      parent.obj[key] = true
    }
  }

  return { root, duplicates }
}

function collectLocaleKeys(locale) {
  const localeDir = path.join(srcDir, 'locales', 'modules', locale)
  const keys = new Set()
  const duplicates = []
  const files = fs.readdirSync(localeDir).filter(file => file.endsWith('.ts') && file !== 'index.ts')

  for (const file of files) {
    const moduleName = file.replace(/\.ts$/, '')
    const { root, duplicates: fileDuplicates } = parseLocaleFile(path.join(localeDir, file), moduleName)
    flattenLocaleObject(root, moduleName, keys)
    duplicates.push(...fileDuplicates.map(duplicate => ({ file: `${locale}/${file}`, ...duplicate })))
  }

  return { keys, duplicates }
}

const usedKeys = collectUsedKeys()
const result = {
  usedCount: usedKeys.length,
  missing: {},
  duplicates: {}
}

let hasProblem = false

for (const locale of locales) {
  const { keys, duplicates } = collectLocaleKeys(locale)
  const missing = usedKeys.filter(key => !keys.has(key))
  result.missing[locale] = missing
  result.duplicates[locale] = duplicates

  if (missing.length > 0 || duplicates.length > 0) {
    hasProblem = true
  }
}

console.log(JSON.stringify(result, null, 2))

if (hasProblem) {
  process.exitCode = 1
}
