import { Injectable, signal } from '@angular/core';

/** Fechas (pc_processdate) del último archivo cargado, para ofrecerlas en la pantalla de proceso. */
@Injectable({ providedIn: 'root' })
export class LoadedDates {
  private readonly datesSignal = signal<string[]>([]);
  readonly dates = this.datesSignal.asReadonly();

  set(dates: string[]): void {
    this.datesSignal.set(dates);
  }
}
