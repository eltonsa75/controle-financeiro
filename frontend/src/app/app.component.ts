
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
  menuMobileAberto = false;

  constructor(private router: Router) {
    this.verificarRota(this.router.url);

    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd)
    ).subscribe((event: NavigationEnd) => {
      this.verificarRota(event.urlAfterRedirects);
      this.fecharMenuMobile();
    });
  }

  abrirFecharMenuMobile(): void {
    this.menuMobileAberto = !this.menuMobileAberto;
  }

  fecharMenuMobile(): void {
    this.menuMobileAberto = false;
  }

  private verificarRota(url: string): void {
    const rotasSemSidebar = [
      '/login',
      '/cadastro',
      '/forgot-password',
      '/nova-senha'
    ];

    this.mostrarSidebar = !rotasSemSidebar.some(
      rota => url.includes(rota)
    );
  }
}
