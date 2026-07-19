const fs = require('fs');
const path = require('path');
const srcDir = 'e:/RiderProjects/DockerPanel/Frontend/src';

function findAllFiles(dir, exts, fileList = []) {
  if (dir.includes('locales')) return fileList;
  const files = fs.readdirSync(dir);
  for (const file of files) {
    const fullPath = path.join(dir, file);
    if (fullPath.includes('locales')) continue;
    if (fs.statSync(fullPath).isDirectory()) {
      findAllFiles(fullPath, exts, fileList);
    } else if (exts.some(ext => fullPath.endsWith(ext))) {
      fileList.push(fullPath);
    }
  }
  return fileList;
}

const vueFiles = findAllFiles(srcDir, ['.vue', '.ts']);
const keys = new Set();
const regex = /(?:\|t)\(['"]([a-zA-Z0-9_\.]+)['"]\)/g;

for (const file of vueFiles) {
  const content = fs.readFileSync(file, 'utf8');
  let match;
  while ((match = regex.exec(content)) !== null) {
    keys.add(match[1]);
  }
}

// Now parse the TS files for existing keys
const zhCnDir = path.join(srcDir, 'locales', 'modules', 'zh-CN');
const tsFiles = fs.readdirSync(zhCnDir).filter(f => f.endsWith('.ts'));

const existingKeys = new Set();
for (const file of tsFiles) {
  const content = fs.readFileSync(path.join(zhCnDir, file), 'utf8');
  const moduleName = file.replace('.ts', '');
  
  // A rough regex to find keys in TS
  const lines = content.split('\n');
  let currentPath = [moduleName];
  for (const line of lines) {
    const trimmed = line.trim();
    if (trimmed.startsWith('//') || trimmed === '') continue;
    
    // matching nested objects
    const objMatch = trimmed.match(/^([a-zA-Z0-9_]+)\s*:\s*\{/);
    if (objMatch) {
      currentPath.push(objMatch[1]);
      continue;
    }
    if (trimmed.startsWith('},')) {
      currentPath.pop();
      continue;
    }
    
    const keyMatch = trimmed.match(/^([a-zA-Z0-9_]+)\s*:/);
    if (keyMatch) {
      existingKeys.add([...currentPath, keyMatch[1]].join('.'));
    }
  }
}

const missing = [];
for (const key of keys) {
  if (!existingKeys.has(key)) {
    missing.push(key);
  }
}

console.log(JSON.stringify(missing, null, 2));
