import type { Ref, TextareaHTMLAttributes } from 'react'
import { cn } from '@/lib/cn'
import { controlBase } from './controlStyles'

export interface TextareaProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  ref?: Ref<HTMLTextAreaElement>
}

export function Textarea({ className, rows = 3, ...props }: TextareaProps) {
  return <textarea rows={rows} className={cn(controlBase, className)} {...props} />
}
