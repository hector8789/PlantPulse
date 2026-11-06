import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuthStore } from '@/stores/authStore'
import { login } from './authApi'

export function useLogin() {
  const qc = useQueryClient()
  const setSession = useAuthStore((s) => s.setSession)
  return useMutation({
    mutationFn: login,
    // El formulario muestra 401 y 422; no queremos el toast global.
    meta: { skipGlobalError: true },
    onSuccess: (data) => {
      qc.clear() // nada en caché de otra sesión
      setSession(data)
    },
  })
}

/** Cierra la sesión y limpia la caché para no mostrar datos de otro usuario. */
export function useLogout() {
  const qc = useQueryClient()
  const logout = useAuthStore((s) => s.logout)
  return () => {
    logout('manual')
    qc.clear()
  }
}
