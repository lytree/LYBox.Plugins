/**
 * 宿主原生能力桥接。
 *
 * WebView2 / WPE WebKit 内嵌浏览器没有可靠的 blob 下载落地路径
 * （<a download> 常静默失败），<input type="file"> 的选择器也不受宿主控制。
 * 故导出 / 导入 / 打印统一走宿主 RPC：
 *   - 导出 PDF / PNG → 原生保存对话框 + 宿主写盘
 *   - 导入 TXT      → 原生打开对话框 + 宿主读盘（UTF-8 优先，回退 GBK）
 *   - 打印          → 生成 PDF 后用系统默认程序打开（由用户在阅读器中打印）
 *
 * 非 WebView 环境（纯浏览器直接开 dist/）自动降级为原有浏览器行为，
 * 保证 zitie 作为独立 Web 应用仍可用。
 */
import { invoke, isWebView } from '@/lybox';

// ==================== 与 C# DTO 对齐的类型 ====================

/** 对应 Rpc/ZitieCommands.cs 的 SaveBytesRequest */
interface SaveBytesPayload {
  fileName: string;
  base64: string;
  filterName?: string;
  extensions?: string[];
}

/** 对应 ReadTextRequest */
interface ReadTextPayload {
  fileName?: string;
  filterName?: string;
  extensions?: string[];
}

/** 对应 ImportedText */
interface ImportedTextResult {
  text: string;
  fileName: string;
}

/** 当前是否具备宿主原生能力（可弹原生对话框、可写盘）。 */
export const hasNativeBridge = (): boolean => isWebView();

// ==================== 导出 ====================

/**
 * 保存二进制内容到磁盘。
 * @returns 实际写入的路径；用户取消返回 null。
 */
export async function saveBytes(
  blob: Blob,
  fileName: string,
  filterName: string,
  extensions: string[]
): Promise<string | null> {
  if (!isWebView()) return null;

  const base64 = await blobToBase64(blob);
  const payload: SaveBytesPayload = { fileName, base64, filterName, extensions };
  return invoke<string | null>('saveBytes', payload);
}

/**
 * 保存多张 PNG（逐张弹对话框）。
 * @returns 成功写入的张数。
 */
export async function savePngPages(
  blobs: readonly { blob: Blob; fileName: string }[]
): Promise<number> {
  let saved = 0;
  for (const item of blobs) {
    const path = await saveBytes(item.blob, item.fileName, 'PNG 图片', ['png']);
    if (path) saved++;
  }
  return saved;
}

/** 用系统默认程序打开路径（打印流程最后一步）。 */
export async function openPath(path: string): Promise<boolean> {
  if (!isWebView()) return false;
  return invoke<boolean>('openPath', { path });
}

// ==================== 导入 ====================

/**
 * 通过原生对话框读取文本文件。
 * @returns 文本与文件名；用户取消返回 null。
 */
export async function readTextFile(): Promise<ImportedTextResult | null> {
  if (!isWebView()) return null;

  const payload: ReadTextPayload = {
    fileName: '导入 TXT',
    filterName: '文本文件',
    extensions: ['txt', 'text'],
  };
  return invoke<ImportedTextResult | null>('readTextFile', payload);
}

// ==================== 辅助 ====================

/** Blob → base64（去掉 data URL 前缀，宿主侧会再兜底剥一次）。 */
function blobToBase64(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(reader.error ?? new Error('读取二进制内容失败'));
    reader.onload = () => {
      const result = reader.result as string;
      const comma = result.indexOf(',');
      resolve(comma >= 0 ? result.slice(comma + 1) : result);
    };
    reader.readAsDataURL(blob);
  });
}
