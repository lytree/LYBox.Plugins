/**
 * TXT 文本导入
 * 原样保留文本的换行、缩进与符号，仅做最轻微的规范化：
 * 统一换行符 / 去 BOM / 去行尾空白 / 去文件首尾空行。
 * 编码按 UTF-8 解码，出现替换字符时回退 GBK（兼容 Windows  Legacy 文本）。
 */
import type { ZitieType } from '@/types';

/** 导入结果 */
export interface TxtImportResult {
  /** 规范化后的文本（保留换行与行首缩进） */
  text: string;
  /** 按内容语言建议的字帖类型 */
  type: ZitieType;
  /** 正文字符数（不含空白） */
  chars: number;
  /** 来源文件名 */
  filename: string;
}

/** CJK 表意文字（中日韩） */
const CJK = /[\u3400-\u4dbf\u4e00-\u9fff\uf900-\ufaff\u3040-\u30ff\uac00-\ud7af]/;
/** 拉丁字母 */
const LATIN = /[a-zA-Z]/;

/** 支持的文件扩展名 */
const TXT_EXT = /\.(txt|text)$/i;

/**
 * 读取文本文件（UTF-8 优先，含替换字符时回退 GBK）
 */
async function readTextFile(file: File): Promise<string> {
  const buf = await file.arrayBuffer();
  const utf8 = new TextDecoder('utf-8').decode(buf);
  if (!utf8.includes('\uFFFD')) return utf8;

  // 疑似 GBK / ANSI 编码，尝试回退解码
  try {
    const gbk = new TextDecoder('gbk').decode(buf);
    if (!gbk.includes('\uFFFD')) return gbk;
  } catch {
    /* 当前环境不支持 gbk 解码，沿用 UTF-8 结果 */
  }
  return utf8;
}

/**
 * 文本规范化：保留换行、缩进与全部符号
 * - 统一 \r\n / \r 为 \n
 * - 去除 BOM、行尾空白、文件首尾空行
 * - 不做任何内容改写（标点、emoji、特殊符号原样保留）
 */
export function normalizeText(raw: string): string {
  return raw
    .replace(/^\uFEFF/, '')
    .replace(/\r\n?/g, '\n')
    .split('\n')
    .map((line) => line.replace(/[ \t]+$/, ''))
    .join('\n')
    .replace(/^\n+/, '')
    .replace(/\s+$/, '');
}

/**
 * 按内容语言推断字帖类型
 * 中文（含 CJK）为主 -> 段落；英文为主 -> 英文段落
 */
export function detectZitieType(text: string): ZitieType {
  let cjk = 0;
  let latin = 0;
  for (const ch of text) {
    if (CJK.test(ch)) cjk++;
    else if (LATIN.test(ch)) latin++;
  }
  return latin > cjk ? 'paragraph' : 'duanluo';
}

/**
 * 读取并识别 TXT 文件
 * @throws 文件类型不支持 / 内容为空 / 读取失败
 */
export async function importTxtFile(file: File): Promise<TxtImportResult> {
  if (!TXT_EXT.test(file.name)) {
    throw new Error(`不支持的文件：${file.name || '未知'}（仅支持 .txt 文本文件）`);
  }

  const raw = await readTextFile(file);
  const text = normalizeText(raw);
  if (!text.replace(/\s/g, '')) {
    throw new Error('文件内容为空');
  }

  return {
    text,
    type: detectZitieType(text),
    chars: text.replace(/\s/g, '').length,
    filename: file.name,
  };
}
