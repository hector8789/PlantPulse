import type { ReactNode } from 'react'
import { cn } from '@/lib/cn'
import { formatPercent } from '@/lib/format'
import { nivelOee, type NivelOee } from '@/lib/oee'

export type BadgeTone = 'neutral' | 'green' | 'yellow' | 'red' | 'blue'

const tones: Record<BadgeTone, string> = {
  neutral: 'bg-slate-100 text-slate-700 ring-slate-300',
  green: 'bg-emerald-50 text-emerald-800 ring-emerald-600/30',
  yellow: 'bg-amber-50 text-amber-800 ring-amber-600/30',
  red: 'bg-red-50 text-red-800 ring-red-600/30',
  blue: 'bg-sky-50 text-sky-800 ring-sky-600/30',
}

export function Badge({ tone = 'neutral', children, className }: { tone?: BadgeTone; children: ReactNode; className?: string }) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium whitespace-nowrap ring-1 ring-inset',
        tones[tone],
        className,
      )}
    >
      {children}
    </span>
  )
}

const nivelTone: Record<NivelOee, BadgeTone> = { verde: 'green', amarillo: 'yellow', rojo: 'red' }
const nivelLabel: Record<NivelOee, string> = { verde: 'Bueno', amarillo: 'Regular', rojo: 'Bajo' }

/**
 * Semáforo de OEE. Sin producción (sin tiempo planificado) se muestra neutro.
 * Con `soloNivel` muestra "Bueno/Regular/Bajo" en vez del porcentaje (útil junto a la cifra grande).
 */
export function OeeBadge({ oee, sinDatos = false, soloNivel = false }: { oee: number; sinDatos?: boolean; soloNivel?: boolean }) {
  if (sinDatos) return <Badge tone="neutral">Sin producción</Badge>
  const nivel = nivelOee(oee)
  return (
    <Badge tone={nivelTone[nivel]}>
      <span aria-hidden="true" className="size-1.5 rounded-full bg-current" />
      {soloNivel ? nivelLabel[nivel] : formatPercent(oee)}
      {!soloNivel && <span className="sr-only"> ({nivelLabel[nivel]})</span>}
    </Badge>
  )
}
