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
    <PopPage :in-text="text" :out-text="outText"></PopPage>
  </div>
  </div>
  
</template>

<script setup lang="ts">
import { defineComponent, reactive, ref } from "vue";
import PopPage from "./PopPage.vue";


interface TableData{
  id: number;
  parentId: number | null;
  title: string;
}

interface Option {
  id: number;
  label: string;
}

interface NodeData {
  id: number;
  parentId: number | null;
  title: string;
  options: Option[];
}

const key="datajsong67ssfds";

let tableData :TableData[]=[

  {
    id: 1,
    parentId: null,
    title: "请选择年份范围",
  },

  {
    id: 2,
    parentId: 1,
    title: "2000年-2005年",
   
  },
  {
    id: 3,
    parentId: 1,
    title: "2006年-2010年",
   
  },
  {
    id: 4,
    parentId: 1,
    title: "2011年-2015年",
    
  },

  {
    id: 5,
    parentId: 4,
    title: "手机",
    
  },

  {
    id: 6,
    parentId: 4,
    title: "冰箱",
    
  },
  {
    id: 7,
    parentId: 4,
    title: "空调",
    
  },

  {
    id: 8,
    parentId: 5,
    title: "CPU",
    
  },

  {
    id: 9,
    parentId: 5,
    title: "GPU",
    
  },
  {
    id: 10,
    parentId: 5,
    title: "RAM",
    
  },


];

const json =  window.localStorage.getItem(key);

if(json){

tableData = JSON.parse(json);
}



const isOpenPop =ref(false);


const text = ref("");

const outText = ref<(s:string)=> void>((s)=> {});


      function getTableDataRootNode():NodeData{
        const vs = tableData.filter(v=> v.parentId=== null);

        if(vs.length !==1){
          console.log(vs);
          throw new Error("find root node length not 1");
        }

        const rootNode = vs[0];


        const rootNodeChildNodes = tableData.filter(v=> v.parentId=== rootNode.id);

        return{
          id:rootNode.id,
          parentId:rootNode.parentId,
          title:rootNode.title,

          options: rootNodeChildNodes.map(v=> {return {id:v.id, label:v.title}})
        };


      }


      function findChildNode(id:number){
        const vs = tableData.filter(v=> v.id=== id);

        if(vs.length !==1){
          console.log(vs);
          throw new Error("find child node length not 1");
        }

        const childNode = vs[0];


        const childNodeChildNodes = tableData.filter(v=> v.parentId=== childNode.id);

        return{
          id:childNode.id,
          parentId:childNode.parentId,
          title:childNode.title,

          options: childNodeChildNodes.map(v=> {return {id:v.id, label:v.title}})
        };


      }

  const displayedNodes = ref<NodeData[]>([]);
  const collapsed = reactive<Record<number, boolean>>({});
  const selected = reactive<Record<number, number | null>>({});

  const initRootNode = () => {
    const root = getTableDataRootNode();
    if (root) {
      displayedNodes.value = [root];
      collapsed[root.id] = false;
    }
  };

  const handleSelect = (node: NodeData, option: Option) => {
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
    const child = findChildNode(option.id);
    if (child) {
      collapsed[child.id] = false;
      displayedNodes.value.push(child);
    }
  };

  function onAddNode(parentId:number){

    text.value="";

    outText.value= (s)=>{

      isOpenPop.value=false;


      if(s){
        let vs = tableData.map(v=> v.id);
        const newID = Math.max(...vs)+1;

        tableData.push({
          id:newID,

          parentId:parentId,

          title:s
        });


        const json = JSON.stringify(tableData);


        window.localStorage.setItem(key, json);
      }

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
