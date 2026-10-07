import { HttpErrorResponse } from '@angular/common/http';

import { ApiException } from '../shared/api/commerce-client.g';

interface ProblemBody {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

/** Convierte cualquier error de la API en un mensaje legible para el usuario. */
export function toErrorMessage(error: unknown, fallback = 'Ocurrió un error inesperado. Intente nuevamente.'): string {
  const status = error instanceof ApiException || error instanceof HttpErrorResponse ? error.status : undefined;

  if (status === 0) return 'No hay conexión con el servidor.';
  if (status === 429) return 'Demasiados intentos. Espere un minuto e intente de nuevo.';

  const raw = error instanceof ApiException ? error.response : error instanceof HttpErrorResponse ? error.error : undefined;
  const problem = parseProblem(raw);

  if (problem?.errors) {
    const messages = Object.values(problem.errors).flat();
    if (messages.length > 0) return messages.join(' ');
  }
  return problem?.detail ?? problem?.title ?? fallback;
}

function parseProblem(raw: unknown): ProblemBody | undefined {
  if (typeof raw === 'string') {
    try {
      return JSON.parse(raw) as ProblemBody;
    } catch {
      return undefined;
    }
  }
  return typeof raw === 'object' && raw !== null ? (raw as ProblemBody) : undefined;
}
