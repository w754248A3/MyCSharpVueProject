<template>
  <div class="survey-tree">
    <div v-for="(node, index) in displayedNodes" :key="node.id" class="node-section">
      <!-- 标题 -->
      <div v-if="node.options.length !== 0" class="node-title" @click="toggleNode(node.id)">
        <span class="title-text">{{ node.title }}</span>
        
        <span class="toggle-icon">{{ collapsed[node.id] ? "▼" : "▲" }}</span>
      </div>

      <div v-if="node.options.length === 0" class="node-title">
        <span class="title-text">{{ node.title }}</span>
      </div>

      <!-- 选项 -->
      <div v-show="node.options.length !==0 && !collapsed[node.id]" class="options">
        <label v-for="opt in node.options" :key="opt.id" class="option-item">
          <input
            type="radio"
            :name="'node-' + node.id"
            :value="opt.id"
            v-model="selected[node.id]"
            @change="handleSelect(node, opt)"
          />
          <span class="option-text">{{ opt.label }}</span>
        </label>

        
      </div>  
      <!--button-->
        <div v-show="isViewAddAndUpDataButton" class="node-button">
          <button @click="onAddNode(node.id)">Add Node</button>
          <button @click="onUpNode(node.id, node.title)">UP Node</button>
        </div> 
    </div>
    <div v-if="isOpenPop">
    <PopPage :in-text="text" @on-confirm-text="outText"></PopPage>
  </div>

  <!-- 最终内容编辑/预览区 -->
  <div v-if="terminalNode" ref="resultAreaRef" class="result-area">
    <div class="result-header">
      <span class="result-label">内容预览与临时编辑</span>
      <button class="copy-btn" @click="copyToClipboard">
        <span class="copy-icon">📋</span> 复制
      </button>
    </div>
    <textarea
      ref="textareaRef"
      v-model="editableResult"
      class="result-textarea"
      placeholder="此处显示末尾节点内容，您可以在此进行临时编辑或复制..."
      spellcheck="false"
    ></textarea>

    <div class="image-entry">
      <div class="image-entry-header">
        <span class="image-entry-label">关联图片</span>
        <button class="image-manage-btn" @click="openImageModal">
          {{ imageSummary.count > 0 ? `管理图片（${imageSummary.count}）` : "上传图片" }}
        </button>
      </div>
      <div v-if="imageSummary.count > 0 && imageSummary.latestImageId" class="image-preview-wrap">
        <img
          class="image-preview"
          :src="getImageUrl(imageSummary.latestImageId)"
          alt="节点图片预览"
          @click="openImageModal"
        />
      </div>
      <div v-else class="image-empty-tip">当前末级节点暂无关联图片</div>
    </div>
  </div>

  <div v-if="isImageModalOpen" class="image-modal-mask" @click.self="closeImageModal">
    <div class="image-modal">
      <div class="image-modal-header">
        <span>节点图片管理</span>
        <button class="modal-close-btn" @click="closeImageModal">✕</button>
      </div>

      <div class="image-modal-toolbar">
        <input ref="fileInputRef" type="file" accept="image/*" @change="onPickFile" />
      </div>

      <div v-if="isImageLoading" class="image-loading">加载中...</div>
      <div v-else-if="imageList.length === 0" class="image-empty-tip">暂无图片，请先上传。</div>
      <div v-else class="image-list">
        <div v-for="item in imageList" :key="item.id" class="image-item">
          <img
            class="image-thumb"
            :src="getImageUrl(item.id)"
            :alt="item.fileName || '节点图片'"
            @click="openLargePreview(item.id)"
          />
          <div class="image-item-meta">
            <div class="image-file-name">{{ item.fileName || `image-${item.id}` }}</div>
            <div class="image-file-info">{{ formatSize(item.size) }} · {{ item.createdUtc }}</div>
          </div>
          <button class="image-delete-btn" @click="deleteImage(item.id)">删除</button>
        </div>
      </div>
    </div>
  </div>

  <div v-if="largePreviewUrl" class="image-modal-mask" @click.self="closeLargePreview">
    <div class="large-preview-modal">
      <img class="large-preview-image" :src="largePreviewUrl" alt="大图预览" />
      <div class="large-preview-actions">
        <button @click="copyImageToClipboard">复制到剪贴板</button>
        <button @click="closeLargePreview">关闭</button>
      </div>
    </div>
  </div>
  </div>
  
