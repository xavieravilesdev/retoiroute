# Reto individual i-Route — Carga y validación de comercios

Aplicación web para cargar el archivo `commerce_DDMMYYYY.csv`, procesar los registros de un día
(`pc_processdate`), mover a cuarentena los inválidos con su motivo y consultarlos. Incluye inicio de
sesión con JWT y refresh token.

| Capa | Tecnología | Carpeta sugerida |

| Base de datos | SQL Server | `database/01_database.sql` |
| Backend | C# / ASP.NET Core (.NET 10) | `backend/Commerce.Api` |
| Frontend | Angular 22 | `frontend/commerce-web` |

## Orden de puesta en marcha
1. Ejecutar el script SQL (sección 1).
2. Configurar y levantar el backend (sección 2).
3. Crear/levantar el frontend (sección 3).
4. Probar con el CSV de ejemplo (sección 4).

## Flujo funcional
1. Login: Access token (JWT, 15 min) + refresh token (7 días, rotativo).
2. Cargar: Elegir el CSV, se previsualiza en el navegador y se envía. El backend lo lee en streaming y lo inserta por lotes con `sp_create_commerce`.
3. Procesar: Elegir un día; `sp_process_commerce` valida, mueve los inválidos a `commerce_quarantine` y devuelve la cantidad.
4. Errores: Lista paginada de `commerce_quarantine` con el `motivo`.

### Reglas de validación (por día procesado)
| Regla | Motivo guardado |

| `pc_nomcomred` vacío | El nombre del comercio (nomcomred) se encuentra vacio |
| `pc_numdoc` vacío | El número (numdoc) se encuentra vacio |
| `pc_numdoc` con letras | El número (numdoc) contiene letras |
| `pc_numdoc` con caracteres especiales | El número (numdoc) contiene caracteres especiales |

Un registro con varios problemas guarda todos los motivos separados por `; `.

---

## 1. SQL Server

### Script
`01_database.sql` — un único archivo, se ejecuta completo.

### Qué realiza
1. Crea la base de datos `CommerceDb` si no existe.
2. Tabla `commerce`: `pc_nomcomred`, `pc_numdoc` (texto, para poder detectar letras) y `pc_processdate` (`DATE`), más un `id` técnico. Índice por `pc_processdate`.
3. Tabla `commerce_quarantine`: se crea con las columnas base y después se agrega la columna `motivo` con `ALTER TABLE`, como pide el reto. Incluye `id` y `created_at` (UTC).
4. Tipo de tabla `dbo.commerce_type` (TVP) para enviar el CSV por lotes.
5. Stored procedures:
   - `sp_create_commerce` — inserta un lote del CSV en `commerce` (devuelve filas insertadas).
   - `sp_process_commerce` — valida el día, mueve los inválidos a cuarentena con su motivo, los elimina de `commerce` y devuelve registros evaluados y enviados a cuarentena. Todo en una transacción.
   - `sp_get_commerce_quarantine` — listado paginado con filtro opcional por día.
6. Tablas de autenticación: `app_user` (solo hash de contraseña) y `refresh_token` (solo hash del token).

### Idempotencia
El script puede ejecutarse varias veces sin error y sin borrar datos:
- Tablas, índices y tipo: `IF OBJECT_ID(...) IS NULL` / `IF TYPE_ID(...) IS NULL`.
- Columna `motivo`: `IF COL_LENGTH(...) IS NULL`.
- Procedimientos: `CREATE OR ALTER PROCEDURE` (se actualizan al volver a ejecutar).
- Limitación: si ya existe un objeto con el mismo nombre pero otra definición (tabla o tipo), no lo modifica. Para empezar de cero, eliminar la base `CommerceDb` y volver a ejecutar.

### Versiones de SQL Server
- Mínima: SQL Server 2016 SP1 (13.0.4001), por `CREATE OR ALTER PROCEDURE`. El resto de funciones usadas (TVP, `THROW`, `OFFSET/FETCH`, `CONCAT`, `COUNT() OVER`, tipo `DATE`) existen desde versiones anteriores.
- Máxima: sin límite conocido. No usa características obsoletas; es compatible con SQL Server 2017, 2019, 2022 y 2025, y con Azure SQL Database/Managed Instance.
- Versión con la que se probó: `<completar: ejecutar SELECT @@VERSION>`.

