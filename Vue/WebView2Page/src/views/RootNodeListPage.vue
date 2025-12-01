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
  (e: 'item-click', payload: ListItem): void
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
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
  overflow: auto;
  font-family: "Segoe UI", "Helvetica Neue", Arial, sans-serif;
  background: #ffffff;
}

.list {
  list-style: none;
  padding: 8px 0;
  margin: 0;
}

.list-item {
  padding: 12px 16px;
  cursor: pointer;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 14px;
  color: #606266;
  border-bottom: 1px solid #f0f2f5;
  transition: all 0.2s ease;
  position: relative;
}

.list-item:hover {
  background: #f5f7fa;
  color: #409eff;
  padding-left: 20px;
}

.list-item.selected {
  background: linear-gradient(90deg, #ecf5ff 0%, #ffffff 100%);
  color: #409eff;
  font-weight: 600;
  border-left: 3px solid #409eff;
  padding-left: 13px;
}

.list-item.selected::before {
  content: '';
  position: absolute;
  left: 0;
  top: 0;
  bottom: 0;
  width: 3px;
  background: #409eff;
}
</style>
