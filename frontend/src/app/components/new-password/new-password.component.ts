import { Component, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, AbstractControl } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { Auth, confirmPasswordReset } from '@angular/fire/auth';
import { ActivatedRoute, Router } from '@angular/router';

@Component({
  selector: 'app-new-password',
  standalone: true,
  imports: [ReactiveFormsModule, CommonModule],
  templateUrl: './new-password.component.html',
  styleUrls: ['./new-password.component.css']
})
export class NewPasswordComponent {
  private auth = inject(Auth);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  
  passForm: FormGroup;
  oobCode: string = '';
  mensagem: string = '';
  enviando: boolean = false;
  isSucesso: boolean = false;

  constructor(private fb: FormBuilder) {
    this.passForm = this.fb.group({
      password: ['', [Validators.required, Validators.minLength(6)]],
      confirmPassword: ['', [Validators.required]]
    }, { 
      validators: this.passwordMatchValidator // Validador customizado
    });
    
    // Captura o token de segurança da URL enviado pelo e-mail
    this.oobCode = this.route.snapshot.queryParams['oobCode'] || '';
  }

  // Validador que compara se os campos de senha e confirmação são iguais
  passwordMatchValidator(control: AbstractControl) {
    const password = control.get('password')?.value;
    const confirmPassword = control.get('confirmPassword')?.value;
    return password === confirmPassword ? null : { mismatch: true };
  }

  async definirNovaSenha() {
    if (this.passForm.valid && this.oobCode) {
      this.enviando = true;
      this.mensagem = '';
      
      try {
        const novaSenha = this.passForm.get('password')?.value;
        await confirmPasswordReset(this.auth, this.oobCode, novaSenha);
        
        this.isSucesso = true;
        this.mensagem = 'Senha redefinida com sucesso! Redirecionando para o login...';
        
        // Redireciona para a tela de login após 2.5 segundos
        setTimeout(() => this.router.navigate(['/login']), 2500);
      } catch (error: any) {
        this.isSucesso = false;
        if (error.code === 'auth/expired-action-code') {
          this.mensagem = 'Este link expirou. Por favor, solicite uma nova recuperação.';
        } else if (error.code === 'auth/invalid-action-code') {
          this.mensagem = 'O link de recuperação é inválido.';
        } else {
          this.mensagem = 'Erro ao atualizar a senha: ' + error.message;
        }
      } finally {
        this.enviando = false;
      }
    }
  }
}