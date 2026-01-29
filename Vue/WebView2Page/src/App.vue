<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
import HelloWorld from './components/HelloWorld.vue'
import { provide, useTemplateRef } from 'vue';
import type { ShallowRefMarker } from '@vue/reactivity';
import { defineComponent, ref, onMounted } from "vue";
import SurveyTree from './views/SurveyTree.vue';
import PopPage from './views/PopPage.vue';
import TabPage from './views/TabPage.vue';
import SearchLayout from './views/SearchLayout.vue';
import ListPage from './views/ListPage.vue';
import RootNodeListPage from "./views/RootNodeListPage.vue";
import { type ListItem, type NodeData, type ViewTreeData, type Tabs, type MessageData, type NodeSearchResult, onAddNodeKey, onFindChildNodeKey, onUPNodeKey, isViewAddAndUpDataButtonKey } from './mytype';


type FunctionMap = {
  ADDNODE: {
    args: NodeData;
    return: Promise<NodeData>;
  };

  QUERY: {
    args: number;
    return: Promise<{ root: NodeData, child: NodeData[] }>;
  };

  SEARCH: {
    args: string;
    return: Promise<NodeSearchResult[]>;
  };

  UPDATA: {
    args: NodeData;
    return: Promise<NodeData>;
  };


  CLIPBOARDHISTORY:{
    args:number;
    return:Promise<string[]>;
  }

};


const messageFunc = (() => {


  ((<any>window).chrome).webview.addEventListener('message', (event: any) => {

    const obj = <MessageData>JSON.parse(event.data);
    
    const func = map.get(obj.index);


    if (!func) {
      throw new Error("map get value error");
    }

    map.delete(obj.index);

    func(obj);


  });

  function sendToDotNet(message: MessageData) {

    ((<any>window).chrome).webview.postMessage(message);
  }

  const sendToDotNetWithTypeIndex = (<T>(type: string, value: any) => {

    const index = getIndex();

    return new Promise<T>((res) => {

      sendToDotNet({ type: type, index: index, value: value });

      map.set(index, (data) => {



        if (data.type !== type) {
          throw new Error("map get value type error");
        }

        res(data.value);



      });



    });
  });


  const map = new Map<number, (data: MessageData) => void>();

  let index = 0;

  function getIndex() {
    let n = index++;

    return n;
  }


  return <K extends keyof FunctionMap, TRWP = Awaited<FunctionMap[K]["return"]>>(
    name: K,
    args: FunctionMap[K]["args"]): Promise<TRWP> => {

    return sendToDotNetWithTypeIndex(name, args);

  };

})();



const handleSearch2 = async (value: string) => {
 
  const vs = await messageFunc("SEARCH", value);
  const fvs: ListItem[] = vs.map(v => { 
    return { 
      text: v.item.title, 
      id: v.item.id,
      path: v.parents.length > 0 ? v.parents.map(p => p.title).join(" -> ") : undefined
    } 
  });
  listPageData.value = fvs;
  if(fvs.length === 0){
    listIsView.value =false;
  }
  else{
    listIsView.value = true;
  }

  


  //data.value.push({text:value,id:id++});

  
};


const addTabValue = ref<ListItem | null>(null);

const onSelect = (v: ListItem) => {


  listIsView.value = false;

  addTabValue.value = { ...v, type: 'survey' };


};

const listPageData = ref<ListItem[]>([]);

const rootNodeListPageData = ref<ListItem[]>([]);

const listIsView = ref(false);


async function findChildNode2(id: number) {

  const obj = await messageFunc("QUERY", id);


  return {
    id: obj.root.id,
    parentId: obj.root.parentId,
    title: obj.root.title,

    options: obj.child.map(v => { return { id: v.id, label: v.title } })
  };


}

async function addNode2(text: string, parentId: number) {
  if (text) {

    return await messageFunc("ADDNODE", {
      id: 0,

      parentId: parentId,

      title: text
    })
  }

  return null;
}

const isViewPop = ref(false);


