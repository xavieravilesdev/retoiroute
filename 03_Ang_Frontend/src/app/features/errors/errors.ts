import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, input, linkedSignal, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { toErrorMessage } from '../../core/api-error';
import { CtCommerceClient, DtoQuarantinePage } from '../../shared/api/commerce-client.g';
import { FormatDateOnly } from '../../shared/format-date-only';

@Component({
  selector: 'app-errors',
  imports: [DatePipe, FormatDateOnly],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="card">
      <h1>Registros con errores</h1>
      <p class="muted">Comercios enviados a cuarentena y el motivo por el que fueron rechazados.</p>

      <label for="filter-date">Filtrar por fecha</label>
      <input id="filter-date" type="date" [value]="filterDate()" (input)="onFilterChange($event)" />

      @if (error()) {
        <div class="alert error" role="alert">{{ error() }}</div>
      }

      @if (loading()) {
        <p class="muted">Cargando…</p>
      } @else {
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>#</th>
                <th>Comercio (pc_nomcomred)</th>
                <th>Documento (pc_numdoc)</th>
                <th>Fecha</th>
                <th>Motivo</th>
                <th>Registrado</th>
              </tr>
            </thead>
            <tbody>
              @for (item of items(); track item.id) {
                <tr>
                  <td>{{ item.id }}</td>
                  <td>{{ item.pcNomcomred }}</td>
                  <td>{{ item.pcNumdoc }}</td>
                  <td>{{ item.pcProcessdate | formatDateOnly }}</td>
                  <td class="wrap">{{ item.motivo }}</td>
                  <td>{{ item.createdAt | date: 'dd/MM/yyyy HH:mm' }}</td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="6" class="muted">No hay registros en cuarentena.</td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        <div class="pager">
          <span class="muted">{{ totalCount() }} registros — página {{ pageNumber() }} de {{ totalPages() }}</span>
          <button type="button" class="btn" [disabled]="pageNumber() <= 1" (click)="goTo(pageNumber() - 1)">Anterior</button>
          <button type="button" class="btn" [disabled]="pageNumber() >= totalPages()" (click)="goTo(pageNumber() + 1)">Siguiente</button>
        </div>
      }
    </section>
  `,
})
export class Errors implements OnInit {
  private readonly commerceClient = inject(CtCommerceClient);
  private readonly destroyRef = inject(DestroyRef);

  private static readonly PageSize = 25;

  /** Query param ?date=yyyy-MM-dd (viene desde la pantalla de proceso). */
  readonly date = input<string>();

  protected readonly filterDate = linkedSignal(() => this.date() ?? '');
  protected readonly loading = signal(false);
  protected readonly error = signal('');
  protected readonly pageNumber = signal(1);
  private readonly page = signal<DtoQuarantinePage | undefined>(undefined);

  protected readonly items = computed(() => this.page()?.items ?? []);
  protected readonly totalCount = computed(() => this.page()?.totalCount ?? 0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / Errors.PageSize)));

  ngOnInit(): void {
    this.load();
  }

  protected onFilterChange(event: Event): void {
    this.filterDate.set((event.target as HTMLInputElement).value);
    this.goTo(1);
  }

  protected goTo(pageNumber: number): void {
    this.pageNumber.set(pageNumber);
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.error.set('');

    this.commerceClient
      .epGetQuarantine(this.filterDate() || undefined, this.pageNumber(), Errors.PageSize)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.page.set(response);
          this.loading.set(false);
        },
        error: (e: unknown) => {
          this.error.set(toErrorMessage(e, 'No se pudo consultar la cuarentena.'));
          this.loading.set(false);
        },
      });
  }
}
