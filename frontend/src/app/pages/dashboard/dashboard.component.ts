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

// Expandimos e modernizamos um pouco a paleta de cores para os gráficos
const CORES_PALETA = ['#635bff', '#2ecc71', '#3498db', '#f1c40f', '#e74c3c', '#9b59b6', '#e67e22', '#1abc9c', '#34495e'];

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
        if (!this.canvasEvolucaoRef?.nativeElement || !dados || !dados.meses || dados.meses.length === 0) return;

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
                borderColor: '#2ecc71',
                backgroundColor: 'rgba(46, 204, 113, 0.04)',
                tension: 0.35,
                fill: true,
                pointBackgroundColor: '#2ecc71',
                pointBorderColor: '#fff',
                pointRadius: 4,
                pointHoverRadius: 6
              },
              {
                label: 'Despesas',
                data: datasetDespesas,
                borderColor: '#e74c3c',
                backgroundColor: 'rgba(231, 76, 60, 0.04)',
                tension: 0.35,
                fill: true,
                pointBackgroundColor: '#e74c3c',
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
              legend: {
                position: 'top',
                labels: { boxWidth: 15, font: { size: 12, weight: 500 } }
              },
              tooltip: {
                callbacks: {
                  label: (context: any) => {
                    const value = context.raw as number;
                    return ` ${context.dataset.label}: R$ ${value.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}`;
                  }
                }
              },
              datalabels: {
                display: true,
                align: 'top',
                backgroundColor: 'rgba(255,255,255,0.9)', // Fundo claro para contrastar com as linhas de grade
                borderRadius: 4,
                borderWidth: 1,
                borderColor: '#e5e7eb',
                padding: { left: 6, right: 6, top: 4, bottom: 4 },
                color: '#1f2937', // Texto escuro mais refinado
                font: { weight: 'bold', size: 10 },
                formatter: (value: number) => value > 0 ? `R$ ${value.toLocaleString('pt-BR', { maximumFractionDigits: 0 })}` : ''
              }
            },
            scales: {
              x: {
                grid: { display: false } // Remove as grades verticais que poluem
              },
              y: {
                beginAtZero: true,
                border: { display: false }, // Remove a linha preta rígida do eixo Y
                grid: { color: '#f3f4f6' }, // Grades horizontais super suaves e discretas
                ticks: {
                  color: '#9ca3af',
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
              borderWidth: 2,
              borderColor: '#ffffff'
            }]
          },
          options: { 
            responsive: true,
            maintainAspectRatio: false,
            plugins: { 
              legend: {
                position: 'right', // Joga as legendas para a direita na vertical, impedindo o achatamento da rosca
                labels: {
                  boxWidth: 12,
                  padding: 12,
                  font: { size: 11, weight: 500 },
                  color: '#4b5563'
                }
              },
              datalabels: { 
                display: true,
                // Mostra apenas valores significativos no gráfico para não amontoar números pequenos
                formatter: (value: number, context: any) => {
                  const dataset = context.dataset.data;
                  const total = dataset.reduce((acc: number, val: number) => acc + val, 0);
                  const percentual = (value / total) * 100;
                  return percentual > 4 ? `R$ ${value.toLocaleString('pt-BR', { maximumFractionDigits: 0 })}` : '';
                },
                backgroundColor: 'rgba(255,255,255,0.85)',
                borderRadius: 4,
                padding: 4,
                color: '#1f2937',
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
            labels: itens.map(i => i.descricao.length > 18 ? i.descricao.substring(0, 18) + '...' : i.descricao),
            datasets: [{
              label: 'Valor (R$)',
              data: itens.map(i => i.valorTotal),
              backgroundColor: 'rgba(99, 91, 255, 0.85)', // Usando o roxo identitário do seu app
              hoverBackgroundColor: '#635bff',
              borderRadius: 6, // Cantos arredondados modernos nas pontas das barras
              borderSkipped: false
            }]
          },
          options: {
            indexAxis: 'y', // Mantém o gráfico na horizontal (modo ranking)
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
              legend: { display: false }, // Oculta a legenda já que o título do card explica o gráfico
              tooltip: {
                callbacks: {
                  label: (context: any) => ` Valor: R$ ${context.raw.toLocaleString('pt-BR')}`
                }
              },
              datalabels: {
                anchor: 'end',
                align: 'end',
                formatter: (value: number) => `R$ ${value.toLocaleString('pt-BR')}`,
                backgroundColor: 'rgba(31, 41, 55, 0.9)',
                borderRadius: 4,
                padding: { left: 6, right: 6, top: 4, bottom: 4 },
                color: 'white',
                font: { weight: 'bold', size: 10 }
              }
            },
            scales: {
              x: {
                display: false, // Oculta o eixo inferior eliminando a redundância com as etiquetas de dados
                grid: { display: false }
              },
              y: {
                border: { display: false },
                grid: { display: false }, // Remove linhas de grade residuais
                ticks: {
                  color: '#4b5563',
                  font: { size: 12, weight: 500 }
                }
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
        this.carregarEvolucaoMensal();
      },
      error: (err: any) => console.error('Erro ao importar nota:', err)
    });
  }
}