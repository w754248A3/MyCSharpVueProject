<template>
  <div class="tabpage-root">
    <!-- 标签页切换 -->
    <div class="tabs">
      <button class="close-all-btn" @click="closeAllTab">关闭所有标签</button>
      <button
        v-for="tab in tabs"
        :key="tab.index"
        :class="{ active: activeTab === tab.index }"
        @click="activeTab = tab.index"
      >
        <span class="tab-text">{{ tab.text }}</span>
        <span class="removeButton" @click.stop="removeTab(tab.index)">×</span>
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
  height: 100%;
  background: #ffffff;
  border-radius: 8px;
  margin: 16px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.08);
  overflow: hidden;
}

.tabs {
  flex: 0 0 auto;
  display: flex;
  gap: 4px;
  overflow-x: auto;
  overflow-y: hidden;
  padding: 12px 12px 0 12px;
  background: #f5f7fa;
  border-bottom: 1px solid #e4e7ed;
  scrollbar-width: thin;
}

.tabs::-webkit-scrollbar {
  height: 4px;
}

.tabs::-webkit-scrollbar-thumb {
  background: #c0c4cc;
  border-radius: 2px;
}

.close-all-btn {
  padding: 10px 16px;
  border: 1px solid #dcdfe6;
  background: #ffffff;
  color: #606266;
  font-size: 13px;
  cursor: pointer;
  border-radius: 6px;
  transition: all 0.3s ease;
  margin-right: 8px;
  white-space: nowrap;
}

.close-all-btn:hover {
  background: #f56c6c;
  border-color: #f56c6c;
  color: #ffffff;
}

.tabs button {
  flex: initial;
  min-width: 80px;
  max-width: 200px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  padding: 10px 32px 10px 16px;
  border: none;
  background: #ffffff;
  color: #606266;
  font-size: 14px;
  cursor: pointer;
  border-radius: 6px 6px 0 0;
  position: relative;
  transition: all 0.3s ease;
  border: 1px solid transparent;
  border-bottom: none;
  display: flex;
  align-items: center;
}

.tabs button:hover {
  background: #ecf5ff;
  color: #409eff;
}

.tabs button.active {
  font-weight: 600;
  background: #ffffff;
  color: #409eff;
  border-color: #e4e7ed;
  box-shadow: 0 -2px 8px rgba(0, 0, 0, 0.05);
  z-index: 1;
}

.tabs button.active::after {
  content: '';
  position: absolute;
  bottom: -1px;
  left: 0;
  right: 0;
  height: 2px;
  background: #409eff;
}

.tab-text {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.removeButton {
  position: absolute;
  right: 8px;
  top: 50%;
  transform: translateY(-50%);
  width: 18px;
  height: 18px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  background: transparent;
  color: #909399;
  font-size: 18px;
  line-height: 1;
  cursor: pointer;
  transition: all 0.2s ease;
  flex-shrink: 0;
  font-weight: 300;
}

.removeButton:hover {
  background: #f56c6c;
  color: #ffffff;
  transform: translateY(-50%) scale(1.1);
}

.tab-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  width: 100%;
  height: 100%;
  overflow: auto;
  padding: 16px;
  background: #ffffff;
}
</style>
