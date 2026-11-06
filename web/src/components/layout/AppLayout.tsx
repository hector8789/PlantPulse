import { useState } from 'react'
import { Outlet } from 'react-router'
import { useSessionExpiry } from '@/features/auth/api/useSessionExpiry'
import { cn } from '@/lib/cn'
import { Header } from './Header'
import { Sidebar } from './Sidebar'

export function AppLayout() {
  useSessionExpiry()
  const [menuAbierto, setMenuAbierto] = useState(false)

  return (
    <div className="min-h-dvh bg-slate-100">
      <a
        href="#contenido"
        className="sr-only focus:not-sr-only focus:fixed focus:top-2 focus:left-2 focus:z-50 focus:rounded focus:bg-white focus:px-3 focus:py-2"
      >
        Saltar al contenido
      </a>

      {/* Escritorio */}
      <aside className="fixed inset-y-0 left-0 hidden w-60 lg:block">
        <Sidebar />
      </aside>

      {/* Móvil */}
      <div className={cn('fixed inset-0 z-40 lg:hidden', !menuAbierto && 'pointer-events-none')}>
        <div
          aria-hidden="true"
          onClick={() => setMenuAbierto(false)}
          className={cn('absolute inset-0 bg-slate-900/50 transition-opacity', menuAbierto ? 'opacity-100' : 'opacity-0')}
        />
        <aside
          aria-hidden={!menuAbierto}
          inert={!menuAbierto}
          className={cn(
            'absolute inset-y-0 left-0 w-64 transition-transform',
            menuAbierto ? 'translate-x-0' : '-translate-x-full',
          )}
        >
          <Sidebar onNavigate={() => setMenuAbierto(false)} />
        </aside>
      </div>

      <div className="lg:pl-60">
        <Header onOpenMenu={() => setMenuAbierto(true)} />
        <main id="contenido" className="mx-auto max-w-7xl px-4 py-6 sm:px-6">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
