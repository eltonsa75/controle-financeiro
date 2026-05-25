import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

// Interface baseada no nosso UML e Back-end
export interface Categoria {
  id?: number;
  nome: string;
  metaMensal: number;
  corHex: string;
  palavrasChave: string;
  usuarioId: string;
}

// Interface para o resumo (Gasto vs Meta)
export interface CategoriaStatus extends Categoria {
  gastoAtual: number;
  percentual: number;
}

@Injectable({
  providedIn: 'root'
})
export class CategoriaService {
  
  // Rota apontando direto para o seu controller .NET
  private readonly API = 'http://localhost:5037/api/categorias';  

  constructor(private http: HttpClient) { }


  listarComStatus(mes: number, ano: number, tipo: string = 'mensal'): Observable<CategoriaStatus[]> {
    return this.http.get<CategoriaStatus[]>(`${this.API}/status?mes=${mes}&ano=${ano}&tipo=${tipo}`);
  }

 
  listarTodas(): Observable<Categoria[]> {
    return this.http.get<Categoria[]>(this.API);
  }

 
  salvar(categoria: Categoria): Observable<Categoria> {
    if (categoria.id && categoria.id > 0) {
      return this.http.put<Categoria>(`${this.API}/${categoria.id}`, categoria);
    }
    return this.http.post<Categoria>(this.API, categoria);
  }

  excluir(id: number): Observable<void> {
    return this.http.delete<void>(`${this.API}/${id}`);
  }
}