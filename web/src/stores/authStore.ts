import { create } from 'zustand'
import { createJSONStorage, persist } from 'zustand/middleware'
import type { Guid, IsoUtc, Rol } from '@/types/domain'

export interface SessionUser {
  id: Guid
  nombre: string
  email: string
  roles: Rol[]
  plantaIds: Guid[]
}

export type LogoutReason = 'manual' | 'expired' | 'unauthorized'

interface AuthState {
  accessToken: string | null
  expiresAtUtc: IsoUtc | null
  user: SessionUser | null
  /** Planta seleccionada en el header. */
  plantaActivaId: Guid | null
  /** Motivo del último cierre de sesión, para mostrarlo en el login. */
  lastLogoutReason: LogoutReason | null
  setSession: (session: { accessToken: string; expiresAtUtc: IsoUtc; user: SessionUser }) => void
  setPlantaActiva: (plantaId: Guid) => void
  logout: (reason?: LogoutReason) => void
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      accessToken: null,
      expiresAtUtc: null,
      user: null,
      plantaActivaId: null,
      lastLogoutReason: null,
      setSession: ({ accessToken, expiresAtUtc, user }) =>
        set({
          accessToken,
          expiresAtUtc,
          user,
          plantaActivaId: user.plantaIds[0] ?? null,
          lastLogoutReason: null,
        }),
      setPlantaActiva: (plantaId) => set({ plantaActivaId: plantaId }),
      logout: (reason = 'manual') =>
        set((s) =>
          s.accessToken === null && s.user === null
            ? s
            : {
                accessToken: null,
                expiresAtUtc: null,
                user: null,
                plantaActivaId: null,
                lastLogoutReason: reason,
              },
        ),
    }),
    {
      name: 'plantpulse-auth',
      storage: createJSONStorage(() => sessionStorage),
      partialize: (s) => ({
        accessToken: s.accessToken,
        expiresAtUtc: s.expiresAtUtc,
        user: s.user,
        plantaActivaId: s.plantaActivaId,
      }),
    },
  ),
)

export function isSessionExpired(expiresAtUtc: IsoUtc | null, now = Date.now()): boolean {
  if (!expiresAtUtc) return true
  return Date.parse(expiresAtUtc) <= now
}

export function hasAnyRole(user: SessionUser | null, roles: readonly Rol[]): boolean {
  if (!user) return false
  return user.roles.some((r) => roles.includes(r))
}

export function isAdmin(user: SessionUser | null): boolean {
  return hasAnyRole(user, ['Admin'])
}
