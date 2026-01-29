<template>
  <div class="clipboard-history">
    <div class="editor-header">
      <span class="info">粘贴板历史 (可编辑，关闭标签页即丢失)</span>
    </div>
    <textarea
      v-model="localContent"
      class="history-editor"
      placeholder="此处可以编辑粘贴板内容..."
    ></textarea>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';

const props = defineProps<{
  content?: string
}>();

const localContent = ref(props.content || "");
</script>

<style scoped>
.clipboard-history {
  display: flex;
  flex-direction: column;
  height: 100%;
  width: 100%;
  background: #ffffff;
}

.editor-header {
  padding: 8px 16px;
  background: #f8f9fa;
  border-bottom: 1px solid #e9ecef;
  display: flex;
  align-items: center;
}

.info {
  font-size: 12px;
  color: #6c757d;
  font-style: italic;
}

.history-editor {
  flex: 1;
  width: 100%;
  padding: 16px;
  border: none;
  resize: none;
  font-family: "Cascadia Code", "Consolas", "Monaco", monospace;
  font-size: 14px;
  line-height: 1.6;
  color: #212529;
  outline: none;
  background: #ffffff;
}

.history-editor:focus {
  background: #fff;
}
</style>
