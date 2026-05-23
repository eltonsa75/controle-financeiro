import { Component, OnInit, AfterViewInit, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FinanceiroService, EvolucaoMensalResponse, ResumoMensalResponse } from '../../services/financeiro.service';
import { FormsModule } from '@angular/forms';
import { Chart, registerables } from 'chart.js';
import ChartDataLabels from 'chartjs-plugin-datalabels';

Chart.register(...registerables, ChartDataLabels);

export interface ItemCaro {
  descricao: string;
  valorTotal: number;
  local?: string;
}

const CORES_PALETA = ['#e74c3c', '#3498db', '#f1c40f', '#2ecc71', '#9b59b6', '#e67e22'];

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent implements OnInit, AfterViewInit {

  @ViewChild('canvasCategorias') canvasRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('canvasCarosRef') canvasCarosRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('canvasEvolucao') canvasEvolucaoRef!: ElementRef<HTMLCanvasElement>;

  chart: any;
  chartCaros: any;
  chartEvolucao: any;

  mesSelecionado: number = new Date().getMonth() + 1;
  anoSelecionado: number = new Date().getFullYear();

  // Cards KPI
  totalReceitas: number = 0;
  totalDespesas: number = 0;
  saldoMes: number = 0;
  saldoAcumulado: number = 0;
  receitasTotal: number = 0;
  despesasTotal: number = 0;
  saldoTotal: number = 0;
  mesAtual: string = '';

  urlNota: string = '';

  constructor(private financeiroService: FinanceiroService) {}

  ngOnInit(): void {
    this.carregarDadosMensais();
  }

  ngAfterViewInit(): void {
    setTimeout(() => this.carregarEvolucaoMensal(), 300);
  }

  getNomeMes(mes: number): string {
    const meses = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez'];
    return meses[mes - 1] || '';
  }

  formatarMoeda(valor: number): string {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL'
    }).format(valor);
  }

