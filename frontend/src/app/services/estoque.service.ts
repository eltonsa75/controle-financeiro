import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

// Interface que espelha exatamente a nossa model do C#
export interface ItemEstoque {
  id: number;
  nome: string;
  quantidadeAtual: number;
  quantidadeMinima: number;
  unidadeMedida: string;
  categoriaId: number;
  categoria?: {
    id: number;
    nome: string;
    corHex: string;
  };
}

@Injectable({
  providedIn: 'root'
})
export class EstoqueService {
  
  // Ajuste a URL base de acordo com o padrão do seu projeto (ex: http://localhost:5123/api/estoque)
  private readonly API = 'http://localhost:5037/api/estoque'; 

  constructor(private http: HttpClient) { }

  // Listar todo o estoque do usuário logado
  listar(): Observable<ItemEstoque[]> {
    return this.http.get<ItemEstoque[]>(this.API);
  }

  // Criar um novo item na despensa
  salvar(item: ItemEstoque): Observable<ItemEstoque> {
    if (item.id && item.id > 0) {
      return this.http.put<ItemEstoque>(`${this.API}/${item.id}`, item);
    }
    return this.http.post<ItemEstoque>(this.API, item);
  }

  // 🎯 Diferencial: Ajuste rápido de +1 ou -1 usando o PATCH que criamos no C#
  ajustarQuantidade(id: number, valor: number): Observable<ItemEstoque> {
    return this.http.patch<ItemEstoque>(`${this.API}/${id}/ajustar?valor=${valor}`, {});
  }

  // Excluir item do estoque
  excluir(id: number): Observable<void> {
    return this.http.delete<void>(`${this.API}/${id}`);
  }
}