</template>

<script setup lang="ts">
import { inject, reactive, ref, computed, watch, nextTick } from "vue";
import PopPage from "./PopPage.vue";
import {type ViewTreeData, type Option, onFindChildNodeKey, onAddNodeKey, onUPNodeKey, isViewAddAndUpDataButtonKey} from "../mytype"

interface NodeImageSummary {
  count: number;
  latestImageId: number | null;
}

interface NodeImageInfo {
  id: number;
  fileName: string;
  mimeType: string;
  size: number;
  createdUtc: string;
}

const isOpenPop =ref(false);


const props = defineProps<{
  id:number

}>();



const findChildNode = inject(onFindChildNodeKey);



const addNode = inject(onAddNodeKey);

const upNode= inject(onUPNodeKey);

const isViewAddAndUpDataButton = inject(isViewAddAndUpDataButtonKey);

if(!isViewAddAndUpDataButton){
  throw new Error("isViewAddAndUpDataButton is not definde on ");
}

if(!upNode){
  throw new Error("upNode is not definde on ");
}

if(!addNode){
  throw new Error("addNode is not definde on ");
}

if(!findChildNode){
  
  throw new Error("findChildNode is not definde on ");


}


const text = ref("");

const outText = ref<(s:string)=> void>((s)=> {});


  const displayedNodes = ref<ViewTreeData[]>([]);
  const collapsed = reactive<Record<number, boolean>>({});
  const selected = reactive<Record<number, number | null>>({});
  const editableResult = ref("");
  const resultAreaRef = ref<HTMLElement | null>(null);
  const textareaRef = ref<HTMLTextAreaElement | null>(null);
  const imageSummary = ref<NodeImageSummary>({ count: 0, latestImageId: null });
  const isImageModalOpen = ref(false);
  const imageList = ref<NodeImageInfo[]>([]);
  const isImageLoading = ref(false);
  const fileInputRef = ref<HTMLInputElement | null>(null);
  const largePreviewUrl = ref("");

  const terminalNode = computed(() => {
    const lastNode = displayedNodes.value[displayedNodes.value.length - 1];
    return (lastNode && lastNode.options.length === 0) ? lastNode : null;
  });

  const adjustTextareaHeight = () => {
    nextTick(() => {
      if (textareaRef.value) {
        textareaRef.value.style.height = "auto";
        textareaRef.value.style.height = textareaRef.value.scrollHeight + "px";
      }
    });
  };

  watch(() => terminalNode.value, (newNode) => {
    editableResult.value = newNode ? newNode.title : "";
    isImageModalOpen.value = false;
    imageList.value = [];
    largePreviewUrl.value = "";
    if (newNode) {
      loadImageSummary(newNode.id);
      nextTick(() => {
        resultAreaRef.value?.scrollIntoView({ behavior: "smooth", block: "nearest" });
        adjustTextareaHeight();
      });
    } else {
      imageSummary.value = { count: 0, latestImageId: null };
    }
  }, { immediate: true });

  watch(editableResult, () => {
    adjustTextareaHeight();
  });

  const copyToClipboard = async () => {
    try {
      await navigator.clipboard.writeText(editableResult.value);
    } catch (err) {
      console.error("Failed to copy: ", err);
    }
  };

  const initRootNode = async () => {
    const root = await findChildNode(props.id);
    if (root) {
      displayedNodes.value = [root];
      collapsed[root.id] = false;
    }
  };

  const getImageUrl = (id: number) => `https://mypage.test/api/images/content?id=${id}`;

  const loadImageSummary = async (nodeId: number) => {
    try {
      const rsp = await fetch(`https://mypage.test/api/images/meta?nodeId=${nodeId}`);
      if (!rsp.ok) {
        throw new Error("加载图片摘要失败");
      }
      imageSummary.value = await rsp.json() as NodeImageSummary;
    } catch (error) {
      console.error(error);
      imageSummary.value = { count: 0, latestImageId: null };
    }
  };

  const loadImageList = async () => {
    if (!terminalNode.value) return;
    isImageLoading.value = true;
    try {
      const rsp = await fetch(`https://mypage.test/api/images/list?nodeId=${terminalNode.value.id}`);
      if (!rsp.ok) {
        throw new Error("加载图片列表失败");
      }
      imageList.value = await rsp.json() as NodeImageInfo[];
    } catch (error) {
      console.error(error);
      imageList.value = [];
    } finally {
      isImageLoading.value = false;
    }
  };

  const openImageModal = async () => {
    if (!terminalNode.value) return;
    isImageModalOpen.value = true;
    await loadImageList();
  };

  const closeImageModal = () => {
    isImageModalOpen.value = false;
  };

  const onPickFile = async (event: Event) => {
    if (!terminalNode.value) return;
    const target = event.target as HTMLInputElement;
    const file = target.files?.[0];
    if (!file) return;

    try {
      const bytes = await file.arrayBuffer();
      const rsp = await fetch(`https://mypage.test/api/images/upload?nodeId=${terminalNode.value.id}`, {
        method: "POST",
        headers: {
          "Content-Type": file.type || "application/octet-stream",
          "X-File-Name": encodeURIComponent(file.name),
        },
        body: bytes,
      });

      if (!rsp.ok) {
        throw new Error("上传失败");
      }

      await Promise.all([loadImageSummary(terminalNode.value.id), loadImageList()]);
    } catch (error) {
      console.error(error);
    } finally {
      if (fileInputRef.value) {
        fileInputRef.value.value = "";
      }
    }
  };

  const deleteImage = async (id: number) => {
    if (!terminalNode.value) return;
    try {
      const rsp = await fetch(`https://mypage.test/api/images/delete?id=${id}`, { method: "DELETE" });
      if (!rsp.ok) {
        throw new Error("删除失败");
      }

      await Promise.all([loadImageSummary(terminalNode.value.id), loadImageList()]);
      if (largePreviewUrl.value.includes(`id=${id}`)) {
        largePreviewUrl.value = "";
      }
    } catch (error) {
      console.error(error);
    }
  };

  const openLargePreview = (id: number) => {
    largePreviewUrl.value = getImageUrl(id);
  };

  const closeLargePreview = () => {
    largePreviewUrl.value = "";
  };

  const copyImageToClipboard = async () => {
    if (!largePreviewUrl.value) return;
    try {
      const rsp = await fetch(largePreviewUrl.value);
      const blob = await rsp.blob();
      await navigator.clipboard.write([
        new ClipboardItem({
          [blob.type || "image/png"]: blob,
        }),
      ]);
    } catch (error) {
      console.error(error);
    }
  };

  const formatSize = (size: number) => {
    if (size < 1024) return `${size} B`;
    if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KB`;
    return `${(size / (1024 * 1024)).toFixed(1)} MB`;
  };

  const flashUI = async(id:number)=>{
    const index = displayedNodes.value.findIndex((n) => n.id === id);

    const child = await findChildNode(id);
    if (child) {
      displayedNodes.value[index]= child;
    }
  };

  const handleSelect = async (node: ViewTreeData, option: Option) => {

    
    // 删除当前节点之后的所有节点
    const index = displayedNodes.value.findIndex((n) => n.id === node.id);

    const deleteValue = displayedNodes.value.slice(index + 1);

    for (const element of deleteValue) {
        delete selected[element.id];
        delete collapsed[element.id];
    }

    displayedNodes.value = displayedNodes.value.slice(0, index + 1);

    selected[node.id] = option.id;

    // 找子节点
    const child = await findChildNode(option.id);
    if (child) {
      collapsed[child.id] = false;
      displayedNodes.value.push(child);
    }
  };

  const onAddNode = async (parentId:number)=>{

    text.value="";

    outText.value= async (s)=>{

      isOpenPop.value=false;

 
      await addNode(s, parentId);

      flashUI(parentId);
    };


    isOpenPop.value=true;



  }


  const onUpNode = (id:number, v:string)=>{
    text.value=v;

    outText.value= async (s)=>{

      isOpenPop.value=false;
      if(s && s !== v){
        await upNode(id, s);
        flashUI(id);
      }
 
      
    };


    isOpenPop.value=true;


  };

  const toggleNode = (nodeId: number) => {
    collapsed[nodeId] = !collapsed[nodeId];
  };

  initRootNode();

</script>

<style scoped>
.survey-tree {
  width: 100%;
  display: flex;
  flex-direction: column;
  overflow: visible;
  font-family: "Segoe UI", "Helvetica Neue", Arial, sans-serif;
}

.node-section {
  border: 1px solid #e4e7ed;
  margin-bottom: 16px;
  border-radius: 8px;
  background: #ffffff;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
  transition: all 0.3s ease;
  overflow: visible;
  flex-shrink: 0;
}

.node-section:hover {
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
  border-color: #c0c4cc;
}

.node-title {
  padding: 14px 16px;
  cursor: pointer;
  font-weight: 600;
  display: flex;
  align-items: center;
  justify-content: space-between;
  white-space: pre-wrap;
  color: #303133;
  background: linear-gradient(135deg, #f5f7fa 0%, #ffffff 100%);
  border-bottom: 1px solid #e4e7ed;
  transition: all 0.2s ease;
}

.title-text {
  flex: 1;
  user-select: text;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.node-title:hover {
  background: linear-gradient(135deg, #ecf5ff 0%, #f5f7fa 100%);
  color: #409eff;
}

.toggle-icon {
  font-size: 12px;
  color: #909399;
  transition: transform 0.3s ease;
  flex-shrink: 0;
  margin-left: 12px;
}

.node-title:hover .toggle-icon {
  color: #409eff;
}

.node-button {
  margin: 12px 16px;
  display: flex;
  gap: 8px;
}

.node-button button {
  padding: 8px 16px;
  font-size: 13px;
  border: 1px solid #dcdfe6;
  border-radius: 6px;
  background: #ffffff;
  color: #606266;
  cursor: pointer;
  transition: all 0.3s ease;
  font-weight: 500;
}

.node-button button:hover {
  background: #409eff;
  border-color: #409eff;
  color: #ffffff;
  transform: translateY(-1px);
  box-shadow: 0 2px 8px rgba(64, 158, 255, 0.3);
}

.node-button button:active {
  transform: translateY(0);
}

.options {
  padding: 16px;
  background: #ffffff;
  overflow: visible;
}

.option-item {
  display: flex;
  align-items: center;
  margin-bottom: 12px;
  padding: 10px;
  border-radius: 6px;
  transition: all 0.2s ease;
  cursor: pointer;
}

.option-item:hover {
  background: #f5f7fa;
}

.option-item input[type="radio"] {
  width: 18px;
  height: 18px;
  cursor: pointer;
  accent-color: #409eff;
  flex-shrink: 0;
}

.option-text {
  margin-left: 12px;
  color: #606266;
  font-size: 14px;
  flex: 1;
  user-select: text;
}

.option-item:hover .option-text {
  color: #409eff;
}

.option-item input[type="radio"]:checked + .option-text {
  color: #409eff;
  font-weight: 500;
}

.result-area {
  margin-top: 32px;
  padding: 24px;
  background: #ffffff;
  border: 1px solid #e4e7ed;
  border-radius: 12px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.08);
  animation: slideUp 0.3s ease-out;
}

@keyframes slideUp {
  from {
    opacity: 0;
    transform: translateY(20px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.result-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.result-label {
  font-size: 15px;
  font-weight: 600;
  color: #303133;
  display: flex;
  align-items: center;
}

.result-label::before {
  content: "";
  display: inline-block;
  width: 4px;
  height: 16px;
  background: #409eff;
  margin-right: 8px;
  border-radius: 2px;
}

.copy-btn {
  padding: 8px 16px;
  font-size: 13px;
  background: #ecf5ff;
  border: 1px solid #b3d8ff;
  border-radius: 6px;
  color: #409eff;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 6px;
  transition: all 0.2s ease;
  font-weight: 500;
}

.copy-btn:hover {
  background: #409eff;
  color: #ffffff;
  border-color: #409eff;
  transform: translateY(-1px);
  box-shadow: 0 2px 8px rgba(64, 158, 255, 0.3);
}

.copy-btn:active {
  transform: translateY(0);
}

.result-textarea {
  width: 100%;
  min-height: 50px;
  padding: 16px;
  border: 1px solid #dcdfe6;
  border-radius: 8px;
  font-family: inherit;
  font-size: 14px;
  line-height: 1.6;
  color: #606266;
  resize: none;
  background: #fafafa;
  transition: border-color 0.3s ease, box-shadow 0.3s ease;
  box-sizing: border-box;
  overflow-y: hidden;
}

.result-textarea:focus {
  outline: none;
  border-color: #409eff;
  background: #ffffff;
  box-shadow: 0 0 0 3px rgba(64, 158, 255, 0.1);
}

.result-textarea::placeholder {
  color: #c0c4cc;
}

.image-entry {
  margin-top: 20px;
  border-top: 1px dashed #dcdfe6;
  padding-top: 16px;
}

.image-entry-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
}

.image-entry-label {
  color: #303133;
  font-weight: 600;
}

.image-manage-btn,
.image-delete-btn,
.large-preview-actions button,
.modal-close-btn {
  padding: 6px 12px;
  border: 1px solid #dcdfe6;
  border-radius: 6px;
  cursor: pointer;
  background: #fff;
}

.image-preview-wrap {
  margin-top: 10px;
}

.image-preview {
  max-width: 180px;
  max-height: 120px;
  border-radius: 6px;
  border: 1px solid #dcdfe6;
  object-fit: cover;
  cursor: pointer;
}

.image-empty-tip {
  margin-top: 10px;
  color: #909399;
}

.image-modal-mask {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.45);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 2000;
}

.image-modal {
  width: min(900px, 92vw);
  max-height: 85vh;
  overflow: auto;
  background: #fff;
  border-radius: 12px;
  padding: 16px;
}

.image-modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.image-modal-toolbar {
  margin-bottom: 12px;
}

.image-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 12px;
}

.image-item {
  border: 1px solid #ebeef5;
  border-radius: 8px;
  padding: 10px;
}

.image-thumb {
  width: 100%;
  height: 140px;
  object-fit: cover;
  border-radius: 6px;
  cursor: pointer;
}

.image-item-meta {
  margin-top: 8px;
  margin-bottom: 8px;
}

.image-file-name {
  font-weight: 600;
  color: #303133;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.image-file-info,
.image-loading {
  color: #909399;
  font-size: 12px;
}

.large-preview-modal {
  width: min(1100px, 92vw);
  max-height: 90vh;
  background: #fff;
  border-radius: 12px;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.large-preview-image {
  max-width: 100%;
  max-height: calc(90vh - 90px);
  object-fit: contain;
}

.large-preview-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
}
</style>
