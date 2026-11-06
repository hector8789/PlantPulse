import type { FieldValues, Path, UseFormSetError } from 'react-hook-form'
import { getApiProblem } from '@/api/problem'

/**
 * Lleva un 422 del API a los campos del formulario.
 * - Los nombres de campo ya vienen normalizados a camelCase por getApiProblem.
 * - Los campos que el formulario no conoce se juntan en `root.server`.
 * Devuelve true si el error era un 422 y quedó mostrado en el formulario.
 */
export function applyServerErrors<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
  campos: readonly Path<T>[],
): boolean {
  const problem = getApiProblem(error)
  if (!problem || problem.status !== 422) return false

  const sueltos: string[] = []
  for (const [campo, mensajes] of Object.entries(problem.errors ?? {})) {
    const conocido = campos.find((c) => c === campo)
    if (conocido) {
      setError(conocido, { type: 'server', message: mensajes.join(' ') }, { shouldFocus: true })
    } else {
      sueltos.push(...mensajes)
    }
  }
  if (sueltos.length > 0 || !problem.errors) {
    setError('root.server', {
      type: 'server',
      message: sueltos.length > 0 ? sueltos.join(' ') : (problem.detail ?? problem.title),
    })
  }
  return true
}
