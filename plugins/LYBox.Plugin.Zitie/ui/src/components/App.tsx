/**
 * 应用根组件
 */
import { defineComponent, ref, onMounted } from 'vue';
import { Sidebar } from './Sidebar';
import { Toolbar, type PreviewHandle } from './Toolbar';
import { PagePreview } from './PagePreview';
import { PdfPreviewModal, type PdfPreviewHandle } from './PdfPreviewModal';
import { state } from '@/core/store';
import { exportPDF, exportPNG, printPages } from '@/core/exporter';
import { applyUrlParams } from '@/core/url-params';

export const App = defineComponent({
  name: 'App',
  setup() {
    const previewRef = ref<PreviewHandle | null>(null);
    const pdfModalRef = ref<PdfPreviewHandle | null>(null);
    const toastMsg = ref('');

    let toastTimer: ReturnType<typeof setTimeout> | null = null;
    function toast(msg: string): void {
      toastMsg.value = msg;
      if (toastTimer) clearTimeout(toastTimer);
      toastTimer = setTimeout(() => (toastMsg.value = ''), 2400);
    }

    async function handlePrint(): Promise<void> {
      const list = previewRef.value?.getCanvases() ?? [];
      if (!list.length) {
        toast('暂无内容');
        return;
      }
      try {
        const result = await printPages(list);
        if (result === 'opened') toast('已在阅读器中打开 PDF，请用阅读器打印');
        else if (result === 'cancelled') toast('已取消打印');
        else toast('PDF 生成失败，请改用「导出 PDF」');
      } catch {
        toast('打印失败，请改用「导出 PDF」');
      }
    }

    async function handlePng(): Promise<void> {
      const list = previewRef.value?.getCanvases() ?? [];
      if (!list.length) {
        toast('暂无内容');
        return;
      }
      try {
        const saved = await exportPNG(list, state.type);
        toast(saved > 0 ? `已导出 ${saved} 张 PNG` : '已取消导出');
      } catch {
        toast('PNG 导出失败');
      }
    }

    async function handlePdf(): Promise<void> {
      const list = previewRef.value?.getCanvases() ?? [];
      if (!list.length) {
        toast('暂无内容');
        return;
      }
      toast('正在生成 PDF...');
      try {
        const ok = await exportPDF(list, state.type);
        toast(ok ? 'PDF 已导出' : '已取消导出');
      } catch {
        toast('PDF 导出失败，请改用「打印」');
      }
    }

    /** 实时渲染 PDF 并直接打开预览（不触发下载） */
    async function handlePdfPreview(): Promise<void> {
      const list = previewRef.value?.getCanvases() ?? [];
      if (!list.length) {
        toast('暂无内容');
        return;
      }
      await pdfModalRef.value?.open(list, state.type);
    }

    onMounted(() => {
      applyUrlParams();
    });

    return () => (
      <div class="app">
        <Sidebar onToast={toast} />
        <main class="main">
          <Toolbar
            preview={previewRef.value}
            onPrint={() => void handlePrint()}
            onPng={() => void handlePng()}
            onPdf={() => void handlePdf()}
            onPdfPreview={() => void handlePdfPreview()}
          />
          <PagePreview ref={previewRef} />
          <PdfPreviewModal ref={pdfModalRef} />
        </main>

        {toastMsg.value && <div class="toast">{toastMsg.value}</div>}
      </div>
    );
  },
});