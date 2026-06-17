import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, from, switchMap } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

export interface Lancamento {
  id?: number;
  descricao: string;
  valor: number;
  data: string;
  tipo: string;
  categoriaId: number;
  usuarioId?: string;
}

@Injectable({
  providedIn: 'root'
})
export class LancamentoService {

  private http = inject(HttpClient);
  private authService = inject(AuthService);

  private readonly apiUrl = `${environment.apiUrl}/lancamentos`;

  private getHeaders() {
    return from(this.authService.getToken()).pipe(
      switchMap(token => {

        console.log('Token enviado:', token ? 'SIM' : 'NÃO');

        const headers = new HttpHeaders({
          Authorization: `Bearer ${token}`
        });

        return [headers];
      })
    );
  }

  listarTodos(): Observable<Lancamento[]> {

    return this.getHeaders().pipe(
      switchMap(headers =>
        this.http.get<Lancamento[]>(this.apiUrl, { headers })
      )
    );
  }

  salvar(lancamento: Lancamento): Observable<Lancamento> {

    return this.getHeaders().pipe(
      switchMap(headers => {

        if (lancamento.id && lancamento.id > 0) {

          return this.http.put<Lancamento>(
            `${this.apiUrl}/${lancamento.id}`,
            lancamento,
            { headers }
          );

        } else {

          return this.http.post<Lancamento>(
            this.apiUrl,
            lancamento,
            { headers }
          );
        }
      })
    );
  }



getGastosCategoria(): Observable<any[]> {
    return this.http.get<any[]>(`${this.apiUrl}/gastos-categoria`);
  }

  getGastosAnuaisPorCategoria(): Observable<any[]> {
    return this.http.get<any[]>(`${this.apiUrl}/gastos-anuais`);
  }

  
  excluir(id: number): Observable<void> {

    return this.getHeaders().pipe(
      switchMap(headers =>
        this.http.delete<void>(
          `${this.apiUrl}/${id}`,
          { headers }
        )
      )
    );
  }

  // --- ADICIONE ESTE MÉTODO AO SEU LancamentoService ---

listarPaginado(page: number, pageSize: number): Observable<{ items: Lancamento[], total: number }> {
  return this.getHeaders().pipe(
    switchMap(headers => {
      // Define os parâmetros de paginação
      const params = new HttpParams()
        .set('page', page.toString())
        .set('pageSize', pageSize.toString());

      // Faz a chamada passando os parâmetros e os headers
      return this.http.get<{ items: Lancamento[], total: number }>(this.apiUrl, { 
        headers, 
        params 
      });
    })
  );
}
}