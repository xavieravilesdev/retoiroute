import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';

import { environment } from '../environments/environment';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth-interceptor';
import { AuthSession } from './core/auth-session';
import { API_BASE_URL } from './shared/api/commerce-client.g';

export const appConfig: ApplicationConfig = {
  providers: [
    // withComponentInputBinding: los query params (date, returnUrl) llegan como inputs de los componentes
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([authInterceptor])),
    { provide: API_BASE_URL, useValue: environment.apiUrl },
    // Recupera la sesión al recargar la página (usa el refresh token guardado en la pestaña)
    provideAppInitializer(() => inject(AuthSession).restoreSession()),
  ],
};
