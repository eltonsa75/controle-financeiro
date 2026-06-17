import { Component, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';
import { Router, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-cadastro',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './cadastro.component.html',
  styleUrls: ['./cadastro.component.css']
})
export class CadastroComponent {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);

  // Variável para controlar carregamento
  loading = false;
  erro = '';

// No seu cadastro.component.ts
cadastroForm: FormGroup = this.fb.group({
  // Campo 'nome' obrigatório, min 3 caracteres
  nome: ['', [Validators.required, Validators.minLength(3)]],
  
  // Campo 'email' obrigatório e deve ser um formato de e-mail válido
  email: ['', [Validators.required, Validators.email]],
  
  // Campo 'senha' obrigatório, mínimo 6 caracteres (regra padrão do Firebase)
  senha: ['', [Validators.required, Validators.minLength(6)]]
});

  async cadastrar() {
    console.log('Botão clicado! Status do formulário:', this.cadastroForm.valid);
  console.log('Dados:', this.cadastroForm.value);
    if (this.cadastroForm.invalid) return;

    this.loading = true;
    this.erro = '';

    const { email, senha } = this.cadastroForm.value;

    try {
      await this.authService.registrar(email, senha);
      
      // Redireciona para o dashboard após sucesso
      this.router.navigate(['/dashboard']);
    } catch (error: any) {
      console.error('Erro no cadastro:', error);
      
      // Tratamento amigável de erro
      if (error.code === 'auth/email-already-in-use') {
        this.erro = 'Este e-mail já está em uso.';
      } else {
        this.erro = 'Erro ao criar conta. Tente novamente.';
      }
    } finally {
      this.loading = false;
    }
  }
}