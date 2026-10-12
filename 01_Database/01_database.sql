/* =====================================================================
   Reto individual i-Route - Base de datos SQL Server
   ===================================================================== */

IF DB_ID(N'CommerceDb') IS NULL
    CREATE DATABASE CommerceDb;
GO

USE CommerceDb;
GO

/* ---------------------------------------------------------------------
   1. Tabla commerce (misma estructura que el CSV + id técnico)
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.commerce', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.commerce
    (
        id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_commerce PRIMARY KEY,
        pc_nomcomred   NVARCHAR(200) NULL,   -- se permite vacío: lo detecta la validación
        pc_numdoc      NVARCHAR(50)  NULL,   -- texto: debe poder traer letras para detectar el error
        pc_processdate DATE          NOT NULL
    );

    -- El proceso y las consultas filtran por día
    CREATE INDEX IX_commerce_processdate ON dbo.commerce (pc_processdate);
END
GO

/* ---------------------------------------------------------------------
   2. Tabla commerce_quarantine
      Se crea con la estructura base y luego se agrega "motivo",
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.commerce_quarantine', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.commerce_quarantine
    (
        id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_commerce_quarantine PRIMARY KEY,
        pc_nomcomred   NVARCHAR(200) NULL,
        pc_numdoc      NVARCHAR(50)  NULL,
        pc_processdate DATE          NOT NULL,
        created_at     DATETIME2(0)  NOT NULL CONSTRAINT DF_commerce_quarantine_created DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_commerce_quarantine_processdate ON dbo.commerce_quarantine (pc_processdate);
END
GO

IF COL_LENGTH(N'dbo.commerce_quarantine', N'motivo') IS NULL
    ALTER TABLE dbo.commerce_quarantine
        ADD motivo NVARCHAR(500) NOT NULL
            CONSTRAINT DF_commerce_quarantine_motivo DEFAULT N'';
GO

/* ---------------------------------------------------------------------
   3. Tipo de tabla (TVP): permite enviar el CSV completo (por lotes)
   --------------------------------------------------------------------- */
IF TYPE_ID(N'dbo.commerce_type') IS NULL
    CREATE TYPE dbo.commerce_type AS TABLE
    (
        pc_nomcomred   NVARCHAR(200) NULL,
        pc_numdoc      NVARCHAR(50)  NULL,
        pc_processdate DATE          NOT NULL
    );
GO

