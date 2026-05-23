import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms'; 
import { FinanceiroService } from '../../services/financeiro.service';
import { CategoriaService } from '../../services/services/categoria.service';
import { Router, RouterLink } from '@angular/router';
import { Lancamento } from '../../models/financeiro.model';
import Swal from 'sweetalert2';

declare var bootstrap: any;


@Component({
  selector: 'app-lista-lancamentos',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './lista-lancamentos.component.html',
  styleUrl: './lista-lancamentos.component.css'
})
export class ListaLancamentosComponent implements OnInit {
  listaLancamentos: Lancamento[] = []; 
  categorias: any[] = [];
  carregando: boolean = true; 
  
  mesAnoSelecionado: string = ''; 

  itensSelecionados: any[] = [];
  descricaoModal: string = '';
  lancamentoSelecionado: Lancamento | null = null;

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
    this.listaLancamentos = [];

    this.financeiroService.getLancamentos().subscribe({
      next: (dados) => {
        if (dados && dados.length > 0) {
          // Opcional: ordenar por dataEmissao descendente
          this.listaLancamentos = dados.sort((a, b) => 
            new Date(b.dataEmissao).getTime() - new Date(a.dataEmissao).getTime()
          );
        } else {
          console.warn('A API retornou um array vazio.');
        }
        this.carregando = false;
      },
      error: (err) => {
        console.error('Erro na requisição:', err);
        this.carregando = false;
      }
    });
  }

  filtrarPorMes(event: any) {
    this.mesAnoSelecionado = event.target.value; 
    if (this.mesAnoSelecionado) {
      this.carregarLancamentos();
      this.carregarCategorias();
    }
  }

  // --- AJUSTADO PARA CORRIGIR TS2345 E TS2322 ---
  adicionarLinha(tipo: string): void {
    const hojeIso = new Date().toISOString();
    
    const novo: Lancamento = {
      id: 0,
      descricao: '',
      valor: 0,
      tipo: tipo, 
      data: hojeIso,
      dataEmissao: hojeIso,   // Preenchimento obrigatório para a interface
      dataImportacao: hojeIso, // Preenchimento obrigatório para a interface
      categoriaId: 1          // Valor padrão (Geral/Outros) para evitar undefined
    };
    this.listaLancamentos.unshift(novo); 
  }

  abrirNovoLancamento(): void {
    this.router.navigate(['/compras']);
  }

  salvarTudo(): void {
    const itensParaSalvar = this.listaLancamentos.filter(l => l.id === 0 && l.descricao !== '');

    if (itensParaSalvar.length === 0) {
       Swal.fire('Info', 'Nenhum lançamento novo para salvar.', 'info');
       return;
    }

    Swal.fire({ title: 'Salvando...', allowOutsideClick: false, didOpen: () => Swal.showLoading() });

    // Ajustado para garantir que as datas sejam enviadas corretamente no loop de salvamento
    const promessas = itensParaSalvar.map(l => {
      // Garante que se o usuário não mexeu na data, enviamos a de hoje
      l.dataEmissao = l.dataEmissao || new Date().toISOString();
      l.dataImportacao = new Date().toISOString();
      return this.financeiroService.salvarLancamento(l).toPromise();
    });

    Promise.all(promessas)
      .then(() => {
        Swal.fire({ icon: 'success', title: 'Sucesso', text: 'Lançamentos salvos!', timer: 1500, showConfirmButton: false });
        this.carregarLancamentos();
      })
      .catch(() => Swal.fire('Erro', 'Problema ao salvar alguns itens.', 'error'));
  }

  temTipo(tipo: string): boolean {
    return this.listaLancamentos.some(item => item.tipo?.toLowerCase() === tipo.toLowerCase());
  }

  remover(id: number | undefined) {
    if (id === undefined || id === 0) {
      // Se for um item que acabou de ser adicionado na tela (ID 0), removemos apenas da lista local
      this.listaLancamentos = this.listaLancamentos.filter(l => l.id !== id);
      return;
    }
    this.excluir(id);
  }

  abrirModalCategorias() {
    const modalElement = document.getElementById('modalCategorias');
    if (modalElement) {
      const modalInstance = new bootstrap.Modal(modalElement);
      modalInstance.show();
    }
  }

  abrirModalItens(lancamento: Lancamento) {
    this.lancamentoSelecionado = lancamento;
    this.descricaoModal = lancamento.descricao;
    Swal.fire({ title: 'Buscando itens...', allowOutsideClick: false, didOpen: () => { Swal.showLoading(); } });

    this.financeiroService.getItensPorLancamento(lancamento.id!).subscribe({
      next: (data) => {
        this.itensSelecionados = data;
        Swal.close();
        const modalElement = document.getElementById('modalItens');
        if (modalElement) { new bootstrap.Modal(modalElement).show(); }
      },
      error: () => Swal.fire('Erro', 'Não carregou os itens.', 'error')
    });
  }

  fecharModal() {
    this.lancamentoSelecionado = null;
    this.itensSelecionados = [];
  }

  excluir(id: number) {
    Swal.fire({
      title: 'Excluir?',
      text: "Isso removerá o registro do banco!",
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