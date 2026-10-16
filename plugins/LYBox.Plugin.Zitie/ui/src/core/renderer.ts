/**
 * 字帖渲染核心
 * 职责：布局计算 -> 内容折行 -> 逐格绘制（描红 / 笔顺 / 拼音）
 */
import { drawCell, isHanzi } from './cells';
import type { LayoutResult, Matrix, RenderOptions } from '@/types';
import { getPinyin } from './pinyin';

/** 颜色线性插值：t=0 取 from，t=1 取 to */
function mixColor(from: string, to: string, t: number): string {
  const parse = (hex: string): [number, number, number] => {
    const s = hex.replace('#', '');
    return [
      parseInt(s.slice(0, 2), 16),
      parseInt(s.slice(2, 4), 16),
      parseInt(s.slice(4, 6), 16),
    ];
  };
  const [r1, g1, b1] = parse(from);
  const [r2, g2, b2] = parse(to);
  const c = (a: number, b: number): number => Math.round(a + (b - a) * t);
  return `rgb(${c(r1, r2)},${c(g1, g2)},${c(b1, b2)})`;
}

export class ZitieRenderer {
  /** 总页数（用于页码显示） */
  totalPages = 1;

  constructor(private readonly o: RenderOptions) {}

  /** 计算页面布局 */
  layout(): LayoutResult {
    const o = this.o;
    const [pt, pr, pb, pl] = o.padding;
    const cell = o.cellSize;
    const gap = o.rowGap;

    // 笔顺区与拼音区各占 0.6 格高度
    const bishunH = o.showBishun ? Math.floor(0.6 * cell) : 0;
    const pinyinH = o.showPinyin ? Math.floor(0.6 * cell) : 0;
    const rowH = cell + bishunH + pinyinH;

    const contentW = o.pageW - pl - pr;
    const contentH = o.pageH - pt - pb;

    const rows = Math.max(1, Math.floor((contentH + gap) / (rowH + gap)));
    const cols = Math.max(1, Math.floor(contentW / cell));

    const gridW = cell * cols;
    const gridH = (rowH + gap) * rows - gap;

    // 网格在可用区域内水平/垂直居中
    const startX = Math.floor(pl + (contentW - gridW) / 2);
    const startY = Math.floor(pt + (contentH - gridH) / 2);

    return {
      bishunH, pinyinH, rowH, rows, cols,
      contentW, contentH, gridW, gridH,
      startX, startY, cell, gap,
    };
  }

  /**
   * 构建内容矩阵
   * - 长文本按每行列数自动折行
   * - 支持插入空行 / 空列
   */
  buildMatrix(lines: readonly string[]): Matrix {
    const o = this.o;
    const L = this.layout();
    const matrix: Matrix = [];

    for (const line of lines) {
      const chars = [...line];

      for (let i = 0; i < chars.length; i += L.cols) {
        const seg = chars.slice(i, i + L.cols);

        // 插入空行
        for (let k = 0; k < o.blankRows; k++) matrix.push([]);

        const cells: string[] = [];
        // 首字高亮：行首留出空列作为起始空白
        if (o.firstCharHighlight) {
          for (let k = 0; k < o.blankCols; k++) cells.push('');
        }
        for (const ch of seg) {
          cells.push(ch);
          // 每字后插入空列
          for (let k = 0; k < o.blankCols; k++) cells.push('');
        }
        matrix.push(cells);
      }
    }
    return matrix;
  }

  /** 渲染整页 */
  renderPage(
    ctx: CanvasRenderingContext2D,
    matrix: Matrix,
    L: LayoutResult,
    pageIndex = 0
  ): void {
    const o = this.o;
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, o.pageW, o.pageH);

    matrix.forEach((row, r) => {
      if (r >= L.rows) return;
      const rowTop = L.startY + r * (L.rowH + L.gap);

      row.forEach((ch, c) => {
        if (c >= L.cols || ch === '') return;

        const x = L.startX + c * L.cell;
        // 行内三段布局：[笔顺区][拼音区][方格区]
        const bishunTop = rowTop;
        const pinyinTop = bishunTop + L.bishunH;
        const cellTop = pinyinTop + L.pinyinH;

        // 范字判定：仅第一行前 N 格（按坐标，避免重复字符误判）
        const isModel = r === 0 && c < o.traceCount;

        if (o.showBishun) {
          this.drawStrokePreview(ctx, x, bishunTop, ch, L.bishunH);
        }
        if (o.showPinyin && isHanzi(ch)) {
          this.drawPinyin(ctx, x, pinyinTop, ch, L.pinyinH);
        }

        drawCell(ctx, x, cellTop, L.cell, L.cell, o.cellType, o.lineColor);
        this.drawChar(ctx, x, cellTop, ch, L.cell, isModel);
      });
    });

