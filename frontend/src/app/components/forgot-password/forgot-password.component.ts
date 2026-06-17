import { Component, inject } from '@angular/core'; // Adicione inject se preferir
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { Auth, sendPasswordResetEmail } from '@angular/fire/auth';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [ReactiveFormsModule, CommonModule, RouterModule],
  templateUrl: './forgot-password.component.html',
  styleUrls: ['./forgot-password.component.css']
})
export class ForgotPasswordComponent {
  forgotForm: FormGroup;
  mensagem: string = '';
  enviando: boolean = false;

  private auth = inject(Auth);

constructor(private fb: FormBuilder, private router: Router) {
    this.forgotForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]]
    });
  }

  voltarLogin() {
    // Agora o 'this.router' deve funcionar
    this.router.navigate(['/login']);
  }


async enviarEmail() {
    if (this.forgotForm.valid) {
      this.enviando = true;
      try {
        // 3. Agora o Firebase é chamado usando a instância injetada corretamente
        await sendPasswordResetEmail(this.auth, this.forgotForm.value.email);
        alert('E-mail enviado com sucesso!');
      } catch (error: any) {
        console.error("Erro:", error);
      } finally {
        this.enviando = false;
      }
    }
  }
}