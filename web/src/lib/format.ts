export const ZONA_DEFAULT = 'America/Mexico_City'
const LOCALE = 'es-MX'

const percentFmt = new Intl.NumberFormat(LOCALE, {
  style: 'percent',
  minimumFractionDigits: 1,
  maximumFractionDigits: 1,
})
const intFmt = new Intl.NumberFormat(LOCALE, { maximumFractionDigits: 0 })
const mxnFmt = new Intl.NumberFormat(LOCALE, { style: 'currency', currency: 'MXN' })

/** El API manda fracciones 0–1 (0.79 = 79 %). */
export function formatPercent(fraccion: number | null | undefined): string {
  if (fraccion === null || fraccion === undefined || Number.isNaN(fraccion)) return '—'
  return percentFmt.format(fraccion)
}

export function formatInt(n: number | null | undefined): string {
  if (n === null || n === undefined) return '—'
  return intFmt.format(n)
}

export function formatMxn(n: number | null | undefined): string {
  if (n === null || n === undefined) return '—'
  return mxnFmt.format(n)
}

export function formatMinutes(min: number | null | undefined): string {
  if (min === null || min === undefined) return '—'
  return `${intFmt.format(min)} min`
}

export function formatDateTime(isoUtc: string | null | undefined, timeZone = ZONA_DEFAULT): string {
  if (!isoUtc) return '—'
  return new Intl.DateTimeFormat(LOCALE, {
    timeZone,
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(isoUtc))
}

export function formatTime(isoUtc: string | null | undefined, timeZone = ZONA_DEFAULT): string {
  if (!isoUtc) return '—'
  return new Intl.DateTimeFormat(LOCALE, { timeZone, hour: '2-digit', minute: '2-digit' }).format(
    new Date(isoUtc),
  )
}

/** "2026-11-04" → "mié 04/11/2026". Es una fecha calendario, no un instante: no se convierte de zona. */
export function formatFechaOperativa(fecha: string): string {
  const [y, m, d] = fecha.split('-').map(Number)
  if (!y || !m || !d) return fecha
  return new Intl.DateTimeFormat(LOCALE, {
    timeZone: 'UTC',
    weekday: 'short',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(new Date(Date.UTC(y, m - 1, d)))
}
