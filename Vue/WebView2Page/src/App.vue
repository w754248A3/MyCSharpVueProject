<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
import HelloWorld from './components/HelloWorld.vue'
import { useTemplateRef } from 'vue';
import type { ShallowRefMarker } from '@vue/reactivity';

type FunctionMap = {
  ADDNODE: {
    args: TableData;
    return: Promise<TableData>;
  };

  QUERY: {
    args: number;
    return: Promise<{root:TableData, child:TableData[]}>;
  };

  SEARCH:{
    args:string;
    return: Promise<TableData[]>;
  };
 
};


const messageFunc = (()=>{


  ((<any>window).chrome).webview.addEventListener('message', (event:any) => {
      
    const obj =  <MessageData>JSON.parse(event.data);

      const func = map.get(obj.index);

      
    if(!func){
      throw new Error("map get value error");
    }

    map.delete(obj.index);

    if(obj.type === "ADDNODE"){

      func(obj.value);

    }
    else if(obj.type === "QUERY"){
      func(obj.value);
    }
    else if(obj.type === "SEARCH"){
      func(obj.value);
    }
    else{
      throw new Error(`type is not define ${obj.type}`);
    }



  });

  function sendToDotNet(message:MessageData) {

      ((<any>window).chrome).webview.postMessage(message);
  }


  const map= new Map<number, (data:any)=> void>();

  let index=0;

  function getIndex(){
    let n = index++;

    return n;
  }
  function f<K extends keyof FunctionMap>(
  name: K,
  args: FunctionMap[K]["args"]
  ):FunctionMap[K]["return"] {

    if(name === "ADDNODE"){
    
      const data = args;
      const index = getIndex();

      return new Promise<NodeData>((res)=>{

        sendToDotNet({type:name, index:index, value:data});

        map.set(index, (data)=>{

          

          res(data);

          console.log(data);
        });

      });

    }
    else if(name == "QUERY") {
      
      const id = args;
      const index = getIndex();
      return new Promise<{root:NodeData, child:NodeData[]}>((res)=>{

        sendToDotNet({type:name, index:index, value:id});

        map.set(index, (data)=>{

          

          res(data);

          console.log(data);
        });

      });


    }
    else if(name == "SEARCH") {
      
      const searchText = args;
      const index = getIndex();
      return new Promise<NodeData>((res)=>{

        sendToDotNet({type:name, index:index, value:searchText});

        map.set(index, (data)=>{

          

          res(data);

          console.log(data);
        });

      });


    }
    else{
      throw new Error("not message type");
    }
  };


  return f;

})();


import { defineComponent, ref, onMounted } from "vue";
import SurveyTree from './views/SurveyTree.vue';
import PopPage from './views/PopPage.vue';
import TabPage from './views/TabPage.vue';
import SearchLayout from './views/SearchLayout.vue';
import ListPage from './views/ListPage.vue';
import type { ListItem, TableData , NodeData, Tabs, MessageData} from './mytype';


let id = 0;
const handleSearch = (value: string) => {
      console.log("搜索内容变化：", value);
      messageFunc("SEARCH", value);
      const fvs = tableData.filter(v=> v.parentId === null).filter(v=> v.title.indexOf(value) !== -1)
      .map(v=> {return {text:v.title, id:v.id}});

      data.value= fvs;


      //data.value.push({text:value,id:id++});

      listIsView.value= true;
};


const handleSearch2 = async (value: string) => {
      console.log("搜索内容变化：", value);
      const vs = await messageFunc("SEARCH", value);
      const fvs =vs.map(v=> {return {text:v.title, id:v.id}});

      data.value= fvs;


      //data.value.push({text:value,id:id++});

      listIsView.value= true;
};


let tabIndex = 0;
const onSelect= (v:ListItem)=>{


  listIsView.value=false;

  tabIndex++;
  tab.value.push({text:v.text, id:v.id, index:tabIndex});

  console.log(v);

};

const data = ref<ListItem[]>([]);

const tab = ref<Tabs[]>([]);

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


  function getTableDataRootNode(id:number):NodeData{
    const vs = tableData.filter(v=> v.parentId=== null);

    if(vs.length ===0){
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

    messageFunc("QUERY", id);

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

  async function findChildNode2(id:number){
    
    const obj = await messageFunc("QUERY", id);

    
    return{
      id:obj.root.id,
      parentId:obj.root.parentId,
      title:obj.root.title,

      options: obj.child.map(v=> {return {id:v.id, label:v.title}})
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

    function addNode2(text:string, parentId:number){
    if(text){
        
        messageFunc("ADDNODE", {
          id:0,

          parentId:parentId,

          title:text
        })
      }
  }

  const isViewPop = ref(false);

function onInputOverText(text:string){
  isViewPop.value=false;



  if(text){
    let vs = tableData.map(v=> v.id);
    const newID = Math.max(...vs)+1;

    const data = {
          id:newID,

          parentId:null,

          title:text
        };

     tableData.push(data);

     messageFunc("ADDNODE", data);


    const json = JSON.stringify(tableData);


    window.localStorage.setItem(key, json);

  }

  

}



function onInputOverText2(text:string){
  isViewPop.value=false;



  if(text){
    
    const data = {
          id:0,

          parentId:null,

          title:text
        };

     
     messageFunc("ADDNODE", data);


  }

  

}

function onAddRoot(){

  isViewPop.value=true;
}


async function onText(){

  const vs = <typeof tableData>JSON.parse(JSON.stringify(tableData));
  const map = new Map<number, number>();

  for (const element of vs) {
    
    if(element.parentId){
      const pid = map.get(element.parentId);

      if(pid){
        element.parentId=pid;
    }
    }

  
    const id = element.id
    const obj = await messageFunc("ADDNODE", element);

    map.set(id, obj.id);




  }


}

</script>




<template>
  <button @click="onText">测试</button>
  <button @click="onAddRoot">添加根</button>
  <SearchLayout @search-change="handleSearch2">
    <ListPage v-show="listIsView" :items="data" @item-click="onSelect"></ListPage>
    <TabPage :tabs="tab" v-show="!listIsView"
    :add-node="addNode2"
        :find-child-node="findChildNode2"
        :get-table-data-root-node="findChildNode2"
        ></TabPage>
  </SearchLayout>
  <PopPage in-text="" @on-confirm-text="onInputOverText2" v-if="isViewPop"></PopPage>
</template>


<style scoped>
</style>
