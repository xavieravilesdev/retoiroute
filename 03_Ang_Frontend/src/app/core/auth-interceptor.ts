import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';

import { environment } from '../../environments/environment';
import { AuthSession } from './auth-session';

const PUBLIC_ENDPOINTS = ['/api/auth/login', '/api/auth/refresh'];

/**
 * Agrega el access token a las llamadas a la API. Si la API responde 401 renueva la sesión una vez
 * y reintenta; si no se puede renovar, cierra la sesión y envía al login.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const session = inject(AuthSession);
  const router = inject(Router);

  const isApiCall = req.url.startsWith(environment.apiUrl);
  const isPublic = PUBLIC_ENDPOINTS.some((endpoint) => req.url.endsWith(endpoint));
  if (!isApiCall || isPublic) return next(req);

  return next(withToken(req, session.accessToken())).pipe(
    catchError((error: unknown) => {
      const isUnauthorized = error instanceof HttpErrorResponse && error.status === 401;
      if (!isUnauthorized || !session.hasRefreshToken()) return throwError(() => error);

      return session.refresh().pipe(
        switchMap(() => next(withToken(req, session.accessToken()))),
        catchError((refreshError: unknown) => {
          session.clear();
          void router.navigateByUrl('/login');
          return throwError(() => refreshError);
        }),
      );
    }),
  );
};

function withToken(req: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
}
