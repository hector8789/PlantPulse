/**
 * Utilidades de fecha sin dependencias. Las fechas operativas son "YYYY-MM-DD"
 * y los instantes viajan en ISO 8601 UTC.
 */

/** Fecha calendario (YYYY-MM-DD) de un instante en una zona IANA. */
export function fechaEnZona(instante: Date, timeZone: string): string {
  // en-CA formatea como YYYY-MM-DD
  return new Intl.DateTimeFormat('en-CA', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(instante)
}

export function hoyEnZona(timeZone: string, ahora = new Date()): string {
  return fechaEnZona(ahora, timeZone)
}

export function sumarDias(fecha: string, dias: number): string {
  const [y, m, d] = fecha.split('-').map(Number)
  const dt = new Date(Date.UTC(y, m - 1, d + dias))
  return dt.toISOString().slice(0, 10)
}

/** Días entre dos fechas YYYY-MM-DD (hasta - desde). */
export function diasEntre(desde: string, hasta: string): number {
  const a = Date.parse(`${desde}T00:00:00Z`)
  const b = Date.parse(`${hasta}T00:00:00Z`)
  return Math.round((b - a) / 86_400_000)
}

function partesEnZona(instante: Date, timeZone: string) {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone,
    hourCycle: 'h23',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  }).formatToParts(instante)
  const get = (type: Intl.DateTimeFormatPartTypes) =>
    Number(parts.find((p) => p.type === type)?.value ?? 0)
  return {
    year: get('year'),
    month: get('month'),
    day: get('day'),
    hour: get('hour'),
    minute: get('minute'),
    second: get('second'),
  }
}

/** Diferencia (ms) entre la hora de pared de la zona y UTC en ese instante. */
function offsetMs(instante: Date, timeZone: string): number {
  const p = partesEnZona(instante, timeZone)
  const comoUtc = Date.UTC(p.year, p.month - 1, p.day, p.hour, p.minute, p.second)
  return comoUtc - Math.floor(instante.getTime() / 1000) * 1000
}

/**
 * Convierte la hora de pared de un <input type="datetime-local"> ("YYYY-MM-DDTHH:mm")
 * en la zona de la planta a ISO 8601 UTC.
 */
export function localAUtcIso(valorLocal: string, timeZone: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(valorLocal)
  if (!match) throw new Error(`Fecha local inválida: ${valorLocal}`)
  const [, y, mo, d, h, mi] = match.map(Number)
  const comoUtc = Date.UTC(y, mo - 1, d, h, mi)
  // Dos pasadas para cubrir cambios de horario.
  let utc = comoUtc - offsetMs(new Date(comoUtc), timeZone)
  utc = comoUtc - offsetMs(new Date(utc), timeZone)
  return new Date(utc).toISOString()
}

/** ISO UTC → valor para <input type="datetime-local"> en la zona de la planta. */
export function utcIsoALocal(isoUtc: string, timeZone: string): string {
  const p = partesEnZona(new Date(isoUtc), timeZone)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${p.year}-${pad(p.month)}-${pad(p.day)}T${pad(p.hour)}:${pad(p.minute)}`
}
