import { Injectable, inject } from '@angular/core';
import {
  Auth,
  signInWithEmailAndPassword,
  signOut,
  user,
  createUserWithEmailAndPassword
} from '@angular/fire/auth';

@Injectable({
  providedIn: 'root'
})
export class AuthService {

  private auth = inject(Auth);

  user$ = user(this.auth);

  async login(email: string, pass: string) {

    const credential = await signInWithEmailAndPassword(
      this.auth,
      email,
      pass
    );

    // Obtém o token do Firebase
    const token = await credential.user.getIdToken();

    // Salva no localStorage
    localStorage.setItem('token', token);

    console.log('Token salvo:', token);

    return credential;
  }

  async registrar(email: string, pass: string) {
  // Cria o usuário no Firebase
  const credential = await createUserWithEmailAndPassword(
    this.auth,
    email,
    pass
  );

  // Opcional: já loga o usuário após criar a conta
  const token = await credential.user.getIdToken();
  localStorage.setItem('token', token);

  return credential;
}

  async logout() {

    // Logout Firebase
    await signOut(this.auth);

    // Remove token local
    localStorage.removeItem('token');

    console.log('Usuário deslogado');
  }

  async getToken(): Promise<string | null> {

    // Primeiro tenta pegar do localStorage
    const tokenLocal = localStorage.getItem('token');

    if (tokenLocal) {
      return tokenLocal;
    }

    // Fallback Firebase
    const currentUser = this.auth.currentUser;

    if (currentUser) {

      const token = await currentUser.getIdToken();

      localStorage.setItem('token', token);

      return token;
    }

    return null;
  }

  isAuthenticated(): boolean {
    return !!localStorage.getItem('token');
  }
}