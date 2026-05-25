import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { FinanceiroService } from '../../services/financeiro.service';
import { Lancamento } from '../../models/financeiro.model'; 
import Swal from 'sweetalert2';

interface ItemCompraLocal {
  id?: number;
  descricao: string;
  quantidade: number;
  unidade: string;
  preco: number | null;
  comprado: boolean;
  categoria: string;
}

// Atualizado para começar com 0 para ativar o placeholder do HTML
const ESTADO_INICIAL_RAPIDO = { 
  descricao: '', 
  valor: null as any, 
  tipo: 'Despesa', 
  categoriaId: 0,
  dataEmissao: '' 
};

@Component({
  selector: 'app-compras',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './compras.component.html',
  styleUrl: './compras.component.css'
})
export class ComprasComponent implements OnInit {
  private financeiroService = inject(FinanceiroService);
  private router = inject(Router);

  itens: ItemCompraLocal[] = [];
  total: number = 0;
  limite: number = 0;
  saldoRestante: number = 0;
  porcentagemGasta: number = 0;
  descricaoCompra: string = '';
  dataCompraPlanejada: string = ''; 
  tentouSalvar: boolean = false;

  // 1. ADICIONADO: Propriedade para alimentar o combo-box do HTML
  categorias: any[] = []; 

  novoLancamento = { ...ESTADO_INICIAL_RAPIDO };

  sugestoesLocais: string[] = ['Atacadão', 'Carrefour', 'Pão de Açúcar', 'Posto Shell', 'Farmácia Preço Popular'];
  listaCategoriasItens: string[] = ['Açougue', 'Hortifruti', 'Limpeza', 'Higiene', 'Bebidas', 'Padaria', 'Mercearia', 'Laticínios', 'Outros', 'Farmacia'];
  
  compraAnterior = {
    valor: 1000.00,
    local: 'Supermercado Atacadão',
    data: new Date(2026, 2, 15)
  };

  diferencaValor: number = 0;
  statusComparativo: string = 'Aguardando itens...';

  ngOnInit(): void {
    const hoje = new Date().toISOString().split('T')[0];
    this.novoLancamento.dataEmissao = hoje;
    this.dataCompraPlanejada = hoje;
    this.inicializarLinhas();
    
    // 2. ADICIONADO: Executa a carga das categorias ao abrir a tela
    this.carregarCategorias();
  }

  carregarCategorias(): void {
    // 🎯 Trocando o mock pela chamada real da API através do seu Service
    this.financeiroService.listarCategorias().subscribe({
      next: (dados) => {
        this.categorias = dados; // Aqui chegam as categorias reais com os IDs certos do MySQL
      },
      error: (err) => {
        console.error('Erro ao buscar categorias do banco:', err);
        Swal.fire('Erro', 'Não foi possível carregar as categorias oficiais.', 'error');
      }
    });
  }

  salvarLancamentoManual(): void {
    if (!this.novoLancamento.valor || !this.novoLancamento.descricao) {
      this.exibirErro('Preencha a descrição e o valor.');
      return;
    }

    if (Number(this.novoLancamento.categoriaId) === 0) {
      this.exibirErro('Por favor, selecione uma categoria para o lançamento.');
      return;
    }

    const valorAbsoluto = Math.abs(Number(this.novoLancamento.valor));
    
    // 3. AJUSTADO: Agora 'categoriaId' pega dinamicamente o valor do select da tela
    const dadosParaEnviar: Lancamento = { 
      descricao: this.novoLancamento.descricao,
      valor: this.novoLancamento.tipo === 'Despesa' ? -valorAbsoluto : valorAbsoluto,
      data: new Date().toISOString(),
      dataEmissao: new Date(this.novoLancamento.dataEmissao).toISOString(),
      dataImportacao: new Date().toISOString(),
      tipo: this.novoLancamento.tipo,
      categoriaId: Number(this.novoLancamento.categoriaId)
    };

    this.exibirLoading('Salvando lançamento...');

    this.financeiroService.salvarLancamento(dadosParaEnviar).subscribe({
      next: () => {
        this.novoLancamento = { ...ESTADO_INICIAL_RAPIDO, dataEmissao: new Date().toISOString().split('T')[0] };
        Swal.fire({ icon: 'success', title: 'Lançado!', timer: 1500, showConfirmButton: false });
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        console.error('Erro API:', err);
        Swal.fire('Erro', 'Não foi possível salvar.', 'error');
      }
    });
  }

