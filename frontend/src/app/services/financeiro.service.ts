import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { catchError, Observable, throwError } from 'rxjs';
import { Lancamento } from '../models/financeiro.model';
import { GastosCategoria } from '../models/gastos-categoria.model';

export interface ItemCaro {
  descricao: string;
  valorTotal: number;
  local?: string; 
}

export interface DadosEvolucao {
  mes: number;
  ano: number;
  totalDespesas: number;
  totalReceitas: number;
}

// Interface para o novo formato de evolução mensal
export interface EvolucaoMensalResponse {
  meses: string[];
  receitas: number[];
  despesas: number[];
  saldos: number[];
  detalhes: any[];
}

// Interface para o resumo mensal
export interface ResumoMensalResponse {
  receitas: number;
  despesas: number;
  saldo: number;
  saldoAcumulado: number;
  mesAtual: string;
  receitasTotal: number;
  despesasTotal: number;
  saldoTotal: number;
}

@Injectable({
  providedIn: 'root'
})
export class FinanceiroService {
  private apiUrl = 'http://localhost:5037/api/Lancamentos';
  private apiUrl2 = 'http://localhost:5037/api/Financeiro';

  constructor(private http: HttpClient) { }

  listarCategorias(): Observable<any[]> {
    const urlCategorias = this.apiUrl.replace('/Lancamentos', '/Categorias');
    return this.http.get<any[]>(urlCategorias);
  }

  // 🎯 ADICIONE ESSE MÉTODO AQUI:
  salvarCategoria(categoria: any): Observable<any> {
    const urlCategorias = this.apiUrl.replace('/Lancamentos', '/Categorias');
    return this.http.post<any>(urlCategorias, categoria);
  }

  // --- MÉTODOS DE CONSULTA (BI) CORRIGIDOS ---

  // CORRIGIDO: Retorna o novo formato do gráfico de evolução
  getEvolucaoMensal(): Observable<EvolucaoMensalResponse> {
    return this.http.get<EvolucaoMensalResponse>(`${this.apiUrl}/evolucao-mensal`).pipe(
      catchError(err => {
        console.error('Erro ao carregar evolução mensal:', err);
        return throwError(() => err);
      })
    );
  }

  // CORRIGIDO: Retorna o resumo com saldo acumulado
  getResumoMensal(mes: number, ano: number): Observable<ResumoMensalResponse> {
    return this.http.get<ResumoMensalResponse>(`${this.apiUrl}/resumo-mensal?mes=${mes}&ano=${ano}`).pipe(
      catchError(err => {
        console.error('Erro ao carregar resumo mensal:', err);
        return throwError(() => err);
      })
    );
  }

  // CORRIGIDO: Retorna dashboard completo
  getDashboardCompleto(): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/dashboard-completo`).pipe(
      catchError(err => {
        console.error('Erro ao carregar dashboard completo:', err);
        return throwError(() => err);
      })
    );
  }

  getLancamentos(): Observable<any[]> {
    const token = localStorage.getItem('token'); 
    const urlFinal = `${this.apiUrl}`; 

    const headers = new HttpHeaders({
      'Authorization': `Bearer ${token}`
    });

    console.log('Chamando API com Token:', token ? 'Presente' : 'Ausente');

    return this.http.get<any[]>(urlFinal, { headers }).pipe(
      catchError(err => {
        console.error('Erro detalhado na chamada:', err);
        return throwError(() => err);
      })
    );
  }

  getItensPorLancamento(lancamentoId: number): Observable<any[]> {
    return this.http.get<any[]>(`${this.apiUrl}/${lancamentoId}/itens`);
  }

  getItensMaisCaros(mes: number, ano: number): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/itens-mais-caros?mes=${mes}&ano=${ano}`).pipe(
      catchError(err => {
        console.error('Erro ao carregar itens mais caros:', err);
        return throwError(() => err);
      })
    );
  }
  
  getGastosPorCategoria(): Observable<GastosCategoria[]> {
    return this.http.get<GastosCategoria[]>(`${this.apiUrl}/gastos-categoria`).pipe(
      catchError(err => {
        console.error('Erro ao carregar gastos por categoria:', err);
        return throwError(() => err);
      })
    );
  }

  // --- MÉTODOS DE IMPORTAÇÃO E SALVAMENTO ---

  salvarLancamentoManual(dados: any): Observable<any> {
    return this.http.post(`${this.apiUrl}/lancamento-manual`, dados);
  }

  importarNota(urlOuChave: string): Observable<Lancamento> {
    return this.http.post<Lancamento>(`${this.apiUrl}/importar-nota`, { url: urlOuChave });
  }

  uploadPdf(formData: FormData): Observable<Lancamento> {
    return this.http.post<Lancamento>(`${this.apiUrl}/importar-pdf`, formData);
  }

  salvarLancamento(lancamento: Lancamento): Observable<Lancamento> {
    return this.http.post<Lancamento>(this.apiUrl, lancamento);
  }

  // --- MÉTODOS DE GERENCIAMENTO ---

  excluirLancamento(id: number): Observable<any> {
    return this.http.delete(`${this.apiUrl}/${id}`);
  }

  // 🎯 ADICIONE ESTES DOIS MÉTODOS NO SEU FINANCEIRO SERVICE:

  atualizarCategoria(id: number, categoria: any): Observable<any> {
    const urlCategorias = this.apiUrl.replace('/Lancamentos', '/Categorias');
    return this.http.put<any>(`${urlCategorias}/${id}`, categoria);
  }

  excluirCategoria(id: number): Observable<any> {
    const urlCategorias = this.apiUrl.replace('/Lancamentos', '/Categorias');
    return this.http.delete<any>(`${urlCategorias}/${id}`);
  }
}