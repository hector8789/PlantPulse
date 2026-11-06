import type { ReactNode } from 'react'
import { cn } from '@/lib/cn'

type Variant = 'info' | 'success' | 'warning' | 'error'

const styles: Record<Variant, string> = {
  info: 'bg-sky-50 text-sky-900 ring-sky-200',
  success: 'bg-emerald-50 text-emerald-900 ring-emerald-200',
  warning: 'bg-amber-50 text-amber-900 ring-amber-200',
  error: 'bg-red-50 text-red-900 ring-red-200',
}

interface AlertProps {
  variant?: Variant
  title?: string
  children?: ReactNode
  actions?: ReactNode
  className?: string
}

export function Alert({ variant = 'info', title, children, actions, className }: AlertProps) {
  return (
    <div
      role={variant === 'error' || variant === 'warning' ? 'alert' : 'status'}
      className={cn('rounded-md px-4 py-3 text-sm ring-1 ring-inset', styles[variant], className)}
    >
      {title && <p className="font-semibold">{title}</p>}
      {children && <div className={cn(title && 'mt-1')}>{children}</div>}
      {actions && <div className="mt-3 flex flex-wrap gap-2">{actions}</div>}
    </div>
  )
}
