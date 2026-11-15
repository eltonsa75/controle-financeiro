import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators
} from '@angular/forms';

import {
  CategoriaService,
  CategoriaStatus
} from '../../services/services/categoria.service';

import { LancamentoService } from '../../services/lancamento.service';
import { FinanceiroService } from '../../services/financeiro.service';

import {
  CdkDragDrop,
  moveItemInArray,
  DragDropModule
} from '@angular/cdk/drag-drop';

import { Categoria } from '../../models/financeiro.model';

import Swal from 'sweetalert2';


@Component({
  selector: 'app-categorias',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    DragDropModule
  ],
  templateUrl: './categorias.component.html',
  styleUrl: './categorias.component.css'
})
export class CategoriasComponent implements OnInit {

  categorias: Categoria[] = [];

  formCategoria: FormGroup;

  visaoAtual: 'mensal' | 'anual' = 'mensal';


  // ============================================================
  // PERÍODO SELECIONADO
  // ============================================================

  mesSelecionado: number = new Date().getMonth() + 1;
  anoSelecionado: number = new Date().getFullYear();


  // ============================================================
  // MODAL
  // ============================================================

  modalAberto: boolean = false;


  constructor(
    private fb: FormBuilder,
    private categoriaService: CategoriaService,
    private lancamentoService: LancamentoService,
    private financeiroService: FinanceiroService
  ) {

    this.formCategoria = this.fb.group({

      id: [null],

      nome: [
        '',
        Validators.required
      ],

      metaMensal: [
        0,
        [
          Validators.required,
          Validators.min(0)
        ]
      ],

      corHex: ['#007bff'],

      palavrasChave: ['']

    });
  }


  // ============================================================
  // INICIALIZAÇÃO
  // ============================================================

  ngOnInit(): void {

    this.carregarCategorias();

  }


  // ============================================================
  // CARREGAR CATEGORIAS + GASTOS
  // ============================================================


carregarCategorias(): void {

  const mes = this.mesSelecionado;
  const ano = this.anoSelecionado;
  const visao = this.visaoAtual;

  console.log(
    `[CATEGORIAS] Carregando período: ${mes}/${ano} - ${visao}`
  );

  this.categoriaService
    .listarComStatus(mes, ano, visao)
    .subscribe({

      next: (dadosCategorias: CategoriaStatus[]) => {

        // =====================================================
        // CATEGORIAS DE RECEITA NÃO APARECEM NOS CARDS
        // =====================================================

        const categoriasDeReceita = [
          'salário',
          'salario',
          'renda extra',
          'investimentos'
        ];

        // =====================================================
        // NORMALIZA NOMES PARA COMPARAÇÃO
        // =====================================================

        const normalizarNome = (nome?: string): string => {
          return (nome ?? '')
            .trim()
            .toLowerCase()
            .normalize('NFD')
            .replace(/[\u0300-\u036f]/g, '');
        };

        const receitasNormalizadas =
          categoriasDeReceita.map(normalizarNome);

        // =====================================================
        // ORDENA CATEGORIAS PELA COLUNA ORDEM DO MYSQL
        // =====================================================

        const categoriasOrdenadas =
          ([...dadosCategorias] as Categoria[])

            .filter(cat =>
              !receitasNormalizadas.includes(
                normalizarNome(cat.nome)
              )
            )

            .sort((a, b) => {

              const ordemA = a.ordem ?? 0;
              const ordemB = b.ordem ?? 0;

              if (ordemA !== ordemB) {
                return ordemA - ordemB;
              }

              return (a.nome ?? '').localeCompare(
                b.nome ?? '',
                'pt-BR'
              );

            });

        console.log(
          '[CATEGORIAS] Ordem recebida do banco:',
          categoriasOrdenadas.map(cat => ({
            id: cat.id,
            nome: cat.nome,
            ordem: cat.ordem
          }))
        );

        // =====================================================
        // DEFINE A CONSULTA DE GASTOS
        // =====================================================

        const requestGastos$ =
          visao === 'anual'

            ? this.lancamentoService
                .getGastosAnuaisPorCategoria()

            : this.lancamentoService
                .getGastosCategoria(mes, ano);

        requestGastos$.subscribe({

          next: (gastosDapper: any[]) => {

            console.log(
              '[CATEGORIAS] Gastos recebidos:',
              gastosDapper
            );

            // =================================================
            // MONTA OS CARDS MANTENDO A ORDEM
            // =================================================

            this.categorias = categoriasOrdenadas.map(cat => {

              // ===============================================
              // LOCALIZA O GASTO DA CATEGORIA
              // ===============================================

              const nomeCard = normalizarNome(cat.nome);

              const gastoReal = gastosDapper.find(
                (g: any) =>
                  normalizarNome(g.categoria) === nomeCard
              );

              // ===============================================
              // VALOR GASTO
              // ===============================================

              const valorGasto = gastoReal
                ? Math.abs(
                    gastoReal.valorGastoAno ??
                    gastoReal.valor ??
                    0
                  )
                : 0;

              // ===============================================
              // META MENSAL OU ANUAL
              // ===============================================

              const metaCalculada =
                visao === 'anual'
                  ? (cat.metaMensal ?? 0) * 12
                  : (cat.metaMensal ?? 0);

              // ===============================================
              // PERCENTUAL DA META
              // ===============================================

              const percentual =
                metaCalculada > 0
                  ? (valorGasto / metaCalculada) * 100
                  : 0;

              // ===============================================
              // RETORNA O CARD COM A ORDEM ORIGINAL
              // ===============================================

              return {
                ...cat,

                id: cat.id ?? 0,

                ordem: cat.ordem ?? 0,

                gastoAtual: valorGasto,

                metaMensal: metaCalculada,

                percentual: percentual

              } as Categoria;

            });

            console.log(
              '[CATEGORIAS] Cards ordenados:',
              this.categorias
            );

          },

          error: (err: any) => {

            console.error(
              '[CATEGORIAS] Erro ao carregar gastos:',
              err
            );

            // Mantém os cards ordenados mesmo sem os gastos
            this.categorias = categoriasOrdenadas.map(cat => ({
              ...cat,
              id: cat.id ?? 0,
              ordem: cat.ordem ?? 0,
              gastoAtual: 0,
              metaMensal: visao === 'anual'
                ? (cat.metaMensal ?? 0) * 12
                : (cat.metaMensal ?? 0),
              percentual: 0
            } as Categoria));

          }

        });

      },

      error: (err: any) => {

        console.error(
          '[CATEGORIAS] Erro ao carregar categorias:',
          err
        );

      }

    });

}



