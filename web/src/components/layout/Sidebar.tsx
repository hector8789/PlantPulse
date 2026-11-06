import { NavLink } from 'react-router'
import { useApiHealth } from '@/features/health/useApiHealth'
import { cn } from '@/lib/cn'
import { NAV_ITEMS } from './navItems'

export function Sidebar({ onNavigate }: { onNavigate?: () => void }) {
  const health = useApiHealth()
  return (
    <div className="flex h-full flex-col bg-slate-900 text-slate-100">
      <div className="flex h-14 items-center gap-2 px-5">
        <span aria-hidden="true" className="size-2.5 rounded-full bg-teal-400" />
        <span className="text-lg font-bold tracking-tight">PlantPulse</span>
      </div>
      <nav aria-label="Principal" className="flex-1 px-3 py-2">
        <ul className="flex flex-col gap-1">
          {NAV_ITEMS.map((item) => (
            <li key={item.to}>
              <NavLink
                to={item.to}
                onClick={onNavigate}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors',
                    'focus-visible:outline-2 focus-visible:outline-teal-400',
                    isActive ? 'bg-slate-800 text-white' : 'text-slate-300 hover:bg-slate-800/60 hover:text-white',
                  )
                }
              >
                <span aria-hidden="true" className="w-4 text-center text-teal-400">
                  {item.icon}
                </span>
                {item.label}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>
      <div className="flex items-center gap-2 px-5 py-4 text-xs text-slate-400">
        <span
          aria-hidden="true"
          className={cn(
            'size-2 rounded-full',
            health.isSuccess ? 'bg-emerald-400' : health.isError ? 'bg-red-400' : 'bg-slate-500',
          )}
        />
        API: {health.isSuccess ? 'en línea' : health.isError ? 'sin conexión' : 'verificando…'}
      </div>
    </div>
  )
}