/* ---------------------------------------------------------------------
   4. sp_create_commerce
      Inserta un lote de registros del CSV en commerce.
      @inserted devuelve la cantidad de filas insertadas.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_create_commerce
    @items    dbo.commerce_type READONLY,
    @inserted INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    INSERT INTO dbo.commerce (pc_nomcomred, pc_numdoc, pc_processdate)
    SELECT pc_nomcomred, pc_numdoc, pc_processdate
    FROM @items;

    SET @inserted = @@ROWCOUNT;
END
GO

/* ---------------------------------------------------------------------
   5. sp_process_commerce
      Valida los registros de un día (pc_processdate):
        - pc_nomcomred no vacío
        - pc_numdoc no vacío, sin letras ni caracteres especiales
      Los inválidos se mueven a commerce_quarantine con su motivo
      (varios motivos se concatenan con "; ") y se eliminan de commerce.
        @evaluated   = registros del día evaluados
        @quarantined = registros enviados a commerce_quarantine
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_process_commerce
    @process_date DATE,
    @evaluated    INT OUTPUT,
    @quarantined  INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @evaluated = COUNT(*)
        FROM dbo.commerce
        WHERE pc_processdate = @process_date;

        CREATE TABLE #invalid
        (
            id     BIGINT         NOT NULL PRIMARY KEY,
            motivo NVARCHAR(500)  NOT NULL
        );

        INSERT INTO #invalid (id, motivo)
        SELECT c.id, STUFF(r.motivo, 1, 2, N'')
        FROM dbo.commerce AS c
        CROSS APPLY (SELECT nom = LTRIM(RTRIM(ISNULL(c.pc_nomcomred, N''))),
                            doc = LTRIM(RTRIM(ISNULL(c.pc_numdoc,    N'')))) AS t
        CROSS APPLY (SELECT motivo = CONCAT(
                        CASE WHEN t.nom = N''
                             THEN N'; El nombre del comercio (nomcomred) se encuentra vacio' END,
                        CASE WHEN t.doc = N''
                             THEN N'; El número (numdoc) se encuentra vacio' END,
                        CASE WHEN t.doc <> N''
                              AND t.doc COLLATE Latin1_General_BIN LIKE N'%[A-Za-z]%'
                             THEN N'; El número (numdoc) contiene letras' END,
                        CASE WHEN t.doc <> N''
                              AND t.doc COLLATE Latin1_General_BIN LIKE N'%[^0-9A-Za-z]%'
                             THEN N'; El número (numdoc) contiene caracteres especiales' END
                     )) AS r
        WHERE c.pc_processdate = @process_date
          AND r.motivo <> N'';

        INSERT INTO dbo.commerce_quarantine (pc_nomcomred, pc_numdoc, pc_processdate, motivo)
        SELECT c.pc_nomcomred, c.pc_numdoc, c.pc_processdate, i.motivo
        FROM dbo.commerce AS c
        INNER JOIN #invalid AS i ON i.id = c.id;

        SET @quarantined = @@ROWCOUNT;

        DELETE c
        FROM dbo.commerce AS c
        INNER JOIN #invalid AS i ON i.id = c.id;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

/* ---------------------------------------------------------------------
   6. sp_get_commerce_quarantine
      Lista paginada de la cuarentena, opcionalmente filtrada por día.
      total_count permite paginar en el front con una sola consulta.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_get_commerce_quarantine
    @process_date DATE = NULL,
    @page_number  INT  = 1,
    @page_size    INT  = 50
AS
BEGIN
    SET NOCOUNT ON;

    IF @page_number < 1 SET @page_number = 1;
    IF @page_size   < 1 SET @page_size   = 50;
    IF @page_size > 1000 SET @page_size  = 1000;

    SELECT id,
           pc_nomcomred,
           pc_numdoc,
           pc_processdate,
           motivo,
           created_at,
           total_count = COUNT(*) OVER ()
    FROM dbo.commerce_quarantine
    WHERE (@process_date IS NULL OR pc_processdate = @process_date)
    ORDER BY id DESC
    OFFSET (@page_number - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY
    OPTION (RECOMPILE);
END
GO

/* ---------------------------------------------------------------------
   7. Autenticación (JWT)
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.app_user', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.app_user
    (
        id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_app_user PRIMARY KEY,
        username      NVARCHAR(100)     NOT NULL CONSTRAINT UQ_app_user_username UNIQUE,
        password_hash NVARCHAR(500)     NOT NULL,
        role          NVARCHAR(50)      NOT NULL CONSTRAINT DF_app_user_role DEFAULT N'user',
        is_active     BIT               NOT NULL CONSTRAINT DF_app_user_active DEFAULT 1,
        created_at    DATETIME2(0)      NOT NULL CONSTRAINT DF_app_user_created DEFAULT SYSUTCDATETIME()
    );
END
GO

IF OBJECT_ID(N'dbo.refresh_token', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.refresh_token
    (
        id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_refresh_token PRIMARY KEY,
        user_id     INT           NOT NULL
                    CONSTRAINT FK_refresh_token_user REFERENCES dbo.app_user (id),
        token_hash  NVARCHAR(128) NOT NULL CONSTRAINT UQ_refresh_token_hash UNIQUE,
        expires_at  DATETIME2(0)  NOT NULL,
        created_at  DATETIME2(0)  NOT NULL CONSTRAINT DF_refresh_token_created DEFAULT SYSUTCDATETIME(),
        revoked_at  DATETIME2(0)  NULL
    );

    CREATE INDEX IX_refresh_token_user ON dbo.refresh_token (user_id);
END
GO

/* ---------------------------------------------------------------------
   8. Prueba rápida (opcional): ejecutar manualmente
   ---------------------------------------------------------------------
DECLARE @items dbo.commerce_type, @ins INT, @eval INT, @quar INT;
INSERT INTO @items VALUES
    (N'Tienda Uno', N'0912345678',  '2026-10-06'),   -- válido
    (N'',           N'0912345678',  '2026-10-06'),   -- nombre vacío
    (N'Tienda Dos', N'09A234567',   '2026-10-06'),   -- letras
    (N'Tienda Tres',N'091-234567',  '2026-10-06'),   -- caracteres especiales
    (N'Tienda Cuatro', N'',         '2026-10-06');   -- documento vacío
EXEC dbo.sp_create_commerce @items, @ins OUTPUT;
EXEC dbo.sp_process_commerce '2026-10-06', @eval OUTPUT, @quar OUTPUT;
SELECT inserted = @ins, evaluated = @eval, quarantined = @quar;   -- esperado: 5, 5, 4
EXEC dbo.sp_get_commerce_quarantine '2026-10-06';
*/
