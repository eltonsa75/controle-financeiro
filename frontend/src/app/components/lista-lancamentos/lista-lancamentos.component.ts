import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms'; 
import { FinanceiroService } from '../../services/financeiro.service';
import { CategoriaService } from '../../services/services/categoria.service';
import { Router, RouterModule } from '@angular/router'; 
import { Lancamento } from '../../models/financeiro.model';
import Swal from 'sweetalert2';
import { firstValueFrom } from 'rxjs';

@Component({
  selector: 'app-lista-lancamentos',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule], 
  templateUrl: './lista-lancamentos.component.html',
  styleUrl: './lista-lancamentos.component.css'
})
export class ListaLancamentosComponent implements OnInit {

  listaLancamentos: Lancamento[] = []; 
  categorias: any[] = [];
  carregando: boolean = true; 
  mesAnoSelecionado: string = ''; 

  // --- VARIÁVEIS DO MODAL ADAPTADAS PARA O REATIVO DO ANGULAR ---
  exibirModal: boolean = false;
  itemSelecionado: any = null; 
  itensSelecionados: any[] = []; // Guarda a lista de sub-itens se houver

  paginaAtual: number = 1;
  itensPorPagina: number = 10;

  constructor(
    private financeiroService: FinanceiroService,
    private categoriaService: CategoriaService,
    private router: Router
  ) {
    const hoje = new Date();
    this.mesAnoSelecionado = `${hoje.getFullYear()}-${String(hoje.getMonth() + 1).padStart(2, '0')}`;
  }

  ngOnInit(): void {
    this.carregarCategorias();
    this.carregarLancamentos();
  }

  carregarCategorias() {
    const [ano, mes] = this.mesAnoSelecionado.split('-');
    this.categoriaService.listarComStatus(Number(mes), Number(ano))
      .subscribe(dados => this.categorias = dados);
  }

  carregarLancamentos() {
    this.carregando = true;
    this.financeiroService.getLancamentos().subscribe({
      next: (dados) => {
        this.listaLancamentos = (dados || []).sort((a, b) => 
          new Date(b.dataEmissao).getTime() - new Date(a.dataEmissao).getTime()
        );
        this.carregando = false;
      },
      error: (err) => {
        console.error('Erro:', err);
        this.carregando = false;
      }
    });
  }

  // --- MÉTODO MODIFICADO PARA COMPATIBILIDADE REATIVA ---
  abrirModalItens(lancamento: Lancamento) {
    // Definimos o card principal selecionado para alimentar o cabeçalho do modal
    this.itemSelecionado = lancamento;
    
    Swal.fire({ 
      title: 'Buscando itens...', 
      allowOutsideClick: false, 
      didOpen: () => Swal.showLoading() 
    });

    this.financeiroService.getItensPorLancamento(lancamento.id!).subscribe({
      next: (data) => {
        this.itensSelecionados = data || [];
        Swal.close();
        
        // Ativa a reatividade do Angular para renderizar o Modal na tela
        this.exibirModal = true;
      },
      error: (err) => {
        console.error('Erro ao buscar itens:', err);
        this.itensSelecionados = [];
        Swal.fire('Erro', 'Não foi possível carregar os itens.', 'error');
      }
    });
  }

  // --- MÉTODO PARA FECHAR O MODAL NO BOTÃO OU BACKDROP ---
  fecharModal(): void {
    this.exibirModal = false;
    this.itemSelecionado = null;
    this.itensSelecionados = [];
  }

  onPageChange(novaPagina: number): void {
    this.paginaAtual = novaPagina;
  }

  async salvarTudo(): Promise<void> {
    const itensParaSalvar = this.listaLancamentos.filter(l => l.id === 0 && l.descricao !== '');
    if (itensParaSalvar.length === 0) return;

    Swal.fire({ title: 'Salvando...', allowOutsideClick: false, didOpen: () => Swal.showLoading() });

    try {
      for (const item of itensParaSalvar) {
        await firstValueFrom(this.financeiroService.salvarLancamento(item));
      }
      Swal.fire({ icon: 'success', title: 'Sucesso', text: 'Lançamentos salvos!', timer: 1500 });
      this.carregarLancamentos();
    } catch (error) {
      Swal.fire('Erro', 'Problema ao salvar some itens.', 'error');
    }
  }

  get totalPaginas(): number {
    return Math.ceil(this.listaLancamentos.length / this.itensPorPagina) || 1;
  }

  get lancamentosPaginados(): Lancamento[] {
    const inicio = (this.paginaAtual - 1) * this.itensPorPagina;
    return this.listaLancamentos.slice(inicio, inicio + this.itensPorPagina);
  }

  excluir(id: number) {
    Swal.fire({
      title: 'Excluir?',
      text: "Isso removerá o registro!",
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#dc3545',
      confirmButtonText: 'Sim'
    }).then((result) => {
      if (result.isConfirmed) {
        this.financeiroService.excluirLancamento(id).subscribe(() => {
          this.listaLancamentos = this.listaLancamentos.filter(l => l.id !== id);
          Swal.fire('Deletado!', '', 'success');
        });
      }
    });
  }
}