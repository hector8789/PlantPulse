import { cn } from '@/lib/cn'

interface SpinnerProps {
  className?: string
  label?: string
}

export function Spinner({ className, label = 'Cargando…' }: SpinnerProps) {
  return (
    <span role="status" className="inline-flex items-center gap-2">
      <span
        aria-hidden="true"
        className={cn(
          'inline-block size-4 animate-spin rounded-full border-2 border-current border-r-transparent',
          className,
        )}
      />
      <span className="sr-only">{label}</span>
    </span>
  )
}

export function PageSpinner({ label = 'Cargando…' }: { label?: string }) {
  return (
    <div className="flex items-center justify-center gap-3 py-16 text-slate-500">
      <Spinner className="size-5" label={label} />
      <span aria-hidden="true">{label}</span>
    </div>
  )
}
