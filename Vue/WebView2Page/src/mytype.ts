
export interface ListItem {
  text: string
  id: string | number
}

export interface TableData{
  id: number;
  parentId: number | null;
  title: string;
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
