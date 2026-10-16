/**
 * PDF 预览弹窗
 * 点击「导出 PDF」后实时生成 PDF Blob，通过 iframe 调用浏览器内置 PDF 查看器预览，
 * 确认无误后再下载。全程离线，无额外依赖。
 */
import { defineComponent, onBeforeUnmount, ref } from 'vue';
import { buildPdfBlob, downloadBlob } from '@/core/exporter';

export interface PdfPreviewHandle {
  open(canvases: HTMLCanvasElement[], tag: string): void;
}

/** 文件体积格式化 */
function formatSize(bytes: number): string {
  if (bytes < 1024 * 1024) return `${Math.max(1, Math.round(bytes / 1024))} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

export const PdfPreviewModal = defineComponent({
  name: 'PdfPreviewModal',
  setup(_, { expose }) {
    const visible = ref(false);
    const loading = ref(false);
    const errorMsg = ref('');
    const pdfUrl = ref('');
    const meta = ref('');
    const filename = ref('字帖.pdf');

    /** 当前 PDF Blob（下载用） */
    let currentBlob: Blob | null = null;

    /** 释放 blob URL 与引用 */
    function cleanup(): void {
      if (pdfUrl.value) {
        URL.revokeObjectURL(pdfUrl.value);
        pdfUrl.value = '';
      }
      currentBlob = null;
    }

    async function open(canvases: HTMLCanvasElement[], tag: string): Promise<void> {
      cleanup();
      visible.value = true;
      loading.value = true;
      errorMsg.value = '';
      meta.value = '';
      filename.value = `字帖_${tag}.pdf`;
      window.addEventListener('keydown', onKeydown);

      try {
        const data = await buildPdfBlob(canvases, tag);
        // 生成期间已被关闭 -> 丢弃结果
        if (!visible.value) return;
        if (!data) {
          errorMsg.value = 'PDF 库加载失败，可改用「打印 → 另存为 PDF」';
          return;
        }
        currentBlob = data.blob;
        meta.value = `共 ${data.pageCount} 页 · ${formatSize(data.blob.size)}`;
        pdfUrl.value = URL.createObjectURL(data.blob);
      } catch {
        if (!visible.value) return;
        errorMsg.value = '生成失败，可改用「打印 → 另存为 PDF」';
      } finally {
        if (visible.value) loading.value = false;
      }
    }

    function onKeydown(e: KeyboardEvent): void {
      if (e.key === 'Escape') close();
    }

    function close(): void {
      visible.value = false;
      loading.value = false;
      errorMsg.value = '';
      window.removeEventListener('keydown', onKeydown);
      cleanup();
    }

    function download(): void {
      if (currentBlob) downloadBlob(currentBlob, filename.value);
    }

    onBeforeUnmount(() => {
      window.removeEventListener('keydown', onKeydown);
      cleanup();
    });

    expose({ open });

    return () => {
      if (!visible.value) return null;
      return (
        <div
          class="pdf-mask"
          onClick={(e: MouseEvent) => {
            if (e.target === e.currentTarget) close();
          }}
        >
          <div class="pdf-dialog" role="dialog" aria-label="PDF 预览">
            <div class="pdf-head">
              <span class="pdf-title">PDF 预览</span>
              <span class="pdf-meta">{meta.value}</span>
              <button class="pdf-close" type="button" title="关闭" onClick={close}>
                ✕
              </button>
            </div>

            <div class="pdf-body">
              {pdfUrl.value && (
                <iframe class="pdf-frame" src={pdfUrl.value} title="PDF 预览" />
              )}
              {(loading.value || errorMsg.value) && (
                <div class="pdf-tip">
                  {loading.value ? (
                    <>
                      <div class="pdf-spinner" />
                      <span>正在实时生成 PDF...</span>
                    </>
                  ) : (
                    <span>{errorMsg.value}</span>
                  )}
                </div>
              )}
            </div>

            <div class="pdf-foot">
              <span class="pdf-filename">{filename.value}</span>
              <div class="toolbar-spacer" />
              <button class="btn" type="button" onClick={close}>
                关 闭
              </button>
              <button
                class="btn primary"
                type="button"
                disabled={loading.value || !currentBlob}
                onClick={download}
              >
                下载 PDF
              </button>
            </div>
          </div>
        </div>
      );
    };
  },
});
