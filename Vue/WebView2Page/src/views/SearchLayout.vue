<template>
  <div class="search-layout">
    <!-- 顶部搜索框 -->
    <input
      type="text"
      class="search-input"
      v-model="searchText"
      @input="onSearchInput"
      @focus="onSearchInput"
      placeholder="请输入搜索内容..."
    />
  </div>
</template>

<script setup lang="ts">
import { defineComponent, ref } from "vue";
import PopPage from "./PopPage.vue";

const emit = defineEmits<{
    "search-change":[value:string],
    "add-root":[value:string]
}>();



const searchText = ref("");
let debounceTimer: number | undefined;

const onSearchInput = () => {
    // 清除上一次定时器
    if (debounceTimer) {
        clearTimeout(debounceTimer);
    }
    // 300ms 防抖
    debounceTimer = window.setTimeout(() =>{
    emit("search-change", searchText.value);
    }, 300);
};


const onAddRoot=()=>{

};

</script>

<style scoped>
.search-layout {
  display: flex;
  flex-direction: column;
  height: 100%;
  width: 100%;
  box-sizing: border-box;
}

.search-input {
  flex: 0 0 auto;
  padding: 8px 12px;
  font-size: 16px;
  border: 1px solid #ccc;
  outline: none;
  box-sizing: border-box;
}

</style>
