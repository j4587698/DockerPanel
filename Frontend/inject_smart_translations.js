import fs from 'fs';
import path from 'path';
import { smartTranslations } from './smart_translations.js';

const insertions = {};

for (const [key, transObj] of Object.entries(smartTranslations)) {
  const parts = key.split('.');
  if (parts.length < 2) continue;
  let moduleName = parts[0]; if (moduleName === "users") moduleName = "user";
  if (!insertions[moduleName]) insertions[moduleName] = {};
  
  let current = insertions[moduleName];
  for (let i = 1; i < parts.length - 1; i++) {
    const p = parts[i];
    if (!current[p]) current[p] = {};
    current = current[p];
  }
  const lastPart = parts[parts.length - 1];
  current[lastPart] = transObj;
}

for (const lang of ['zh-CN', 'en-US']) {
  const localeDir = path.join('src', 'locales', 'modules', lang);
  for (const moduleName of Object.keys(insertions)) {
    const tsFile = path.join(localeDir, moduleName + '.ts');
    if (!fs.existsSync(tsFile)) continue;
    
    let content = fs.readFileSync(tsFile, 'utf8');
    const isEng = lang === 'en-US';
    
    // Add missing commas to existing lines if needed (only if ends with quote)
    content = content.replace(/(\n\s*['a-zA-Z0-9_]+:\s*['"][^,\n]*['"])(?=\n\s*['a-zA-Z0-9_]+:)/g, '$1,');
    
    function escapeValue(str) {
      return str.replace(/'/g, "\\'");
    }

    function inject(obj, parentKey = '') {
      for (const [k, v] of Object.entries(obj)) {
        if (v && v.eng) {
          const translated = escapeValue(isEng ? v.eng : v.zh);
          const insertion = `  ${k}: '${translated}',`;
          if (parentKey) {
            const regex = new RegExp(`^\\s*${parentKey}\\s*:\\s*\\{`, 'm');
            const match = content.match(regex);
            if (match) {
              const insertPos = match.index + match[0].length;
              content = content.slice(0, insertPos) + '\n    ' + insertion + content.slice(insertPos);
            } else if (typeof v === "object") {
              const endMatch = content.lastIndexOf('}');
              content = content.slice(0, endMatch) + `,\n  ${parentKey}: {\n    ${insertion}\n  }\n` + content.slice(endMatch);
            }
          } else if (typeof v === "object") {
             const endMatch = content.lastIndexOf('}');
             const beforeBrace = content.slice(0, endMatch).trimEnd();
             let prependComma = '';
             if (beforeBrace && !beforeBrace.endsWith(',') && !beforeBrace.endsWith('{')) {
                prependComma = ',';
             }
             content = content.slice(0, endMatch).trimEnd() + prependComma + `\n  ${insertion}\n` + content.slice(endMatch);
          }
        } else if (typeof v === "object") {
           inject(v, k);
        }
      }
    }
    
    inject(insertions[moduleName]);
    // cleanup double commas or trailing commas before }
    content = content.replace(/,\s*,/g, ',');
    content = content.replace(/,\s*\}/g, '\n}');
    fs.writeFileSync(tsFile, content);
  }
}
console.log('Successfully injected SMART translations!');


