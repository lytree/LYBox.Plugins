/**
 * 全局响应式状态 —— 基于 Vue reactive
 * 单一数据源，所有组件共享
 */
import { computed, reactive } from 'vue';
import type { CellType, RenderOptions, ZitieState, ZitieType } from '@/types';
import { CN_FONTS, EN_FONTS, MM, PAPERS, TYPE_MAP } from './config';
import { loadStrokesBatch } from './stroke-data';

/** 创建初始状态 */
export function createInitialState(): ZitieState {
  return {
    type: 'hanzi',
    text: '',
    paper: 'A4',
    padding: [36, 36, 36, 36],
    cellSize: 10 * MM,
    cellType: 'tzg',
    rowGap: 2 * MM,
    insertBlankRow: false,
    insertBlankCol: false,
    blankRows: 0,
    blankCols: 0,
    traceCount: 20,
    firstCharHighlight: false,
    showBishun: false,
    showPinyin: false,
    showPageNumber: true,
    traceColor: '#cccccc',
    lineColor: '#4a4a4a',
    textColor: '#1a1a1a',
    pinyinColor: '#4a6fa5',
    fontFamily: CN_FONTS[0]!.value,
    fontWeight: 'normal',
    fontScale: 68,
    fontOffset: 0,
    zoom: 0.75,
  };
}

/** 全局单例状态 */
export const state: ZitieState = reactive(createInitialState());

/** 笔顺数据缓存（不放入 reactive，避免深层响应式开销） */
let bishunCache: Record<string, string[]> = {};

/** 当前字帖类型定义 */
export const currentType = computed(() => TYPE_MAP[state.type]);

/** 是否英文体系 */
export const isEnglish = computed(() => currentType.value.en);

/** 可选字体列表 */
export const fontOptions = computed(() => (isEnglish.value ? EN_FONTS : CN_FONTS));

/** 当前纸张尺寸 */
export const paperSize = computed(() => PAPERS[state.paper]);

/**
 * 每页最大列数 —— 描红数量上限
 */
export const maxCols = computed(() => {
  const p = paperSize.value;
  const [, pr, , pl] = state.padding;
  return Math.max(1, Math.floor((p.width - pr - pl) / state.cellSize));
});

/** 当前描红值（钳制到合法范围） */
export const safeTraceCount = computed(() =>
  Math.min(state.traceCount, maxCols.value)
);

/**
 * 构造渲染参数
 */
export function buildRenderOptions(bishun: Record<string, string[]>): RenderOptions {
  return {
    pageW: paperSize.value.width,
    pageH: paperSize.value.height,
    padding: state.padding,
    cellSize: state.cellSize,
    cellType: state.cellType,
    rowGap: state.rowGap,
    blankRows: state.blankRows,
    blankCols: state.blankCols,
    traceCount: safeTraceCount.value,
    firstCharHighlight: state.firstCharHighlight,
    showBishun: state.showBishun,
    showPinyin: state.showPinyin,
    showPageNumber: state.showPageNumber,
    traceColor: state.traceColor,
    lineColor: state.lineColor,
    textColor: state.textColor,
    pinyinColor: state.pinyinColor,
    // 切换类型时若字体不属于当前体系则回退到首个
    fontFamily: fontOptions.value.some((f) => f.value === state.fontFamily)
      ? state.fontFamily
      : fontOptions.value[0]!.value,
    fontWeight: state.fontWeight,
    fontScale: state.fontScale,
    fontOffset: state.fontOffset,
    bishunData: bishun,
  };
}

/** 读取笔顺缓存 */
export function getBishun(): Record<string, string[]> {
  return bishunCache;
}

/**
 * 按需加载笔顺数据
 */
export async function ensureBishun(chars: readonly string[]): Promise<void> {
  const needCn = ['hanzi', 'cizu', 'duanluo', 'bihua'].includes(state.type);
  if (!needCn) {
    bishunCache = {};
    return;
  }
  const loaded = await loadStrokesBatch(chars);
  bishunCache = loaded;
}

/**
 * 切换字帖类型：重置字体 / 格子 / 字号
 */
export function switchType(type: ZitieType): void {
  const def = TYPE_MAP[type];
  state.type = type;
  state.text = '';
  const en = def.en;
  state.cellType = (en ? 'sxg' : 'tzg') as CellType;
  state.fontFamily = (en ? EN_FONTS : CN_FONTS)[0]!.value;
  state.fontScale = en ? 62 : 68;
  bishunCache = {};
}

/**
 * 重置为初始状态（保留字帖类型）
 */
export function resetState(): void {
  const type = state.type;
  const initial = createInitialState();
  Object.assign(state, initial, { type });
  bishunCache = {};
}

/** 毫米 <-> 像素换算 */
export const mmToPx = (mm: number): number => mm * MM;
export const pxToMm = (px: number): number => px / MM;