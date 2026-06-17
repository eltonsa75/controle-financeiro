import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { LancamentoService, Lancamento } from '../../services/lancamento.service';
import { CategoriaService } from '../../services/services/categoria.service';

@Component({
  selector: 'app-lancamentos',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './lancamentos.component.html',
  styleUrl: './lancamentos.component.css'
})
export class LancamentosComponent implements OnInit {
  
  listaLancamentos: Lancamento[] = [];
  categorias: any[] = [];
  carregando: boolean = true;
  
  // Controle de Paginação
  currentPage: number = 1;
  itemsPerPage: number = 10;
  totalItems: number = 0;

  constructor(
    private lancamentoService: LancamentoService,
    private categoriaService: CategoriaService
  ) {}

  ngOnInit(): void {
    this.carregarCategorias();
    this.carregarLancamentos();
  }

  carregarLancamentos(page: number = 1): void {
    this.carregando = true;
    this.currentPage = page;

    this.lancamentoService.listarPaginado(this.currentPage, this.itemsPerPage).subscribe({
      next: (resposta: any) => {
        // Ajuste conforme a estrutura real que sua API retorna
        this.listaLancamentos = resposta.items || resposta; 
        this.totalItems = resposta.total || resposta.length;
        this.carregando = false;
      },
      error: (err) => {
        console.error('Erro ao carregar lançamentos:', err);
        this.carregando = false;
      }
    });
  }

  onPageChange(page: number): void {
    this.carregarLancamentos(page);
  }

  carregarCategorias(): void {
    const d = new Date();
    this.categoriaService.listarComStatus(d.getMonth() + 1, d.getFullYear())
      .subscribe(dados => this.categorias = dados);
  }

  adicionarLinha(tipo: string): void {
    this.listaLancamentos.push({
      id: 0,
      descricao: '',
      valor: 0,
      tipo: tipo,
      categoriaId: 0,
      data: new Date().toISOString(),
      // Ajuste conforme a interface Lancamento do seu serviço
    } as any);
  }

  remover(id: number): void {
    if (id === 0) {
      this.listaLancamentos = this.listaLancamentos.filter(l => l.id !== id);
    } else if (confirm('Deseja excluir este lançamento?')) {
      this.lancamentoService.excluir(id).subscribe(() => this.carregarLancamentos(this.currentPage));
    }
  }

salvarTudo(): void {
  this.listaLancamentos.forEach(item => {
    // Garante que o item.valor seja tratado como string para o replace
    let valorParaConverter = item.valor ? item.valor.toString() : '0';
    
    if (valorParaConverter.includes(',')) {
      valorParaConverter = valorParaConverter.replace(',', '.');
    }
    
    item.valor = parseFloat(valorParaConverter) || 0;

    this.lancamentoService.salvar(item).subscribe({
      next: () => console.log('Item salvo!'),
      error: (err) => console.error('Erro:', err)
    });
  });
}

filtrarPorMes(event: Event): void {
    const target = event.target as HTMLInputElement;
    const valor = target.value; // Formato: "YYYY-MM"
    console.log('Filtrando por:', valor);
    
    // Exemplo: Dividir em ano e mes para usar na sua lógica
    const [ano, mes] = valor.split('-');
    
    // Chame sua lógica de recarregamento aqui, por exemplo:
    // this.carregarLancamentosPorMes(parseInt(mes), parseInt(ano));
  }

  temTipo(tipo: string): boolean {
    return this.listaLancamentos.some(item => item.tipo === tipo);
  }
}