<template>
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
</template>

<script setup lang="ts">
import { ref, watch } from "vue";

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

const props = defineProps<{
  nodeId: number;
}>();

const imageSummary = ref<NodeImageSummary>({ count: 0, latestImageId: null });
const isImageModalOpen = ref(false);
const imageList = ref<NodeImageInfo[]>([]);
const isImageLoading = ref(false);
const imageListRequestSeq = ref(0);
const fileInputRef = ref<HTMLInputElement | null>(null);
const largePreviewUrl = ref("");

const getImageUrl = (id: number) => `https://mypage.test/api/images/content?id=${id}`;

const loadImageSummary = async (nodeId: number) => {
  try {
    const rsp = await fetch(`https://mypage.test/api/images/meta?nodeId=${nodeId}`);
    if (!rsp.ok) {
      throw new Error("加载图片摘要失败");
    }
    imageSummary.value = (await rsp.json()) as NodeImageSummary;
  } catch (error) {
    console.error(error);
    imageSummary.value = { count: 0, latestImageId: null };
  }
};

const loadImageList = async () => {
  const requestNodeId = props.nodeId;
  const requestSeq = ++imageListRequestSeq.value;
  isImageLoading.value = true;
  try {
    const rsp = await fetch(`https://mypage.test/api/images/list?nodeId=${requestNodeId}`);
    if (!rsp.ok) {
      throw new Error("加载图片列表失败");
    }
    const list = (await rsp.json()) as NodeImageInfo[];
    if (requestSeq !== imageListRequestSeq.value) return;
    if (props.nodeId !== requestNodeId) return;
    imageList.value = list;
  } catch (error) {
    console.error(error);
    if (requestSeq === imageListRequestSeq.value) {
      imageList.value = [];
    }
  } finally {
    if (requestSeq === imageListRequestSeq.value) {
      isImageLoading.value = false;
    }
  }
};

const openImageModal = async () => {
  isImageModalOpen.value = true;
  await loadImageList();
};

const closeImageModal = () => {
  isImageModalOpen.value = false;
};

const onPickFile = async (event: Event) => {
  const target = event.target as HTMLInputElement;
  const file = target.files?.[0];
  if (!file) return;

  try {
    const bytes = await file.arrayBuffer();
    const rsp = await fetch(`https://mypage.test/api/images/upload?nodeId=${props.nodeId}`, {
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

    await Promise.all([loadImageSummary(props.nodeId), loadImageList()]);
  } catch (error) {
    console.error(error);
  } finally {
    if (fileInputRef.value) {
      fileInputRef.value.value = "";
    }
  }
};

const deleteImage = async (id: number) => {
  if (!window.confirm("请确认是否删除")) {
    return;
  }

  try {
    const rsp = await fetch(`https://mypage.test/api/images/delete?id=${id}`, { method: "DELETE" });
    if (!rsp.ok) {
      throw new Error("删除失败");
    }

    await Promise.all([loadImageSummary(props.nodeId), loadImageList()]);
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

const toPNG = async (blob: Blob) => {
  if (blob.type === "image/png") {
    return blob;
  }

  const img = await createImageBitmap(blob);

  const canvas = document.createElement("canvas");
  canvas.width = img.width;
  canvas.height = img.height;

  const ctx = canvas.getContext("2d");

  if (!ctx) {
    throw Error("ctx 2d is null");
  }

  ctx.drawImage(img, 0, 0);

  return await new Promise<Blob>((resolve, reject) => {
    canvas.toBlob((newBlob) => (newBlob ? resolve(newBlob) : reject(Error("toBlob is null"))), "image/png");
  });
};

const copyImageToClipboard = async (e: MouseEvent) => {
  if (!largePreviewUrl.value) return;
  try {
    const rsp = await fetch(largePreviewUrl.value);
    const blob = await toPNG(await rsp.blob());
    await navigator.clipboard.write([
      new ClipboardItem({
        [blob.type || "image/png"]: blob,
      }),
    ]);

    const b = e.target as HTMLButtonElement;
    if (b) {
      b.disabled = true;

      setTimeout(() => (b.disabled = false), 2000);
    }
  } catch (error) {
    console.error(error);
  }
};

const formatSize = (size: number) => {
  if (size < 1024) return `${size} B`;
  if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KB`;
  return `${(size / (1024 * 1024)).toFixed(1)} MB`;
};

watch(
  () => props.nodeId,
  (newNodeId) => {
    isImageModalOpen.value = false;
    imageList.value = [];
    largePreviewUrl.value = "";
    loadImageSummary(newNodeId);
  },
  { immediate: true }
);
</script>

<style scoped>
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
