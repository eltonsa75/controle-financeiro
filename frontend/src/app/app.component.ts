import { Component } from '@angular/core';
import { RouterOutlet, Router, NavigationEnd } from '@angular/router'; // Adicionado Router e NavigationEnd
import { SidebarComponent } from './components/sidebar/sidebar.component';
import { CommonModule } from '@angular/common';
import { filter } from 'rxjs/operators'; // Adicionado filter para interceptar as rotas

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, SidebarComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  title = 'financeiro-app';
  mostrarSidebar = true; // Variável que o HTML vai ler para ocultar/exibir a barra

  constructor(private router: Router) {
    // Escuta os eventos de navegação do Angular
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd)
    ).subscribe((event: any) => {
      // Se a URL atual tiver '/login', o 'includes' dá true, e o '!' inverte para false (esconde)
      this.mostrarSidebar = !event.url.includes('/login');
    });
  }
}