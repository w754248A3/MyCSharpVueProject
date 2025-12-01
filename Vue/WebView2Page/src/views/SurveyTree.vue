<template>
  <div class="survey-tree">
    <div v-for="(node, index) in displayedNodes" :key="node.id" class="node-section">
      <!-- 标题 -->
      <div class="node-title" @click="toggleNode(node.id)">
        {{ node.title }}
        
        <span class="toggle-icon">{{ collapsed[node.id] ? "▼" : "▲" }}</span>
      </div>

      <!-- 选项 -->
      <div v-show="node.options.length ===0 || !collapsed[node.id]" class="options">
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

        <!--button-->
        <div v-show="isViewAddAndUpDataButton" class="node-button">
          <button @click="onAddNode(node.id)">Add Node</button>
          <button @click="onUpNode(node.id, node.title)">UP Node</button>
        </div>
      </div>   
    </div>
    <div v-if="isOpenPop">
    <PopPage :in-text="text" @on-confirm-text="outText"></PopPage>
  </div>
  </div>
  
</template>

<script setup lang="ts">
import { defineComponent, inject, reactive, ref } from "vue";
import PopPage from "./PopPage.vue";
import {type NodeData, type ViewTreeData, type Option, onFindChildNodeKey, onAddNodeKey, onUPNodeKey, isViewAddAndUpDataButtonKey} from "../mytype"


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

  const initRootNode = async () => {
    const root = await findChildNode(props.id);
    if (root) {
      displayedNodes.value = [root];
      collapsed[root.id] = false;
    }
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
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
  overflow: auto;
  font-family: "Segoe UI", "Helvetica Neue", Arial, sans-serif;
  padding: 16px;
  margin-bottom: 80px;
}

.node-section {
  border: 1px solid #e4e7ed;
  margin-bottom: 16px;
  border-radius: 8px;
  background: #ffffff;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
  transition: all 0.3s ease;
  overflow: hidden;
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
  user-select: none;
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
  user-select: none;
}

.option-item:hover .option-text {
  color: #409eff;
}

.option-item input[type="radio"]:checked + .option-text {
  color: #409eff;
  font-weight: 500;
}

</style>
