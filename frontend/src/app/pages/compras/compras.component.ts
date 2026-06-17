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
    // 🎯 CORRIGIDO: Captura o dia, mês e ano locais do computador, evitando conversões UTC truncadas
    const hojeLocal = this.obterDataLocalString();
    
    this.novoLancamento.dataEmissao = hojeLocal;
    this.dataCompraPlanejada = hojeLocal;
    this.inicializarLinhas();
    
    this.carregarCategorias();
  }

  // 🎯 MÉTODO AUXILIAR: Gera a string "AAAA-MM-DD" com segurança baseada no relógio físico de Brasília
  private obterDataLocalString(): string {
    const hoje = new Date();
    const ano = hoje.getFullYear();
    const mes = String(hoje.getMonth() + 1).padStart(2, '0');
    const dia = String(hoje.getDate()).padStart(2, '0');
    return `${ano}-${mes}-${dia}`;
  }

  carregarCategorias(): void {
    this.financeiroService.listarCategorias().subscribe({
      next: (dados) => {
        this.categorias = dados;
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
    
    // Convertendo para String ISO contendo fuso local ou adicionando o horário de meio-dia para evitar retrocesso de fuso
    const dataEmissaoFormatada = new Date(this.novoLancamento.dataEmissao + 'T12:00:00').toISOString();

    const dadosParaEnviar: Lancamento = { 
      descricao: this.novoLancamento.descricao,
      valor: this.novoLancamento.tipo === 'Despesa' ? -valorAbsoluto : valorAbsoluto,
      data: new Date().toISOString(),
      dataEmissao: dataEmissaoFormatada,
      dataImportacao: new Date().toISOString(),
      tipo: this.novoLancamento.tipo,
      categoriaId: Number(this.novoLancamento.categoriaId)
    };

    this.exibirLoading('Salvando lançamento...');

    this.financeiroService.salvarLancamento(dadosParaEnviar).subscribe({
      next: () => {
        // 🎯 CORRIGIDO: O reset do formulário agora ganha a data local limpa, ao invés do toISOString() antigo
        this.novoLancamento = { ...ESTADO_INICIAL_RAPIDO, dataEmissao: this.obterDataLocalString() };
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
    return { descricao: '', quantidade: 1, unidade: 'un', preco: null, comprado: false, category: 'Mercearia' } as any;
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

  calcularTotalManual(valor: any): void {}

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
    
    // 🎯 TRATAMENTO ADICIONAL: Aplica segurança ao salvar a lista de compras manual do supermercado também
    const dataCompraFormatada = new Date(this.dataCompraPlanejada + 'T12:00:00').toISOString();

    const lancamentoFinal: Lancamento = {
      descricao: this.descricaoCompra,
      valor: -Math.abs(this.total),
      data: new Date().toISOString(),
      dataEmissao: dataCompraFormatada,
      dataImportacao: new Date().toISOString(),
      tipo: 'Despesa',
      categoriaId: 1, 
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