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
  categorias: any[] = [];
  formEstoque: FormGroup;

  filtroTexto: string = '';
  filtroStatus: 'todos' | 'critico' | 'abastecido' = 'todos';

  totalItens: number = 0;
  totalCriticos: number = 0;
  totalAbastecidos: number = 0;

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
      next: (dados) => { this.itens = dados; this.calcularMétricas(); this.aplicarFiltros(); },
      error: (err) => console.error('Erro ao listar:', err)
    });
  }

  carregarCategorias(): void {
    this.categoriaService.listarTodas().subscribe({
      next: (dados) => this.categorias = dados
    });
  }

  calcularMétricas(): void {
    this.totalItens = this.itens.length;
    this.totalCriticos = this.itens.filter(i => i.quantidadeAtual <= i.quantidadeMinima).length;
    this.totalAbastecidos = this.itens.filter(i => i.quantidadeAtual > i.quantidadeMinima).length;
  }

  aplicarFiltros(): void {
    this.itensFiltrados = this.itens.filter(item => {
      const bateTexto = item.nome.toLowerCase().includes(this.filtroTexto.toLowerCase());
      if (this.filtroStatus === 'critico') return bateTexto && (item.quantidadeAtual <= item.quantidadeMinima);
      if (this.filtroStatus === 'abastecido') return bateTexto && (item.quantidadeAtual > item.quantidadeMinima);
      return bateTexto;
    });
  }

  alterarFiltroStatus(status: 'todos' | 'critico' | 'abastecido'): void { this.filtroStatus = status; this.paginaAtual = 1; this.aplicarFiltros(); }
  aoDigitarBusca(event: any): void { this.filtroTexto = event.target.value; this.paginaAtual = 1; this.aplicarFiltros(); }
  
  isItemCritico(item: ItemEstoque): boolean { return item.quantidadeAtual <= item.quantidadeMinima; }

  ajustarQtd(item: ItemEstoque, valor: number): void {
    this.estoqueService.ajustarQuantidade(item.id, valor).subscribe(() => this.carregarEstoque());
  }

  abrirModalNovoItem(): void { 
    this.formEstoque.reset({ id: null, quantidadeAtual: 0, quantidadeMinima: 1, unidadeMedida: 'un', categoriaId: '' });
    this.exibirModalBootstrap(); 
  }
  
  editar(item: ItemEstoque): void { this.formEstoque.patchValue(item); this.exibirModalBootstrap(); }

salvarItem(): void {
    if (this.formEstoque.invalid) return;

    const rawValues = this.formEstoque.value;
    
    // Ajuste aqui: adicionamos o campo 'model' e forçamos 'usuarioId' para string
    const itemParaEnviar = {
      ...rawValues,
      id: rawValues.id || undefined,
      model: rawValues.nome, // Mapeando 'nome' para 'model' se forem a mesma coisa
      categoriaId: Number(rawValues.categoriaId),
      usuarioId: "1" // Convertido para string conforme erro do servidor
    };

    Swal.fire({ title: 'Salvando...', allowOutsideClick: false, didOpen: () => Swal.showLoading() });

    this.estoqueService.salvar(itemParaEnviar).subscribe({
      next: () => {
        this.carregarEstoque();
        this.fecharModalBootstrap();
        Swal.fire({ icon: 'success', title: 'Sucesso!', timer: 1500 });
      },
      error: (err) => {
        Swal.close();
        console.error('Erro detalhado:', err.error);
        Swal.fire('Erro', 'Verifique o console (F12) para os novos erros.', 'error');
      }
    });
  }

  excluir(id: number, nome: string): void {
    Swal.fire({ title: 'Remover?', text: `Remover ${nome}?`, icon: 'warning', showCancelButton: true }).then(result => {
      if (result.isConfirmed) this.estoqueService.excluir(id).subscribe(() => this.carregarEstoque());
    });
  }

  // --- PAGINAÇÃO ---
  get totalPaginas(): number { return Math.ceil(this.itensFiltrados.length / this.itensPorPagina) || 1; }
  get itensPaginados(): ItemEstoque[] { return this.itensFiltrados.slice((this.paginaAtual - 1) * this.itensPorPagina, this.paginaAtual * this.itensPorPagina); }
  
  paginaAnterior(): void { if (this.paginaAtual > 1) this.paginaAtual--; }
  proximaPagina(): void { if (this.paginaAtual < this.totalPaginas) this.paginaAtual++; }
  irParaPagina(pagina: number | string): void { if (typeof pagina === 'number') this.paginaAtual = pagina; }
  
  get paginasVisiveis(): (number | string)[] {
    let paginas: (number | string)[] = [];
    for (let i = 1; i <= this.totalPaginas; i++) paginas.push(i);
    return paginas;
  }

  private exibirModalBootstrap() { document.getElementById('modalEstoque') && new bootstrap.Modal(document.getElementById('modalEstoque')).show(); }
  private fecharModalBootstrap() { const m = bootstrap.Modal.getInstance(document.getElementById('modalEstoque')); m?.hide(); }
}