  // ============================================================
  // MÊS ANTERIOR
  // ============================================================

  mesAnterior(): void {

    this.mesSelecionado--;


    if (this.mesSelecionado < 1) {

      this.mesSelecionado = 12;

      this.anoSelecionado--;

    }


    this.carregarCategorias();

  }


  // ============================================================
  // PRÓXIMO MÊS
  // ============================================================

  proximoMes(): void {

    this.mesSelecionado++;


    if (this.mesSelecionado > 12) {

      this.mesSelecionado = 1;

      this.anoSelecionado++;

    }


    this.carregarCategorias();

  }


  // ============================================================
  // NOME DO MÊS SELECIONADO
  // ============================================================

  get nomeMesSelecionado(): string {

    const data = new Date(
      this.anoSelecionado,
      this.mesSelecionado - 1,
      1
    );


    const nome =
      data.toLocaleDateString(
        'pt-BR',
        {
          month: 'long',
          year: 'numeric'
        }
      );


    // Primeira letra maiúscula
    return nome.charAt(0).toUpperCase()
      + nome.slice(1);

  }


  // ============================================================
  // ALTERAR VISÃO MENSAL / ANUAL
  // ============================================================

  alterarVisao(
    novaVisao: 'mensal' | 'anual'
  ): void {

    this.visaoAtual =
      novaVisao;

    this.carregarCategorias();

  }


  // ============================================================
  // DRAG AND DROP
  // ============================================================


drop(event: CdkDragDrop<Categoria[]>): void {

  if (event.previousIndex === event.currentIndex) {
    return;
  }

  const ordemAnterior = [...this.categorias];

  moveItemInArray(
    this.categorias,
    event.previousIndex,
    event.currentIndex
  );

  this.categorias.forEach((categoria, index) => {
    categoria.ordem = index;
  });

  this.financeiroService
    .atualizarOrdemCategorias(this.categorias)
    .subscribe({
      next: () => {
        console.log(
          '[CATEGORIAS] Ordem salva com sucesso.'
        );
      },

      error: (err) => {
        console.error(
          '[CATEGORIAS] Erro ao salvar ordem:',
          err
        );

        this.categorias = ordemAnterior;

        Swal.fire(
          'Erro',
          'Não foi possível salvar a posição dos cards.',
          'error'
        );
      }
    });
}



  // ============================================================
  // NOVA CATEGORIA
  // ============================================================

  abrirModalNovaCategoria(): void {

    this.formCategoria.reset({

      id: null,

      corHex:
        '#007bff',

      metaMensal:
        0,

      nome:
        '',

      palavrasChave:
        ''

    });


    this.modalAberto =
      true;

  }


  // ============================================================
  // EDITAR CATEGORIA
  // ============================================================

  editar(
    categoria: Categoria
  ): void {

    this.formCategoria.patchValue({

      ...categoria,

      gastoAtual:
        categoria.gastoAtual
        ?? 0,

      metaMensal:
        categoria.metaMensal
        ?? 0

    });


    this.modalAberto =
      true;

  }


