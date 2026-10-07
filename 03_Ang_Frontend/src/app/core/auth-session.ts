import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, finalize, firstValueFrom, map, of, shareReplay, tap } from 'rxjs';

import {
  CtAuthClient,
  DtoAuthResponse,
  DtoLoginRequest,
  DtoRefreshRequest,
} from '../shared/api/commerce-client.g';

const REFRESH_TOKEN_KEY = 'commerce.refresh-token';

/**
 * Estado de la sesión.
 * - access token: solo en memoria (no sobrevive a una recarga ni queda expuesto en el almacenamiento).
 * - refresh token: en sessionStorage (se borra al cerrar la pestaña) para recuperar la sesión al recargar.
 */
@Injectable({ providedIn: 'root' })
export class AuthSession {
  private readonly authClient = inject(CtAuthClient);

  private readonly accessTokenSignal = signal('');
  private readonly usernameSignal = signal('');
  private refreshInFlight$?: Observable<DtoAuthResponse>;

  readonly username = this.usernameSignal.asReadonly();
  readonly isAuthenticated = computed(() => this.accessTokenSignal() !== '');

  accessToken(): string {
    return this.accessTokenSignal();
  }

  hasRefreshToken(): boolean {
    return this.readRefreshToken() !== '';
  }

  login(username: string, password: string): Observable<DtoAuthResponse> {
    return this.authClient
      .epLogin(new DtoLoginRequest({ username, password }))
      .pipe(tap((response) => this.store(response)));
  }

  /** Renueva los tokens. Las llamadas simultáneas comparten una sola petición (el refresh token rota en cada uso). */
  refresh(): Observable<DtoAuthResponse> {
    this.refreshInFlight$ ??= this.authClient
      .epRefreshToken(new DtoRefreshRequest({ refreshToken: this.readRefreshToken() }))
      .pipe(
        tap((response) => this.store(response)),
        finalize(() => (this.refreshInFlight$ = undefined)),
        shareReplay(1),
      );
    return this.refreshInFlight$;
  }

  /** Revoca el refresh token en el servidor (si es posible) y limpia la sesión local. */
  logout(): Observable<void> {
    const refreshToken = this.readRefreshToken();
    if (!this.isAuthenticated() || refreshToken === '') {
      this.clear();
      return of(undefined);
    }

    return this.authClient.epLogout(new DtoRefreshRequest({ refreshToken })).pipe(
      catchError(() => of(undefined)),
      map(() => undefined),
      finalize(() => this.clear()),
    );
  }

  /** Intenta recuperar la sesión al arrancar la aplicación. Nunca falla: si no se puede, queda sin sesión. */
  async restoreSession(): Promise<void> {
    if (!this.hasRefreshToken()) return;
    try {
      await firstValueFrom(this.refresh());
    } catch {
      this.clear();
    }
  }

  clear(): void {
    this.accessTokenSignal.set('');
    this.usernameSignal.set('');
    this.writeRefreshToken('');
  }

  private store(response: DtoAuthResponse): void {
    this.accessTokenSignal.set(response.accessToken ?? '');
    this.usernameSignal.set(response.username ?? '');
    this.writeRefreshToken(response.refreshToken ?? '');
  }

  private readRefreshToken(): string {
    try {
      return sessionStorage.getItem(REFRESH_TOKEN_KEY) ?? '';
    } catch {
      return '';
    }
  }

  private writeRefreshToken(token: string): void {
    try {
      if (token) sessionStorage.setItem(REFRESH_TOKEN_KEY, token);
      else sessionStorage.removeItem(REFRESH_TOKEN_KEY);
    } catch {
      /* almacenamiento no disponible: la sesión dura hasta recargar */
    }
  }
}
