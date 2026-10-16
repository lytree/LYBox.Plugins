/**
 * 字帖类型切换标签
 */
import { defineComponent, type PropType } from 'vue';
import { ZITIE_TYPES } from '@/core/config';
import type { ZitieType } from '@/types';

export const TypeTabs = defineComponent({
  name: 'TypeTabs',
  props: {
    modelValue: { type: String as PropType<ZitieType>, required: true },
  },
  emits: ['update:modelValue'],
  setup(props, { emit }) {
    return () => (
      <div class="type-tabs">
        {ZITIE_TYPES.map((t) => (
          <div
            key={t.key}
            class={['type-tab', t.key === props.modelValue && 'active']}
            role="button"
            tabindex={0}
            onClick={() => emit('update:modelValue', t.key)}
            onKeydown={(e: KeyboardEvent) => {
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                emit('update:modelValue', t.key);
              }
            }}
          >
            {t.label}
          </div>
        ))}
      </div>
    );
  },
});