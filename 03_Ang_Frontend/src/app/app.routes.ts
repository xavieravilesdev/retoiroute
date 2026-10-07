import { Routes } from '@angular/router';

import { authGuard } from './core/auth-guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'upload' },
  {
    path: 'login',
    title: 'Iniciar sesión',
    loadComponent: () => import('./features/login/login').then((m) => m.Login),
  },
  {
    path: 'upload',
    title: 'Cargar archivo',
    canActivate: [authGuard],
    loadComponent: () => import('./features/upload/upload').then((m) => m.Upload),
  },
  {
    path: 'process',
    title: 'Procesar registros',
    canActivate: [authGuard],
    loadComponent: () => import('./features/process/process').then((m) => m.Process),
  },
  {
    path: 'errors',
    title: 'Registros con errores',
    canActivate: [authGuard],
    loadComponent: () => import('./features/errors/errors').then((m) => m.Errors),
  },
  { path: '**', redirectTo: 'upload' },
];
