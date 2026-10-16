/**
 * 导出：PNG / PDF / 打印
 * jsPDF 采用按需动态加载，避免首屏加载 ~365KB
 *
 * WebView 环境下统一走宿主原生能力（见 native-bridge.ts）：
 * 内嵌浏览器没有可靠的 blob 下载落地路径，a[download] 常静默失败。
 */
import { hasNativeBridge, openPath, readTextFile, saveBytes, savePngPages } from './native-bridge';

type JsPdfDoc = {
  addPage(format?: unknown, orientation?: string): void;
  addImage(data: string, format: string, x: number, y: number, w: number, h: number): void;
  save(filename: string): void;
  /** output('blob') 返回 Blob；其余类型返回字符串 */
  output(type: 'blob'): Blob;
  output(type?: string): string;
  getNumberOfPages(): number;
};

type JsPdfCtor = new (opts?: Record<string, unknown>) => JsPdfDoc;

let jsPdfPromise: Promise<JsPdfCtor | null> | null = null;

/** 懒加载 jsPDF（UMD 全局） */
async function loadJsPdf(): Promise<JsPdfCtor | null> {
  if (jsPdfPromise) return jsPdfPromise;

  jsPdfPromise = new Promise((resolve) => {
    const readGlobal = (): JsPdfCtor | null =>
      (window as unknown as { jspdf?: { jsPDF?: JsPdfCtor } }).jspdf?.jsPDF ?? null;

    // 已被静态引入
    const existing = readGlobal();
    if (existing) {
      resolve(existing);
      return;
    }

    // 动态注入 script（jsPDF 位于 public/vendor）
    const script = document.createElement('script');
    script.src = new URL('vendor/jspdf.js', document.baseURI).href;
    script.onload = () => resolve(readGlobal());
    script.onerror = () => resolve(null);
    document.head.appendChild(script);
  });

  return jsPdfPromise;
}

/** 触发浏览器下载 */
export function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

/** 导出每页为 PNG。WebView 下走宿主保存对话框，返回成功张数。 */
export async function exportPNG(
  canvases: readonly HTMLCanvasElement[],
  tag: string
): Promise<number> {
  if (!canvases.length) return 0;

  const items = await Promise.all(
    canvases.map(async (cv, i) => ({
      blob: await canvasToBlob(cv),
      fileName: `字帖_${tag}_${i + 1}.png`,
    }))
  );

  if (hasNativeBridge()) {
    return savePngPages(items);
  }

  // 纯浏览器：逐张触发下载
  items.forEach((item) => downloadBlob(item.blob, item.fileName));
  return items.length;
}

/** 导出多页 PDF。WebView 下走宿主保存对话框。 */
export async function exportPDF(
  canvases: readonly HTMLCanvasElement[],
  tag: string
): Promise<boolean> {
  const pdf = await buildPdfDoc(canvases);
  if (!pdf) return false;

  const filename = `字帖_${tag}.pdf`;

  if (hasNativeBridge()) {
    const path = await saveBytes(pdf.output('blob'), filename, 'PDF 文件', ['pdf']);
    return path !== null;
  }

  pdf.save(filename);
  return true;
}

/**
 * 打印：Avalonia 无跨平台打印对话框，故生成 PDF 后用系统默认程序打开，
 * 由用户在阅读器中打印。非 WebView 环境降级为浏览器打印。
 */
export async function printPages(
  canvases: readonly HTMLCanvasElement[]
): Promise<'opened' | 'cancelled' | 'unsupported'> {
  if (!hasNativeBridge()) {
    window.print();
    return 'opened';
  }

  const pdf = await buildPdfDoc(canvases);
  if (!pdf) return 'unsupported';

  // 打印路径不询问保存位置：落到系统临时目录再交给默认程序，
  // 用户在阅读器中打印即可，避免多一次无谓的对话框。
  const path = await saveBytes(
    pdf.output('blob'),
    `字帖打印_${Date.now()}.pdf`,
    'PDF 文件',
    ['pdf']
  );
  if (!path) return 'cancelled';

  return (await openPath(path)) ? 'opened' : 'unsupported';
}

/** canvas → PNG Blob（WebView 下 PNG 导出依赖宿主写盘，需要真实 Blob 而非 data URL）。 */
function canvasToBlob(cv: HTMLCanvasElement): Promise<Blob> {
  return new Promise((resolve, reject) => {
    cv.toBlob((blob) => {
      if (blob) resolve(blob);
      else reject(new Error('canvas 转 PNG 失败'));
    }, 'image/png');
  });
}

export { readTextFile };

/** 构造 PDF 文档对象（预览 / 导出共用） */
async function buildPdfDoc(
  canvases: readonly HTMLCanvasElement[]
): Promise<JsPdfDoc | null> {
  const JsPDF = await loadJsPdf();
  if (!JsPDF || !canvases.length) return null;

  const first = canvases[0]!;
  const orientation = first.width > first.height ? 'landscape' : 'portrait';
  const pdf = new JsPDF({
    orientation,
    unit: 'pt',
    format: [first.width, first.height],
    compress: true,
  });

  canvases.forEach((cv, i) => {
    if (i > 0) {
      pdf.addPage(
        [cv.width, cv.height],
        cv.width > cv.height ? 'landscape' : 'portrait'
      );
    }
    pdf.addImage(cv.toDataURL('image/png'), 'PNG', 0, 0, cv.width, cv.height);
  });

  return pdf;
}

/** PDF 预览数据：Blob + 文件名 + 页数 */
export interface PdfPreviewData {
  blob: Blob;
  filename: string;
  pageCount: number;
}

/** 实时生成 PDF Blob（供预览弹窗使用） */
export async function buildPdfBlob(
  canvases: readonly HTMLCanvasElement[],
  tag: string
): Promise<PdfPreviewData | null> {
  const pdf = await buildPdfDoc(canvases);
  if (!pdf) return null;
  return {
    blob: pdf.output('blob'),
    filename: `字帖_${tag}.pdf`,
    pageCount: pdf.getNumberOfPages(),
  };
}