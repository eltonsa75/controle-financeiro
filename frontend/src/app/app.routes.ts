import { Routes } from '@angular/router';
import { LoginComponent } from './components/login/login.component';
import { ListaLancamentosComponent } from './components/lista-lancamentos/lista-lancamentos.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { LancamentosComponent } from './pages/lancamentos/lancamentos.component';
import { CategoriasComponent } from './pages/categorias/categorias.component';
import { ComprasComponent } from './pages/compras/compras.component';
import { EstoqueComponent } from './pages/estoque/estoque.component';

export const routes: Routes = [
  // 1. Rota inicial e Login
  { path: '', redirectTo: '/login', pathMatch: 'full' },
  { path: 'login', component: LoginComponent },

  // 2. Sua tela principal pós-login (onde está o importador de PDF)
  { path: 'compras', component: ComprasComponent },

  // 3. Outras rotas do sistema
  { path: 'dashboard', component: DashboardComponent },
  { path: 'lista', component: ListaLancamentosComponent },
  { path: 'lancamentos', component: LancamentosComponent },
  { path: 'categorias', component: CategoriasComponent },
  { path: 'estoque', component: EstoqueComponent }, 
  
  { path: '', redirectTo: '/categorias', pathMatch: 'full' }, // ou sua rota padrão

  // 4. Rota de fuga: se digitar algo errado, volta para o login (ou dashboard)
  { path: '**', redirectTo: '/login' }
];