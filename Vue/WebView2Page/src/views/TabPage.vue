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
        {{ tab.title }}
        <span @click.stop="removeTab(tab.id)">×</span>
      </button>
      <button @click="addTab">+ 添加</button>
    </div>

    <!-- 标签页内容 -->
    <div class="tab-content">
      <SurveyTree
        v-for="tab in tabs"
        :key="tab.id"
        v-show="activeTab === tab.id"
      />
    </div>
  </div>
</template>

<script lang="ts">
import { defineComponent, ref } from 'vue';
import SurveyTree from './SurveyTree.vue';

interface Tab {
  id: number;
  title: string;
}

export default defineComponent({
  name: 'TabExample',
  components: { SurveyTree },
  setup() {
    const tabs = ref<Tab[]>([
      { id: 1, title: '标签 1' },
      { id: 2, title: '标签 2' },
    ]);
    const activeTab = ref(1);
    let nextId = 3;

    const addTab = () => {
      tabs.value.push({ id: nextId, title: `标签 ${nextId}` });
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

    return { tabs, activeTab, addTab, removeTab };
  },
});
</script>

<style scoped>
.tabs {
  display: flex;
  gap: 8px;
}
.tabs button.active {
  font-weight: bold;
}
.tab-content {
  margin-top: 16px;
}
</style>
