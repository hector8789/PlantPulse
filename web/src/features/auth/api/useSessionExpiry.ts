import { useEffect } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { isSessionExpired, useAuthStore } from '@/stores/authStore'

/** setTimeout acepta como máximo ~24.8 días. */
const MAX_TIMEOUT = 2_147_483_647

/** Cierra la sesión justo cuando vence el token (expiresAtUtc). */
export function useSessionExpiry() {
  const expiresAtUtc = useAuthStore((s) => s.expiresAtUtc)
  const accessToken = useAuthStore((s) => s.accessToken)
  const logout = useAuthStore((s) => s.logout)
  const qc = useQueryClient()

  useEffect(() => {
    if (!accessToken) return
    const expire = () => {
      logout('expired')
      qc.clear()
    }
    if (isSessionExpired(expiresAtUtc)) {
      expire()
      return
    }
    const ms = Math.min(Date.parse(expiresAtUtc as string) - Date.now(), MAX_TIMEOUT)
    const id = window.setTimeout(expire, ms)
    return () => window.clearTimeout(id)
  }, [accessToken, expiresAtUtc, logout, qc])
}
