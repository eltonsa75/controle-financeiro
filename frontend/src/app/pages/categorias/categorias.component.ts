import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { CategoriaService, CategoriaStatus } from '../../services/services/categoria.service';

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

  constructor(private fb: FormBuilder, private categoriaService: CategoriaService) {
    this.formCategoria = this.fb.group({
      id: [null],
      nome: ['', Validators.required],
      metaMensal: [0, [Validators.required, Validators.min(1)]],
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

    this.categoriaService.listarComStatus(mes, ano)
      .subscribe({
        next: (dados: CategoriaStatus[]) => {
          this.categorias = dados;
        },
        error: (err) => console.error('Erro ao carregar categorias', err)
      });
  }

  abrirModalNovaCategoria(): void {
    // Resetamos o formulário para garantir que o ID seja null (novo registro)
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
    // Preenche o formulário com os dados existentes
    this.formCategoria.patchValue(categoria);
    this.exibirModal();
  }

  salvarCategoria() {
    if (this.formCategoria.valid) {
      const formValues = this.formCategoria.value;

      const categoriaParaEnviar = {
        nome: formValues.nome,
        metaMensal: formValues.metaMensal,
        corHex: formValues.corHex,
        palavrasChave: formValues.palavrasChave,
        usuarioId: "1", // ID fixo conforme sua necessidade atual
        ...(formValues.id && formValues.id > 0 ? { id: formValues.id } : {})
      };

      this.categoriaService.salvar(categoriaParaEnviar).subscribe({
        next: () => {
          this.carregarCategorias();
          this.fecharModal();
          this.formCategoria.reset({ corHex: '#007bff', metaMensal: 0 });
        },
        error: (err) => {
          console.error('Erro ao salvar:', err);
          if (err.error?.errors) console.table(err.error.errors);
        }
      });
    }
  }

  excluir(id: number | undefined): void {
    if (id && confirm('Deseja realmente excluir esta categoria?')) {
      this.categoriaService.excluir(id).subscribe({
        next: () => this.carregarCategorias(),
        error: (err) => console.error('Erro ao excluir:', err)
      });
    }
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