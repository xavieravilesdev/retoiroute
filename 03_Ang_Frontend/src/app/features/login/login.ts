import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { form, FormField, required, submit } from '@angular/forms/signals';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { toErrorMessage } from '../../core/api-error';
import { AuthSession } from '../../core/auth-session';

@Component({
  selector: 'app-login',
  imports: [FormField],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="card login">
      <h1>Iniciar sesión</h1>
      <p class="muted">Ingrese con su usuario para cargar y procesar archivos.</p>

      <form (submit)="onSubmit(); $event.preventDefault()">
        <label for="username">Usuario</label>
        <input id="username" type="text" autocomplete="username" [formField]="loginForm.username" />
        @if (loginForm.username().touched() && loginForm.username().errors().length) {
          <span class="field-error">{{ loginForm.username().errors()[0].message }}</span>
        }

        <label for="password">Contraseña</label>
        <input id="password" type="password" autocomplete="current-password" [formField]="loginForm.password" />
        @if (loginForm.password().touched() && loginForm.password().errors().length) {
          <span class="field-error">{{ loginForm.password().errors()[0].message }}</span>
        }

        @if (error()) {
          <div class="alert error" role="alert">{{ error() }}</div>
        }

        <div class="actions">
          <button type="submit" class="btn primary" [disabled]="loginForm().invalid() || busy()">
            {{ busy() ? 'Ingresando…' : 'Ingresar' }}
          </button>
        </div>
      </form>
    </section>
  `,
  styles: `
    .login { max-width: 420px; margin: 3rem auto; }
  `,
})
export class Login {
  private readonly session = inject(AuthSession);
  private readonly router = inject(Router);

  /** Ruta a la que volver después de iniciar sesión (query param ?returnUrl=). */
  readonly returnUrl = input<string>();

  protected readonly error = signal('');
  protected readonly busy = signal(false);

  private readonly model = signal({ username: '', password: '' });
  protected readonly loginForm = form(this.model, (path) => {
    required(path.username, { message: 'El usuario es obligatorio' });
    required(path.password, { message: 'La contraseña es obligatoria' });
  });

  protected onSubmit(): void {
    submit(this.loginForm, async () => {
      this.busy.set(true);
      this.error.set('');
      try {
        const { username, password } = this.model();
        await firstValueFrom(this.session.login(username, password));
        await this.router.navigateByUrl(this.safeReturnUrl());
      } catch (e) {
        this.error.set(toErrorMessage(e, 'No se pudo iniciar sesión.'));
      } finally {
        this.busy.set(false);
      }
    });
  }

  /** Solo se aceptan rutas internas (evita open redirect). */
  private safeReturnUrl(): string {
    const url = this.returnUrl() ?? '';
    return url.startsWith('/') && !url.startsWith('//') ? url : '/upload';
  }
}
