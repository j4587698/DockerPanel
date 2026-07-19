import fs from 'fs';
import path from 'path';

const missingKeysPath = 'missing_keys.txt';
if (!fs.existsSync(missingKeysPath)) {
  console.log('No missing_keys.txt');
  process.exit(1);
}

const lines = fs.readFileSync(missingKeysPath, 'utf8').split('\n').map(l => l.trim()).filter(l => l);

const dictEng = {
  "application": "Application", "cancelled": "Cancelled", "log": "Log", "progress": "Progress",
  "cancel": "Cancel", "message": "Message", "title": "Title", "renew": "Renew", "button": "Button",
  "certificate": "Certificate", "still": "Still", "pending": "Pending", "collapse": "Collapse",
  "completed": "Completed", "time": "Time", "configured": "Configured", "confirm": "Confirm",
  "current": "Current", "status": "Status", "step": "Step", "detailed": "Detailed", "steps": "Steps",
  "dns": "DNS", "provider": "Provider", "label": "Label", "validation": "Validation", "config": "Config",
  "download": "Download", "failed": "Failed", "error": "Error", "errors": "Errors", "and": "And",
  "warnings": "Warnings", "expand": "Expand", "expires": "Expires", "today": "Today", "tomorrow": "Tomorrow",
  "http": "HTTP", "keep": "Keep", "no": "No", "data": "Data", "not": "Not", "found": "Found",
  "cleaned": "Cleaned", "refresh": "Refresh", "renewal": "Renewal", "started": "Started", "in": "In",
  "unknown": "Unknown", "waiting": "Waiting", "web": "Web", "root": "Root", "create": "Create",
  "format": "Format", "parse": "Parse", "success": "Success", "validate": "Validate", "msg": "Msg",
  "container": "Container", "detail": "Detail", "connect": "Connect", "network": "Network", "disconnect": "Disconnect",
  "export": "Export", "recreate": "Recreate", "rename": "Rename", "force": "Force", "delete": "Delete",
  "overview": "Overview", "stats": "Stats", "logs": "Logs", "terminal": "Terminal", "files": "Files",
  "auto": "Auto", "update": "Update", "json": "JSON", "pulling": "Pulling", "missing": "Missing",
  "template": "Template", "description": "Description", "swap": "Swap", "space": "Space", "usage": "Usage",
  "system": "System", "ten": "Ten", "seconds": "Seconds", "total": "Total", "two": "Two", "user": "User",
  "write": "Write", "bytes": "Bytes", "ops": "Ops", "aliases": "Aliases", "hint": "Hint", "ip": "IP",
  "ipv4": "IPv4", "address": "Address", "placeholder": "Placeholder", "ipv6": "IPv6", "load": "Load",
  "available": "Available", "containers": "Containers", "search": "Search", "select": "Select", "registry": "Registry",
  "results": "Results", "images": "Images", "searching": "Searching", "ssh": "SSH", "users": "Users",
  "account": "Account", "active": "Active", "admin": "Admin", "cannot": "Cannot", "self": "Self",
  "created": "Created", "at": "At", "name": "Name", "display": "Display", "edit": "Edit", "filter": "Filter",
  "role": "Role", "last": "Last", "login": "Login", "locked": "Locked", "must": "Must", "change": "Change",
  "password": "Password", "new": "New", "length": "Length", "required": "Required", "reset": "Reset",
  "operator": "Operator", "viewer": "Viewer", "save": "Save", "volume": "Volume", "driver": "Driver",
  "option": "Option", "advanced": "Advanced", "generate": "Generate", "backup": "Backup", "file": "File",
  "path": "Path", "compress": "Compress", "compressed": "Compressed", "size": "Size", "local": "Local",
  "preset": "Preset", "daily": "Daily", "migration": "Migration", "pre": "Pre", "upgrade": "Upgrade",
  "preview": "Preview", "preparing": "Preparing", "starting": "Starting", "target": "Target", "node": "Node",
  "global": "Global", "basic": "Basic", "info": "Info", "bind": "Bind", "common": "Common", "presets": "Presets",
  "dest": "Dest", "capabilities": "Capabilities", "version": "Version", "used": "Used", "mount": "Mount",
  "mode": "Mode", "special": "Special", "propagation": "Propagation", "read": "Read", "only": "Only",
  "ref": "Ref", "count": "Count", "related": "Related", "scope": "Scope", "source": "Source", "supported": "Supported",
  "rate": "Rate", "id": "Id", "writable": "Writable", "type": "Type", "duplicate": "Duplicate", "confirm": "Confirm",
  "copy": "Copy", "metadata": "Metadata", "permissions": "Permissions", "timestamps": "Timestamps",
  "enable": "Enable", "compression": "Compression", "copying": "Copying", "transfer": "Transfer", "result": "Result",
  "duration": "Duration", "verify": "Verify", "integrity": "Integrity", "fetch": "Fetch", "nodes": "Nodes",
  "high": "High", "perf": "Perf", "nfs": "NFS", "large": "Large", "mem": "Mem", "storage": "Storage",
  "memory": "Memory", "range": "Range", "pattern": "Pattern", "server": "Server", "operation": "Operation",
  "value": "Value", "performance": "Performance", "optimized": "Optimized", "remote": "Remote", "tmpfs": "Tmpfs",
  "restarted": "Restarted", "disconnected": "Disconnected"
};

