import { useEffect, useState } from 'react'

/** Hora actual (ms) que se actualiza cada `cadaMs`, para habilitar acciones que dependen del reloj. */
export function useAhora(cadaMs = 30_000): number {
  const [ahora, setAhora] = useState(() => Date.now())
  useEffect(() => {
    const id = window.setInterval(() => setAhora(Date.now()), cadaMs)
    return () => window.clearInterval(id)
  }, [cadaMs])
  return ahora
}
