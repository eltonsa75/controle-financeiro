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

  const token = await credential.user.getIdToken();

  localStorage.setItem('token', token);

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

  // Aguarda o Firebase restaurar a sessão após F5
  await this.auth.authStateReady();

  // Obtém o usuário após a inicialização
  const currentUser = this.auth.currentUser;

  if (currentUser) {

    // Obtém o token válido e renova quando necessário
    const token = await currentUser.getIdToken();

    localStorage.setItem('token', token);

    return token;
  }

  // Remove o token somente após confirmar ausência de sessão
  localStorage.removeItem('token');

  return null;
}


  isAuthenticated(): boolean {
    return !!localStorage.getItem('token');
  }
}