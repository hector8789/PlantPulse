import { Navigate, Outlet, useLocation } from 'react-router'
import { isSessionExpired, useAuthStore } from '@/stores/authStore'

/** Rutas que requieren sesión. Si no hay sesión, manda al login y recuerda a dónde iba. */
export function RequireAuth() {
  const accessToken = useAuthStore((s) => s.accessToken)
  const expiresAtUtc = useAuthStore((s) => s.expiresAtUtc)
  const lastLogoutReason = useAuthStore((s) => s.lastLogoutReason)
  const location = useLocation()

  if (!accessToken || isSessionExpired(expiresAtUtc)) {
    // Tras "Salir" no se recuerda la página: el siguiente usuario puede tener otro rol.
    const state = lastLogoutReason === 'manual' ? undefined : { from: location.pathname + location.search }
    return <Navigate to="/login" replace state={state} />
  }
  return <Outlet />
}
