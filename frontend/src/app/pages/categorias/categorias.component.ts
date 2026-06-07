import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { CategoriaService, CategoriaStatus } from '../../services/services/categoria.service';
import Swal from 'sweetalert2'; // 🎯 Importação do SweetAlert2
import { LancamentoService } from '../../services/lancamento.service';

// Declaração global para o Bootstrap abrir/fechar modais via código
declare var bootstrap: any;

@Component({
  selector: 'app-categorias',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './categorias.component.html',
  styleUrl: './categorias.component.css'
})
export class CategoriasComponent implements OnInit {

  categorias: CategoriaStatus[] = [];
  formCategoria: FormGroup;
  
  // 🎯 NOVO: Controle de estado da visão atual (Mensal vs Anual)
  visaoAtual: 'mensal' | 'anual' = 'mensal';

constructor(
  private fb: FormBuilder, 
  private categoriaService: CategoriaService,
  private lancamentoService: LancamentoService // 🎯 INJETADO AQUI
) {
  this.formCategoria = this.fb.group({
    id: [null],
    nome: ['', Validators.required],
    metaMensal: [0, [Validators.required, Validators.min(0)]], 
    corHex: ['#007bff'],
    palavrasChave: ['']
  });
}

  ngOnInit(): void {
    this.carregarCategorias();
  }

carregarCategorias(): void {
  const data = new Date();
  const mes = data.getMonth() + 1;
  const ano = data.getFullYear();

  // 1. Busca as categorias padrão mapeadas no banco do usuário
  this.categoriaService.listarComStatus(mes, ano, this.visaoAtual).subscribe({
    next: (dadosCategorias: CategoriaStatus[]) => {
      
      // 2. Busca os gastos reais calculados no Dapper (seja do mês ou do ano acumulado)
      const requestGastos$ = this.visaoAtual === 'anual' 
        ? this.lancamentoService.getGastosAnuaisPorCategoria()
        : this.lancamentoService.getGastosCategoria();

      requestGastos$.subscribe({
        next: (gastosDapper: any[]) => {
          
          // 🎯 O PULO DO GATO PARA ESCONDER AS RECEITAS:
          // Filtramos a lista vinda do banco ANTES de rodar o .map().
          // Adicione aqui todos os nomes de categorias que você não quer que gerem cards na tela.
          const categoriasDeReceita = ['salário', 'salario', 'renda extra', 'investimentos'];

          this.categorias = dadosCategorias
            .filter(cat => !categoriasDeReceita.includes(cat.nome.trim().toLowerCase())) // 🎯 Filtro Ativo!
            .map(cat => {
              const nomeCardLimpo = cat.nome ? cat.nome.trim().toLowerCase() : '';

             const gastoReal = gastosDapper.find(g => {
  // 🎯 Remove espaços, passa para minúsculas e remove todos os acentos para comparar com segurança
  const nomeGastoLimpo = g.categoria ? g.categoria.trim().toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "") : '';
  const nomeCardLimpo = cat.nome ? cat.nome.trim().toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "") : '';
  
  return nomeGastoLimpo === nomeCardLimpo || nomeGastoLimpo.includes(nomeCardLimpo) || nomeCardLimpo.includes(nomeGastoLimpo);
});

              // Se achou o gasto no Dapper, usa o valor. Caso contrário, joga zero.
              const valorGasto = gastoReal ? Math.abs(gastoReal.valorGastoAno || gastoReal.valor || 0) : 0;

              // REGLA DE BI DA OPÇÃO A: Se for visão anual, multiplica por 12
              const metaCalculada = this.visaoAtual === 'anual' ? (cat.metaMensal * 12) : cat.metaMensal;

              // Força o cálculo do percentual com base na meta correta do período
              const percentualCalculated = (metaCalculada > 0) ? (valorGasto / metaCalculada) * 100 : 0;

              return {
                ...cat,
                gastoAtual: valorGasto,
                metaMensal: metaCalculada, 
                percentual: percentualCalculated
              };
            });
          
        },
        error: (err: any) => {
          console.error('Erro ao buscar somatória do Dapper', err);
          this.categorias = dadosCategorias; 
        }
      });
    },
    error: (err: any) => console.error('Erro ao carregar categorias', err)
  });
}

  // 🎯 NOVO: Altera entre a visão Mensal/Anual e dispara uma nova requisição automática
  alterarVisao(novaVisao: 'mensal' | 'anual'): void {
    this.visaoAtual = novaVisao;
    this.carregarCategorias();
  }

  abrirModalNovaCategoria(): void {
    // Resetamos o formulário limpando os resquícios de edições anteriores
    this.formCategoria.reset({ 
      id: null, 
      corHex: '#007bff', 
      metaMensal: 0,
      nome: '',
      palavrasChave: '' 
    });
    
    this.exibirModal();
  }

  editar(categoria: CategoriaStatus): void {
    // Preenche o formulário reativo com o card selecionado automaticamente
    this.formCategoria.patchValue(categoria);
    this.exibirModal();
  }

  salvarCategoria() {
    if (this.formCategoria.valid) {
      const formValues = this.formCategoria.value;

      // Monta o objeto limpando referências nulas e preparando o ID e tipagem para o C#
      const categoriaParaEnviar = {
        id: formValues.id && formValues.id > 0 ? formValues.id : 0,
        nome: formValues.nome,
        metaMensal: formValues.metaMensal,
        corHex: formValues.corHex,
        palavrasChave: formValues.palavrasChave,
        usuarioId: '' // Mantém o TypeScript feliz e em conformidade com o C#
      };

      // Abre o loading para evitar múltiplos cliques no botão de salvar
      Swal.fire({
        title: 'Salvando alterações...',
        allowOutsideClick: false,
        didOpen: () => Swal.showLoading()
      });

      this.categoriaService.salvar(categoriaParaEnviar).subscribe({
        next: () => {
          this.carregarCategorias();
          this.fecharModal();
          
          // Toast de sucesso rápido para o usuário
          Swal.fire({
            icon: 'success',
            title: formValues.id ? 'Categoria Atualizada!' : 'Categoria Criada!',
            timer: 1500,
            showConfirmButton: false
          });
        },
        error: (err) => {
          Swal.close();
          console.error('Erro ao salvar:', err);
          Swal.fire('Erro no Servidor', 'Não foi possível salvar as alterações da categoria.', 'error');
          if (err.error?.errors) console.table(err.error.errors);
        }
      });
    } else {
      Swal.fire('Atenção', 'Por favor, preencha os campos obrigatórios corretamente.', 'warning');
    }
  }

  // Substituído o confirm do navegador pelo modal premium do SweetAlert2
  excluir(id: number | undefined, nome: string): void {
    if (!id) return;

    Swal.fire({
      title: 'Tem certeza?',
      text: `Você vai excluir a categoria "${nome}". Isso pode impactar os lançamentos associados a ela!`,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#d33',
      cancelButtonColor: '#3085d6',
      confirmButtonText: 'Sim, excluir!',
      cancelButtonText: 'Cancelar'
    }).then((result) => {
      if (result.isConfirmed) {
        
        this.categoriaService.excluir(id).subscribe({
          next: () => {
            this.carregarCategorias();
            Swal.fire({
              icon: 'success',
              title: 'Excluída!',
              text: 'A categoria foi removida com sucesso.',
              timer: 1500,
              showConfirmButton: false
            });
          },
          error: (err) => {
            console.error('Erro ao excluir:', err);
            Swal.fire('Erro', 'Não foi possível remover a categoria. Verifique se há dependências.', 'error');
          }
        });

      }
    });
  }

  // --- MÉTODOS AUXILIARES PARA O BOOTSTRAP ---

  private exibirModal() {
    const modalElement = document.getElementById('modalCategoria');
    if (modalElement) {
      const modalInstance = new bootstrap.Modal(modalElement);
      modalInstance.show();
    } else {
      console.error('Elemento #modalCategoria não encontrado no HTML.');
    }
  }

  private fecharModal() {
    const modalElement = document.getElementById('modalCategoria');
    if (modalElement) {
      const modalInstance = bootstrap.Modal.getInstance(modalElement);
      if (modalInstance) {
        modalInstance.hide();
      }
    }
  }

  getProgressBarClass(percentual: number): string {
    if (percentual >= 100) return 'bg-danger';
    if (percentual >= 80) return 'bg-warning';
    return 'bg-success';
  }
}