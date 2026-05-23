import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms'; // ESSENCIAL para ngModel
import { AuthService } from '../../services/auth.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-login',
  standalone: true,
  // Certifique-se de que FormsModule está aqui!
  imports: [CommonModule, FormsModule], 
  templateUrl: './login.component.html',
  styleUrl: './login.component.css'
})
export class LoginComponent {
  private authService = inject(AuthService);
  private router = inject(Router);

  // Variáveis para o ngModel (mesmo nome usado no HTML)
  email = '';
  senha = '';
  rememberMe = false; // Adicionada esta variável para o checkbox
  erro = '';

  async onSubmit() {
    try {
      console.log('Tentando logar...');
      await this.authService.login(this.email, this.senha);
      console.log('Logado com sucesso!');
      
      // Mudar para a rota correta após o login
      this.router.navigate(['/compras']); 
      
    } catch (e: any) {
      console.error(e);
      // Firebase costuma retornar erros no formato { code: 'auth/invalid-email' }
      this.erro = 'Usuário ou senha inválidos.';
    }
  }
}