import { Component } from '@angular/core';
import { RouterOutlet, RouterLink } from '@angular/router'; 
import { SidebarComponent } from './components/sidebar/sidebar.component';
import { CommonModule } from '@angular/common';


@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, SidebarComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  title = 'financeiro-app';
}