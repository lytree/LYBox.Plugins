// 离线抓取汉字笔顺数据（从 z2h.cn 抓取一次，存入本地 data/hanzi-data）
const fs = require('fs');
const path = require('path');

const CHARS = fs.readFileSync(path.join(__dirname, 'chars.txt'), 'utf8').split('');
const OUT = path.join(__dirname, '..', 'data', 'hanzi-data');
const CONCURRENCY = 16;

if (!fs.existsSync(OUT)) fs.mkdirSync(OUT, { recursive: true });

async function fetchOne(ch) {
  const file = path.join(OUT, `${ch}.json`);
  if (fs.existsSync(file) && fs.statSync(file).size > 100) return { ch, ok: true, cached: true };
  try {
    const url = `https://z2h.cn/hanzi-data/${encodeURIComponent(ch)}.json`;
    const ctrl = new AbortController();
    const timer = setTimeout(() => ctrl.abort(), 15000);
    const res = await fetch(url, { signal: ctrl.signal, headers: { 'User-Agent': 'Mozilla/5.0' } });
    clearTimeout(timer);
    if (!res.ok) return { ch, ok: false };
    const json = await res.json();
    if (!json.strokes || !json.strokes.length) return { ch, ok: false };
    fs.writeFileSync(file, JSON.stringify(json));
    return { ch, ok: true };
  } catch (e) {
    return { ch, ok: false, err: e.message };
  }
}

(async () => {
  console.log(`开始抓取 ${CHARS.length} 个汉字的笔顺数据...`);
  let done = 0, ok = 0, fail = 0;
  const queue = [...CHARS];
  const workers = Array.from({ length: CONCURRENCY }, async () => {
    while (queue.length) {
      const ch = queue.shift();
      const r = await fetchOne(ch);
      done++;
      if (r.ok) ok++; else fail++;
      if (done % 50 === 0 || done === CHARS.length) {
        process.stdout.write(`\r进度 ${done}/${CHARS.length} | 成功 ${ok} | 失败 ${fail}`);
      }
    }
  });
  await Promise.all(workers);
  console.log(`\n完成！成功 ${ok}，失败 ${fail}`);
})();