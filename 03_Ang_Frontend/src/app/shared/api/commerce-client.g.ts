/* eslint-disable */
/**
 * Cliente de la API Commerce.
 *
 * ESTE ARCHIVO SE REGENERA CON NSWAG:  npm run api:generate   (con la API en ejecución)
 * La versión incluida está escrita a mano con la misma forma que genera NSwag (template Angular,
 * un cliente por controlador, método = OperationId, fechas como string) para poder compilar y probar
 * el front antes de ejecutar el generador. No editar a mano una vez regenerado.
 */
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, InjectionToken, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';

export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');

/* ----------------------------- Clientes ----------------------------- */

@Injectable({ providedIn: 'root' })
export class CtAuthClient {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = (inject(API_BASE_URL, { optional: true }) ?? '').replace(/\/+$/, '');

  /** Inicia sesión y devuelve un access token y un refresh token. */
  epLogin(body: DtoLoginRequest): Observable<DtoAuthResponse> {
    return this.http.post<unknown>(`${this.baseUrl}/api/auth/login`, body.toJSON()).pipe(
      map((json) => DtoAuthResponse.fromJS(json)),
      catchError(toApiException),
    );
  }

  /** Renueva la sesión: canjea un refresh token por un nuevo par de tokens. */
  epRefreshToken(body: DtoRefreshRequest): Observable<DtoAuthResponse> {
    return this.http.post<unknown>(`${this.baseUrl}/api/auth/refresh`, body.toJSON()).pipe(
      map((json) => DtoAuthResponse.fromJS(json)),
      catchError(toApiException),
    );
  }

