import { z } from 'zod'

/** Límite del backend (422): piezasTotales 0–100000. */
export const PIEZAS_MAX = 100_000

const piezas = (campo: string) =>
  z
    .number({ error: `Captura ${campo}.` })
    .int(`${campo[0].toUpperCase()}${campo.slice(1)} debe ser un número entero.`)
    .min(0, 'No puede ser negativo.')
    .max(PIEZAS_MAX, `Máximo ${PIEZAS_MAX.toLocaleString('es-MX')} piezas.`)

/** Mismas reglas que el backend: enteros 0–100000 y buenas ≤ totales (422 en errors.piezasBuenas). */
const piezasBase = z.object({
  piezasTotales: piezas('las piezas totales'),
  piezasBuenas: piezas('las piezas buenas'),
})

const buenasNoMayorATotales = <T extends { piezasTotales: number; piezasBuenas: number }>(
  v: T,
  ctx: z.RefinementCtx,
) => {
  if (v.piezasBuenas > v.piezasTotales) {
    ctx.addIssue({
      code: 'custom',
      path: ['piezasBuenas'],
      message: 'Las piezas buenas no pueden ser más que las totales.',
    })
  }
}

export const corregirProduccionSchema = piezasBase.superRefine(buenasNoMayorATotales)
export type CorregirProduccionValues = z.infer<typeof corregirProduccionSchema>

export const registrarProduccionSchema = piezasBase
  .extend({ ordenProduccionId: z.string().min(1, 'Selecciona la orden de producción.') })
  .superRefine(buenasNoMayorATotales)
export type RegistrarProduccionValues = z.infer<typeof registrarProduccionSchema>

/** Ventana de captura: hasta 7 días atrás, nunca a futuro (fecha en la zona de la planta). */
export const DIAS_CAPTURA_RETROACTIVA = 7

export function abrirTurnoSchema(hoy: string, minimo: string) {
  return z.object({
    lineaId: z.string().min(1, 'Selecciona la línea.'),
    turnoId: z.string().min(1, 'Selecciona el turno.'),
    fechaOperativa: z
      .string()
      .regex(/^\d{4}-\d{2}-\d{2}$/, 'Selecciona la fecha operativa.')
      .refine((f) => f <= hoy, 'No se puede capturar en una fecha futura.')
      .refine((f) => f >= minimo, `Solo se puede capturar hasta ${DIAS_CAPTURA_RETROACTIVA} días atrás.`),
  })
}
export type AbrirTurnoValues = z.infer<ReturnType<typeof abrirTurnoSchema>>
