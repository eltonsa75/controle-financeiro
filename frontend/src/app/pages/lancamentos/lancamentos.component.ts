import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { LancamentoService, Lancamento } from '../../services/lancamento.service';
import { CategoriaService } from '../../services/services/categoria.service';
import { firstValueFrom } from 'rxjs';

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


async salvarTudo(): Promise<void> {

  // Busca as categorias reais cadastradas no banco
  let categoriasValidas: any[];

  try {
    categoriasValidas = await firstValueFrom(
      this.categoriaService.listarComStatus(
        new Date().getMonth() + 1,
        new Date().getFullYear()
      )
    );
  } catch (erro) {
    console.error('Erro ao carregar categorias:', erro);
    alert('Não foi possível validar as categorias.');
    return;
  }

  // Salva apenas lançamentos novos
  const itensParaSalvar = this.listaLancamentos.filter(
    item => item.id === 0 && item.descricao?.trim()
  );

  for (const item of itensParaSalvar) {

    // Verifica se a categoria existe
    const categoriaExiste = categoriasValidas.some(
      (c: any) => Number(c.id) === Number(item.categoriaId)
    );

    if (!categoriaExiste) {
      alert(
        `Categoria inválida para o lançamento "${item.descricao}". Selecione uma categoria válida.`
      );
      return;
    }

    // Mantém sua conversão original do valor
    let valorParaConverter = item.valor
      ? item.valor.toString()
      : '0';

    if (valorParaConverter.includes(',')) {
      valorParaConverter = valorParaConverter.replace(',', '.');
    }

    item.valor = parseFloat(valorParaConverter) || 0;

    try {
      await firstValueFrom(
        this.lancamentoService.salvar(item)
      );

      console.log('Item salvo:', item.descricao);

    } catch (erro) {
      console.error('Erro ao salvar:', erro);
      alert(`Não foi possível salvar "${item.descricao}".`);
      return;
    }
  }

  // Recarrega a lista após salvar
  this.carregarLancamentos(this.currentPage);
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