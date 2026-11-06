import { cn } from '@/lib/cn'
import { useToastStore, type ToastVariant } from '@/stores/toastStore'

const styles: Record<ToastVariant, string> = {
  info: 'border-sky-500',
  success: 'border-emerald-500',
  warning: 'border-amber-500',
  error: 'border-red-500',
}

export function Toaster() {
  const toasts = useToastStore((s) => s.toasts)
  const dismiss = useToastStore((s) => s.dismiss)
  return (
    <div
      aria-live="polite"
      className="pointer-events-none fixed inset-x-0 top-3 z-50 flex flex-col items-center gap-2 px-3 sm:top-auto sm:right-4 sm:bottom-4 sm:left-auto sm:items-end"
    >
      {toasts.map((t) => (
        <div
          key={t.id}
          role={t.variant === 'error' || t.variant === 'warning' ? 'alert' : 'status'}
          className={cn(
            'pointer-events-auto w-full max-w-sm rounded-md border-l-4 bg-white px-4 py-3 shadow-lg ring-1 ring-slate-200',
            styles[t.variant],
          )}
        >
          <div className="flex items-start gap-3">
            <div className="flex-1 text-sm">
              <p className="font-semibold text-slate-900">{t.title}</p>
              {t.message && <p className="mt-0.5 text-slate-600">{t.message}</p>}
            </div>
            <button
              type="button"
              onClick={() => dismiss(t.id)}
              className="rounded text-slate-400 hover:text-slate-700 focus-visible:outline-2 focus-visible:outline-slate-500"
              aria-label="Cerrar aviso"
            >
              ✕
            </button>
          </div>
        </div>
      ))}
    </div>
  )
}
