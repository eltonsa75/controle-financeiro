import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface ItemEstoque {
  id: number;
  nome: string;
  quantidadeAtual: number;
  quantidadeMinima: number;
  unidadeMedida: string;
  categoriaId: number;
  // Adicionamos corHex aqui para evitar erro no template HTML
  categoria?: { 
    nome: string; 
    corHex?: string; 
  }; 
}

@Injectable({
  providedIn: 'root'
})
export class EstoqueService {
  private readonly API = 'http://localhost:5037/api/estoque';

  constructor(private http: HttpClient) {}

  listar(): Observable<ItemEstoque[]> {
    return this.http.get<ItemEstoque[]>(this.API);
  }

  // O uso de Partial aqui resolve o erro "Property 'id' is missing"
  salvar(item: Partial<ItemEstoque>): Observable<any> {
    if (item.id) {
      return this.http.put(`${this.API}/${item.id}`, item);
    } else {
      return this.http.post(this.API, item);
    }
  }

  ajustarQuantidade(id: number, valor: number): Observable<ItemEstoque> {
    // Certifique-se de que o seu Controller aceita este formato de URL
    return this.http.patch<ItemEstoque>(`${this.API}/${id}/ajustar?valor=${valor}`, {});
  }

  excluir(id: number): Observable<any> {
    return this.http.delete(`${this.API}/${id}`);
  }
}