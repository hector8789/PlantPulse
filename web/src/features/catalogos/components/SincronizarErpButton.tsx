import { Button } from '@/components/ui'
import { toast } from '@/stores/toastStore'
import type { Guid } from '@/types/domain'
import { useSincronizarOrdenesErp } from '../api/useCatalogos'

export function SincronizarErpButton({ plantaId }: { plantaId: Guid }) {
  const sync = useSincronizarOrdenesErp()
  return (
    <Button
      variant="secondary"
      loading={sync.isPending}
      onClick={() =>
        sync.mutate(
          { plantaId },
          {
            onSuccess: (r) => {
              const resumen = `Recibidas ${r.recibidas} · nuevas ${r.creadas} · actualizadas ${r.actualizadas} · sin cambios ${r.sinCambios}`
              if (r.omitidas.length > 0) {
                toast.warning(
                  `Órdenes de ${r.plantaCodigo} sincronizadas con omisiones`,
                  `${resumen}. Omitidas: ${r.omitidas.map((o) => `${o.numero} (${o.motivo})`).join(', ')}`,
                )
              } else {
                toast.success(`Órdenes de ${r.plantaCodigo} sincronizadas`, resumen)
              }
            },
          },
        )
      }
    >
      Sincronizar órdenes ERP
    </Button>
  )
}
