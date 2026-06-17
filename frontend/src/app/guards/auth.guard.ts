import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true; // Usuário está logado, pode passar
  } else {
    router.navigate(['/login']); // Usuário não logado, manda para o login
    return false; // Bloqueia o acesso
  }
};