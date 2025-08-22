<template>
  <div class="tabpage-root">
    <!-- 标签页切换 -->
    <div class="tabs">
      <button @click="closeAllTab">关闭所有标签</button>
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
        :key="tab.index"
        v-show="activeTab === tab.index"
        :id="tab.id"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { defineComponent, ref, watch } from 'vue';
import SurveyTree from './SurveyTree.vue';
import type {NodeData, ViewTreeData, Option, ListItem, Tabs} from "../mytype"


const props = defineProps<{
  addTabValue:ListItem|null
}>();

  const tabs = ref<Tabs[]>([]);

  const activeTab = ref(-1);
  
  const removeTab = (index: number) => {

    if(!window.confirm("确定关闭这个标签吗?")){
      return;
    }




    const v = tabs.value.findIndex(t => t.index === index);
    if (v !== -1) {
      tabs.value.splice(v, 1);
      // 如果删除的是当前激活的标签，切换到其他标签
      if (activeTab.value === index && tabs.value.length > 0) {
        activeTab.value = tabs.value[Math.max(0, v - 1)].index;
      }
    }
  };

  const closeAllTab = ()=>{

     if(!window.confirm("确定关闭所有标签吗?")){
      return;
    }

    tabs.value = [];

    activeTab.value= 0;

  };

  let tabIndex = 0;
  watch(
  () => props.addTabValue,
  (newValue, oldValue) => {
    if(newValue){

      tabIndex++;
      tabs.value.push({text:newValue.text, id:newValue.id, index:tabIndex});

      activeTab.value= tabIndex;
    }
  },
  { deep: true }
)
</script>

<style scoped>

.tabpage-root{
  flex: 1;

  display: flex;
  flex-direction: column;
  width: 100%;
  height: 100%
}

.tabs {
  flex: 0 0 auto;
  
  display: flex;
  gap: 8px;
  overflow: hidden; 
}
.tabs button.active {
  font-weight: bold;
  

}

.tabs button{
  flex:initial;                 
  min-width: 5px;  
  max-width: 150px;            
  white-space: nowrap;      
  overflow: hidden;        
  text-overflow: ellipsis; 
  padding-right: 2px;      
}

.removeButton{
  flex-shrink: 0;           /* 禁止收缩 */
}

.tab-content {
  flex: 1;
  margin-top: 16px;

  display: flex;
  flex-direction: column;
  width: 100%;
  height: 100%;
  
  overflow: auto;
  
}
</style>
