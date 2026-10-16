/**
 * 方格绘制引擎
 * 7 种中文格子 + 3 种英文格子
 */
import type { CellType, CnCellType, EnCellType, Option } from '@/types';

type Painter = (
  ctx: CanvasRenderingContext2D,
  w: number,
  h: number,
  color: string
) => void;

/** 实线矩形 */
function strokeRect(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  w: number,
  h: number,
  color: string
): void {
  ctx.strokeStyle = color;
  ctx.lineWidth = 1;
  ctx.strokeRect(x + 0.5, y + 0.5, w - 1, h - 1);
}

/** 实线 */
function line(
  ctx: CanvasRenderingContext2D,
  color: string,
  x1: number,
  y1: number,
  x2: number,
  y2: number
): void {
  ctx.strokeStyle = color;
  ctx.lineWidth = 1;
  ctx.beginPath();
  ctx.moveTo(x1, y1);
  ctx.lineTo(x2, y2);
  ctx.stroke();
}

/** 辅助虚线（田字格中心线等） */
function dashed(
  ctx: CanvasRenderingContext2D,
  color: string,
  x1: number,
  y1: number,
  x2: number,
  y2: number
): void {
  ctx.save();
  ctx.strokeStyle = color;
  ctx.lineWidth = 0.6;
  ctx.setLineDash([3, 3]);
  ctx.beginPath();
  ctx.moveTo(x1, y1);
  ctx.lineTo(x2, y2);
  ctx.stroke();
  ctx.restore();
}

/** 田字格：外框 + 中心十字 */
const tzg: Painter = (ctx, s, _h, color) => {
  strokeRect(ctx, 0, 0, s, s, color);
  dashed(ctx, color, s / 2, 0, s / 2, s);
  dashed(ctx, color, 0, s / 2, s, s / 2);
};

/** 米字格：外框 + 十字 + 对角线 */
const mzg: Painter = (ctx, s, _h, color) => {
  strokeRect(ctx, 0, 0, s, s, color);
  dashed(ctx, color, s / 2, 0, s / 2, s);
  dashed(ctx, color, 0, s / 2, s, s / 2);
  dashed(ctx, color, 0, 0, s, s);
  dashed(ctx, color, s, 0, 0, s);
};

/** 作文格：外框 + 实线十字 */
const zwg: Painter = (ctx, s, _h, color) => {
  strokeRect(ctx, 0, 0, s, s, color);
  line(ctx, color, s / 2, 0, s / 2, s);
  line(ctx, color, 0, s / 2, s, s / 2);
};

/** 回宫格：外框 + 内框 */
const hgg: Painter = (ctx, s, _h, color) => {
  strokeRect(ctx, 0, 0, s, s, color);
  const inset = s / 6;
  strokeRect(ctx, inset, inset, s - inset * 2, s - inset * 2, color);
};

/** 九宫格：外框 + 十字 + 上半区四等分虚线 */
const jgg: Painter = (ctx, s, _h, color) => {
  strokeRect(ctx, 0, 0, s, s, color);
  line(ctx, color, s / 2, 0, s / 2, s);
  line(ctx, color, 0, s / 2, s, s / 2);
  const q = s / 4;
  dashed(ctx, color, q, 0, q, s / 2);
  dashed(ctx, color, q * 3, 0, q * 3, s / 2);
  dashed(ctx, color, q, s / 2, q, s);
  dashed(ctx, color, q * 3, s / 2, q * 3, s);
};

/** 回田格：外框 + 内框 + 内框十字 */
const htg: Painter = (ctx, s, _h, color) => {
  strokeRect(ctx, 0, 0, s, s, color);
  const inset = s / 6;
  const inner = s - inset * 2;
  strokeRect(ctx, inset, inset, inner, inner, color);
  dashed(ctx, color, inset + inner / 2, inset, inset + inner / 2, inset + inner);
  dashed(ctx, color, inset, inset + inner / 2, inset + inner, inset + inner / 2);
};

/** 回米格：外框 + 内框 + 内框十字与对角线 */
const hmg: Painter = (ctx, s, _h, color) => {
  strokeRect(ctx, 0, 0, s, s, color);
  const inset = s / 6;
  const inner = s - inset * 2;
  strokeRect(ctx, inset, inset, inner, inner, color);
  const cx = inset + inner / 2;
  const cy = inset + inner / 2;
  dashed(ctx, color, cx, inset, cx, inset + inner);
  dashed(ctx, color, inset, cy, inset + inner, cy);
  dashed(ctx, color, inset, inset, inset + inner, inset + inner);
  dashed(ctx, color, inset + inner, inset, inset, inset + inner);
};

/** 四线格：3 实线 + 2 虚线 */
const sxg: Painter = (ctx, w, h, color) => {
  line(ctx, color, 0, 0, w, 0);
  line(ctx, color, 0, h / 2, w, h / 2);
  line(ctx, color, 0, h, w, h);
  dashed(ctx, color, 0, h / 4, w, h / 4);
  dashed(ctx, color, 0, (h * 3) / 4, w, (h * 3) / 4);
};

/** 两线格：3 实线 */
const lxg: Painter = (ctx, w, h, color) => {
  line(ctx, color, 0, 0, w, 0);
  line(ctx, color, 0, h / 2, w, h / 2);
  line(ctx, color, 0, h, w, h);
};

/** 单线格：1 实线 */
const dxg: Painter = (ctx, w, h, color) => {
  line(ctx, color, 0, h / 2, w, h / 2);
};

const PAINTERS: Record<CellType, Painter> = {
  zwg, tzg, mzg, hgg, jgg, htg, hmg,
  sxg, lxg, dxg,
};

/**
 * 绘制一个格子
 * @param ctx 画布上下文
 * @param x 左上角 X
 * @param y 左上角 Y
 * @param w 宽
 * @param h 高
 * @param type 格子类型
 * @param color 线色
 */
export function drawCell(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  w: number,
  h: number,
  type: CellType,
  color: string
): void {
  const painter = PAINTERS[type] ?? tzg;
  ctx.save();
  ctx.translate(x, y);
  ctx.beginPath();
  ctx.rect(0, 0, w, h);
  ctx.clip();
  painter(ctx, w, h, color);
  ctx.restore();
}

/** 中文格子选项 */
export const CELL_TYPES: Option<CnCellType>[] = [
  { value: 'zwg', label: '作文格' },
  { value: 'tzg', label: '田字格' },
  { value: 'mzg', label: '米字格' },
  { value: 'hgg', label: '回宫格' },
  { value: 'jgg', label: '九宫格' },
  { value: 'htg', label: '回田格' },
  { value: 'hmg', label: '回米格' },
];

/** 英文格子选项 */
export const EN_CELL_TYPES: Option<EnCellType>[] = [
  { value: 'sxg', label: '四线格' },
  { value: 'lxg', label: '两线格' },
  { value: 'dxg', label: '单线格' },
];

/** 判断是否为汉字（含扩展区） */
export function isHanzi(ch: string): boolean {
  return /[\u4e00-\u9fff\u3400-\u4dbf]/.test(ch);
}