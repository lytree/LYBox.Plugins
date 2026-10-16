/**
 * 页面预览区：把渲染结果画到 canvas
 */
import { defineComponent, onMounted, onUpdated, ref, watch } from 'vue';
import { buildPages } from '@/core/renderer';
import { buildRenderOptions, ensureBishun, getBishun, state } from '@/core/store';
import { parseInput } from '@/core/parse';
import type { Matrix } from '@/types';

export const PagePreview = defineComponent({
  name: 'PagePreview',
  setup(_, { expose }) {
    /** 渲染出的 canvas 列表 */
    const canvases = ref<HTMLCanvasElement[]>([]);
    /** 渲染是否进行中 */
    const loading = ref(false);
    /** 供导出使用 */
    let currentCanvases: HTMLCanvasElement[] = [];

    /** 挂载到 DOM 的容器 */
    let container: HTMLDivElement | null = null;

    /**
     * 执行渲染
     */
    async function render(): Promise<void> {
      if (!container) return;
      loading.value = true;

      try {
        // 解析内容
        const lines = parseInput(state.type, state.text);

        // 加载笔顺
        await ensureBishun(lines.join('').split(''));

        // 构造渲染参数
        const opts = buildRenderOptions(getBishun());
        const { renderer, pages } = buildPages(opts, lines);

        const paper = { w: opts.pageW, h: opts.pageH };
        const zoom = state.zoom;

        // 复用 canvas，避免每次重建
        ensureElements(container, pages.length, paper);

        pages.forEach((page: Matrix, i: number) => {
          const cv = currentCanvases[i];
          if (!cv) return;
          const ctx = cv.getContext('2d');
          if (!ctx) return;
          renderer.renderPage(ctx, page, renderer.layout(), i);
        });

        canvases.value = [...currentCanvases];
        applyZoom(currentCanvases, zoom);
      } finally {
        loading.value = false;
      }
    }

    /**
     * 确保 DOM 中有足够的 canvas
     */
    function ensureElements(
      el: HTMLDivElement,
      count: number,
      paper: { w: number; h: number }
    ): void {
      el.innerHTML = '';
      currentCanvases = [];

      for (let i = 0; i < count; i++) {
        const wrap = document.createElement('div');
        wrap.className = 'page-wrap';

        const cv = document.createElement('canvas');
        cv.width = paper.w;
        cv.height = paper.h;

        wrap.appendChild(cv);
        el.appendChild(wrap);
        currentCanvases.push(cv);
      }
    }

    /**
     * 应用预览缩放
     */
    function applyZoom(list: HTMLCanvasElement[], zoom: number): void {
      list.forEach((cv) => {
        cv.style.width = `${cv.width * zoom}px`;
        cv.style.height = `${cv.height * zoom}px`;
      });
    }

    /** 防抖渲染 */
    let timer: ReturnType<typeof setTimeout> | null = null;
    function scheduleRender(): void {
      if (timer) clearTimeout(timer);
      timer = setTimeout(() => void render(), 80);
    }

    onMounted(() => {
      void render();
    });

    // 状态变化 -> 重新渲染
    watch(
      () => [
        state.type,
        state.text,
        state.paper,
        state.padding.join(','),
        state.cellSize,
        state.cellType,
        state.rowGap,
        state.insertBlankRow,
        state.insertBlankCol,
        state.firstCharHighlight,
        state.traceCount,
        state.showBishun,
        state.showPinyin,
        state.showPageNumber,
        state.traceColor,
        state.lineColor,
        state.textColor,
        state.pinyinColor,
        state.fontFamily,
        state.fontWeight,
        state.fontScale,
        state.fontOffset,
      ],
      () => scheduleRender()
    );

    // 缩放单独处理（无需重绘）
    watch(
      () => state.zoom,
      (z) => applyZoom(currentCanvases, z)
    );

    onUpdated(() => {
      /* 保留占位，避免 Vue 认为 setup 未使用 onUpdated */
    });

    /** 暴露给父组件用于导出 */
    expose({
      getCanvases: (): HTMLCanvasElement[] => currentCanvases,
      rerender: (): Promise<void> => render(),
    });

    return () => (
      <div class="pages" ref={(el: any) => (container = el as HTMLDivElement)}>
        {loading.value && <div class="pages-tip">渲染中...</div>}
      </div>
    );
  },
});