alterarMes(delta: number): void {
  // O PULO DO GATO: Se o usuário estiver em Janeiro de 2026 e tentar voltar (-1), o código barra na hora
  if (this.mesSelecionado === 1 && this.anoSelecionado === 2026 && delta === -1) {
    console.log('[DEBUG] Navegação bloqueada: Limite mínimo é Janeiro de 2026.');
    return;
  }

  this.mesSelecionado += delta;
  if (this.mesSelecionado > 12) {
    this.mesSelecionado = 1;
    this.anoSelecionado++;
  } else if (this.mesSelecionado < 1) {
    this.mesSelecionado = 12;
    this.anoSelecionado--;
  }
  
  this.carregarDadosMensais();
}

  carregarDadosMensais(): void {
    this.carregarResumoCard();
    this.carregarGraficoCategorias();
    this.carregarGraficoMaisCaros();
  }

  carregarResumoCard(): void {
    this.financeiroService.getResumoMensal(this.mesSelecionado, this.anoSelecionado).subscribe({
      next: (resposta: ResumoMensalResponse) => {
        console.log('[DEBUG DASHBOARD] Resumo recebido do back-end:', resposta);
        
        this.totalReceitas = resposta.receitas || 0;
        this.totalDespesas = Math.abs(resposta.despesas || 0); 
        this.saldoMes = resposta.saldo || 0;
        this.saldoAcumulado = resposta.saldoAcumulado || 0;
        this.receitasTotal = resposta.receitasTotal || 0;
        this.despesasTotal = Math.abs(resposta.despesasTotal || 0);
        this.saldoTotal = resposta.saldoTotal || 0;
        this.mesAtual = resposta.mesAtual || '';
      },
      error: (err: any) => console.error('Erro ao carregar o resumo mensal dos cards:', err)
    });
  }

  carregarEvolucaoMensal(): void {
    this.financeiroService.getEvolucaoMensal().subscribe({
      next: (dados: EvolucaoMensalResponse) => {
        if (!this.canvasEvolucaoRef?.nativeElement || !dados || !dados.meses || dados.meses.length === 0) {
          console.log('[DEBUG GRAPH] Sem dados estruturados para o gráfico de evolução histórica');
          return;
        }

        console.log('[DEBUG GRAPH] Resposta estruturada recebida do back-end:', dados);

        // Alimentação direta mapeada com base no contrato unificado do back-end
        const labels = dados.meses;
        const datasetReceitas = dados.receitas;
        const datasetDespesas = dados.despesas;

        if (this.chartEvolucao) this.chartEvolucao.destroy();

        this.chartEvolucao = new Chart(this.canvasEvolucaoRef.nativeElement, {
          type: 'line',
          data: {
            labels: labels,
            datasets: [
              {
                label: 'Receitas',
                data: datasetReceitas,
                borderColor: '#28a745',
                backgroundColor: 'rgba(40, 167, 69, 0.05)',
                tension: 0.3,
                fill: true,
                pointBackgroundColor: '#28a745',
                pointBorderColor: '#fff',
                pointRadius: 4,
                pointHoverRadius: 6
              },
              {
                label: 'Despesas',
                data: datasetDespesas,
                borderColor: '#dc3545',
                backgroundColor: 'rgba(220, 53, 69, 0.05)',
                tension: 0.3,
                fill: true,
                pointBackgroundColor: '#dc3545',
                pointBorderColor: '#fff',
                pointRadius: 4,
                pointHoverRadius: 6
              }
            ]
          },
          options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
              tooltip: {
                callbacks: {
                  label: (context: any) => {
                    const value = context.raw as number;
                    return `${context.dataset.label}: R$ ${value.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}`;
                  }
                }
              },
              datalabels: {
                display: true,
                align: 'top',
                backgroundColor: 'rgba(0,0,0,0.7)',
                borderRadius: 4,
                padding: { left: 6, right: 6, top: 2, bottom: 2 },
                color: 'white',
                font: { weight: 'bold', size: 10 },
                formatter: (value: number) => value > 0 ? `R$ ${value.toLocaleString('pt-BR', { maximumFractionDigits: 0 })}` : ''
              }
            },
            scales: {
              y: {
                beginAtZero: true,
                ticks: {
                  callback: (value: any) => `R$ ${Number(value).toLocaleString('pt-BR')}`
                }
              }
            }
          }
        });
      },
      error: (err: any) => console.error('Erro ao carregar histórico do gráfico de evolução:', err)
    });
  }

  carregarGraficoCategorias(): void {
    this.financeiroService.getGastosPorCategoria().subscribe({
      next: (dadosDoBanco: any[]) => {
        if (!this.canvasRef?.nativeElement || !dadosDoBanco || dadosDoBanco.length === 0) return;
        if (this.chart) this.chart.destroy();

        this.chart = new Chart(this.canvasRef.nativeElement, {
          type: 'doughnut',
          data: {
            labels: dadosDoBanco.map(d => d.categoria),
            datasets: [{
              data: dadosDoBanco.map(d => d.valor),
              backgroundColor: CORES_PALETA,
              borderWidth: 1
            }]
          },
          options: { 
            responsive: true,
            maintainAspectRatio: false,
            plugins: { 
              datalabels: { 
                display: true,
                formatter: (value: number) => `R$ ${value.toLocaleString('pt-BR')}`,
                backgroundColor: 'rgba(0,0,0,0.6)',
                borderRadius: 4,
                padding: 4,
                color: 'white',
                font: { weight: 'bold', size: 10 }
              } 
            } 
          }
        });
      },
      error: (err: any) => console.error('Erro ao carregar gráfico por categorias:', err)
    });
  }

  carregarGraficoMaisCaros(): void {
    this.financeiroService.getItensMaisCaros(this.mesSelecionado, this.anoSelecionado).subscribe({
      next: (itens: ItemCaro[]) => {
        if (!this.canvasCarosRef?.nativeElement || !itens || itens.length === 0) return;
        if (this.chartCaros) this.chartCaros.destroy();

        this.chartCaros = new Chart(this.canvasCarosRef.nativeElement, {
          type: 'bar',
          data: {
            labels: itens.map(i => i.descricao.length > 15 ? i.descricao.substring(0, 15) + '...' : i.descricao),
            datasets: [{
              label: 'Valor (R$)',
              data: itens.map(i => i.valorTotal),
              backgroundColor: '#3498db',
              borderRadius: 5
            }]
          },
          options: {
            indexAxis: 'y',
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
              tooltip: {
                callbacks: {
                  label: (context: any) => {
                    const value = context.raw as number;
                    return `R$ ${value.toLocaleString('pt-BR')}`;
                  }
                }
              },
              datalabels: {
                anchor: 'end',
                align: 'end',
                formatter: (value: number) => `R$ ${value.toLocaleString('pt-BR')}`,
                backgroundColor: 'rgba(0,0,0,0.6)',
                borderRadius: 4,
                padding: { left: 6, right: 6, top: 2, bottom: 2 },
                color: 'white',
                font: { weight: 'bold', size: 10 }
              }
            }
          }
        });
      },
      error: (err: any) => console.error('Erro ao carregar gráfico de itens caros:', err)
    });
  }

  importarNota(): void {
    if (!this.urlNota) return;
    this.financeiroService.importarNota(this.urlNota).subscribe({
      next: () => {
        this.urlNota = '';
        this.carregarDadosMensais();
        this.carregarEvolucaoMensal(); // Recarrega a evolução histórica caso entre nota nova
      },
      error: (err: any) => console.error('Erro ao importar nota:', err)
    });
  }
}