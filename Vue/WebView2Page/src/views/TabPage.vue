<template>
  <div>
    <!-- 标签页切换 -->
    <div class="tabs">
      <button
        v-for="tab in tabs"
        :key="tab.id"
        :class="{ active: activeTab === tab.id }"
        @click="activeTab = tab.id"
      >
      <span class="removeButton" @click.stop="removeTab(tab.id)">×</span>
        {{ tab.title }}
        
      </button>
      
      <button @click="addTab">+ 添加</button>
    </div>

    <!-- 标签页内容 -->
    <div class="tab-content">
      <SurveyTree
        v-for="tab in tabs"
        :key="tab.id"
        v-show="activeTab === tab.id"
        :add-node="funcs.addNode"
        :find-child-node="funcs.findChildNode"
        :get-table-data-root-node="funcs.getTableDataRootNode"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { defineComponent, ref } from 'vue';
import SurveyTree from './SurveyTree.vue';
import type {TableData, NodeData, Option} from "../mytype"

interface Tab {
  id: number;
  title: string;
}



const funcs = defineProps<{
  getTableDataRootNode:()=> NodeData,
  findChildNode:(id:number)=>NodeData,
  addNode:(s:string, parentId:number)=> void

}>();




const tabs = ref<Tab[]>([
    { id: 1, title: '标签 1' },
    { id: 2, title: '标签 2' },
  ]);
  const activeTab = ref(1);
  let nextId = 3;
  let n = 1;
  const addTab = () => {
    n = n*10;
    tabs.value.push({ id: nextId, title: `标签${n.toString()} ${nextId}` });
    activeTab.value = nextId;
    nextId++;
  };

  const removeTab = (id: number) => {
    const index = tabs.value.findIndex(t => t.id === id);
    if (index !== -1) {
      tabs.value.splice(index, 1);
      // 如果删除的是当前激活的标签，切换到其他标签
      if (activeTab.value === id && tabs.value.length > 0) {
        activeTab.value = tabs.value[Math.max(0, index - 1)].id;
      }
    }
  };
</script>

<style scoped>
.tabs {
  display: flex;
  gap: 8px;
  overflow: hidden; 
}
.tabs button.active {
  font-weight: bold;
  

}

.tabs button{
  flex: 1;                  /* 关键：占据剩余空间 */
  min-width: 0;             /* 关键：允许收缩到小于内容宽度 */
  white-space: nowrap;      /* 禁止文本换行 */
  overflow: hidden;         /* 隐藏溢出内容 */
  text-overflow: ellipsis;  /* 溢出时显示省略号 */
  padding-right: 8px;       /* 与按钮间距 */
}

.removeButton{
  flex-shrink: 0;           /* 禁止收缩 */
}

.tab-content {
  margin-top: 16px;
}
</style>
