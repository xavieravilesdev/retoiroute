import { ChangeDetectionStrategy, Component, DestroyRef, inject, input, linkedSignal, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';

import { toErrorMessage } from '../../core/api-error';
import { LoadedDates } from '../../core/loaded-dates';
import {
  CtCommerceClient,
  DtoProcessCommerceRequest,
  DtoProcessCommerceResponse,
} from '../../shared/api/commerce-client.g';
import { FormatDateOnly } from '../../shared/format-date-only';

@Component({
  selector: 'app-process',
  imports: [RouterLink, FormatDateOnly],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="card">
      <h1>Procesar registros</h1>
      <p class="muted">
        Valida los comercios cargados del día elegido (columna <code>pc_processdate</code>). Los registros inválidos
        se mueven a cuarentena con su motivo.
      </p>

      @if (loadedDates.dates().length) {
        <div class="chips">
          <span class="muted">Fechas del último archivo:</span>
          @for (d of loadedDates.dates(); track d) {
            <button type="button" class="chip" [class.active]="d === selectedDate()" (click)="selectedDate.set(d)">
              {{ d | formatDateOnly }}
            </button>
          }
        </div>
      }

      <label for="process-date">Fecha a procesar</label>
      <input id="process-date" type="date" [value]="selectedDate()" (input)="onDateInput($event)" />

      @if (error()) {
        <div class="alert error" role="alert">{{ error() }}</div>
      }

      <div class="actions">
        <button type="button" class="btn primary" [disabled]="!selectedDate() || busy()" (click)="process()">
          {{ busy() ? 'Procesando…' : 'Procesar' }}
        </button>
      </div>
    </section>

    @if (result(); as r) {
      <section class="card">
        <div class="alert success" role="status">Proceso finalizado para {{ r.processDate | formatDateOnly }}.</div>
        <div class="stats">
          <div class="stat"><strong>{{ r.evaluated }}</strong> registros evaluados</div>
          <div class="stat"><strong>{{ r.quarantined }}</strong> enviados a cuarentena</div>
        </div>

        @if ((r.quarantined ?? 0) > 0) {
          <div class="actions">
            <a class="btn" routerLink="/errors" [queryParams]="{ date: r.processDate }">Ver registros con error</a>
          </div>
        }
      </section>
    }
  `,
})
export class Process {
  protected readonly loadedDates = inject(LoadedDates);
  private readonly commerceClient = inject(CtCommerceClient);
  private readonly destroyRef = inject(DestroyRef);

  /** Query param ?date=yyyy-MM-dd (viene desde la pantalla de carga). */
  readonly date = input<string>();

  /** Fecha elegida: parte de la del query param, pero el usuario puede cambiarla. */
  protected readonly selectedDate = linkedSignal(() => this.date() ?? '');

  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly result = signal<DtoProcessCommerceResponse | undefined>(undefined);

  protected onDateInput(event: Event): void {
    this.selectedDate.set((event.target as HTMLInputElement).value);
  }

  protected process(): void {
    const processDate = this.selectedDate();
    if (!processDate || this.busy()) return;

    this.busy.set(true);
    this.error.set('');
    this.result.set(undefined);

    this.commerceClient
      .epProcessCommerce(new DtoProcessCommerceRequest({ processDate }))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.result.set(response);
          this.busy.set(false);
        },
        error: (e: unknown) => {
          this.error.set(toErrorMessage(e, 'No se pudo procesar la fecha.'));
          this.busy.set(false);
        },
      });
  }
}
