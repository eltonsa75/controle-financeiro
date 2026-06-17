import { Component, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { Auth, sendPasswordResetEmail, ActionCodeSettings } from '@angular/fire/auth';
import { Router } from '@angular/router'; // Adicionado para gerenciar o redirecionamento

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [ReactiveFormsModule, CommonModule],
  templateUrl: './forgot-password.component.html',
  styleUrls: ['./forgot-password.component.css']
})
export class ForgotPasswordComponent {
  private auth = inject(Auth);
  private fb = inject(FormBuilder);
  private router = inject(Router); // Injetando o Router

  forgotForm: FormGroup;
  mensagem: string = '';
  enviando: boolean = false;
  isSucesso: boolean = false;

  constructor() {
    this.forgotForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]]
    });
  }

  async enviarEmail() {
    if (this.forgotForm.valid) {
      this.enviando = true;
      this.mensagem = ''; // Limpa mensagens anteriores

      const actionCodeSettings: ActionCodeSettings = {
        url: 'http://localhost:4200/nova-senha',
        handleCodeInApp: true
      };

      try {
        await sendPasswordResetEmail(this.auth, this.forgotForm.value.email, actionCodeSettings);
        
        // Ativa o estado de sucesso e define a mensagem para o usuário
        this.isSucesso = true;
        this.mensagem = 'Sucesso! Um link de recuperação foi enviado para o seu e-mail.';
        
        // Limpa o campo de e-mail após o envio bem-sucedido
        this.forgotForm.reset();
        
      } catch (error: any) {
        this.isSucesso = false;
        
        // Tratamento de erros comuns do Firebase
        if (error.code === 'auth/user-not-found') {
          this.mensagem = 'Este e-mail não está cadastrado no sistema.';
        } else if (error.code === 'auth/invalid-email') {
          this.mensagem = 'O formato do e-mail inserido é inválido.';
        } else {
          this.mensagem = 'Erro ao enviar o e-mail. Tente novamente mais tarde.';
        }
        console.error('Erro Firebase Auth:', error);
      } finally {
        this.enviando = false;
      }
    }
  }

  // Função chamada pelo link "Voltar para o Login" no HTML
  voltarLogin() {
    this.router.navigate(['/login']);
  }
}