/**
 * 全局配置：字帖类型、纸张、字体、输入占位符
 */
import type { FontDef, PaperName, PaperSize, ZitieType } from '@/types';

/** mm -> px（96dpi） */
export const MM = 96 / 25.4;

/** 纸张尺寸 */
export const PAPERS: Record<PaperName, PaperSize> = {
  A4: { width: 210 * MM, height: 297 * MM },
  A5: { width: 148 * MM, height: 210 * MM },
  B5: { width: 176 * MM, height: 250 * MM },
  Letter: { width: 215.9 * MM, height: 279.4 * MM },
};

export const PAPER_NAMES = Object.keys(PAPERS) as PaperName[];

/** 字帖类型定义 */
export interface ZitieTypeDef {
  key: ZitieType;
  label: string;
  /** 输入框占位符 */
  placeholder: string;
  /** 是否使用英文格子体系 */
  en: boolean;
}

export const ZITIE_TYPES: readonly ZitieTypeDef[] = [
  { key: 'hanzi', label: '汉字', placeholder: '请输入要练习的汉字，例如：永 和 字', en: false },
  { key: 'cizu', label: '词组', placeholder: '请输入词组，用空格或逗号分隔，如：世界 你好', en: false },
  { key: 'duanluo', label: '段落', placeholder: '请输入段落或文章内容', en: false },
  { key: 'bihua', label: '笔画', placeholder: '留空显示全部 20 种基本笔画', en: false },
  { key: 'alphabet', label: '字母', placeholder: '请输入要练习的英文字母，例如：ABC', en: true },
  { key: 'word', label: '单词', placeholder: '请输入词汇，用空格或逗号分隔', en: true },
  { key: 'paragraph', label: '英文段落', placeholder: '请输入英文文章、段落或语句', en: true },
  { key: 'number', label: '数字', placeholder: '请输入数字，例如：1234567890', en: true },
  { key: 'pinyin', label: '拼音', placeholder: '请输入带声调的音节，例如：ā á ǎ à', en: false },
  { key: 'pen', label: '控笔', placeholder: '留空显示全部控笔轨迹', en: false },
];

export const TYPE_MAP: Record<ZitieType, ZitieTypeDef> = ZITIE_TYPES.reduce(
  (acc, t) => {
    acc[t.key] = t;
    return acc;
  },
  {} as Record<ZitieType, ZitieTypeDef>
);

/** 中文字体（使用本机字体，离线可用） */
export const CN_FONTS: readonly FontDef[] = [
  { value: 'KaiTi, STKaiti, 楷体', label: '楷体', scale: 68, offset: 0 },
  { value: '"STXingkai", 华文行楷, cursive', label: '华文行楷', scale: 68, offset: 0 },
  { value: 'SimHei, 黑体', label: '黑体', scale: 68, offset: 0 },
  { value: 'SimSun, 宋体', label: '宋体', scale: 68, offset: 0 },
  { value: 'FangSong, 仿宋', label: '仿宋', scale: 68, offset: 0 },
  { value: '"Microsoft YaHei", 微软雅黑', label: '微软雅黑', scale: 68, offset: 0 },
  { value: 'STSong, 华文宋体', label: '华文宋体', scale: 68, offset: 0 },
];

/** 英文字体 */
export const EN_FONTS: readonly FontDef[] = [
  { value: '"Comic Sans MS", cursive', label: 'Comic Sans', scale: 62, offset: 0 },
  { value: 'Georgia, serif', label: 'Georgia', scale: 62, offset: 0 },
  { value: 'Arial, sans-serif', label: 'Arial', scale: 62, offset: 0 },
  { value: '"Times New Roman", serif', label: 'Times New Roman', scale: 62, offset: 0 },
  { value: '"Segoe Print", cursive', label: '手写体', scale: 62, offset: 0 },
  { value: '"Courier New", monospace', label: 'Courier New', scale: 62, offset: 0 },
];

/** 粗细选项 */
export const FONT_WEIGHTS = [
  { value: 'normal', label: '常规' },
  { value: 'bold', label: '加粗' },
] as const;

/** 段落标点（中文断句用） */
export const CN_PUNCT = /[。，、；：！？）】》」』）"'…—·]/;