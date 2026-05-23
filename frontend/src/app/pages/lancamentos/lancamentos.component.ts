import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms'; // FormsModule é OBRIGATÓRIO aqui
import { LancamentoService } from '../../services/lancamento.service';
import { CategoriaService } from '../../services/services/categoria.service';

@Component({
  selector: 'app-lancamentos',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule], // Adicionado FormsModule
  templateUrl: './lancamentos.component.html',
  styleUrl: './lancamentos.component.css'
})
export class LancamentosComponent implements OnInit {
  
  listaLancamentos: any[] = []; // O HTML usa 'listaLancamentos'
  categorias: any[] = [];
  carregando: boolean = true; // Nova variável de controle

  constructor(
    private lancamentoService: LancamentoService,
    private categoriaService: CategoriaService
  ) {}

  ngOnInit(): void {
    this.carregarCategorias();
    this.carregarLancamentos();
  }

carregarLancamentos(): void {
    this.carregando = true; 
    this.lancamentoService.listarTodos().subscribe({
      next: (dados) => {
        this.listaLancamentos = dados;
        this.carregando = false; // Finaliza o loading mesmo se vier []
      },
      error: (err) => {
        console.error(err);
        this.carregando = false;
      }
    });
  }


  carregarCategorias(): void {
    const d = new Date();
    this.categoriaService.listarComStatus(d.getMonth() + 1, d.getFullYear())
      .subscribe(dados => this.categorias = dados);
  }

  // Função para o botão "Nova Receita/Despesa"
  adicionarLinha(tipo: string): void {
    this.listaLancamentos.push({
      id: 0,
      descricao: '',
      valor: 0,
      tipo: tipo,
      categoriaId: null,
      data: new Date().toISOString().substring(0, 10)
    });
  }

  // Corrigindo o erro de 'remover' que o HTML pediu
  remover(id: number): void {
    if (id === 0) {
      // Se for um item novo que nem foi salvo ainda
      this.listaLancamentos = this.listaLancamentos.filter(l => l.id !== id);
    } else {
      if (confirm('Deseja excluir este lançamento?')) {
        this.lancamentoService.excluir(id).subscribe(() => this.carregarLancamentos());
      }
    }
  }

  // Corrigindo o erro de 'salvarTudo'
  salvarTudo(): void {
    console.log('Salvando todos:', this.listaLancamentos);
    // Aqui você pode fazer um loop salvando cada um ou criar um endpoint de lote na API
    this.listaLancamentos.forEach(item => {
      this.lancamentoService.salvar(item).subscribe();
    });
    alert('Lançamentos processados!');
  }

  // Função auxiliar para o *ngIf do HTML
  temTipo(tipo: string): boolean {
    return this.listaLancamentos.some(item => item.tipo === tipo);
  }

  filtrarPorMes(event: any): void {
    console.log('Filtrando por:', event.target.value);
    // Lógica de filtro aqui
  }
}