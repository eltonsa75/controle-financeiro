import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { EstoqueService, ItemEstoque } from '../../services/estoque.service';
import { CategoriaService } from '../../services/services/categoria.service';
import Swal from 'sweetalert2';

declare var bootstrap: any;

@Component({
  selector: 'app-estoque',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './estoque.component.html',
  styleUrl: './estoque.component.css'
})
export class EstoqueComponent implements OnInit {

  itens: ItemEstoque[] = [];
  itensFiltrados: ItemEstoque[] = [];
  categorias: any[] = []; // Para alimentar o select do modal
  formEstoque: FormGroup;

  // Variáveis de controle de filtros (Desktop Dashboard)
  filtroTexto: string = '';
  filtroStatus: 'todos' | 'critico' | 'abastecido' = 'todos';

  // Contadores para os mini-cards do topo
  totalItens: number = 0;
  totalCriticos: number = 0;
  totalAbastecidos: number = 0;

  // 🎯 VARIÁVEIS DE CONTROLE DA PAGINAÇÃO LOCAL (MÁXIMO 12 ITENS)
  paginaAtual: number = 1;
  itensPorPagina: number = 12;

  constructor(
    private fb: FormBuilder,
    private estoqueService: EstoqueService,
    private categoriaService: CategoriaService
  ) {
    this.formEstoque = this.fb.group({
      id: [null],
      nome: ['', [Validators.required, Validators.maxLength(100)]],
      quantidadeAtual: [0, [Validators.required, Validators.min(0)]],
      quantidadeMinima: [1, [Validators.required, Validators.min(0)]],
      unidadeMedida: ['un', Validators.required],
      categoriaId: ['', Validators.required]
    });
  }

  ngOnInit(): void {
    this.carregarEstoque();
    this.carregarCategorias();
  }

  carregarEstoque(): void {
    this.estoqueService.listar().subscribe({
      next: (dados) => {
        this.itens = dados;
        this.calcularMétricas();
        this.aplicarFiltros();
      },
      error: (err) => console.error('Erro ao carregar estoque:', err)
    });
  }

  carregarCategorias(): void {
    this.categoriaService.listarTodas().subscribe({
      next: (dados) => this.categorias = dados,
      error: (err) => console.error('Erro ao buscar categorias:', err)
    });
  }

  // Calcula os totais dos mini-cards dinamicamente
  calcularMétricas(): void {
    this.totalItens = this.itens.length;
    this.totalCriticos = this.itens.filter(i => i.quantidadeAtual <= i.quantidadeMinima).length;
    this.totalAbastecidos = this.itens.filter(i => i.quantidadeAtual > i.quantidadeMinima).length;
  }

  // FILTRAGEM MULTI-CAMADAS (Texto + Status do Card)
  aplicarFiltros(): void {
    this.itensFiltrados = this.itens.filter(item => {
      const bateTexto = item.nome.toLowerCase().includes(this.filtroTexto.toLowerCase());
      
      if (this.filtroStatus === 'critico') {
        return bateTexto && (item.quantidadeAtual <= item.quantidadeMinima);
      }
      if (this.filtroStatus === 'abastecido') {
        return bateTexto && (item.quantidadeAtual > item.quantidadeMinima);
      }
      
      return bateTexto;
    });
  }

  alterarFiltroStatus(status: 'todos' | 'critico' | 'abastecido'): void {
    this.filtroStatus = status;
    this.paginaAtual = 1; // Reseta a página para evitar index fora do limite ao filtrar
    this.aplicarFiltros();
  }

  aoDigitarBusca(event: any): void {
    this.filtroTexto = event.target.value;
    this.paginaAtual = 1; // Reseta a página ao digitar um termo de busca
    this.aplicarFiltros();
  }

  // Ajuste rápido (+1 ou -1) na linha da tabela
  ajustarQtd(item: ItemEstoque, valor: number): void {
    if (item.quantidadeAtual + valor < 0) return; 

    this.estoqueService.ajustarQuantidade(item.id, valor).subscribe({
      next: (itemAtualizado) => {
        const index = this.itens.findIndex(i => i.id === item.id);
        if (index !== -1) {
          this.itens[index].quantidadeAtual = itemAtualizado.quantidadeAtual;
          this.calcularMétricas();
          this.aplicarFiltros();
        }
      },
      error: (err) => console.error('Erro ao ajustar quantidade:', err)
    });
  }

  abrirModalNovoItem(): void {
    this.formEstoque.reset({ id: null, quantidadeAtual: 0, quantidadeMinima: 1, unidadeMedida: 'un', categoriaId: '' });
    this.exibirModalBootstrap();
  }

