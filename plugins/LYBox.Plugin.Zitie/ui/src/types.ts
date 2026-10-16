/** 字帖生成器 —— 全局类型定义 */

/** 字帖类型标识 */
export type ZitieType =
  | 'hanzi'    // 汉字
  | 'cizu'     // 词组
  | 'duanluo'  // 段落
  | 'bihua'    // 笔画
  | 'alphabet' // 字母
  | 'word'     // 单词
  | 'paragraph'// 英文段落
  | 'number'   // 数字
  | 'pinyin'   // 拼音
  | 'pen';     // 控笔

/** 中文格子类型 */
export type CnCellType = 'zwg' | 'tzg' | 'mzg' | 'hgg' | 'jgg' | 'htg' | 'hmg';
/** 英文格子类型 */
export type EnCellType = 'sxg' | 'lxg' | 'dxg';
/** 统一格子类型 */
export type CellType = CnCellType | EnCellType;

export type PaperName = 'A4' | 'A5' | 'B5' | 'Letter';
export type FontWeight = 'normal' | 'bold';

/** 选项项通用结构 */
export interface Option<T extends string = string> {
  value: T;
  label: string;
}

/** 纸张尺寸（单位 px） */
export interface PaperSize {
  width: number;
  height: number;
}

/** 字体定义 */
export interface FontDef {
  value: string;
  label: string;
  /** 默认缩放百分比 */
  scale?: number;
  /** 默认上下偏移百分比 */
  offset?: number;
}

/** 应用状态（全部为响应式字段） */
export interface ZitieState {
  /** 当前字帖类型 */
  type: ZitieType;
  /** 输入文本 */
  text: string;
  /** 纸张 */
  paper: PaperName;
  /** 页边距 [上, 右, 下, 左] */
  padding: [number, number, number, number];
  /** 方格边长（px） */
  cellSize: number;
  /** 格子类型 */
  cellType: CellType;
  /** 行间距（px） */
  rowGap: number;
  /** 插入空行开关 -> 数量 */
  insertBlankRow: boolean;
  /** 插入空列开关 -> 数量 */
  insertBlankCol: boolean;
  /** 实际空行数（由开关派生） */
  blankRows: number;
  /** 实际空列数（由开关派生） */
  blankCols: number;
  /** 描红数量（范字格数，上限 = 每行列数） */
  traceCount: number;
  /** 首字高亮（每行前留空列） */
  firstCharHighlight: boolean;
  /** 显示笔顺 */
  showBishun: boolean;
  /** 显示拼音 */
  showPinyin: boolean;
  /** 显示页码 */
  showPageNumber: boolean;
  /** 描红（范字）颜色 */
  traceColor: string;
  /** 线条颜色 */
  lineColor: string;
  /** 正文字色 */
  textColor: string;
  /** 拼音颜色 */
  pinyinColor: string;
  /** 字体族 */
  fontFamily: string;
  /** 粗细 */
  fontWeight: FontWeight;
  /** 字号缩放（%） */
  fontScale: number;
  /** 上下偏移（%） */
  fontOffset: number;
  /** 预览缩放 */
  zoom: number;
}

/** 渲染器完整参数 */
export interface RenderOptions {
  pageW: number;
  pageH: number;
  padding: [number, number, number, number];
  cellSize: number;
  cellType: CellType;
  rowGap: number;
  blankRows: number;
  blankCols: number;
  traceCount: number;
  firstCharHighlight: boolean;
  showBishun: boolean;
  showPinyin: boolean;
  showPageNumber: boolean;
  traceColor: string;
  lineColor: string;
  textColor: string;
  pinyinColor: string;
  fontFamily: string;
  fontWeight: FontWeight;
  fontScale: number;
  fontOffset: number;
  /** 字 -> 笔顺路径数组 */
  bishunData: Record<string, string[]>;
}

/** 布局计算结果 */
export interface LayoutResult {
  /** 笔顺区高度 */
  bishunH: number;
  /** 拼音区高度 */
  pinyinH: number;
  /** 单行总高 */
  rowH: number;
  /** 每页行数 */
  rows: number;
  /** 每行列数 */
  cols: number;
  /** 可用内容宽 */
  contentW: number;
  /** 可用内容高 */
  contentH: number;
  /** 网格总宽 */
  gridW: number;
  /** 网格总高 */
  gridH: number;
  /** 起始 X */
  startX: number;
  /** 起始 Y */
  startY: number;
  /** 格子边长 */
  cell: number;
  /** 行间距 */
  gap: number;
}

/** 内容矩阵：二维字符数组，'' 表示空位 */
export type Matrix = string[][];

/** 笔画信息 */
export interface BihuaInfo {
  name: string;
  char: string;
  type: string;
}

/** 控笔轨迹模板 */
export interface PenPattern {
  name: string;
  path?: [number, number][];
  wave?: 'y' | 'x';
  spiral?: boolean;
  zigzag?: boolean;
}