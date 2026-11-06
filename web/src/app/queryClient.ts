import { MutationCache, QueryClient } from '@tanstack/react-query'
import { getApiProblem, problemMessage } from '@/api/problem'
import { toast } from '@/stores/toastStore'

declare module '@tanstack/react-query' {
  interface Register {
    mutationMeta: {
      /** Si es true, el formulario maneja el error y no se muestra toast global. */
      skipGlobalError?: boolean
    }
  }
}

function isClientError(error: unknown): boolean {
  const p = getApiProblem(error)
  return p !== null && p.status >= 400 && p.status < 500
}

/** Aviso estándar para un error de mutación (403, 409, 503, red). 401 y 422 no se avisan aquí. */
export function notificarErrorDeMutacion(error: unknown): void {
  const p = getApiProblem(error)
  if (p?.status === 401 || p?.status === 422) return
  if (p?.status === 403) return toast.error('Sin permiso', problemMessage(p))
  if (p?.status === 409) {
    // Otro usuario cambió el dato: se recarga todo lo visible para que el usuario reintente sobre lo actual.
    if (p.codigo === 'conflicto.concurrencia') void queryClient.invalidateQueries()
    return toast.warning('No se pudo completar', problemMessage(p))
  }
  toast.error('Error', problemMessage(p))
}

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
      // Sin reintentos en 4xx: no van a cambiar solos.
      retry: (failureCount, error) => !isClientError(error) && failureCount < 2,
    },
    mutations: { retry: false },
  },
  mutationCache: new MutationCache({
    // 401 ya lo atiende el interceptor (logout); 422 lo pinta el formulario.
    onError: (error, _vars, _ctx, mutation) => {
      if (!mutation.meta?.skipGlobalError) notificarErrorDeMutacion(error)
    },
  }),
})