  editar(item: ItemEstoque): void {
    this.formEstoque.patchValue(item);
    this.exibirModalBootstrap();
  }

  salvarItem(): void {
    if (this.formEstoque.invalid) return;

    const formValues = this.formEstoque.value;
    const itemParaEnviar: ItemEstoque = {
      id: formValues.id ? formValues.id : 0,
      nome: formValues.nome,
      quantidadeAtual: formValues.quantidadeAtual,
      quantidadeMinima: formValues.quantidadeMinima,
      unidadeMedida: formValues.unidadeMedida,
      categoriaId: Number(formValues.categoriaId)
    };

    Swal.fire({ title: 'Salvando item...', allowOutsideClick: false, didOpen: () => Swal.showLoading() });

    this.estoqueService.salvar(itemParaEnviar).subscribe({
      next: () => {
        this.carregarEstoque();
        this.fecharModalBootstrap();
        Swal.fire({ icon: 'success', title: itemParaEnviar.id ? 'Item Atualizado!' : 'Item Adicionado!', timer: 1500, showConfirmButton: false });
      },
      error: (err) => {
        Swal.close();
        Swal.fire('Erro', 'Não foi possível salvar o item no estoque.', 'error');
        console.error(err);
      }
    });
  }

  excluir(id: number, nome: string): void {
    Swal.fire({
      title: 'Remover da Despensa?',
      text: `Tem certeza que deseja excluir o item "${nome}"?`,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#d33',
      cancelButtonColor: '#3085d6',
      confirmButtonText: 'Sim, remover!',
      cancelButtonText: 'Cancelar'
    }).then((result) => {
      if (result.isConfirmed) {
        this.estoqueService.excluir(id).subscribe({
          next: () => {
            this.carregarEstoque();
            Swal.fire({ icon: 'success', title: 'Removido!', timer: 1500, showConfirmButton: false });
          },
          error: (err) => Swal.fire('Erro', 'Erro ao excluir o item.', 'error')
        });
      }
    });
  }

  // ========== GETTERS DE FATIAMENTO DA PAGINAÇÃO LOCAL DO ESTOQUE ==========
  get totalPaginas(): number {
    if (!this.itensFiltrados || this.itensFiltrados.length === 0) return 1;
    return Math.ceil(this.itensFiltrados.length / this.itensPorPagina);
  }

  get itensPaginados(): ItemEstoque[] {
    if (!this.itensFiltrados || this.itensFiltrados.length === 0) return [];
    const inicio = (this.paginaAtual - 1) * this.itensPorPagina;
    const fim = inicio + this.itensPorPagina;
    return this.itensFiltrados.slice(inicio, fim);
  }

  proximaPagina(): void {
    if (this.paginaAtual < this.totalPaginas) this.paginaAtual++;
  }

  paginaAnterior(): void {
    if (this.paginaAtual > 1) this.paginaAtual--;
  }

  // ========== 🎯 NOVO GERADOR DE NÚMEROS DA PAGINAÇÃO DINÂMICA SAAS ==========
  get paginasVisiveis(): (number | string)[] {
    const total = this.totalPaginas;
    const atual = this.paginaAtual;
    const paginas: (number | string)[] = [];

    if (total <= 5) {
      for (let i = 1; i <= total; i++) paginas.push(i);
    } else {
      paginas.push(1);

      if (atual > 3) {
        paginas.push('...');
      }

      const inicio = Math.max(2, atual - 1);
      const fim = Math.min(total - 1, atual + 1);

      for (let i = inicio; i <= fim; i++) {
        if (!paginas.includes(i)) paginas.push(i);
      }

      if (atual < total - 2) {
        paginas.push('...');
      }

      if (!paginas.includes(total)) paginas.push(total);
    }

    return paginas;
  }

  irParaPagina(pagina: number | string): void {
    if (typeof pagina === 'number') {
      this.paginaAtual = pagina;
    }
  }

  // --- MÉTODOS VISUAIS AUXILIARES ---
  
  isItemCritico(item: ItemEstoque): boolean {
    return item.quantidadeAtual <= item.quantidadeMinima;
  }

  private exibirModalBootstrap() {
    const modalElement = document.getElementById('modalEstoque');
    if (modalElement) {
      const modalInstance = new bootstrap.Modal(modalElement);
      modalInstance.show();
    }
  }

  private fecharModalBootstrap() {
    const modalElement = document.getElementById('modalEstoque');
    if (modalElement) {
      const modalInstance = bootstrap.Modal.getInstance(modalElement);
      if (modalInstance) modalInstance.hide();
    }
  }
}