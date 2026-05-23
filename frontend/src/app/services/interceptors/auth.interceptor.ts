import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../auth.service';
import { from, switchMap } from 'rxjs';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);

  // Convertemos a Promise do getToken() em um Observable
  return from(authService.getToken()).pipe(
    switchMap(token => {
      // Se tivermos um token, clonamos a requisição e adicionamos o Bearer Token
      if (token) {
        const authReq = req.clone({
          setHeaders: {
            Authorization: `Bearer ${token}`
          }
        });
        return next(authReq);
      }
      
      // Se não tiver token (ex: tela de login), segue a requisição original
      return next(req);
    })
  );
};