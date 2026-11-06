import axios from 'axios'

/** ProblemDetails (RFC 9457) con las extensiones del contrato: codigo (409), errors (422), servicio (503). */
export interface ApiProblem {
  status: number
  title: string
  detail?: string
  codigo?: string
  servicio?: string
  /** Nombres de campo ya normalizados a camelCase. */
  errors?: Record<string, string[]>
  /** true cuando no hubo respuesta (red caída, CORS, certificado). */
  isNetworkError: boolean
}

function isRecord(v: unknown): v is Record<string, unknown> {
  return typeof v === 'object' && v !== null && !Array.isArray(v)
}

/**
 * "PiezasBuenas" → "piezasBuenas"; "Registros[0].PiezasBuenas" → "registros.0.piezasBuenas"
 * (formato de rutas de React Hook Form).
 */
export function normalizeFieldName(campo: string): string {
  return campo
    .replace(/\[(\d+)\]/g, '.$1')
    .replace(/^\$\./, '')
    .split('.')
    .filter(Boolean)
    .map((seg) => (seg.length > 0 ? seg[0].toLowerCase() + seg.slice(1) : seg))
    .join('.')
}

function normalizeErrors(raw: unknown): Record<string, string[]> | undefined {
  if (!isRecord(raw)) return undefined
  const out: Record<string, string[]> = {}
  for (const [campo, mensajes] of Object.entries(raw)) {
    const lista = Array.isArray(mensajes)
      ? mensajes.filter((m): m is string => typeof m === 'string')
      : typeof mensajes === 'string'
        ? [mensajes]
        : []
    const key = normalizeFieldName(campo)
    out[key] = [...(out[key] ?? []), ...lista]
  }
  return out
}

export function getApiProblem(error: unknown): ApiProblem | null {
  if (!axios.isAxiosError(error)) return null
  if (!error.response) {
    return {
      status: 0,
      title: 'No se pudo conectar con el servidor',
      detail: 'Revisa que la API esté encendida y que el certificado HTTPS de desarrollo sea de confianza.',
      isNetworkError: true,
    }
  }
  const { status, data } = error.response
  const body = isRecord(data) ? data : {}
  const str = (k: string) => (typeof body[k] === 'string' ? (body[k] as string) : undefined)
  return {
    status,
    title: str('title') ?? `Error ${status}`,
    detail: str('detail'),
    codigo: str('codigo'),
    servicio: str('servicio'),
    errors: normalizeErrors(body.errors),
    isNetworkError: false,
  }
}

/** Mensajes en español para los códigos 409 estables del contrato. */
export const CONFLICT_MESSAGES: Record<string, string> = {
  'turno_linea.ya_existe': 'Ya existe un turno abierto para esa línea, fecha y turno.',
  'turno_linea.fecha_futura': 'No se puede capturar en una fecha futura.',
  'turno_linea.fuera_de_ventana_captura': 'Solo se puede capturar hasta 7 días atrás.',
  'turno_linea.turno_no_iniciado': 'El turno todavía no inicia; podrás abrirlo a partir de su hora de inicio.',
  'turno_linea.cerrado': 'El turno está cerrado y no admite cambios. Solo un Admin puede reabrirlo.',
  'turno_linea.turno_no_terminado': 'El turno aún no termina; podrás cerrarlo después de su hora de fin.',
  'turno_linea.paro_fuera_de_turno':
    'El paro sale de la ventana del turno. Si cruza de turno, captúralo como dos paros.',
  'turno_linea.paro_traslapado': 'El paro se traslapa con otro paro ya capturado.',
  'turno_linea.paro_futuro': 'El paro no puede terminar en el futuro.',
  'turno_linea.orden_ya_registrada':
    'Esa orden ya tiene captura en este turno. Usa "Corregir" en la tabla de producción.',
  'turno_linea.orden_de_otra_linea': 'La orden pertenece a otra línea.',
  'turno_linea.orden_no_acepta_produccion': 'La orden está terminada y no acepta producción.',
  'turno_linea.causa_inactiva': 'La causa de paro está inactiva.',
  'turno_linea.no_cerrado': 'El turno no está cerrado, así que no se puede reabrir.',
  'turno_linea.paro_invalido': 'El paro no es válido: revisa la causa y las horas de inicio y fin.',
  'turno_linea.registro_no_existe': 'El registro de producción ya no existe. Recarga el turno.',
  'turno_linea.paro_no_existe': 'El paro ya no existe. Recarga el turno.',
  'turno_linea.linea_inactiva': 'La línea está inactiva y no admite captura.',
  'planta.turno_no_existe': 'El turno seleccionado no existe en esta planta.',
  'conflicto.duplicado': 'Ya existe un registro con esos datos.',
  'conflicto.concurrencia': 'Alguien más modificó estos datos. Se recargó la información; revisa y vuelve a intentar.',
}

/** Texto legible para cualquier error del API. */
export function problemMessage(problem: ApiProblem | null): string {
  if (!problem) return 'Ocurrió un error inesperado.'
  if (problem.isNetworkError) return problem.detail ?? problem.title
  switch (problem.status) {
    case 401:
      return 'Tu sesión expiró. Inicia sesión de nuevo.'
    case 403:
      return 'No tienes permiso para realizar esta acción o ver esta planta.'
    case 404:
      return problem.detail ?? 'No se encontró el recurso solicitado.'
    case 409:
      return (problem.codigo && CONFLICT_MESSAGES[problem.codigo]) ?? problem.detail ?? problem.title
    case 503:
      return `${problem.servicio ? `El servicio ${problem.servicio}` : 'Un servicio externo'} no está disponible. Intenta más tarde.`
    default:
      return problem.detail ?? problem.title
  }
}
