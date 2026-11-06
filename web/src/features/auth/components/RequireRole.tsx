import { Navigate, Outlet } from 'react-router'
import { hasAnyRole, useAuthStore } from '@/stores/authStore'
import type { Rol } from '@/types/domain'

/** Rutas restringidas por rol. Sin el rol, muestra la página "sin permiso". */
export function RequireRole({ roles }: { roles: readonly Rol[] }) {
  const user = useAuthStore((s) => s.user)
  if (!hasAnyRole(user, roles)) return <Navigate to="/sin-permiso" replace />
  return <Outlet />
}