function onInputOverText2(text: string) {
  isViewPop.value = false;



  if (text) {

    const data = {
      id: 0,

      parentId: null,

      title: text
    };


    messageFunc("ADDNODE", data);


  }



}

function onAddRoot() {

  isViewPop.value = true;
}


async function onText() {


}

const upNode = async (id: number, text: string) => {

  return await messageFunc("UPDATA", { id: id, parentId: null, title: text });
};


const isViewAddAndUpDataButton = ref(false);

const onViewAddAndUpDataButton= ()=>{

  isViewAddAndUpDataButton.value = !isViewAddAndUpDataButton.value;

};


const initRootNodeListPageData= async ()=>{

  const vs = await messageFunc("SEARCH", "");
  const fvs: ListItem[] = vs.map(v => { 
    return { 
      text: v.item.title, 
      id: v.item.id,
      path: v.parents.length > 0 ? v.parents.map(p => p.title).join(" -> ") : undefined
    } 
  });
  rootNodeListPageData.value = fvs;

};


const onViewClipboardHistory = async ()=>{

  
  const vs = await messageFunc("CLIPBOARDHISTORY", 0);
 
  addTabValue.value = {
    text: `粘贴板 ${new Date().toLocaleTimeString()}`,
    id: Date.now(),
    type: 'clipboard',
    content: vs.join("\r\n\r\n")
  };

};


setTimeout(initRootNodeListPageData, 1000);

provide(onAddNodeKey, addNode2);

provide(isViewAddAndUpDataButtonKey, isViewAddAndUpDataButton);


provide(onFindChildNodeKey, findChildNode2);


provide(onUPNodeKey, upNode);

</script>




<template>
  <div class="app-root">
    <div class="left-e">
      <RootNodeListPage :items="rootNodeListPageData" @item-click="onSelect"></RootNodeListPage>
    </div>
    <div class="right-e">
      <div class="app-search">
        <SearchLayout @search-change="handleSearch2"></SearchLayout> 
        <button @click="onAddRoot">添加根</button>
        <button @click="onViewAddAndUpDataButton">切换显示更改按钮</button>
        <button @click="onViewClipboardHistory">粘贴板</button>
        <button @click="onText">测试</button>
      </div>
      <div class="app-content">
        <ListPage v-show="listIsView" :items="listPageData" @item-click="onSelect"></ListPage>
        <TabPage :add-tab-value="addTabValue" v-show="!listIsView"></TabPage>
      </div>
    </div>
    
    <PopPage in-text="" @on-confirm-text="onInputOverText2" v-if="isViewPop"></PopPage>
  </div>
</template>


<style scoped>
.app-root{
  position: fixed;
  top: 0px;
  left: 0px;
  display: flex;
  flex-direction: row;
  width: 100%;
  height:100%;
  overflow: hidden;
  background: #f5f7fa;
}

.left-e{
  flex: 1;
  display: flex;
  flex-direction: column;
  width: 100%;
  height:100%;
  background: #ffffff;
  border-right: 1px solid #e4e7ed;
  box-shadow: 2px 0 8px rgba(0, 0, 0, 0.04);
}

.right-e{
  flex: 7;
  display: flex;
  flex-direction: column;
  width: 100%;
  height:100%;
  background: #f5f7fa;
}

.app-search{
  flex: 0 0 auto;
  padding: 16px;
  background: #ffffff;
  border-bottom: 1px solid #e4e7ed;
  display: flex;
  align-items: center;
  gap: 12px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.02);
}

.app-search button {
  padding: 8px 16px;
  font-size: 14px;
  border: 1px solid #dcdfe6;
  border-radius: 4px;
  background: #ffffff;
  color: #606266;
  cursor: pointer;
  transition: all 0.3s ease;
  white-space: nowrap;
}

.app-search button:hover {
  background: #ecf5ff;
  border-color: #b3d8ff;
  color: #409eff;
}

.app-search button:active {
  background: #d9ecff;
  border-color: #409eff;
}

.app-content{
  flex: 1;
  display: flex;
  flex-direction: column;
  width: 100%;
  height: 100%;
  overflow: hidden;
}

</style>
