import type { InputHTMLAttributes, Ref } from 'react'
import { cn } from '@/lib/cn'
import { controlBase } from './controlStyles'

export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  ref?: Ref<HTMLInputElement>
}

export function Input({ className, ...props }: InputProps) {
  return <input className={cn(controlBase, 'h-10', className)} {...props} />
}
