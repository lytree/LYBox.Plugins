/**
 * 输入文本解析：按字帖类型把原始文本转为"行"序列
 */
import { BIHUA, PEN_PATTERNS } from './stroke-data';
import type { ZitieType } from '@/types';

/** 单词/词组分隔符：空格、逗号、顿号、换行 */
const WORD_SEP = /[\s,，、\n]+/;

/** 去除所有空白 */
const noSpace = (s: string): string => s.replace(/\s/g, '');

/**
 * 解析输入
 * @returns 行数组；汉字/字母/数字/拼音为连续序列（渲染时自动折行）
 */
export function parseInput(type: ZitieType, text: string): string[] {
  const raw = (text ?? '').trim();
  if (!raw) return defaultLines(type);

  switch (type) {
    case 'hanzi':
    case 'alphabet':
    case 'number':
    case 'pinyin':
      // 连续序列，由渲染器按列数折行
      return [noSpace(raw)];

    case 'cizu':
    case 'word':
      // 每词一行，行内逐字符排布
      return raw.split(WORD_SEP).filter(Boolean);

    case 'duanluo':
    case 'paragraph':
      // 按换行分段，行内自动折行
      return raw.split('\n');

    case 'bihua':
      return raw.split(WORD_SEP).filter(Boolean);

    case 'pen':
      return raw.split(WORD_SEP).filter(Boolean);

    default:
      return [raw];
  }
}

/** 各类型的默认内容 */
function defaultLines(type: ZitieType): string[] {
  switch (type) {
    case 'bihua':
      // 默认展示全部基本笔画
      return BIHUA.map((b) => b.char);
    case 'pen':
      // 默认展示全部控笔轨迹
      return PEN_PATTERNS.map((p) => p.name);
    default:
      return [];
  }
}