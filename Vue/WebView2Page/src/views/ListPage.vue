<template>
  <div class="list-container">
    <ul class="list">
      <li
        v-for="(item, index) in items"
        :key="item.id"
        :class="{ selected: selectedIndex === index }"
        @click="handleClick(item, index)"
        class="list-item"
        :title="item.text"
      >
        {{ item.text }}
      </li>
    </ul>
  </div>
</template>

<script lang="ts" setup>
import { ref, watch, defineEmits, defineProps } from 'vue'
import type { ListItem} from "@/mytype"


const props = defineProps<{
  items: ListItem[]
}>()

const emit = defineEmits<{
  (e: 'item-click', payload: { text: string; id: string | number }): void
}>()

const selectedIndex = ref<number | null>(null)

// 当父组件数据更新时，清空选中状态
watch(
  () => props.items,
  () => {
    selectedIndex.value = null
  },
  { deep: true }
)

function handleClick(item: ListItem, index: number) {
  selectedIndex.value = index
  emit('item-click', { text: item.text, id: item.id })
}
</script>

<style scoped>
.list-container {
  width: 100%;
  height: 100%;
  overflow: auto;
  font-family: "Segoe UI", "Helvetica Neue", Arial, sans-serif;
}

.list {
  list-style: none;
  padding: 0;
  margin: 0;
}

.list-item {
  padding: 8px 12px;
  cursor: pointer;
  white-space: nowrap;          /* 不换行 */
  overflow: hidden;             /* 隐藏超出 */
  text-overflow: ellipsis;      /* 显示省略号 */
  font-size: 14px;
  border-bottom: 1px solid #f0f0f0;
  transition: background 0.2s;
}

.list-item:hover {
  background: #f5f5f5;
}

.selected {
  background: #e6f7ff;
  font-weight: bold;
}
</style>
