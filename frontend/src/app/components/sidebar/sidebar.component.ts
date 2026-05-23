import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
// Importe seu serviço de autenticação (ajuste o caminho se necessário)
import { AuthService } from '../../services/auth.service'; 

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.css'
})
export class SidebarComponent {
  // Injetando as dependências no padrão Angular 19
  private authService = inject(AuthService);
  private router = inject(Router);

  logout(): void {
    // 1. Limpa o token/sessão no seu serviço
    this.authService.logout(); 
    
    // 2. Manda o usuário de volta para a tela de login roxa
    this.router.navigate(['/login']);
    
    console.log('Usuário deslogado com sucesso.');
  }
}