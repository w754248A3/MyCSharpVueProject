<template>
  <div>
    <!-- 标签页切换 -->
    <div class="tabs">
      <button
        v-for="tab in tabs"
        :key="tab.index"
        :class="{ active: activeTab === tab.index }"
        @click="activeTab = tab.index"
      >
      <span class="removeButton" @click.stop="removeTab(tab.index)">×</span>
        {{ tab.text }}
        
      </button>
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
        :id="tab.id"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { defineComponent, ref, watch } from 'vue';
import SurveyTree from './SurveyTree.vue';
import type {TableData, NodeData, Option, ListItem, Tabs} from "../mytype"


const funcs = defineProps<{
  getTableDataRootNode:(id:number)=> NodeData,
  findChildNode:(id:number)=>NodeData,
  addNode:(s:string, parentId:number)=> void,
  tabs:Tabs[]
}>();



  const activeTab = ref(-1);
  
  const removeTab = (index: number) => {
    const v = funcs.tabs.findIndex(t => t.index === index);
    if (v !== -1) {
      funcs.tabs.splice(v, 1);
      // 如果删除的是当前激活的标签，切换到其他标签
      if (activeTab.value === index && funcs.tabs.length > 0) {
        activeTab.value = funcs.tabs[Math.max(0, v - 1)].index;
      }
    }
  };


  watch(
  () => funcs.tabs,
  () => {
    
  },
  { deep: true }
)
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
