export interface Categoria {
  id: number;
  nome: string;
}

export interface Lancamento {
  id?: number;
  descricao: string;
  valor: number;
  dataEmissao: Date;
  dataImportacao?: Date;
  tipo: string;
  categoriaId: number;
  
  // ADICIONE ESTA LINHA:
  categoria?: Categoria; 
  
  itens?: any[]; // ou a interface de itens que você já tiver
}