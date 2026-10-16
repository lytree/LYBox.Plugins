/**
 * URL 参数预填 —— 便于分享与批量导出
 * 例：?type=hanzi&text=永和字&cell=mzg&size=12&trace=5&pinyin=1
 */
import { mmToPx, state, switchType } from './store';
import { ZITIE_TYPES } from './config';
import type { CellType, PaperName, ZitieType } from '@/types';

const TYPE_KEYS = new Set(ZITIE_TYPES.map((t) => t.key));

export function applyUrlParams(): void {
  if (typeof window === 'undefined') return;
  const q = new URLSearchParams(window.location.search);

  // 字帖类型
  const type = q.get('type');
  if (type && TYPE_KEYS.has(type as ZitieType)) {
    switchType(type as ZitieType);
  }

  // 输入内容
  const text = q.get('text');
  if (text) state.text = text;

  // 格子
  const cell = q.get('cell');
  if (cell) state.cellType = cell as CellType;

  // 尺寸（mm）
  const size = q.get('size');
  if (size) state.cellSize = mmToPx(parseFloat(size) || 10);

  const gap = q.get('gap');
  if (gap) state.rowGap = mmToPx(parseFloat(gap) || 2);

  // 描红
  const trace = q.get('trace');
  if (trace !== null) state.traceCount = parseInt(trace, 10) || 0;

  // 字体参数
  const scale = q.get('scale');
  if (scale) state.fontScale = parseInt(scale, 10) || 68;

  const offset = q.get('offset');
  if (offset) state.fontOffset = parseInt(offset, 10) || 0;

  const font = q.get('font');
  if (font) state.fontFamily = font;

  // 布尔开关
  const flags: Array<[string, (v: boolean) => void]> = [
    ['bishun', (v) => (state.showBishun = v)],
    ['pinyin', (v) => (state.showPinyin = v)],
    ['highlight', (v) => (state.firstCharHighlight = v)],
    ['pagenum', (v) => (state.showPageNumber = v)],
    ['brow', (v) => {
      state.insertBlankRow = v;
      state.blankRows = v ? 1 : 0;
    }],
    ['bcol', (v) => {
      state.insertBlankCol = v;
      state.blankCols = v ? 1 : 0;
    }],
  ];
  flags.forEach(([key, setter]) => {
    if (q.has(key)) setter(q.get(key) === '1');
  });

  // 纸张
  const paper = q.get('paper');
  if (paper) state.paper = paper as PaperName;

  // 预览缩放
  const zoom = q.get('zoom');
  if (zoom) state.zoom = parseFloat(zoom) || 0.75;
}