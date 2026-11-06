import { useState } from 'react'
import { Button, type ButtonProps } from './Button'

interface ConfirmButtonProps extends Omit<ButtonProps, 'onClick'> {
  onConfirm: () => void
  pregunta: string
  confirmarLabel?: string
}

/** Botón de dos pasos para acciones destructivas o irreversibles, sin diálogos del navegador. */
export function ConfirmButton({
  onConfirm,
  pregunta,
  confirmarLabel = 'Sí, continuar',
  variant = 'secondary',
  size = 'sm',
  loading,
  children,
  ...props
}: ConfirmButtonProps) {
  const [confirmando, setConfirmando] = useState(false)
  if (!confirmando) {
    return (
      <Button variant={variant} size={size} loading={loading} onClick={() => setConfirmando(true)} {...props}>
        {children}
      </Button>
    )
  }
  return (
    <span className="inline-flex flex-wrap items-center gap-2" role="group" aria-label={pregunta}>
      <span className="text-sm text-slate-700">{pregunta}</span>
      <Button
        variant={variant === 'secondary' ? 'primary' : variant}
        size={size}
        loading={loading}
        autoFocus
        onClick={() => {
          setConfirmando(false)
          onConfirm()
        }}
      >
        {confirmarLabel}
      </Button>
      <Button variant="ghost" size={size} onClick={() => setConfirmando(false)}>
        Cancelar
      </Button>
    </span>
  )
}
