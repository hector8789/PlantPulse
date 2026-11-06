/**
 * Exportación a Excel del lado cliente (SheetJS), con los datos que la página ya tiene.
 * La librería se carga bajo demanda para no inflar el bundle inicial.
 */

export type FormatoColumna = 'texto' | 'entero' | 'decimal' | 'porcentaje' | 'moneda'

export interface ColumnaExcel<T> {
  header: string
  valor: (fila: T) => string | number | boolean | null | undefined
  formato?: FormatoColumna
  /** Ancho en caracteres */
  ancho?: number
}

type Celda = string | number | boolean | null

/** Hoja ya calculada: encabezados + filas, lista para escribirse. */
export interface HojaExcel {
  nombre: string
  columnas: Omit<ColumnaExcel<never>, 'valor'>[]
  matriz: Celda[][]
}

/** Excel limita los nombres de hoja a 31 caracteres y prohíbe : \ / ? * [ ] */
export function nombreHojaValido(nombre: string): string {
  return nombre.replace(/[:\\/?*[\]]/g, ' ').slice(0, 31) || 'Hoja'
}

/** Crea una hoja a partir de filas tipadas: la matriz es [encabezados, ...filas]. */
export function hoja<T>(nombre: string, columnas: ColumnaExcel<T>[], filas: readonly T[]): HojaExcel {
  return {
    nombre,
    columnas: columnas.map(({ header, formato, ancho }) => ({ header, formato, ancho })),
    matriz: [columnas.map((c) => c.header), ...filas.map((f) => columnas.map((c) => c.valor(f) ?? null))],
  }
}

const FORMATOS: Record<Exclude<FormatoColumna, 'texto'>, string> = {
  entero: '#,##0',
  decimal: '#,##0.00',
  porcentaje: '0.0%',
  moneda: '"$"#,##0.00',
}

export async function exportarExcel(nombreArchivo: string, hojas: HojaExcel[]): Promise<void> {
  const XLSX = await import('xlsx')
  const libro = XLSX.utils.book_new()
  for (const h of hojas) {
    const ws = XLSX.utils.aoa_to_sheet(h.matriz)
    h.columnas.forEach((col, c) => {
      if (!col.formato || col.formato === 'texto') return
      for (let r = 1; r < h.matriz.length; r++) {
        const celda = ws[XLSX.utils.encode_cell({ r, c })]
        if (celda && celda.t === 'n') celda.z = FORMATOS[col.formato]
      }
    })
    ws['!cols'] = h.columnas.map((col) => ({ wch: col.ancho ?? Math.max(col.header.length + 2, 12) }))
    XLSX.utils.book_append_sheet(libro, ws, nombreHojaValido(h.nombre))
  }
  XLSX.writeFile(libro, nombreArchivo.endsWith('.xlsx') ? nombreArchivo : `${nombreArchivo}.xlsx`, {
    compression: true,
  })
}
