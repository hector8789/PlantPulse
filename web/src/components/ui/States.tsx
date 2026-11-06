import type { ReactNode } from 'react'
import { getApiProblem, problemMessage } from '@/api/problem'
import { Alert } from './Alert'
import { Button } from './Button'

export function EmptyState({ title, children }: { title: string; children?: ReactNode }) {
  return (
    <div className="rounded-md border border-dashed border-slate-300 px-4 py-10 text-center">
      <p className="font-medium text-slate-700">{title}</p>
      {children && <div className="mt-1 text-sm text-slate-500">{children}</div>}
    </div>
  )
}

export function ErrorState({ error, onRetry, title = 'No se pudo cargar la información' }: { error: unknown; onRetry?: () => void; title?: string }) {
  const problem = getApiProblem(error)
  const status = problem?.status
  return (
    <Alert
      variant={status === 403 ? 'warning' : 'error'}
      title={status === 403 ? 'Sin permiso' : title}
      actions={
        onRetry && status !== 403 ? (
          <Button size="sm" variant="secondary" onClick={onRetry}>
            Reintentar
          </Button>
        ) : undefined
      }
    >
      {problemMessage(problem)}
    </Alert>
  )
}
