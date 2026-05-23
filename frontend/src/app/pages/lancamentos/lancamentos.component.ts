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
  const dataAtual = new Date().toISOString();

  this.listaLancamentos.push({
    id: 0,
    descricao: '',
    valor: 0,
    tipo: tipo,
    categoriaId: 0, // Mudado de null para 0 para bater com o tipo number do JSON
    data: dataAtual,
    dataEmissao: dataAtual,
    dataImportacao: dataAtual,
    categoria: null, // Pode ir nulo, pois a API usa o categoriaId para associar
    itens: []        // Inicia como um array vazio para os itens manuais
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


salvarTudo(): void {
  console.log('Salvando todos:', this.listaLancamentos);
  
  this.listaLancamentos.forEach(item => {
    // Tratamento do valor (Task 1)
    if (item.valor && typeof item.valor === 'string') {
      item.valor = item.valor.replace(',', '.');
    }
    item.valor = parseFloat(item.valor) || 0;

    // Envia para a API
    this.lancamentoService.salvar(item).subscribe({
      next: (res) => console.log(`Item ${item.descricao} salvo!`, res),
      error: (err) => console.error(`Erro ao salvar ${item.descricao}:`, err)
    });
  });
  
  alert('Lançamentos processados!');
  this.carregarLancamentos(); 
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