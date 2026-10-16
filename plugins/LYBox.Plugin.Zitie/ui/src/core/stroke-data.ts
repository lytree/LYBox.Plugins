/**
 * 笔顺数据离线加载器
 * 数据来源：public/data/hanzi-data/{汉字}.json
 */
import type { BihuaInfo, PenPattern } from '@/types';

/** 笔顺数据根目录（public 目录，由 Vite 原样拷贝到 dist） */
const DATA_BASE = new URL('data/hanzi-data/', document.baseURI).href;

/** 已加载缓存 */
const cache = new Map<string, string[]>();
/** 确认缺失的字（避免重复请求） */
const missing = new Set<string>();

/** 汉字正则 */
const HANZI_RE = /[\u4e00-\u9fff]/;

/** 并发上限 */
const CONCURRENCY = 24;

/**
 * 加载单个汉字的笔顺路径
 */
export async function loadStrokes(ch: string): Promise<string[] | null> {
  if (!HANZI_RE.test(ch)) return null;

  const cached = cache.get(ch);
  if (cached) return cached;
  if (missing.has(ch)) return null;

  try {
    const res = await fetch(`${DATA_BASE}${encodeURIComponent(ch)}.json`);
    if (!res.ok) throw new Error(`HTTP ${res.status}`);

    const json = (await res.json()) as { strokes?: string[]; medians?: string[] };
    const strokes = json.strokes ?? json.medians;

    if (!strokes?.length) {
      missing.add(ch);
      return null;
    }
    cache.set(ch, strokes);
    return strokes;
  } catch {
    missing.add(ch);
    return null;
  }
}

/**
 * 批量加载，返回 字 -> 笔顺路径 的映射
 */
export async function loadStrokesBatch(
  chars: readonly string[]
): Promise<Record<string, string[]>> {
  const uniq = [...new Set(chars)].filter((c) => HANZI_RE.test(c));
  const pending = uniq.filter((c) => !cache.has(c) && !missing.has(c));

  let idx = 0;
  await Promise.all(
    Array.from({ length: CONCURRENCY }, async () => {
      while (idx < pending.length) {
        const c = pending[idx++];
        if (c) await loadStrokes(c);
      }
    })
  );

  const out: Record<string, string[]> = {};
  for (const c of uniq) {
    const s = cache.get(c);
    if (s) out[c] = s;
  }
  return out;
}

/** 基本笔画表（用于笔画字帖） */
export const BIHUA: readonly BihuaInfo[] = [
  { name: '横', char: '一', type: 'h' },
  { name: '竖', char: '丨', type: 's' },
  { name: '撇', char: '丿', type: 'p' },
  { name: '捺', char: '㇏', type: 'n' },
  { name: '点', char: '丶', type: 'd' },
  { name: '提', char: '㇀', type: 't' },
  { name: '横折', char: '𠃍', type: 'hz' },
  { name: '横钩', char: '乛', type: 'hg' },
  { name: '竖钩', char: '亅', type: 'sg' },
  { name: '竖提', char: '𠄌', type: 'st' },
  { name: '弯钩', char: '㇁', type: 'wg' },
  { name: '斜钩', char: '㇂', type: 'xg' },
  { name: '卧钩', char: '㇃', type: 'og' },
  { name: '横折钩', char: '𠃌', type: 'hzg' },
  { name: '横撇', char: '㇇', type: 'hp' },
  { name: '撇折', char: '𠃋', type: 'pz' },
  { name: '撇点', char: '㇛', type: 'pd' },
  { name: '横折弯钩', char: '㇈', type: 'hzwg' },
  { name: '横折提', char: '𠃍', type: 'hzt' },
  { name: '横折折撇', char: '㇋', type: 'hzzp' },
  { name: '竖折', char: '𠃊', type: 'sz' },
  { name: '竖折折钩', char: '㇉', type: 'szzg' },
];

/** 控笔轨迹模板 */
export const PEN_PATTERNS: readonly PenPattern[] = [
  { name: '横线', path: [[0, 0.5], [1, 0.5]] },
  { name: '竖线', path: [[0.5, 0], [0.5, 1]] },
  { name: '斜线', path: [[0.1, 0.9], [0.9, 0.1]] },
  { name: '波浪线', wave: 'y' },
  { name: '螺旋线', spiral: true },
  { name: '折线', zigzag: true },
];