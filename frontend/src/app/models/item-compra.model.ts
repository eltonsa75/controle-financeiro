export interface ItemCompra {
  id: number;
  nome: string;
  quantidade: number;
  comprado: boolean;
}

export interface CompraAnterior {
  valor: number;
  local: string;
  data: Date;
}