  /** Cierra la sesión revocando el refresh token. */
  epLogout(body: DtoRefreshRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/api/auth/logout`, body.toJSON()).pipe(catchError(toApiException));
  }
}

@Injectable({ providedIn: 'root' })
export class CtCommerceClient {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = (inject(API_BASE_URL, { optional: true }) ?? '').replace(/\/+$/, '');

  /** Carga el archivo commerce_DDMMYYYY.csv en la tabla commerce. */
  epUploadCommerce(file: FileParameter): Observable<DtoUploadCommerceResponse> {
    const form = new FormData();
    form.append('File', file.data, file.fileName);
    return this.http.post<unknown>(`${this.baseUrl}/api/commerce/upload`, form).pipe(
      map((json) => DtoUploadCommerceResponse.fromJS(json)),
      catchError(toApiException),
    );
  }

  /** Valida los registros de un día y envía los inválidos a commerce_quarantine. */
  epProcessCommerce(body: DtoProcessCommerceRequest): Observable<DtoProcessCommerceResponse> {
    return this.http.post<unknown>(`${this.baseUrl}/api/commerce/process`, body.toJSON()).pipe(
      map((json) => DtoProcessCommerceResponse.fromJS(json)),
      catchError(toApiException),
    );
  }

  /** Lista los comercios de commerce_quarantine con el motivo del rechazo. */
  epGetQuarantine(processDate?: string, pageNumber?: number, pageSize?: number): Observable<DtoQuarantinePage> {
    let params = new HttpParams();
    if (processDate) params = params.set('processDate', processDate);
    if (pageNumber !== undefined) params = params.set('pageNumber', pageNumber);
    if (pageSize !== undefined) params = params.set('pageSize', pageSize);

    return this.http.get<unknown>(`${this.baseUrl}/api/commerce/quarantine`, { params }).pipe(
      map((json) => DtoQuarantinePage.fromJS(json)),
      catchError(toApiException),
    );
  }
}

/* ------------------------------- DTOs ------------------------------- */

export interface IDtoLoginRequest { username: string; password: string; }
export class DtoLoginRequest implements IDtoLoginRequest {
  username!: string;
  password!: string;
  constructor(data?: IDtoLoginRequest) { if (data) Object.assign(this, data); }
  toJSON(): any { return { username: this.username, password: this.password }; }
}

export interface IDtoRefreshRequest { refreshToken: string; }
export class DtoRefreshRequest implements IDtoRefreshRequest {
  refreshToken!: string;
  constructor(data?: IDtoRefreshRequest) { if (data) Object.assign(this, data); }
  toJSON(): any { return { refreshToken: this.refreshToken }; }
}

export interface IDtoAuthResponse {
  accessToken?: string; refreshToken?: string; tokenType?: string; expiresAt?: string; username?: string; role?: string;
}
export class DtoAuthResponse implements IDtoAuthResponse {
  accessToken?: string;
  refreshToken?: string;
  tokenType?: string;
  /** Vencimiento del access token (UTC, ISO 8601). */
  expiresAt?: string;
  username?: string;
  role?: string;
  constructor(data?: IDtoAuthResponse) { if (data) Object.assign(this, data); }
  static fromJS(data: any): DtoAuthResponse { return new DtoAuthResponse(data); }
}

export interface IDtoProcessCommerceRequest { processDate: string; }
export class DtoProcessCommerceRequest implements IDtoProcessCommerceRequest {
  /** Formato yyyy-MM-dd. */
  processDate!: string;
  constructor(data?: IDtoProcessCommerceRequest) { if (data) Object.assign(this, data); }
  toJSON(): any { return { processDate: this.processDate }; }
}

export interface IDtoProcessCommerceResponse { processDate?: string; evaluated?: number; quarantined?: number; }
export class DtoProcessCommerceResponse implements IDtoProcessCommerceResponse {
  processDate?: string;
  evaluated?: number;
  quarantined?: number;
  constructor(data?: IDtoProcessCommerceResponse) { if (data) Object.assign(this, data); }
  static fromJS(data: any): DtoProcessCommerceResponse { return new DtoProcessCommerceResponse(data); }
}

export interface IDtoUploadCommerceResponse { fileName?: string; inserted?: number; batches?: number; processDates?: string[]; }
export class DtoUploadCommerceResponse implements IDtoUploadCommerceResponse {
  fileName?: string;
  inserted?: number;
  batches?: number;
  processDates?: string[];
  constructor(data?: IDtoUploadCommerceResponse) { if (data) Object.assign(this, data); }
  static fromJS(data: any): DtoUploadCommerceResponse { return new DtoUploadCommerceResponse(data); }
}

export interface IDtoQuarantineItem {
  id?: number; pcNomcomred?: string | undefined; pcNumdoc?: string | undefined;
  pcProcessdate?: string; motivo?: string; createdAt?: string;
}
export class DtoQuarantineItem implements IDtoQuarantineItem {
  id?: number;
  pcNomcomred?: string | undefined;
  pcNumdoc?: string | undefined;
  pcProcessdate?: string;
  motivo?: string;
  createdAt?: string;
  constructor(data?: IDtoQuarantineItem) { if (data) Object.assign(this, data); }
  static fromJS(data: any): DtoQuarantineItem { return new DtoQuarantineItem(data); }
}

export interface IDtoQuarantinePage { items?: IDtoQuarantineItem[]; totalCount?: number; pageNumber?: number; pageSize?: number; }
export class DtoQuarantinePage implements IDtoQuarantinePage {
  items?: DtoQuarantineItem[];
  totalCount?: number;
  pageNumber?: number;
  pageSize?: number;
  constructor(data?: IDtoQuarantinePage) { if (data) Object.assign(this, data); }
  static fromJS(data: any): DtoQuarantinePage {
    const page = new DtoQuarantinePage(data);
    page.items = Array.isArray(data?.items) ? data.items.map((item: any) => DtoQuarantineItem.fromJS(item)) : [];
    return page;
  }
}

export interface FileParameter { data: any; fileName: string; }

/* ------------------------------ Errores ------------------------------ */

export class ApiException extends Error {
  override message: string;
  status: number;
  response: string;
  headers: { [key: string]: any };
  result: any;

  constructor(message: string, status: number, response: string, headers: { [key: string]: any }, result: any) {
    super();
    this.message = message;
    this.status = status;
    this.response = response;
    this.headers = headers;
    this.result = result;
  }

  protected isApiException = true;
  static isApiException(obj: any): obj is ApiException { return obj.isApiException === true; }
}

function toApiException(error: unknown): Observable<never> {
  if (error instanceof HttpErrorResponse) {
    const body = typeof error.error === 'string' ? error.error : error.error ? JSON.stringify(error.error) : '';
    return throwError(() => new ApiException('A server side error occurred.', error.status, body, {}, null));
  }
  return throwError(() => error);
}
