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
import { type ListItem, type NodeData, type ViewTreeData, type Tabs, type MessageData, onAddNodeKey, onFindChildNodeKey, onUPNodeKey, isViewAddAndUpDataButtonKey } from './mytype';


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
    return: Promise<NodeData[]>;
  };

  UPDATA: {
    args: NodeData;
    return: Promise<NodeData>;
  };

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
  const fvs = vs.map(v => { return { text: v.title, id: v.id } });

  listPageData.value = fvs;


  //data.value.push({text:value,id:id++});

  listIsView.value = true;
};


const addTabValue = ref<ListItem | null>(null);

const onSelect = (v: ListItem) => {


  listIsView.value = false;

  addTabValue.value = v;


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
  const fvs = vs.map(v => { return { text: v.title, id: v.id } });

  rootNodeListPageData.value=fvs;




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
        <button @click="onText">测试</button>
        <button @click="onAddRoot">添加根</button>
        <button @click="onViewAddAndUpDataButton">切换显示更改按钮</button>
        <SearchLayout @search-change="handleSearch2"></SearchLayout>
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

}

.left-e{

  flex: 1;

  display: flex;
  flex-direction: column;
  width: 100%;
  height:100%;
}

.right-e{
  flex: 7;

  display: flex;
  flex-direction: column;
  width: 100%;
  height:100%;
}


.app-search{
  flex: 0 0 auto;
 
}

.app-content{
  flex: 1;

  display: flex;
  flex-direction: column;
  width: 100%;
  height: 100%
}

</style>
