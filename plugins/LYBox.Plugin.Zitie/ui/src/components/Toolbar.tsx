/**
 * 顶部工具栏：打印 / 导出 / 缩放
 */
import { defineComponent, type PropType } from 'vue';
import { resetState, state } from '@/core/store';

export interface PreviewHandle {
  getCanvases(): HTMLCanvasElement[];
  rerender(): Promise<void>;
}

export const Toolbar = defineComponent({
  name: 'Toolbar',
  props: {
    preview: {
      type: Object as PropType<PreviewHandle | null>,
      default: null,
    },
    onPrint: { type: Function as PropType<() => void>, required: true },
    onPng: { type: Function as PropType<() => void>, required: true },
    onPdf: { type: Function as PropType<() => void>, required: true },
    onPdfPreview: { type: Function as PropType<() => void>, required: true },
  },
  setup(props) {
    const zoomIn = () => {
      state.zoom = Math.min(2, Math.round((state.zoom + 0.1) * 10) / 10);
    };
    const zoomOut = () => {
      state.zoom = Math.max(0.25, Math.round((state.zoom - 0.1) * 10) / 10);
    };
    const onReset = () => resetState();

    return () => (
      <div class="toolbar">
        <button class="btn primary" type="button" onClick={props.onPrint}>
          打 印
        </button>
        <button class="btn" type="button" title="实时生成 PDF 并预览，不下载" onClick={props.onPdfPreview}>
          PDF 预览
        </button>
        <button class="btn" type="button" onClick={props.onPdf}>
          导出 PDF
        </button>
        <button class="btn" type="button" onClick={props.onPng}>
          导出 PNG
        </button>
        <button class="btn" type="button" onClick={onReset}>
          重置
        </button>

        <div class="toolbar-spacer" />

        <button class="btn" type="button" title="缩小" onClick={zoomOut}>
          −
        </button>
        <span class="zoom-label">{Math.round(state.zoom * 100)}%</span>
        <button class="btn" type="button" title="放大" onClick={zoomIn}>
          ＋
        </button>
      </div>
    );
  },
});