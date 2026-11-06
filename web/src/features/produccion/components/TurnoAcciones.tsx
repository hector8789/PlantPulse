import { Button, ConfirmButton } from '@/components/ui'
import { usePermisos } from '@/features/auth/api/usePermisos'
import { formatTime } from '@/lib/format'
import { useAhora } from '@/lib/useAhora'
import { toast } from '@/stores/toastStore'
import { useCerrarTurno, useReabrirTurno } from '../api/useTurnosLinea'
import type { TurnoLinea } from '../types'

export function TurnoAcciones({ turno, zona }: { turno: TurnoLinea; zona: string }) {
  const permisos = usePermisos()
  const cerrar = useCerrarTurno(turno.id)
  const reabrir = useReabrirTurno(turno.id)
  const ahora = useAhora()

  if (turno.estado === 'Abierto' && permisos.puedeCapturarEnPlanta(turno.plantaId)) {
    // El API rechaza el cierre antes del fin (409 turno_no_terminado): se habilita al terminar el turno.
    if (ahora < Date.parse(turno.finUtc)) {
      return (
        <Button disabled title="El turno se puede cerrar cuando termina">
          Cerrar turno (desde {formatTime(turno.finUtc, zona)})
        </Button>
      )
    }
    return (
      <ConfirmButton
        variant="primary"
        size="md"
        pregunta="Al cerrar, el turno ya no admite cambios."
        confirmarLabel="Cerrar turno"
        loading={cerrar.isPending}
        onConfirm={() => cerrar.mutate(undefined, { onSuccess: () => toast.success('Turno cerrado') })}
      >
        Cerrar turno
      </ConfirmButton>
    )
  }
  if (turno.estado === 'Cerrado' && permisos.puedeReabrir) {
    return (
      <ConfirmButton
        size="md"
        pregunta="¿Reabrir el turno para corregirlo?"
        confirmarLabel="Reabrir"
        loading={reabrir.isPending}
        onConfirm={() => reabrir.mutate(undefined, { onSuccess: () => toast.success('Turno reabierto') })}
      >
        Reabrir turno
      </ConfirmButton>
    )
  }
  return null
}