  inicializarLinhas(): void {
    this.itens = Array.from({ length: 3 }, () => this.criarLinhaVazia());
    this.descricaoCompra = '';
    this.calcularTotal();
  }

  criarLinhaVazia(): ItemCompraLocal {
    return { descricao: '', quantidade: 1, unidade: 'un', preco: null, comprado: false, categoria: 'Mercearia' };
  }

  adicionarLinha(): void {
    this.itens.push(this.criarLinhaVazia());
  }

  removerLinha(index: number): void {
    this.itens.splice(index, 1);
    this.calcularTotal();
  }

  calcularTotal(): void {
    this.total = this.itens.reduce((acc, item) => acc + (item.quantidade * (item.preco || 0)), 0);
    this.saldoRestante = this.limite > 0 ? this.limite - this.total : 0;
    this.porcentagemGasta = this.limite > 0 ? (this.total / this.limite) * 100 : 0;
    this.calcularComparativo();
  }

  calcularTotalManual(valor: any): void {
    // Método auxiliar caso necessário para formatações futuras
  }

  calcularComparativo(): void {
    this.diferencaValor = this.total - this.compraAnterior.valor;
    this.statusComparativo = this.total === 0 ? 'Aguardando itens...' : 
                             (this.total > this.compraAnterior.valor ? 'Mais cara' : 'Mais barata');
  }

  aoSelecionarArquivo(event: any) {
    const ficheiro = event.target.files[0];
    if (ficheiro) {
      const formData = new FormData();
      formData.append("pdf", ficheiro);

      this.exibirLoading('Lendo PDF e salvando lançamento...');

      this.financeiroService.uploadPdf(formData).subscribe({
        next: (res: any) => {
          Swal.close();

          Swal.fire({ 
            icon: 'success', 
            title: 'Importado com Sucesso!', 
            text: 'A nota e os itens já foram registrados.',
            timer: 2000, 
            showConfirmButton: false 
          });

          this.router.navigate(['/lista']);
        },
        error: (err) => {
          console.error('Erro no upload/salvamento:', err);
          Swal.fire('Erro na Importação', 'Não conseguimos processar ou salvar este PDF.', 'error');
        }
      });
    }
  }

  salvarCompra(): void {
    this.tentouSalvar = true;
    if (!this.descricaoCompra) {
      this.exibirErro('Informe o Local ou Supermercado.');
      return;
    }

    const itensValidos = this.itens.filter(i => i.descricao && i.descricao.trim() !== '');
    
    const lancamentoFinal: Lancamento = {
      descricao: this.descricaoCompra,
      valor: -Math.abs(this.total),
      data: new Date().toISOString(),
      dataEmissao: new Date(this.dataCompraPlanejada).toISOString(),
      dataImportacao: new Date().toISOString(),
      tipo: 'Despesa',
      categoriaId: 1, // ID 1 fixo para "Mercado", pois este bloco é exclusivo para a lista de supermercado
      itens: itensValidos.map(item => ({
        descricao: item.descricao,
        preco: item.preco || 0,
        quantidade: item.quantidade || 1,
        unidade: item.unidade,
        categoria: item.categoria,
        comprado: true
      }))
    };

    this.exibirLoading('Registrando compra no banco...');

    this.financeiroService.salvarLancamento(lancamentoFinal).subscribe({
      next: () => {
        Swal.fire({ icon: 'success', title: 'Compra Salva!', timer: 2000, showConfirmButton: false });
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        console.error('Erro ao salvar:', err);
        Swal.fire('Erro no Servidor', 'Verifique a API.', 'error');
      }
    });
  }

  private exibirErro(msg: string) {
    Swal.fire({ icon: 'error', title: 'Atenção', text: msg, confirmButtonColor: '#7c4dff' });
  }

  private exibirLoading(titulo: string) {
    Swal.fire({ 
      title: titulo, 
      allowOutsideClick: false, 
      didOpen: () => Swal.showLoading(),
      confirmButtonColor: '#7c4dff'
    });
  }
}