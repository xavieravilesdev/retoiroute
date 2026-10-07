import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';

import { toErrorMessage } from '../../core/api-error';
import { LoadedDates } from '../../core/loaded-dates';
import { CtCommerceClient, DtoUploadCommerceResponse } from '../../shared/api/commerce-client.g';
import { CsvPreview, isValidCommerceFileName, readCsvPreview } from '../../shared/csv-preview';
import { FormatDateOnly } from '../../shared/format-date-only';

type UploadStatus = 'idle' | 'reading' | 'uploading';

@Component({
  selector: 'app-upload',
  imports: [RouterLink, FormatDateOnly],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="card">
      <h1>Cargar archivo</h1>
      <p class="muted">
        Seleccione el archivo <code>commerce_DDMMYYYY.csv</code>. Podrá revisarlo antes de enviarlo al servidor.
      </p>

      <label for="csv-file">Archivo CSV</label>
      <input id="csv-file" type="file" accept=".csv,text/csv" (change)="onFileSelected($event)" />

      @if (error()) {
        <div class="alert error" role="alert">{{ error() }}</div>
      }

      @if (preview(); as p) {
        <h2>Previsualización</h2>
        <p class="muted">
          {{ fileName() }} — {{ p.totalRows }} registros. Mostrando los primeros {{ p.rows.length }}.
        </p>

        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                @for (header of p.headers; track $index) {
                  <th>{{ header }}</th>
                }
              </tr>
            </thead>
            <tbody>
              @for (row of p.rows; track $index) {
                <tr>
                  @for (cell of row; track $index) {
                    <td>{{ cell }}</td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
      }

      <div class="actions">
        <button type="button" class="btn primary" [disabled]="!canUpload()" (click)="upload()">
          {{ status() === 'uploading' ? 'Enviando…' : 'Enviar al servidor' }}
        </button>
        @if (status() === 'reading') {
          <span class="muted">Leyendo archivo…</span>
        }
      </div>
    </section>

    @if (result(); as r) {
      <section class="card">
        <div class="alert success" role="status">Archivo cargado correctamente.</div>
        <div class="stats">
          <div class="stat"><strong>{{ r.inserted }}</strong> registros insertados</div>
          <div class="stat"><strong>{{ r.batches }}</strong> lotes</div>
        </div>

        <h2>Fechas encontradas</h2>
        <p class="muted">Elija el día que desea procesar.</p>
        <div class="chips">
          @for (date of r.processDates ?? []; track date) {
            <a class="chip" routerLink="/process" [queryParams]="{ date }">{{ date | formatDateOnly }}</a>
          }
        </div>
      </section>
    }
  `,
})
export class Upload {
  private readonly commerceClient = inject(CtCommerceClient);
  private readonly loadedDates = inject(LoadedDates);
  private readonly destroyRef = inject(DestroyRef);

  private readonly file = signal<File | undefined>(undefined);
  protected readonly fileName = computed(() => this.file()?.name ?? '');
  protected readonly preview = signal<CsvPreview | undefined>(undefined);
  protected readonly status = signal<UploadStatus>('idle');
  protected readonly error = signal('');
  protected readonly result = signal<DtoUploadCommerceResponse | undefined>(undefined);

  protected readonly canUpload = computed(
    () => this.preview() !== undefined && this.error() === '' && this.status() === 'idle',
  );

  protected async onFileSelected(event: Event): Promise<void> {
    const selected = (event.target as HTMLInputElement).files?.item(0) ?? undefined;
    this.file.set(selected);
    this.preview.set(undefined);
    this.result.set(undefined);
    this.error.set('');
    if (!selected) return;

    if (selected.size === 0) {
      this.error.set('El archivo está vacío.');
      return;
    }
    if (!isValidCommerceFileName(selected.name)) {
      this.error.set('El nombre del archivo debe ser commerce_DDMMYYYY.csv con una fecha válida.');
      return;
    }

    this.status.set('reading');
    try {
      const preview = await readCsvPreview(selected);
      if (preview.missingColumns.length > 0) {
        this.error.set(`Faltan las columnas: ${preview.missingColumns.join(', ')}.`);
        return;
      }
      if (preview.totalRows === 0) {
        this.error.set('El archivo no contiene registros de datos.');
        return;
      }
      this.preview.set(preview);
    } catch {
      this.error.set('No se pudo leer el archivo.');
    } finally {
      this.status.set('idle');
    }
  }

  protected upload(): void {
    const file = this.file();
    if (!file || !this.canUpload()) return;

    this.status.set('uploading');
    this.error.set('');

    this.commerceClient
      .epUploadCommerce({ data: file, fileName: file.name })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.result.set(response);
          this.loadedDates.set(response.processDates ?? []);
          this.status.set('idle');
        },
        error: (e: unknown) => {
          this.error.set(toErrorMessage(e, 'No se pudo cargar el archivo.'));
          this.status.set('idle');
        },
      });
  }
}
