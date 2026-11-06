import { create } from 'zustand'

export type ToastVariant = 'info' | 'success' | 'warning' | 'error'

export interface Toast {
  id: number
  variant: ToastVariant
  title: string
  message?: string
}

interface ToastState {
  toasts: Toast[]
  push: (toast: Omit<Toast, 'id'>, durationMs?: number) => void
  dismiss: (id: number) => void
}

let nextId = 1

export const useToastStore = create<ToastState>()((set, get) => ({
  toasts: [],
  push: (toast, durationMs = 6000) => {
    const id = nextId++
    // Evita duplicados idénticos (p. ej. varias queries con el mismo 403).
    const repetido = get().toasts.some((t) => t.title === toast.title && t.message === toast.message)
    if (repetido) return
    set((s) => ({ toasts: [...s.toasts.slice(-3), { ...toast, id }] }))
    if (durationMs > 0) setTimeout(() => get().dismiss(id), durationMs)
  },
  dismiss: (id) => set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })),
}))

/** Para usar fuera de componentes (interceptores, QueryClient). */
export const toast = {
  info: (title: string, message?: string) => useToastStore.getState().push({ variant: 'info', title, message }),
  success: (title: string, message?: string) =>
    useToastStore.getState().push({ variant: 'success', title, message }),
  warning: (title: string, message?: string) =>
    useToastStore.getState().push({ variant: 'warning', title, message }),
  error: (title: string, message?: string) =>
    useToastStore.getState().push({ variant: 'error', title, message }, 9000),
}
