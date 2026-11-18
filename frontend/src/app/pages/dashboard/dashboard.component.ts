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
  categoriasCarregando = false;
  categoriasMensagem = "";

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

  layout: {
    padding: {
      top: window.innerWidth <= 480 ? 25 : 10
    }
  },

  plugins: {
             legend: {
  position: 'top',
  align: 'center',
  labels: {
    boxWidth: window.innerWidth <= 480 ? 10 : 15,
    boxHeight: window.innerWidth <= 480 ? 10 : 15,
    padding: window.innerWidth <= 480 ? 8 : 15,
    font: {
      size: window.innerWidth <= 480 ? 10 : 12,
      weight: 500
    }
  }
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
  display: (context: any) => {
    const value = Number(context.dataset.data[context.dataIndex]);
    return value > 0;
  },
  align: (context: any) => {
    const index = context.dataIndex;
    const total = context.chart.data.labels?.length ?? 0;

    if (index === 0) return 'right';
    if (index === total - 1) return 'left';

    return 'top';
  },
  anchor: 'end',
  clamp: true,
  clip: false,
  offset: 4,
  backgroundColor: 'rgba(255,255,255,0.95)',
  borderRadius: 4,
  borderWidth: 1,
  borderColor: '#e5e7eb',
  padding: {
    left: 4,
    right: 4,
    top: 3,
    bottom: 3
  },
  color: '#1f2937',
  font: {
    weight: 'bold',
    size: 9
  },
  formatter: (value: number) =>
    value > 0
      ? `R$ ${value.toLocaleString('pt-BR', {
          maximumFractionDigits: 0
        })}`
      : ''
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
    this.categoriasCarregando = true;
    this.categoriasMensagem = '';


    this.financeiroService.getGastosPorCategoria(
  this.mesSelecionado,
  this.anoSelecionado

).subscribe({
      next: (resposta: unknown) => {
        this.categoriasCarregando = false;
        console.log('[Dashboard] Resposta de gastos por categoria:', resposta);

        if (this.chart) {
          this.chart.destroy();
          this.chart = null;
        }

        if (!Array.isArray(resposta)) {
          this.categoriasMensagem = 'Formato de resposta inesperado. Verifique a API de categorias.';
          return;
        }

        const dados = resposta
          .map((item: any) => ({
            categoria: String(item?.categoria ?? item?.Categoria ?? '').trim(),
            valor: Number(item?.valor ?? item?.Valor ?? 0)
          }))
          .filter(item => item.categoria && Number.isFinite(item.valor) && item.valor > 0);

        if (dados.length === 0) {
          this.categoriasMensagem = 'Nenhuma despesa por categoria encontrada.';
          return;
        }

        if (!this.canvasRef?.nativeElement) {
          this.categoriasMensagem = 'Não foi possível localizar o gráfico de categorias.';
          return;
        }

        this.chart = new Chart(this.canvasRef.nativeElement, {
          type: 'doughnut',
          data: {
            labels: dados.map(d => d.categoria),
            datasets: [{
              data: dados.map(d => d.valor),
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
position: (window.visualViewport?.width ?? window.innerWidth) <= 575
  ? 'bottom'
  : 'right',
                labels: { boxWidth: 12, padding: 12, font: { size: 11 }, color: '#4b5563' }
              },
              tooltip: {
                callbacks: {
                  label: (context: any) =>
                    `${context.label}: ${this.formatarMoeda(Number(context.raw))}`
                }
              },
            datalabels: {
  display: (context: any) => {
    const valor = Number(context.dataset.data[context.dataIndex]);
    return valor > 0;
  },
  align: (context: any) => {
    const indice = context.dataIndex;
    const total = context.chart.data.labels?.length ?? 0;

    if (indice === 0) return 'right';
    if (indice === total - 1) return 'left';

    return 'top';
  },
  anchor: 'end',
  clamp: true,
  clip: false,
  offset: 4,
  backgroundColor: 'rgba(255,255,255,0.95)',
  borderRadius: 4,
  borderWidth: 1,
  borderColor: '#e5e7eb',
  padding: 3,
  color: '#1f2937',
  font: {
    weight: 'bold',
    size: 9
  },
  formatter: (value: number) =>
    value > 0
      ? `R$ ${value.toLocaleString('pt-BR', {
          maximumFractionDigits: 0
        })}`
      : ''
}
            }
          }
        });
      },
      error: (err: any) => {
        this.categoriasCarregando = false;
        this.categoriasMensagem = 'Erro ao consultar categorias. Verifique o console.';
        if (this.chart) {
          this.chart.destroy();
          this.chart = null;
        }
        console.error('[Dashboard] Erro ao carregar categorias:', err);
      }
    });

    console.log(
  '[DEBUG DASHBOARD] Consultando categorias:',
  this.mesSelecionado,
  this.anoSelecionado
);
  }



carregarGraficoMaisCaros(): void {
  this.financeiroService
    .getItensMaisCaros(this.mesSelecionado, this.anoSelecionado)
    .subscribe({
      next: (itens: ItemCaro[]) => {
        if (!this.canvasCarosRef?.nativeElement) return;

        if (this.chartCaros) {
          this.chartCaros.destroy();
          this.chartCaros = null;
        }

        if (!itens || itens.length === 0) return;

        const canvas = this.canvasCarosRef.nativeElement;
        const mobile = (window.visualViewport?.width ?? window.innerWidth) <= 575;

        const valores = itens.map(i => Number(i.valorTotal) || 0);
        const maiorValor = Math.max(...valores, 1);

        // Reserva aproximadamente 30% da escala para os valores.
        const limiteEixoX = maiorValor * 1.3;

        // Ajusta a altura conforme a quantidade de produtos.
        const alturaGrafico = Math.max(
          mobile ? 220 : 260,
          itens.length * (mobile ? 42 : 46) + 25
        );

        const container = canvas.parentElement;
        if (container) {
          container.style.height = `${alturaGrafico}px`;
        }

        this.chartCaros = new Chart(canvas, {
          type: 'bar',
          data: {
            labels: itens.map(i => {
              const descricao = i.descricao || '';
              const limite = mobile ? 16 : 22;

              return descricao.length > limite
                ? descricao.substring(0, limite) + '...'
                : descricao;
            }),
            datasets: [{
              label: 'Valor (R$)',
              data: valores,
              backgroundColor: 'rgba(99, 91, 255, 0.85)',
              hoverBackgroundColor: '#635bff',
              borderRadius: 6,
              borderSkipped: false,
              maxBarThickness: mobile ? 22 : 28,
              categoryPercentage: 0.75,
              barPercentage: 0.85
            }]
          },
          options: {
            indexAxis: 'y',
            responsive: true,
            maintainAspectRatio: false,

            layout: {
              padding: {
                top: 8,
                bottom: 8,
                left: 0,
                right: 8
              }
            },

            plugins: {
              legend: {
                display: false
              },

              tooltip: {
                callbacks: {
                  title: (context: any) => {
                    const indice = context[0]?.dataIndex;
                    return indice !== undefined
                      ? itens[indice].descricao
                      : '';
                  },
                  label: (context: any) =>
                    ` Valor: R$ ${Number(context.raw).toLocaleString('pt-BR', {
                      minimumFractionDigits: 2,
                      maximumFractionDigits: 2
                    })}`
                }
              },

              datalabels: {
                display: (context: any) =>
                  Number(context.dataset.data[context.dataIndex]) > 0,

                anchor: 'end',
                align: 'end',
                offset: 5,

                clamp: false,
                clip: false,

                backgroundColor: 'rgba(255,255,255,0.98)',
                borderColor: '#e5e7eb',
                borderWidth: 1,
                borderRadius: 4,

                padding: {
                  top: 3,
                  bottom: 3,
                  left: 4,
                  right: 4
                },

                color: '#1f2937',

                font: {
                  weight: 'bold',
                  size: mobile ? 9 : 10
                },

                formatter: (value: number) =>
                  value > 0
                    ? `R$ ${value.toLocaleString('pt-BR', {
                        maximumFractionDigits: 0
                      })}`
                    : ''
              }
            },

            scales: {
              x: {
                display: false,
                beginAtZero: true,
                max: limiteEixoX,
                grid: {
                  display: false
                }
              },

              y: {
                border: {
                  display: false
                },

                grid: {
                  display: false
                },

                ticks: {
                  color: '#4b5563',
                  font: {
                    size: mobile ? 10 : 12,
                    weight: 500
                  },
                  autoSkip: false
                }
              }
            }
          }
        });
      },

      error: (err: any) =>
        console.error('Erro ao carregar gráfico de itens caros:', err)
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