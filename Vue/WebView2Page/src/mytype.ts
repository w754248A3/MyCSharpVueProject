import type { InjectionKey, Ref } from "vue"

export interface ListItem {
  text: string
  id: number
  path?: string
}

export interface NodeSearchResult {
  item: NodeData;
  parents: NodeData[];
}

export interface Tabs {
  text: string
  id: number
  index:number
}

export interface NodeData{
  id: number;
  parentId: number | null;
  title: string;
}

export interface MessageData{
  type:string;
  index:number;
  value:any;
}


export interface Option {
  id: number;
  label: string;
}

export interface ViewTreeData {
  id: number;
  parentId: number | null;
  title: string;
  options: Option[];
}

export const onAddNodeKey = Symbol() as  InjectionKey<(text:string, parentId:number)=> Promise<NodeData | null>>


export const onUPNodeKey = Symbol() as  InjectionKey<(id:number, text:string)=> Promise<NodeData | null>>

export const onFindChildNodeKey = Symbol() as  InjectionKey<(id:number)=> Promise<ViewTreeData>>

export const isViewAddAndUpDataButtonKey = Symbol() as  InjectionKey<Ref<boolean, boolean>>
