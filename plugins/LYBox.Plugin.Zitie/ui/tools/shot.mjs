/**
 * 无头截图工具（基于系统 Edge）
 * 用法：node tools/shot.mjs <url> <输出路径> [等待ms] [宽] [高]
 */
import { spawn } from 'node:child_process';
import { existsSync, statSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';

const EDGE = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';

const [, , url, out = 'shot.png', wait = '4500', w = '1440', h = '900'] = process.argv;

if (!url) {
  console.error('用法: node tools/shot.mjs <url> <out.png> [waitMs] [w] [h]');
  process.exit(1);
}

const udd = path.join(tmpdir(), `edge-shot-${Date.now()}`);

const p = spawn(
  EDGE,
  [
    '--headless=new',
    '--disable-gpu',
    '--no-sandbox',
    '--hide-scrollbars',
    `--window-size=${w},${h}`,
    `--screenshot=${out}`,
    `--virtual-time-budget=${wait}`,
    `--user-data-dir=${udd}`,
    String(url),
  ],
  { stdio: ['ignore', 'ignore', 'pipe'] }
);

p.on('exit', () => {
  if (existsSync(out)) {
    console.log(`截图成功: ${out} (${(statSync(out).size / 1024).toFixed(1)} KB)`);
    process.exit(0);
  }
  console.error('截图失败');
  process.exit(1);
});