const dictZh = {
  "application": "申请", "cancelled": "已取消", "log": "日志", "progress": "进度",
  "cancel": "取消", "message": "消息", "title": "标题", "renew": "续期", "button": "按钮",
  "certificate": "证书", "still": "仍然", "pending": "处理中", "collapse": "折叠",
  "completed": "已完成", "time": "时间", "configured": "已配置", "confirm": "确认",
  "current": "当前", "status": "状态", "step": "步骤", "detailed": "详细", "steps": "步骤",
  "dns": "DNS", "provider": "提供商", "label": "标签", "validation": "验证", "config": "配置",
  "download": "下载", "failed": "失败", "error": "错误", "errors": "错误", "and": "与",
  "warnings": "警告", "expand": "展开", "expires": "过期", "today": "今天", "tomorrow": "明天",
  "http": "HTTP", "keep": "保留", "no": "暂无", "data": "数据", "not": "未", "found": "找到",
  "cleaned": "已清理", "refresh": "刷新", "renewal": "续期", "started": "已开始", "in": "在",
  "unknown": "未知", "waiting": "等待中", "web": "Web", "root": "根目录", "create": "创建",
  "format": "格式", "parse": "解析", "success": "成功", "validate": "验证", "msg": "消息",
  "container": "容器", "detail": "详情", "connect": "连接", "network": "网络", "disconnect": "断开",
  "export": "导出", "recreate": "重建", "rename": "重命名", "force": "强制", "delete": "删除",
  "overview": "概览", "stats": "统计", "logs": "日志", "terminal": "终端", "files": "文件",
  "auto": "自动", "update": "更新", "json": "JSON", "pulling": "拉取中", "missing": "缺失",
  "template": "模板", "description": "描述", "swap": "交换空间", "space": "空间", "usage": "使用量",
  "system": "系统", "ten": "十", "seconds": "秒", "total": "总计", "two": "两", "user": "用户",
  "write": "写入", "bytes": "字节", "ops": "操作", "aliases": "别名", "hint": "提示", "ip": "IP",
  "ipv4": "IPv4", "address": "地址", "placeholder": "占位符", "ipv6": "IPv6", "load": "加载",
  "available": "可用", "containers": "容器", "search": "搜索", "select": "选择", "registry": "镜像仓库",
  "results": "结果", "images": "镜像", "searching": "搜索中", "ssh": "SSH", "users": "用户",
  "account": "账户", "active": "活跃", "admin": "管理员", "cannot": "不能", "self": "自己",
  "created": "创建", "at": "时间", "name": "名称", "display": "显示", "edit": "编辑", "filter": "过滤",
  "role": "角色", "last": "最后", "login": "登录", "locked": "锁定", "must": "必须", "change": "更改",
  "password": "密码", "new": "新", "length": "长度", "required": "必填", "reset": "重置",
  "operator": "操作员", "viewer": "查看者", "save": "保存", "volume": "数据卷", "driver": "驱动",
  "option": "选项", "advanced": "高级", "generate": "生成", "backup": "备份", "file": "文件",
  "path": "路径", "compress": "压缩", "compressed": "已压缩", "size": "大小", "local": "本地",
  "preset": "预设", "daily": "日常", "migration": "迁移", "pre": "预", "upgrade": "升级",
  "preview": "预览", "preparing": "准备中", "starting": "启动中", "target": "目标", "node": "节点",
  "global": "全局", "basic": "基础", "info": "信息", "bind": "绑定", "common": "通用", "presets": "预设",
  "dest": "目标", "capabilities": "能力", "version": "版本", "used": "使用", "mount": "挂载",
  "mode": "模式", "special": "特殊", "propagation": "传播", "read": "只读", "only": "仅",
  "ref": "引用", "count": "数量", "related": "相关", "scope": "范围", "source": "源", "supported": "支持的",
  "rate": "速率", "id": "ID", "writable": "可写", "type": "类型", "duplicate": "复制", "confirm": "确认",
  "copy": "复制", "metadata": "元数据", "permissions": "权限", "timestamps": "时间戳",
  "enable": "启用", "compression": "压缩", "copying": "复制中", "transfer": "传输", "result": "结果",
  "duration": "耗时", "verify": "验证", "integrity": "完整性", "fetch": "获取", "nodes": "节点",
  "high": "高", "perf": "性能", "nfs": "NFS", "large": "大", "mem": "内存", "storage": "存储",
  "memory": "内存", "range": "范围", "pattern": "模式", "server": "服务器", "operation": "操作",
  "value": "值", "performance": "性能", "optimized": "优化", "remote": "远程", "tmpfs": "Tmpfs",
  "restarted": "已重启", "disconnected": "已断开"
};

