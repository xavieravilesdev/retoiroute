/** Columnas que debe traer el CSV (en cualquier orden). */
export const REQUIRED_COLUMNS = ['pc_nomcomred', 'pc_numdoc', 'pc_processdate'] as const;

export interface CsvPreview {
  headers: string[];
  /** Primeras filas de datos (máximo `maxRows`). */
  rows: string[][];
  /** Total de filas de datos del archivo (sin el encabezado). */
  totalRows: number;
  /** Columnas requeridas que no aparecen en el encabezado. */
  missingColumns: string[];
}

const FILE_NAME_PATTERN = /^commerce_(\d{2})(\d{2})(\d{4})\.csv$/i;
const PREVIEW_BYTES = 128 * 1024;

/** Valida el nombre commerce_DDMMYYYY.csv, incluida una fecha real. */
export function isValidCommerceFileName(name: string): boolean {
  const match = FILE_NAME_PATTERN.exec(name);
  if (!match) return false;

  const day = Number(match[1]);
  const month = Number(match[2]);
  const year = Number(match[3]);
  const date = new Date(year, month - 1, day);
  return date.getFullYear() === year && date.getMonth() === month - 1 && date.getDate() === day;
}

/**
 * Genera la previsualización sin cargar todo el archivo en memoria:
 * lee solo los primeros KB para las filas y cuenta las líneas recorriendo el archivo en streaming.
 */
export async function readCsvPreview(file: File, delimiter = ',', maxRows = 50): Promise<CsvPreview> {
  const [head, totalLines] = await Promise.all([file.slice(0, PREVIEW_BYTES).text(), countLines(file)]);

  let text = head.replace(/^\uFEFF/, '');
  if (file.size > PREVIEW_BYTES) {
    // La última línea del bloque puede estar cortada: se descarta.
    text = text.slice(0, Math.max(text.lastIndexOf('\n'), 0));
  }

  const lines = text.split(/\r?\n/).filter((line) => line.trim() !== '');
  const headers = (lines[0] ? splitLine(lines[0], delimiter) : []).map((h) => h.trim());
  const normalized = headers.map((h) => h.toLowerCase());

  return {
    headers,
    rows: lines.slice(1, 1 + maxRows).map((line) => splitLine(line, delimiter)),
    totalRows: Math.max(totalLines - 1, 0),
    missingColumns: REQUIRED_COLUMNS.filter((column) => !normalized.includes(column)),
  };
}

/** Cuenta líneas no vacías recorriendo el archivo por bloques. */
async function countLines(file: File): Promise<number> {
  const reader = file.stream().pipeThrough(new TextDecoderStream()).getReader();
  let lines = 0;
  let lineHasContent = false;

  for (;;) {
    const { done, value } = await reader.read();
    if (done) break;

    for (let i = 0; i < value.length; i++) {
      const char = value.charCodeAt(i);
      if (char === 10) {
        if (lineHasContent) lines++;
        lineHasContent = false;
      } else if (char !== 13 && char !== 32 && char !== 0xfeff) {
        lineHasContent = true;
      }
    }
  }

  return lineHasContent ? lines + 1 : lines;
}

/** Divide una línea CSV respetando campos entre comillas. */
function splitLine(line: string, delimiter: string): string[] {
  const fields: string[] = [];
  let current = '';
  let inQuotes = false;

  for (let i = 0; i < line.length; i++) {
    const char = line[i];
    if (inQuotes) {
      if (char === '"' && line[i + 1] === '"') {
        current += '"';
        i++;
      } else if (char === '"') {
        inQuotes = false;
      } else {
        current += char;
      }
    } else if (char === '"') {
      inQuotes = true;
    } else if (char === delimiter) {
      fields.push(current);
      current = '';
    } else {
      current += char;
    }
  }

  fields.push(current);
  return fields;
}
