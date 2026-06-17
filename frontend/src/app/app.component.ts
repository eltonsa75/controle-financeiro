import { Component } from '@angular/core';
import { RouterOutlet, Router, NavigationEnd } from '@angular/router';
import { SidebarComponent } from './components/sidebar/sidebar.component';
import { CommonModule } from '@angular/common';
import { filter } from 'rxjs/operators';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, SidebarComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  mostrarSidebar = false;

  constructor(private router: Router) {
    // 1. Verificação imediata ao iniciar o app
    this.verificarRota(this.router.url);

    // 2. Escuta as mudanças futuras
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd)
    ).subscribe((event: any) => {
      this.verificarRota(event.urlAfterRedirects);
    });
  }

  private verificarRota(url: string) {
    // Adicionamos '/forgot-password' na lista para esconder a sidebar também nela
    const rotasSemSidebar = ['/login', '/cadastro', '/forgot-password'];
    
    // A sidebar só aparece se a URL atual não estiver na lista de rotas ignoradas
    this.mostrarSidebar = !rotasSemSidebar.some(rota => url.includes(rota));
  }
}