    if (pageIndex >= 0 && o.showPageNumber && this.totalPages > 1) {
      this.drawPageNumber(ctx, pageIndex);
    }
  }

  /**
   * 绘制字符
   * @param y 格子顶部 Y
   * @param isModel 是否为描红范字
   */
  private drawChar(
    ctx: CanvasRenderingContext2D,
    x: number,
    y: number,
    ch: string,
    size: number,
    isModel: boolean
  ): void {
    const o = this.o;
    const fontSize = size * (o.fontScale / 100);
    const fontStr = `${o.fontWeight === 'bold' ? 'bold ' : ''}${fontSize}px ${o.fontFamily}`;

    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.font = fontStr;

    const cx = x + size / 2;
    // 文字垂直居中于格子 + 用户偏移
    const offsetY = y + size / 2 + (size * o.fontOffset) / 100;

    // 笔顺模式：用 SVG 路径精确还原字形
    if (o.showBishun && o.bishunData[ch]) {
      this.drawCharByStrokes(ctx, x, y, ch, size);
      return;
    }

    ctx.fillStyle = isModel && o.traceCount > 0 ? o.traceColor : o.textColor;
    ctx.fillText(ch, cx, offsetY);
  }

  /** 用笔顺 SVG 路径绘制字符（1024 基准，放大 1.14 倍填满格子） */
  private drawCharByStrokes(
    ctx: CanvasRenderingContext2D,
    x: number,
    y: number,
    ch: string,
    size: number
  ): void {
    const data = this.o.bishunData[ch];
    if (!data?.length) return;

    const FILL = 1.14;
    const inner = size * FILL;
    const scale = inner / 1024;
    const offX = x + (size - inner) / 2;
    const offY = y + (size - inner) / 2 + (size * this.o.fontOffset) / 100;

    ctx.save();
    ctx.translate(offX, offY);
    ctx.scale(scale, scale);
    ctx.fillStyle = this.o.textColor;
    for (const d of data) {
      try {
        ctx.fill(new Path2D(d));
      } catch {
        /* 路径异常时跳过该笔 */
      }
    }
    ctx.restore();
  }

  /** 笔顺首笔提示（画在格子上方的笔顺区） */
  private drawStrokePreview(
    ctx: CanvasRenderingContext2D,
    x: number,
    y: number,
    ch: string,
    h: number
  ): void {
    const data = this.o.bishunData[ch];
    if (!data?.length || h <= 0) return;

    const scale = h / 1024;
    ctx.save();
    ctx.translate(x, y + (h - 1024 * scale) / 2);
    ctx.scale(scale, scale);
    ctx.fillStyle = this.o.traceColor;
    try {
      ctx.fill(new Path2D(data[0]));
    } catch {
      /* ignore */
    }
    ctx.restore();
  }

  /** 拼音标注（绘制在格子上方拼音区内） */
  private drawPinyin(
    ctx: CanvasRenderingContext2D,
    x: number,
    y: number,
    ch: string,
    h: number
  ): void {
    const py = getPinyin(ch);
    if (!py || h <= 0) return;

    const o = this.o;
    ctx.save();
    ctx.fillStyle = o.pinyinColor;
    ctx.font = `${h * 0.86}px "Times New Roman", Georgia, "SimSun", serif`;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(py, x + o.cellSize / 2, y + h / 2);
    ctx.restore();
  }

  /** 页码 */
  private drawPageNumber(ctx: CanvasRenderingContext2D, idx: number): void {
    const o = this.o;
    ctx.save();
    ctx.fillStyle = '#888888';
    ctx.font = '12px sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText(`${idx + 1} / ${this.totalPages}`, o.pageW / 2, o.pageH - 14);
    ctx.restore();
  }
}

/**
 * 把矩阵按每页容量切片
 */
export function paginate(matrix: Matrix, perPage: number): Matrix[] {
  if (!matrix.length) return [[]];
  const pages: Matrix[] = [];
  for (let i = 0; i < matrix.length; i += perPage) {
    pages.push(matrix.slice(i, i + perPage));
  }
  return pages;
}

/**
 * 生成渲染器与分页结果
 */
export function buildPages(
  opts: RenderOptions,
  lines: readonly string[]
): { renderer: ZitieRenderer; pages: Matrix[] } {
  const renderer = new ZitieRenderer(opts);
  const L = renderer.layout();
  const matrix = renderer.buildMatrix(lines);
  // 每页容量为「行数」：matrix 按行组织，renderPage 每页仅绘制前 L.rows 行
  const pages = paginate(matrix, L.rows);
  renderer.totalPages = pages.length;
  return { renderer, pages };
}

export { mixColor };