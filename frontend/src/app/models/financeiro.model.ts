export interface Categoria {
  id: number;
  nome: string;
}

export interface ItemCompra {
  id?: number;
  descricao: string;
  quantidade: number;
  unidade: string;
  preco: number;
  comprado: boolean;
  categoria?: string; // Pode ser string ou o objeto, dependendo da sua lógica de classificação
}

export interface Lancamento {
  id?: number;
  descricao: string;
  valor: number;
  tipo: string;
  data: string | Date;
  dataEmissao: string | Date;
  dataImportacao: string | Date;
  categoriaId: number;
  
  // 🔥 ADICIONE ESTA LINHA: 
  // É ela que permite usar item.categoria?.nome no HTML
  categoria?: Categoria; 
  
  usuarioId?: string;
  itens?: ItemCompra[]; // Ajustado de any[] para usar sua interface ItemCompra
}