### Ejecución
- SSMS: abrir `01_database.sql` y ejecutar (F5).

---

## 2. Backend — C# / ASP.NET Core (.NET 10)

### Versiones
| Componente | Versión |

| .NET / framework | .NET 10 (`net10.0`), SDK 10.x |
| IDE | Visual Studio `<completar versión exacta: Ayuda > Acerca de Microsoft Visual Studio>` |
| Paquetes principales | EF Core SqlServer 10.x, Microsoft.Data.SqlClient 6.x, JwtBearer 10.x, Swashbuckle.AspNetCore 9.x, CsvHelper 33.x |

### Arquitectura
Una API con carpetas por responsabilidad:
`Controllers` (`Ct…`), `Dtos`, `Business` (lectura del CSV, servicio, repositorio de stored procedures), `Security` (JWT, hash de contraseñas, seeder), `Data` (EF Core, solo autenticación), `Options`, `Extensions`.

Convención de nombres: controladores `Ct`, endpoints `Ep`, métodos privados `Me`, DTOs `Dto`, clases públicas de negocio `bc`, privadas/internas `pv`, estáticas `st`.

Puntos técnicos:
- Carga eficiente: el CSV se lee en streaming (nunca completo en memoria) y un `Channel` acotado conecta el lector con el escritor, que inserta lotes de 5 000 filas con un parámetro de tabla en una sola transacción (atómica: si una fila falla no se inserta nada). Todo con `CancellationToken`.
- Seguridad: contraseñas con PBKDF2-SHA256 (600 000 iteraciones); refresh tokens guardados solo como hash, con rotación y detección de reutilización; límite de 5 intentos por minuto en login/refresh; CORS restringido; cabeceras de seguridad; errores 500 sin detalles internos; validación del nombre y tamaño del archivo (máx. 20 MB).

### Endpoints
| Método | Ruta | Acción | Auth |

| POST | `/api/auth/login` | `EpLogin` | No |
| POST | `/api/auth/refresh` | `EpRefreshToken` | No |
| POST | `/api/auth/logout` | `EpLogout` | Sí |
| POST | `/api/commerce/upload` | `EpUploadCommerce` | Sí |
| POST | `/api/commerce/process` | `EpProcessCommerce` | Sí |
| GET | `/api/commerce/quarantine` | `EpGetQuarantine` | Sí |

Códigos relevantes: `upload` devuelve 400 si el archivo está vacío, el nombre no cumple `commerce_DDMMYYYY.csv`, faltan columnas o hay una fecha inválida; `process` devuelve 404 si no hay registros de ese día.

### Configuración (`appsettings.json`)

Cadena de conexión a la base — propiedad `ConnectionStrings:CommerceDb`:
```json
"ConnectionStrings": {
  "CommerceDb": "Server=__EQUIPO__\\__INSTANCIA__;Database=CommerceDb;Trusted_Connection=True;TrustServerCertificate=True;"
}
```
- Cambiar `Server=` por la instancia propia. Con autenticación SQL: `Server=...;Database=CommerceDb;User Id=...;Password=...;TrustServerCertificate=True;`
- Sin editar archivos: `dotnet user-secrets set "ConnectionStrings:CommerceDb" "<cadena>"` o la variable de entorno `ConnectionStrings__CommerceDb`.

Clave (seed) del token JWT — propiedad `Jwt:Key`:
- Debe tener mínimo 32 caracteres; si no, la API no arranca.
- En desarrollo ya viene una clave en `appsettings.Development.json` (solo para pruebas).

Usuario inicial — sección `Seed`:
- Al arrancar, si no existe ningún usuario y `Seed:AdminPassword` no está vacío, se crea `Seed:AdminUsername` con rol `admin`.
- Test: `admin` / `Admin#2026!` (definido en `appsettings.Development.json`).


Archivo CSV — sección `Csv`: `Delimiter` (`,`), `DateFormat` (`dd/MM/yyyy`), `BatchSize` (5000), `ChannelCapacity` (4).

CORS — `Cors:Origins`: lista de orígenes del frontend (por defecto `http://localhost:4200`).

### Ejecución en desarrollo
```
cd backend/Commerce.Api
dotnet run
```
Swagger: http://localhost:5000/swagger (solo en Development). En Swagger: login → copiar `accessToken` → botón Authorize (sin la palabra Bearer).

---

