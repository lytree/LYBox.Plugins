/**
 * 侧边栏参数面板
 * 所有控件双向绑定到全局 state
 */
import { computed, defineComponent, type PropType, ref } from 'vue';
import {
  ColorControl,
  GroupTitle,
  Row,
  SelectControl,
  SliderControl,
  SwitchControl,
  TextAreaControl,
} from './controls';
import { CELL_TYPES, EN_CELL_TYPES } from '@/core/cells';
import { FONT_WEIGHTS, PAPER_NAMES, TYPE_MAP } from '@/core/config';
import { BIHUA } from '@/core/stroke-data';
import { importTxtFile, detectZitieType, normalizeText } from '@/core/txt-import';
import { hasNativeBridge, readTextFile } from '@/core/native-bridge';
import {
  currentType,
  fontOptions,
  maxCols,
  mmToPx,
  pxToMm,
  state,
} from '@/core/store';
import { ZITIE_TYPES } from '@/core/config';
import { TypeTabs } from './TypeTabs';
import type { CellType, PaperName, ZitieType } from '@/types';
import { switchType } from '@/core/store';

export const Sidebar = defineComponent({
  name: 'Sidebar',
  props: {
    /** 全局提示（由 App 注入，与工具栏导出提示共用） */
    onToast: {
      type: Function as PropType<(msg: string) => void>,
      default: () => {},
    },
  },
  setup(props) {
    /** 当前格子选项（中文 / 英文体系） */
    const cellOptions = computed(() =>
      currentType.value.en ? EN_CELL_TYPES : CELL_TYPES
    );

    /** 描红数量钳制上限 */
    const traceMax = computed(() => maxCols.value);

    /** 方格大小（mm 视图） */
    const cellSizeMm = computed({
      get: () => pxToMm(state.cellSize),
      set: (v: number) => {
        state.cellSize = mmToPx(v);
      },
    });

    /** 行间距（mm 视图） */
    const rowGapMm = computed({
      get: () => pxToMm(state.rowGap),
      set: (v: number) => {
        state.rowGap = mmToPx(v);
      },
    });

    /** 描红数量（钳制） */
    const traceValue = computed({
      get: () => Math.min(state.traceCount, traceMax.value),
      set: (v: number) => {
        state.traceCount = v;
      },
    });

    /** 页边距：4 个独立输入 */
    const padValues = () =>
      state.padding.map((v, i) => ({
        value: v,
        index: i,
        onInput: (e: Event) => {
          const next = parseFloat((e.target as HTMLInputElement).value) || 0;
          state.padding[i] = next;
        },
      }));

    /** 点击笔画快捷按钮 */
    const appendBihua = (ch: string) => {
      state.text += ch;
    };

    /** ---------- TXT 导入 ---------- */
    let fileInputEl: HTMLInputElement | null = null;
    const importing = ref(false);

    /**
     * WebView 下走宿主原生打开对话框 + 宿主读盘（UTF-8 优先 / GBK 回退）；
     * 纯浏览器环境仍用 <input type="file">。
     */
    const pickTxtFile = () => {
      if (hasNativeBridge()) void onTxtPicked();
      else fileInputEl?.click();
    };

    const applyImported = (text: string, filename: string) => {
      const normalized = normalizeText(text);
      if (!normalized.replace(/\s/g, '')) {
        props.onToast('文件内容为空');
        return;
      }

      const type = detectZitieType(normalized);
      const chars = normalized.replace(/\s/g, '').length;

      // 段落 / 英文段落才会按行保留换行与缩进，
      // 其余类型（汉字连排、词组逐词等）会打散原文结构，故统一切换到识别类型
      // （switchType 会清空文本，必须先切换再赋值）
      const needSwitch = state.type !== type;
      if (needSwitch) switchType(type);
      state.text = normalized;

      props.onToast(
        `已导入 ${filename} · ${chars} 字` +
          (needSwitch ? `（已切换到${TYPE_MAP[type].label}）` : '')
      );
    };

    /** 宿主原生路径：对话框 + 读盘都在 C# 侧完成 */
    const onTxtPicked = async () => {
      importing.value = true;
      try {
        const result = await readTextFile();
        if (!result) return; // 用户取消
        applyImported(result.text, result.fileName);
      } catch (err) {
        props.onToast(err instanceof Error ? err.message : 'TXT 导入失败');
      } finally {
        importing.value = false;
      }
    };

    /** 浏览器路径：<input type="file"> */
    const onTxtFileChange = async (e: Event) => {
      const input = e.target as HTMLInputElement;
      const file = input.files?.[0];
      input.value = ''; // 重置，允许再次选择同一文件
      if (!file) return;

      importing.value = true;
      try {
        const result = await importTxtFile(file);
        applyImported(result.text, result.filename);
      } catch (err) {
        props.onToast(err instanceof Error ? err.message : 'TXT 导入失败');
      } finally {
        importing.value = false;
      }
    };

    const onTypeChange = (t: ZitieType) => switchType(t);

    const fmtMm = (v: number) => `${v.toFixed(0)}mm`;
    const fmtMm1 = (v: number) => `${v.toFixed(1)}mm`;
    const fmtPct = (v: number) => `${v}%`;

    return () => {
      const t = currentType.value;
      const isBihua = state.type === 'bihua';

      return (
        <aside class="sidebar">
          <div class="brand">
            <div class="brand-mark">字</div>
            <div>
              <div class="brand-name">字帖生成器</div>
              <div class="brand-sub">离线版 · Vue 3 + TSX</div>
            </div>
          </div>

          <TypeTabs
            modelValue={state.type}
            onUpdate:modelValue={onTypeChange}
          />

          <div class="controls">
            {/* ---------- 输入 ---------- */}
            <GroupTitle title="输入内容" />
            <TextAreaControl
              modelValue={state.text}
              placeholder={t.placeholder}
              onUpdate:modelValue={(v: string) => (state.text = v)}
            />
            <div class="txt-import">
              <button
                class="txt-import-btn"
                type="button"
                disabled={importing.value}
                title="导入 .txt 文件，保留换行、缩进与符号"
                onClick={pickTxtFile}
              >
                {importing.value ? '解析中...' : '导入 TXT'}
              </button>
              <span class="txt-import-hint">保留换行、缩进与符号，按语言切换段落类型</span>
              <input
                ref={(el: any) => (fileInputEl = el as HTMLInputElement | null)}
                class="txt-import-input"
                type="file"
                accept=".txt,.text,text/plain"
                onChange={(e: Event) => void onTxtFileChange(e)}
              />
            </div>
            {isBihua && (
              <div class="bihua-quick">
                {BIHUA.slice(0, 20).map((b) => (
                  <button
                    key={b.type}
                    type="button"
                    class="bihua-chip"
                    title={b.name}
                    onClick={() => appendBihua(b.char)}
                  >
                    {b.char}
                  </button>
                ))}
              </div>
            )}

            {/* ---------- 显示选项 ---------- */}
            <GroupTitle title="显示选项" />
            {!t.en && (
              <>
                <Row label="显示笔顺">
                  <SwitchControl
                    modelValue={state.showBishun}
                    onUpdate:modelValue={(v: boolean) => (state.showBishun = v)}
                  />
                </Row>
                <Row label="显示拼音">
                  <SwitchControl
                    modelValue={state.showPinyin}
                    onUpdate:modelValue={(v: boolean) => (state.showPinyin = v)}
                  />
                </Row>
              </>
            )}
            <Row label="首字高亮">
              <SwitchControl
                modelValue={state.firstCharHighlight}
                onUpdate:modelValue={(v: boolean) => (state.firstCharHighlight = v)}
              />
            </Row>
            <Row label="显示页码">
              <SwitchControl
                modelValue={state.showPageNumber}
                onUpdate:modelValue={(v: boolean) => (state.showPageNumber = v)}
              />
            </Row>

            {/* ---------- 插入空白 ---------- */}
            <GroupTitle title="插入空白" />
            <Row label="插入空行">
              <SwitchControl
                modelValue={state.insertBlankRow}
                onUpdate:modelValue={(v: boolean) => {
                  state.insertBlankRow = v;
                  state.blankRows = v ? 1 : 0;
                }}
              />
            </Row>
            <Row label="插入空列">
              <SwitchControl
                modelValue={state.insertBlankCol}
                onUpdate:modelValue={(v: boolean) => {
                  state.insertBlankCol = v;
                  state.blankCols = v ? 1 : 0;
                }}
              />
            </Row>

            {/* ---------- 格子设置 ---------- */}
            <GroupTitle title="格子设置" />
            <Row label="方格类型">
              <SelectControl
                modelValue={state.cellType}
                options={cellOptions.value}
                onUpdate:modelValue={(v: string) => (state.cellType = v as CellType)}
              />
            </Row>
            <Row label="方格大小">
              <SliderControl
                modelValue={cellSizeMm.value}
                min={6}
                max={30}
                step={0.5}
                format={fmtMm}
                onUpdate:modelValue={(v: number) => (cellSizeMm.value = v)}
              />
            </Row>
            <Row label="行间距">
              <SliderControl
                modelValue={rowGapMm.value}
                min={0}
                max={10}
                step={0.5}
                format={fmtMm1}
                onUpdate:modelValue={(v: number) => (rowGapMm.value = v)}
              />
            </Row>

            {/* ---------- 描红 ---------- */}
            <GroupTitle title="描红" />
            <Row label="描红数量">
              <SliderControl
                modelValue={traceValue.value}
                min={0}
                max={traceMax.value}
                step={1}
                format={(v: number) => String(v)}
                onUpdate:modelValue={(v: number) => (traceValue.value = v)}
              />
            </Row>
            <Row label="描红颜色">
              <ColorControl
                modelValue={state.traceColor}
                onUpdate:modelValue={(v: string) => (state.traceColor = v)}
              />
            </Row>
            <Row label="线条颜色">
              <ColorControl
                modelValue={state.lineColor}
                onUpdate:modelValue={(v: string) => (state.lineColor = v)}
              />
            </Row>
            <Row label="文字颜色">
              <ColorControl
                modelValue={state.textColor}
                onUpdate:modelValue={(v: string) => (state.textColor = v)}
              />
            </Row>
            {!t.en && (
              <Row label="拼音颜色">
                <ColorControl
                  modelValue={state.pinyinColor}
                  onUpdate:modelValue={(v: string) => (state.pinyinColor = v)}
                />
              </Row>
            )}

            {/* ---------- 字体 ---------- */}
            <GroupTitle title="字体" />
            <Row label="字体">
              <SelectControl
                modelValue={state.fontFamily}
                options={fontOptions.value}
                onUpdate:modelValue={(v: string) => (state.fontFamily = v)}
              />
            </Row>
            <Row label="字体粗细">
              <SelectControl
                modelValue={state.fontWeight}
                options={FONT_WEIGHTS}
                onUpdate:modelValue={(v: string) =>
                  (state.fontWeight = v as 'normal' | 'bold')
                }
              />
            </Row>
            <Row label="字体大小">
              <SliderControl
                modelValue={state.fontScale}
                min={30}
                max={110}
                step={1}
                format={fmtPct}
                onUpdate:modelValue={(v: number) => (state.fontScale = v)}
              />
            </Row>
            <Row label="上下偏移">
              <SliderControl
                modelValue={state.fontOffset}
                min={-30}
                max={30}
                step={1}
                format={fmtPct}
                onUpdate:modelValue={(v: number) => (state.fontOffset = v)}
              />
            </Row>

            {/* ---------- 页面 ---------- */}
            <GroupTitle title="页面设置" />
            <Row label="纸张">
              <SelectControl
                modelValue={state.paper}
                options={PAPER_NAMES.map((p) => ({ value: p, label: p }))}
                onUpdate:modelValue={(v: string) => (state.paper = v as PaperName)}
              />
            </Row>
            <Row label="页边距">
              <div class="pad-group">
                {padValues().map((p) => (
                  <input
                    key={p.index}
                    type="number"
                    min={0}
                    max={120}
                    value={p.value}
                    title={['上', '右', '下', '左'][p.index]}
                    onInput={p.onInput}
                  />
                ))}
              </div>
            </Row>
            <div class="pad-hint">上 右 下 左</div>

            <div class="type-hint">
              当前：{ZITIE_TYPES.find((x) => x.key === state.type)?.label}
            </div>
          </div>
        </aside>
      );
    };
  },
});