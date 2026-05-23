import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { FinanceiroService } from '../../services/financeiro.service';
import { Lancamento } from '../../models/financeiro.model'; // Importe sua interface
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

// Atualizado para incluir a estrutura de data
const ESTADO_INICIAL_RAPIDO = { 
  descricao: '', 
  valor: null as any, 
  tipo: 'Despesa', 
  categoriaId: 3,
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
  dataCompraPlanejada: string = ''; // Nova variável para o HTML
  tentouSalvar: boolean = false;

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
  }

  salvarLancamentoManual(): void {
    if (!this.novoLancamento.valor || !this.novoLancamento.descricao) {
      this.exibirErro('Preencha a descrição e o valor.');
      return;
    }

    const valorAbsoluto = Math.abs(Number(this.novoLancamento.valor));
    
    // Objeto ajustado com as novas propriedades obrigatórias
    const dadosParaEnviar: Lancamento = { 
      descricao: this.novoLancamento.descricao,
      valor: this.novoLancamento.tipo === 'Despesa' ? -valorAbsoluto : valorAbsoluto,
      data: new Date().toISOString(),
      dataEmissao: new Date(this.novoLancamento.dataEmissao).toISOString(),
      dataImportacao: new Date().toISOString(),
      tipo: this.novoLancamento.tipo,
      categoriaId: 3
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

    // Mostramos o loading porque o processamento de PDF + Salvamento pode levar 1 ou 2 segundos
    this.exibirLoading('Lendo PDF e salvando lançamento...');

    this.financeiroService.uploadPdf(formData).subscribe({
      next: (res: any) => {
        // Fechamos o loading do Swal
        Swal.close();

        // Feedback de sucesso para o usuário
        Swal.fire({ 
          icon: 'success', 
          title: 'Importado com Sucesso!', 
          text: 'A nota e os itens já foram registrados.',
          timer: 2000, 
          showConfirmButton: false 
        });

        // NAVEGAÇÃO DIRETA: Como o C# já salvou, não precisamos clicar em mais nada.
        // Vamos para a tela de listagem para ver o resultado.
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
    
    // Objeto ajustado com as novas propriedades obrigatórias
    const lancamentoFinal: Lancamento = {
      descricao: this.descricaoCompra,
      valor: -Math.abs(this.total),
      data: new Date().toISOString(),
      dataEmissao: new Date(this.dataCompraPlanejada).toISOString(),
      dataImportacao: new Date().toISOString(),
      tipo: 'Despesa',
      categoriaId: 3, 
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