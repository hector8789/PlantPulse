import { z } from 'zod'

export const COMENTARIO_MAX = 500

export interface VentanaParo {
  /** Valores "YYYY-MM-DDTHH:mm" en la zona de la planta */
  inicioTurno: string
  finTurno: string
  /** Ahora, en la zona de la planta */
  ahora: string
}

/**
 * Reglas del backend reflejadas en el cliente: fin > inicio, el paro cae completo dentro del turno
 * y no termina en el futuro. Los traslapes los valida el API (409 paro_traslapado).
 * Los valores locales "YYYY-MM-DDTHH:mm" se comparan como texto porque tienen ancho fijo.
 */
export function paroSchema({ inicioTurno, finTurno, ahora }: VentanaParo) {
  return z
    .object({
      causaParoId: z.string().min(1, 'Selecciona la causa del paro.'),
      inicio: z.string().min(1, 'Captura la hora de inicio.'),
      fin: z.string().min(1, 'Captura la hora de fin.'),
      comentario: z
        .string()
        .trim()
        .max(COMENTARIO_MAX, `Máximo ${COMENTARIO_MAX} caracteres.`),
    })
    .superRefine((v, ctx) => {
      if (!v.inicio || !v.fin) return
      if (v.inicio < inicioTurno || v.inicio >= finTurno) {
        ctx.addIssue({ code: 'custom', path: ['inicio'], message: 'El inicio debe caer dentro del turno.' })
      }
      if (v.fin > finTurno) {
        ctx.addIssue({
          code: 'custom',
          path: ['fin'],
          message: 'El fin sale del turno. Si el paro cruza de turno, captúralo como dos paros.',
        })
      }
      if (v.fin <= v.inicio) {
        ctx.addIssue({ code: 'custom', path: ['fin'], message: 'El fin debe ser posterior al inicio.' })
      } else if (v.fin > ahora) {
        ctx.addIssue({ code: 'custom', path: ['fin'], message: 'El paro no puede terminar en el futuro.' })
      }
    })
}

export type ParoFormValues = z.infer<ReturnType<typeof paroSchema>>
