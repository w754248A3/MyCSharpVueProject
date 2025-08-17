import type { InjectionKey } from "vue"

export interface ListItem {
  text: string
  id: number
}

export interface Tabs {
  text: string
  id: number
  index:number
}

export interface TableData{
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

export interface NodeData {
  id: number;
  parentId: number | null;
  title: string;
  options: Option[];
}

export const onAddNodeKey = Symbol() as  InjectionKey<(text:string, parentId:number)=> void>

export const onFindChildNodeKey = Symbol() as  InjectionKey<(id:number)=> Promise<NodeData>>
