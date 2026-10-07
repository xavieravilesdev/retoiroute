import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthSession } from './auth-session';

/** Protege las rutas privadas. (La seguridad real está en la API: este guard solo mejora la experiencia.) */
export const authGuard: CanActivateFn = (_route, state) => {
  const session = inject(AuthSession);
  const router = inject(Router);

  return session.isAuthenticated()
    ? true
    : router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};
