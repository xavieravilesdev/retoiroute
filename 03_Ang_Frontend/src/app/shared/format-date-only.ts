import { Pipe, PipeTransform } from '@angular/core';

/** Convierte una fecha "yyyy-MM-dd" (DateOnly de la API) a "dd/MM/yyyy" sin pasar por Date (evita desfases de zona horaria). */
@Pipe({ name: 'formatDateOnly' })
export class FormatDateOnly implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value) return '';
    const [year, month, day] = value.split('-');
    return year && month && day ? `${day}/${month}/${year}` : value;
  }
}
