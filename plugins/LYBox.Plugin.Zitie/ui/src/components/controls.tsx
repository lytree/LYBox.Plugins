/**
 * 通用控件组件（TSX）
 * 全部为无状态展示组件，由父级控制数据流
 */
import { defineComponent, type PropType } from 'vue';
import type { Option } from '@/types';

const cls = (...xs: (string | false | undefined)[]): string =>
  xs.filter(Boolean).join(' ');

/** 分组标题 */
export const GroupTitle = defineComponent({
  name: 'GroupTitle',
  props: { title: { type: String, required: true } },
  setup(props) {
    return () => <div class="grp-title">{props.title}</div>;
  },
});

/** 设置行：左标签 + 右控件 */
export const Row = defineComponent({
  name: 'Row',
  props: {
    label: { type: String, required: true },
  },
  setup(props, { slots }) {
    return () => (
      <div class="row">
        <span class="row-label">{props.label}</span>
        <div class="row-ctl">{slots.default?.()}</div>
      </div>
    );
  },
});

/** 开关 */
export const SwitchControl = defineComponent({
  name: 'SwitchControl',
  props: {
    modelValue: { type: Boolean, required: true },
  },
  emits: ['update:modelValue'],
  setup(props, { emit }) {
    return () => (
      <div
        class={cls('switch', props.modelValue && 'on')}
        role="switch"
        aria-checked={props.modelValue}
        tabindex={0}
        onClick={() => emit('update:modelValue', !props.modelValue)}
        onKeydown={(e: KeyboardEvent) => {
          if (e.key === ' ' || e.key === 'Enter') {
            e.preventDefault();
            emit('update:modelValue', !props.modelValue);
          }
        }}
      />
    );
  },
});

/** 滑块（带数值显示） */
export const SliderControl = defineComponent({
  name: 'SliderControl',
  props: {
    modelValue: { type: Number, required: true },
    min: { type: Number, required: true },
    max: { type: Number, required: true },
    step: { type: Number, default: 1 },
    /** 数值格式化 */
    format: {
      type: Function as PropType<(v: number) => string>,
      default: (v: number) => String(v),
    },
  },
  emits: ['update:modelValue'],
  setup(props, { emit }) {
    return () => (
      <>
        <input
          type="range"
          min={props.min}
          max={props.max}
          step={props.step}
          value={props.modelValue}
          onInput={(e: Event) =>
            emit('update:modelValue', parseFloat((e.target as HTMLInputElement).value))
          }
        />
        <span class="range-val">{props.format(props.modelValue)}</span>
      </>
    );
  },
});

/** 下拉选择 */
export const SelectControl = defineComponent({
  name: 'SelectControl',
  props: {
    modelValue: { type: String, required: true },
    options: { type: Array as PropType<readonly Option[]>, required: true },
  },
  emits: ['update:modelValue'],
  setup(props, { emit }) {
    return () => (
      <select
        value={props.modelValue}
        onChange={(e: Event) =>
          emit('update:modelValue', (e.target as HTMLSelectElement).value)
        }
      >
        {props.options.map((o) => (
          <option key={o.value} value={o.value} selected={o.value === props.modelValue}>
            {o.label}
          </option>
        ))}
      </select>
    );
  },
});

/** 颜色选择 */
export const ColorControl = defineComponent({
  name: 'ColorControl',
  props: {
    modelValue: { type: String, required: true },
  },
  emits: ['update:modelValue'],
  setup(props, { emit }) {
    return () => (
      <input
        type="color"
        value={props.modelValue}
        onInput={(e: Event) =>
          emit('update:modelValue', (e.target as HTMLInputElement).value)
        }
      />
    );
  },
});

/** 数字输入 */
export const NumberControl = defineComponent({
  name: 'NumberControl',
  props: {
    modelValue: { type: Number, required: true },
    min: { type: Number, default: 0 },
    max: { type: Number, default: 999 },
  },
  emits: ['update:modelValue'],
  setup(props, { emit }) {
    return () => (
      <input
        type="number"
        min={props.min}
        max={props.max}
        value={props.modelValue}
        onInput={(e: Event) =>
          emit('update:modelValue', parseFloat((e.target as HTMLInputElement).value) || 0)
        }
      />
    );
  },
});

/** 多行文本输入 */
export const TextAreaControl = defineComponent({
  name: 'TextAreaControl',
  props: {
    modelValue: { type: String, required: true },
    placeholder: { type: String, default: '' },
  },
  emits: ['update:modelValue'],
  setup(props, { emit }) {
    return () => (
      <textarea
        value={props.modelValue}
        placeholder={props.placeholder}
        rows={3}
        onInput={(e: Event) =>
          emit('update:modelValue', (e.target as HTMLTextAreaElement).value)
        }
      />
    );
  },
});

/** 普通按钮 */
export const ButtonControl = defineComponent({
  name: 'ButtonControl',
  props: {
    title: { type: String, default: '' },
    primary: { type: Boolean, default: false },
  },
  emits: ['click'],
  setup(props, { emit }) {
    return () => (
      <button
        class={cls('btn', props.primary && 'primary')}
        type="button"
        onClick={() => emit('click')}
      >
        {props.title}
      </button>
    );
  },
});