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
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 9999;
  overflow: hidden;
  backdrop-filter: blur(4px);
  animation: fadeIn 0.2s ease;
}

@keyframes fadeIn {
  from {
    opacity: 0;
  }
  to {
    opacity: 1;
  }
}

.modal-window {
  background: white;
  padding: 24px;
  border-radius: 12px;
  min-width: 400px;
  max-width: 600px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.2);
  animation: slideUp 0.3s ease;
}

@keyframes slideUp {
  from {
    transform: translateY(20px);
    opacity: 0;
  }
  to {
    transform: translateY(0);
    opacity: 1;
  }
}

.modal-window h3 {
  margin: 0 0 20px 0;
  font-size: 18px;
  font-weight: 600;
  color: #303133;
  border-bottom: 2px solid #ecf5ff;
  padding-bottom: 12px;
}

.modal-window textarea {
  font-family: 
    "PingFang SC",
    "Hiragino Sans GB",
    "Microsoft YaHei UI",
    "Microsoft YaHei",
    "Source Han Sans SC",
    "Noto Sans SC",
    sans-serif;
  font-size: 14px;
  font-weight: 400;
  line-height: 1.6;
  letter-spacing: 0.02em;
  word-spacing: 0.05em;
  text-rendering: optimizeLegibility;
  width: 100%;
  resize: vertical;
  margin-top: 10px;
  margin-bottom: 20px;
  padding: 12px;
  border: 1px solid #dcdfe6;
  border-radius: 6px;
  outline: none;
  transition: all 0.3s ease;
  color: #606266;
  min-height: 120px;
}

.modal-window textarea:focus {
  border-color: #409eff;
  box-shadow: 0 0 0 2px rgba(64, 158, 255, 0.1);
}

.modal-actions {
  text-align: right;
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

.modal-actions button {
  padding: 10px 20px;
  font-size: 14px;
  border: 1px solid #dcdfe6;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.3s ease;
  min-width: 80px;
}

.modal-actions button:first-child {
  background: #ffffff;
  color: #606266;
}

.modal-actions button:first-child:hover {
  background: #f5f7fa;
  border-color: #c0c4cc;
}

.modal-actions button:last-child {
  background: #409eff;
  color: #ffffff;
  border-color: #409eff;
}

.modal-actions button:last-child:hover {
  background: #66b1ff;
  border-color: #66b1ff;
}

.modal-actions button:active {
  transform: scale(0.98);
}
</style>