  // ============================================================
  // SALVAR CATEGORIA
  // ============================================================


salvarCategoria(): void {

  if (!this.formCategoria.valid) {

    Swal.fire(
      'Atenção',
      'Por favor, preencha os campos obrigatórios corretamente.',
      'warning'
    );

    return;
  }

  const formValues = this.formCategoria.value;

  const userId =
    localStorage.getItem('usuarioId') ||
    'PiGS3DH27AaUiLdNhnw9AiedMpm2';

  // =====================================================
  // IDENTIFICA SE É CADASTRO OU EDIÇÃO
  // =====================================================

  const categoriaId =
    Number(formValues.id) > 0
      ? Number(formValues.id)
      : 0;

  const isEdicao = categoriaId > 0;

  // =====================================================
  // PRESERVA A ORDEM DA CATEGORIA
  // =====================================================

  const categoriaExistente = isEdicao
    ? this.categorias.find(c => c.id === categoriaId)
    : undefined;

  const ordemCategoria = isEdicao
    ? (categoriaExistente?.ordem ?? 0)
    : this.categorias.length > 0
      ? Math.max(...this.categorias.map(c => c.ordem ?? 0)) + 1
      : 0;

  // =====================================================
  // PREPARA OS DADOS PARA A API
  // =====================================================

  const categoriaParaEnviar = {

    id: categoriaId,

    nome: formValues.nome,

    metaMensal: formValues.metaMensal,

    corHex: formValues.corHex,

    palavrasChave: formValues.palavrasChave,

    ordem: ordemCategoria,

    usuarioId: userId

  };

  console.log(
    '[CATEGORIAS] Salvando categoria:',
    categoriaParaEnviar
  );

  // =====================================================
  // EXIBE CARREGAMENTO
  // =====================================================

  Swal.fire({
    title: 'Salvando alterações...',
    allowOutsideClick: false,
    didOpen: () => Swal.showLoading()
  });

  // =====================================================
  // ENVIA PARA A API
  // =====================================================

  this.categoriaService
    .salvar(categoriaParaEnviar)
    .subscribe({

      next: () => {

        this.carregarCategorias();

        this.fecharModal();

        Swal.fire({
          icon: 'success',
          title: isEdicao
            ? 'Categoria Atualizada!'
            : 'Categoria Criada!',
          timer: 1500,
          showConfirmButton: false
        });

      },

      error: (err: any) => {

        Swal.close();

        console.error(
          '[CATEGORIAS] Erro ao salvar:',
          err
        );

        const mensagemErro =
          typeof err.error === 'string'
            ? err.error
            : err.error?.message ||
              'Não foi possível salvar as alterações da categoria.';

        Swal.fire(
          'Atenção',
          mensagemErro,
          'warning'
        );

      }

    });

}



  // ============================================================
  // EXCLUIR CATEGORIA
  // ============================================================

  excluir(
    id: number | undefined,
    nome: string
  ): void {

    if (!id)
      return;


    Swal.fire({

      title:
        'Tem certeza?',

      text:
        `Você vai excluir a categoria "${nome}". Isso pode impactar os lançamentos associados a ela!`,

      icon:
        'warning',

      showCancelButton:
        true,

      confirmButtonColor:
        '#d33',

      cancelButtonColor:
        '#3085d6',

      confirmButtonText:
        'Sim, excluir!',

      cancelButtonText:
        'Cancelar'

    })
    .then(
      (result) => {

        if (!result.isConfirmed)
          return;


        this.categoriaService
          .excluir(id)
          .subscribe({

            next: () => {

              this.carregarCategorias();


              Swal.fire({

                icon:
                  'success',

                title:
                  'Excluída!',

                text:
                  'A categoria foi removida com sucesso.',

                timer:
                  1500,

                showConfirmButton:
                  false

              });

            },


            error: (err: any) => {

              console.error(
                'Erro ao excluir:',
                err
              );


              const mensagemErro =

                err.error

                ||

                'Não foi possível remover a categoria. Verifique se há dependências.';


              Swal.fire(
                'Atenção',
                mensagemErro,
                'warning'
              );

            }

          });

      }
    );

  }


  // ============================================================
  // FECHAR MODAL
  // ============================================================

  fecharModal(): void {

    this.modalAberto =
      false;

  }


  // ============================================================
  // TRACK BY
  // ============================================================

  trackById(
    index: number,
    categoria: CategoriaStatus
  ): number {

    return categoria.id
      ?? index;

  }


  // ============================================================
  // COR DA BARRA DE PROGRESSO
  // ============================================================

  getProgressBarClass(
    percentual: number
  ): string {

    if (percentual >= 100)
      return 'bg-danger';


    if (percentual >= 80)
      return 'bg-warning';


    return 'bg-success';

  }

}