export interface Categoria {
  id: number; // O banco espera number
  nome: string;
  corHex?: string;  // O '?' torna opcional, aceitando undefined
  ordem?: number;   // O '?' torna opcional
  metaMensal?: number;
  palavrasChave?: string;
  usuarioId?: string;
  // Campos calculados que você usa no front:
  gastoAtual?: number;
  percentual?: number;
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