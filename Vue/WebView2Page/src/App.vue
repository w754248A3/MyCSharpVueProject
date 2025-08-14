<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
import HelloWorld from './components/HelloWorld.vue'
import { useTemplateRef } from 'vue';
import type { ShallowRefMarker } from '@vue/reactivity';



((<any>window).chrome).webview.addEventListener('message', (event:any) => {

});

function sendToDotNet(message:any) {
    ((<any>window).chrome).webview.postMessage({mes:message});
}

import { defineComponent, ref, onMounted } from "vue";
import SurveyTree from './views/SurveyTree.vue';
import PopPage from './views/PopPage.vue';
import TabPage from './views/TabPage.vue';
import SearchLayout from './views/SearchLayout.vue';
import ListPage from './views/ListPage.vue';
import type { ListItem, TableData , NodeData} from './mytype';


let id = 0;
const handleSearch = (value: string) => {
      console.log("搜索内容变化：", value);

      data.value.push({text:value,id:id++});

      listIsView.value= true;
};


const onSelect= (v:ListItem)=>{

  listIsView.value=false;

  console.log(v);

};

const data = ref<ListItem[]>([]);

const listIsView = ref(false);


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

const key="datajsong67ssfds";

const json =  window.localStorage.getItem(key);

if(json){

tableData = JSON.parse(json);
}


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

  function addNode(text:string, parentId:number){
    if(text){
        let vs = tableData.map(v=> v.id);
        const newID = Math.max(...vs)+1;

        tableData.push({
          id:newID,

          parentId:parentId,

          title:text
        });


        const json = JSON.stringify(tableData);


        window.localStorage.setItem(key, json);
      }
  }



</script>




<template>
  <SearchLayout @search-change="handleSearch">

    <ListPage v-show="listIsView" :items="data" @item-click="onSelect"></ListPage>
    <TabPage v-show="!listIsView"
    :add-node="addNode"
        :find-child-node="findChildNode"
        :get-table-data-root-node="getTableDataRootNode"
        ></TabPage>
  </SearchLayout>
</template>


<style scoped>
</style>