## 3. Frontend — Angular 22

### Versiones
| Componente | Versión |
|---|---|
| Node.js | 24 LTS (24.15.0 o superior) |
| Angular / Angular CLI | 22 (`@angular/cli@22`) |
| TypeScript | 6.0 (el que instala Angular 22) |
| Generador del cliente | NSwag (`nswag` por npm, requiere runtime .NET instalado) |

Angular 22 acepta Node 22.22.3+, 24.15.0+ o 26+. No funciona con versiones anteriores.

### Estructura
```
src/app/
├── core/        auth-session, auth-interceptor, auth-guard, api-error, loaded-dates
├── shared/      api/commerce-client.g.ts (cliente NSwag), csv-preview, format-date-only
└── features/    login, upload, process, errors   (carga diferida por ruta)
```
Componentes standalone con `OnPush`, estado con signals, formulario de login con Signal Forms, rutas con `authGuard`.

### Funcionalidades
- Login: JWT. El access token vive solo en memoria; el refresh token en `sessionStorage`. El interceptor agrega el token, renueva la sesión una sola vez ante un 401 (llamadas simultáneas comparten la misma renovación) y redirige al login si no puede. Al recargar la página la sesión se recupera sola.
- Cargar: valida nombre `commerce_DDMMYYYY.csv`, archivo no vacío y columnas requeridas; previsualiza los primeros 50 registros y el total (lee solo el inicio del archivo) antes de enviarlo; al terminar ofrece las fechas encontradas.
- Procesar: elegir el día y ver registros evaluados / enviados a cuarentena.
- Errores: tabla paginada con motivo, filtro por fecha.


### Ejecución en desarrollo

cd commerce-web
npm i        
ng serve


http://localhost:4200 — usuario `admin` / `Admin#2026!` (test).

### Regenerar el cliente de la API (NSwag)
Con el backend corriendo: `npm run api:generate`. Reemplaza `src/app/shared/api/commerce-client.g.ts` a partir de `http://localhost:5000/swagger/v1/swagger.json`.
- Cada operación se llama igual que la acción del backend (`EpLogin` → `epLogin()`), un cliente por controlador (`CtAuthClient`, `CtCommerceClient`).
- Las fechas se generan como `string` (`dateTimeType: String`) para evitar desfases de zona horaria con `DateOnly`.
- Si NSwag no encuentra el runtime `Net100`, cambiar `"runtime"` en `nswag.json` por el instalado.

---

## Test .csv

Crear `commerce_06102026.csv`:
```csv
pc_nomcomred,pc_numdoc,pc_processdate
Tienda Uno,0912345678,06/10/2026
,0912345678,06/10/2026
Tienda Dos,09A234567,06/10/2026
Tienda Tres,091-234567,06/10/2026
Tienda Cuatro,,06/10/2026
```
1. Iniciar sesión → Cargar → seleccionar el archivo → revisar la previsualización → Enviar. Resultado: 5 registros insertados.
2. Procesar el día 06/10/2026. Resultado: 5 evaluados, 4 en cuarentena.
3. Errores: aparecen 4 registros con los motivos: nombre vacío, número con letras, número con caracteres especiales y número vacío.

Casos que el backend rechaza con 400: archivo vacío, nombre distinto de `commerce_DDMMYYYY.csv`, encabezado sin las 3 columnas, fecha con formato distinto a `dd/MM/yyyy`.

## 5. Problemas frecuentes
| Síntoma | Causa y solución |
|---|---|
| La API no arranca: "Jwt:Key debe tener al menos 32 bytes" | Definir `Jwt:Key` (user-secrets o variable de entorno). |
| Log: "No se pudo crear el usuario inicial" | La base no existe o no hay conexión: ejecutar `01_database.sql` y revisar la cadena de conexión. |
| Login siempre da 401 | No hay usuario: revisar `Seed:AdminPassword` y que la tabla `app_user` esté vacía en el primer arranque. |
| Error de CORS en el navegador | El origen del frontend no está en `Cors:Origins`. |
| 429 en login | Más de 5 intentos por minuto desde la misma IP: esperar un minuto. |
| Error de versión al ejecutar `ng new` | Node inferior a 24.15.0 (o 22.22.3): actualizar Node. |
| NSwag: runtime no disponible | Ajustar `"runtime"` en `nswag.json` al runtime .NET instalado. |
