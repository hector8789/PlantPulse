import { hasAnyRole, isAdmin, useAuthStore } from '@/stores/authStore'
import { ROLES_CAPTURA, type Guid } from '@/types/domain'

/** Permisos derivados del rol y de las plantas del usuario. El API es quien decide; esto solo ajusta la UI. */
export function usePermisos() {
  const user = useAuthStore((s) => s.user)
  const admin = isAdmin(user)
  const puedeCapturarRol = hasAnyRole(user, ROLES_CAPTURA)
  return {
    esAdmin: admin,
    puedeCapturar: puedeCapturarRol,
    /** Captura en una planta concreta: Admin en todas; Supervisor solo en las suyas. */
    puedeCapturarEnPlanta: (plantaId: Guid) =>
      puedeCapturarRol && (admin || (user?.plantaIds.includes(plantaId) ?? false)),
    puedeReabrir: admin,
  }
}
