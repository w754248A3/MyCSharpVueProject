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
        <div class="node-button">
          <button @click="onAddNode(node.id)">Add Node</button>
        </div>
      </div>

      
    </div>
    <div v-if="isOpenPop">
    <PopPage :in-text="text" @on-confirm-text="outText"></PopPage>
  </div>
  </div>
  
</template>

<script setup lang="ts">
import { defineComponent, reactive, ref } from "vue";
import PopPage from "./PopPage.vue";
import type {TableData, NodeData, Option} from "../mytype"


const isOpenPop =ref(false);


const funcs = defineProps<{
  getTableDataRootNode:(id:number)=> Promise<NodeData>,
  findChildNode:(id:number)=>Promise<NodeData>,
  addNode:(s:string, parentId:number)=> void
  id:number

}>();


const text = ref("");

const outText = ref<(s:string)=> void>((s)=> {});


  const displayedNodes = ref<NodeData[]>([]);
  const collapsed = reactive<Record<number, boolean>>({});
  const selected = reactive<Record<number, number | null>>({});

  const initRootNode = async () => {
    const root = await funcs.getTableDataRootNode(funcs.id);
    if (root) {
      displayedNodes.value = [root];
      collapsed[root.id] = false;
    }
  };

  const handleSelect = async (node: NodeData, option: Option) => {

    
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
    const child = await funcs.findChildNode(option.id);
    if (child) {
      collapsed[child.id] = false;
      displayedNodes.value.push(child);
    }
  };

  function onAddNode(parentId:number){

    text.value="";

    outText.value= (s)=>{

      isOpenPop.value=false;


      funcs.addNode(s, parentId);
    };


    isOpenPop.value=true;



  }

  const toggleNode = (nodeId: number) => {
    collapsed[nodeId] = !collapsed[nodeId];
  };

  initRootNode();

</script>

<style scoped>
.survey-tree {
  font-family: Arial, sans-serif;
  padding: 10px;
}

.node-section {
  border: 2px solid black;
  margin-bottom: 10px;
  border-radius: 5px;
}

.node-title {
  padding: 8px;
  cursor: pointer;
  font-weight: bold;
  display: flex;
  justify-content: space-between;
  white-space: pre-wrap;
}

.node-button{
  margin-left: 10px;
}


.options {
  padding: 10px;
}

.option-item {
  display: flex;
  align-items: center;
  margin-bottom: 8px;
}

.option-text {
  margin-left: 8px;
}
</style>
