import type { Ref, SelectHTMLAttributes } from 'react'
import { cn } from '@/lib/cn'
import { controlBase } from './controlStyles'

export interface SelectOption {
  value: string
  label: string
  disabled?: boolean
}

export interface SelectProps extends SelectHTMLAttributes<HTMLSelectElement> {
  ref?: Ref<HTMLSelectElement>
  options: readonly SelectOption[]
  /** Opción vacía al inicio (p. ej. "Selecciona…" o "Todas"). */
  placeholder?: string
}

export function Select({ className, options, placeholder, ...props }: SelectProps) {
  return (
    <select className={cn(controlBase, 'h-10 pr-8', className)} {...props}>
      {placeholder !== undefined && <option value="">{placeholder}</option>}
      {options.map((o) => (
        <option key={o.value} value={o.value} disabled={o.disabled}>
          {o.label}
        </option>
      ))}
    </select>
  )
}
