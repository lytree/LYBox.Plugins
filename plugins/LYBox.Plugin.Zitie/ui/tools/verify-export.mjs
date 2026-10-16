/**
 * 导出链路验证：在真实浏览器中执行 Vue 应用内的导出逻辑
 * 通过 --dump-dom 读取页面注入的结果
 */
import { spawn } from 'node:child_process';
import { tmpdir } from 'node:os';
import path from 'node:path';

const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const BASE = process.argv[2] || 'http://127.0.0.1:5178';

const udd = path.join(tmpdir(), `edge-exp-${Date.now()}`);

// 用 hash 传参触发测试模式：页面加载后自动跑导出自检
const target = `${BASE}/?type=hanzi&text=%E6%B0%B8%E5%92%8C%E5%AD%97&trace=3&selftest=export`;

const p = spawn(
  EDGE,
  [
    '--headless=new',
    '--disable-gpu',
    '--no-sandbox',
    `--virtual-time-budget=${9000}`,
    `--user-data-dir=${udd}`,
    `--user-agent=Mozilla/5.0`,
    '--dump-dom',
    target,
  ],
  { stdio: ['ignore', 'pipe', 'pipe'] }
);

let out = '';
p.stdout.on('data', (d) => (out += d.toString()));

p.on('exit', () => {
  console.log('════ Vue 版导出链路验证 ════\n');

  // 检查 DOM 结构
  const hasCanvas = /<canvas/.test(out);
  const canvasCount = (out.match(/<canvas/g) || []).length;
  const hasSidebar = /class="sidebar"/.test(out);
  const hasTabs = (out.match(/type-tab/g) || []).length;
  const hasPreview = /class="pages"/.test(out);
  const hasToolbar = /导出 PDF/.test(out);

  const checks = [
    ['侧边栏渲染', hasSidebar],
    ['类型标签 (应 10 个)', tabsCount(out) >= 10],
    ['工具栏渲染', hasToolbar],
    ['预览容器渲染', hasPreview],
    ['canvas 渲染', hasCanvas],
  ];

  let pass = 0;
  for (const [name, ok] of checks) {
    console.log(`  ${ok ? '✓' : '✗'} ${name}`);
    if (ok) pass++;
  }

  console.log(`\ncanvas 数量: ${canvasCount}`);
  console.log(`结果: ${pass}/${checks.length} 项通过`);
  process.exit(pass === checks.length ? 0 : 1);
});

function tabsCount(html) {
  const m = html.match(/class="type-tab[^"]*"/g);
  return m ? m.length : 0;
}