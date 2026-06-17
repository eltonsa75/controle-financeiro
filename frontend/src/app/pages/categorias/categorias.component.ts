import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { CategoriaService, CategoriaStatus } from '../../services/services/categoria.service';
import { LancamentoService } from '../../services/lancamento.service';
import { FinanceiroService } from '../../services/financeiro.service';
import { CdkDragDrop, moveItemInArray, DragDropModule } from '@angular/cdk/drag-drop';

import { Categoria } from '../../models/financeiro.model';
import Swal from 'sweetalert2'; 

// Declaração global para o Bootstrap
declare var bootstrap: any;

@Component({
  selector: 'app-categorias',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, DragDropModule],
  templateUrl: './categorias.component.html',
  styleUrl: './categorias.component.css'
})
export class CategoriasComponent implements OnInit {

  categorias: Categoria[] = [];
  formCategoria: FormGroup;
  visaoAtual: 'mensal' | 'anual' = 'mensal';

  constructor(
    private fb: FormBuilder, 
    private categoriaService: CategoriaService,
    private lancamentoService: LancamentoService,
    private financeiroService: FinanceiroService // Injetado corretamente
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

// No método drop:
drop(event: CdkDragDrop<Categoria[]>) {
  moveItemInArray(this.categorias, event.previousIndex, event.currentIndex);
  
  this.categorias.forEach((item, index) => {
    // Forçamos o 'ordem' como number
    item.ordem = index;
  });

  // Usamos 'as any' para evitar o erro de tipagem na chamada do serviço
  this.financeiroService.atualizarOrdemCategorias(this.categorias as any).subscribe({
    next: () => console.log('Salvo!'),
    error: (err: any) => console.error(err)
  });
}

carregarCategorias(): void {
    const data = new Date();
    const mes = data.getMonth() + 1;
    const ano = data.getFullYear();

    // 1. Mantenha o tipo que o serviço exige: CategoriaStatus[]
    this.categoriaService.listarComStatus(mes, ano, this.visaoAtual).subscribe({
      next: (dadosCategorias: CategoriaStatus[]) => {
        
        // 2. Converta dadosCategorias (Status) para Categoria se necessário
        // Ou apenas trate como o tipo que o serviço devolve
        const requestGastos$ = this.visaoAtual === 'anual' 
          ? this.lancamentoService.getGastosAnuaisPorCategoria()
          : this.lancamentoService.getGastosCategoria();

        requestGastos$.subscribe({
          next: (gastosDapper: any[]) => {
            const categoriasDeReceita = ['salário', 'salario', 'renda extra', 'investimentos'];
            
            // 3. Aqui transformamos CategoriaStatus em Categoria (o formato que seu Dashboard espera)
            this.categorias = (dadosCategorias as Categoria[])
              .filter(cat => !categoriasDeReceita.includes(cat.nome.trim().toLowerCase()))
              .map(cat => {
                const gastoReal = gastosDapper.find(g => {
                  const nomeGasto = g.categoria?.trim().toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "");
                  const nomeCard = cat.nome?.trim().toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "");
                  return nomeGasto === nomeCard || nomeGasto?.includes(nomeCard) || nomeCard?.includes(nomeGasto);
                });

                const valorGasto = gastoReal ? Math.abs(gastoReal.valorGastoAno ?? gastoReal.valor ?? 0) : 0;
                const metaCalculada = this.visaoAtual === 'anual' ? ((cat.metaMensal ?? 0) * 12) : (cat.metaMensal ?? 0);
                const percentual = (metaCalculada > 0) ? (valorGasto / metaCalculada) * 100 : 0;

                return { 
                  ...cat, 
                  id: cat.id ?? 0,
                  gastoAtual: valorGasto, 
                  metaMensal: metaCalculada, 
                  percentual: percentual 
                } as Categoria;
              });
          },
          error: (err: any) => { 
            console.error('Erro gastos', err); 
            this.categorias = dadosCategorias as Categoria[]; 
          }
        });
      },
      error: (err: any) => console.error('Erro categorias', err)
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

editar(categoria: Categoria): void {
  // Garantimos valores padrão para campos opcionais antes de enviar para o formulário
  this.formCategoria.patchValue({
    ...categoria,
    gastoAtual: categoria.gastoAtual ?? 0,
    metaMensal: categoria.metaMensal ?? 0
  });
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