function splitCamelCase(str) {
  return str.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase().split(/[\s_\.]+/);
}

function translate(str, dict, isEng) {
  const words = splitCamelCase(str);
  return words.map(w => {
    let t = dict[w] || (isEng ? w.charAt(0).toUpperCase() + w.slice(1) : w);
    return t;
  }).join(isEng ? ' ' : '');
}

const insertions = {};

for (const key of lines) {
  const parts = key.split('.');
  if (parts.length < 2) continue;
  const moduleName = parts[0];
  if (!insertions[moduleName]) insertions[moduleName] = {};
  
  let current = insertions[moduleName];
  for (let i = 1; i < parts.length - 1; i++) {
    const p = parts[i];
    if (!current[p]) current[p] = {};
    current = current[p];
  }
  const lastPart = parts[parts.length - 1];
  current[lastPart] = { eng: translate(lastPart, dictEng, true), zh: translate(lastPart, dictZh, false) };
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
    
    function inject(obj, lines, parentKey = '') {
      for (const [k, v] of Object.entries(obj)) {
        if (v.eng) {
          const translated = isEng ? v.eng : v.zh;
          const insertion = `  ${k}: '${translated}',`;
          if (parentKey) {
            const regex = new RegExp(`^\\s*${parentKey}\\s*:\\s*\\{`, 'm');
            const match = content.match(regex);
            if (match) {
              const insertPos = match.index + match[0].length;
              content = content.slice(0, insertPos) + '\n    ' + insertion + content.slice(insertPos);
            } else {
              const endMatch = content.lastIndexOf('}');
              content = content.slice(0, endMatch) + `,\n  ${parentKey}: {\n    ${insertion}\n  }\n` + content.slice(endMatch);
            }
          } else {
             const endMatch = content.lastIndexOf('}');
             // check if there's a comma before the last }
             const beforeBrace = content.slice(0, endMatch).trimEnd();
             let prependComma = '';
             if (beforeBrace && !beforeBrace.endsWith(',') && !beforeBrace.endsWith('{')) {
                prependComma = ',';
             }
             content = content.slice(0, endMatch).trimEnd() + prependComma + `\n  ${insertion}\n` + content.slice(endMatch);
          }
        } else {
           inject(v, lines, k);
        }
      }
    }
    
    inject(insertions[moduleName], content.split('\n'));
    // cleanup double commas or trailing commas before }
    content = content.replace(/,\s*,/g, ',');
    content = content.replace(/,\s*\}/g, '\n}');
    fs.writeFileSync(tsFile, content);
  }
}
console.log('Successfully injected translations!');
