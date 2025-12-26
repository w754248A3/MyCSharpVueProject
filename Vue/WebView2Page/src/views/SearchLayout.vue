<template>
  <div class="search-layout">
    <!-- 顶部搜索框 -->
    <input
      type="text"
      class="search-input"
      v-model="searchText"
      @input="onSearchInput"
      @focus="onSearchInput"
      @blur="onBlur"
      placeholder="请输入搜索内容..."
    />
  </div>
</template>

<script setup lang="ts">
import { defineComponent, ref } from "vue";
import PopPage from "./PopPage.vue";

const emit = defineEmits<{
    "search-change":[value:string],
    "add-root":[value:string],
    "on-blur":[]
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


const onBlur =()=>{
  emit("on-blur");

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
  flex: 1;
  min-width: 0;
}

.search-input {
  flex: 0 0 auto;
  padding: 10px 16px;
  font-size: 14px;
  border: 1px solid #dcdfe6;
  border-radius: 6px;
  outline: none;
  box-sizing: border-box;
  transition: all 0.3s ease;
  background: #ffffff;
  color: #606266;
  width: 100%;
  min-width: 200px;
}

.search-input:focus {
  border-color: #409eff;
  box-shadow: 0 0 0 2px rgba(64, 158, 255, 0.1);
}

.search-input::placeholder {
  color: #c0c4cc;
}

</style>
