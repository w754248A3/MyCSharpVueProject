<template>
  <div>
    <!-- 模态弹窗 -->
    <div
      v-if="showModal"
      class="modal-overlay"
      @keydown.esc="onCancel"
      tabindex="0"
    >
      <div class="modal-window" @click.stop>
        <h3>编辑内容</h3>
        <textarea v-model="text" rows="10" cols="80"></textarea>
        <div class="modal-actions">
          <button @click="onCancel">取消</button>
          <button @click="onConfirm">确定</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
  import { defineComponent, ref } from "vue";

  const { inText } = defineProps<{
    inText:string
  }>();

  const emit = defineEmits<{"onConfirmText":[text:string]}>();

  
  const showModal = ref(true);
  const text = ref(inText);

  function onCancel() {
    emit("onConfirmText", inText);
   
    showModal.value = false;
  }

  function onConfirm() {
    emit("onConfirmText", text.value);
   
    showModal.value = false;
  }

</script>

<style scoped>
.modal-overlay {
  position: fixed;
  top: 0; left: 0; right: 0; bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 9999;
  overflow: hidden;
}

.modal-window {
  background: white;
  padding: 20px;
  border-radius: 6px;
  min-width: 300px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.3);
}

.modal-window textarea {
   /* 字体栈（优先使用现代圆润字体） */
  font-family: 
    "PingFang SC",         /* iOS/macOS 首选 */
    "Hiragino Sans GB",     /* macOS 备选 */
    "Microsoft YaHei UI",   /* Windows 首选 */
    "Microsoft YaHei",      /* Windows 备选 */
    "Source Han Sans SC",   /* 思源黑体（需引入） */
    "Noto Sans SC",         /* Google 字体 */
    sans-serif;             /* 通用兜底 */

  /* 基础字号设置 */
  font-size: 16px;          /* 标准正文字号 */
  font-weight: 400;         /* 常规粗细（避免过细） */

  /* 间距优化 */
  line-height: 1.6;         /* 行高（1.5-1.8最佳） */
  letter-spacing: 0.02em;   /* 字间距微调 */
  word-spacing: 0.05em;     /* 词间距微调 */

  /* 渲染优化 */
  text-rendering: optimizeLegibility; /* 提升可读性 */



  width: 100%;
  resize: vertical;
  margin-top: 10px;
  margin-bottom: 15px;
}

.modal-actions {
  text-align: right;
}

.modal-actions button {
  margin-left: 10px;
}
</style>
