import { Routes } from '@angular/router';
import { LoginComponent } from './components/login/login.component';
import { authGuard } from './guards/auth.guard'; // Importação do Guard
import { ForgotPasswordComponent } from './components/forgot-password/forgot-password.component';

export const routes: Routes = [
  // 1. ROTAS DE AUTENTICAÇÃO (Públicas)
  { path: 'login', component: LoginComponent },
  { path: 'forgot-password', component: ForgotPasswordComponent },
  { path: '', redirectTo: '/login', pathMatch: 'full' }, // Rota padrão
  { 
    path: 'cadastro', 
    loadComponent: () => import('./components/cadastro/cadastro.component').then(m => m.CadastroComponent) 
  },

  // 2. ROTAS DO SISTEMA (Protegidas pelo AuthGuard)
  {
    path: '',
    canActivate: [authGuard], // Protege todas as rotas filhas abaixo
    children: [
      { path: 'dashboard', loadComponent: () => import('./pages/dashboard/dashboard.component').then(m => m.DashboardComponent) },
      { path: 'compras', loadComponent: () => import('./pages/compras/compras.component').then(m => m.ComprasComponent) },
      { path: 'lista', loadComponent: () => import('./components/lista-lancamentos/lista-lancamentos.component').then(m => m.ListaLancamentosComponent) },
      { path: 'lancamentos', loadComponent: () => import('./pages/lancamentos/lancamentos.component').then(m => m.LancamentosComponent) },
      { path: 'categorias', loadComponent: () => import('./pages/categorias/categorias.component').then(m => m.CategoriasComponent) },
      { path: 'estoque', loadComponent: () => import('./pages/estoque/estoque.component').then(m => m.EstoqueComponent) },
      { path: 'forgot-password', component: ForgotPasswordComponent },
    ]
  },
  
  // 3. REDIRECIONAMENTOS
  { path: '', redirectTo: '/login', pathMatch: 'full' },
  { path: '**', redirectTo: '/login' }
];