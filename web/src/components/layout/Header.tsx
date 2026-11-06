import { Button } from '@/components/ui'
import { useLogout } from '@/features/auth/api/useAuth'
import { useAuthStore } from '@/stores/authStore'
import { PlantaSelector } from './PlantaSelector'

export function Header({ onOpenMenu }: { onOpenMenu: () => void }) {
  const user = useAuthStore((s) => s.user)
  const logout = useLogout()
  return (
    <header className="sticky top-0 z-20 flex h-14 items-center gap-3 border-b border-slate-200 bg-white/95 px-4 backdrop-blur sm:px-6">
      <button
        type="button"
        onClick={onOpenMenu}
        className="-ml-1 rounded p-2 text-slate-600 hover:bg-slate-100 focus-visible:outline-2 focus-visible:outline-slate-500 lg:hidden"
        aria-label="Abrir menú"
      >
        ☰
      </button>
      <PlantaSelector />
      <div className="ml-auto flex items-center gap-3">
        {user && (
          <div className="hidden text-right sm:block">
            <p className="text-sm leading-tight font-medium text-slate-900">{user.nombre}</p>
            <p className="text-xs leading-tight text-slate-500">{user.roles.join(', ')}</p>
          </div>
        )}
        <Button variant="secondary" size="sm" onClick={logout}>
          Salir
        </Button>
      </div>
    </header>
  )
}
