import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

import { routes } from './app.routes';
import { authInterceptor } from './services/interceptors/auth.interceptor';

// Firebase - Note que não usamos mais o importProvidersFrom para estes
import { initializeApp, provideFirebaseApp } from '@angular/fire/app';
import { getAuth, provideAuth } from '@angular/fire/auth';
import { environment } from '../environments/environment';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    
    // Configuração correta para Angular 18/19:
    provideFirebaseApp(() => initializeApp(environment.firebase)),
    provideFirebaseApp(() => {
  if (!environment.firebase || !environment.firebase.apiKey) {
    console.error("Erro: Configurações do Firebase não encontradas no environment!");
  }
  return initializeApp(environment.firebase);
}),
    provideAuth(() => getAuth())
  ]
};