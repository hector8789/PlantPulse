import { zodResolver } from '@hookform/resolvers/zod'
import { useForm, useWatch } from 'react-hook-form'
import { Alert, Button, Field, Input, Select } from '@/components/ui'
import { useOrdenes } from '@/features/catalogos/api/useCatalogos'
import { applyServerErrors } from '@/lib/forms'
import { formatInt } from '@/lib/format'
import { toast } from '@/stores/toastStore'
import { useRegistrarProduccion } from '../api/useTurnosLinea'
import { registrarProduccionSchema, type RegistrarProduccionValues } from '../schemas/produccionSchemas'
import type { TurnoLinea } from '../types'

export function RegistroProduccionForm({ turno }: { turno: TurnoLinea }) {
  const ordenes = useOrdenes(turno.lineaId, true)
  const registrar = useRegistrarProduccion(turno.id)
  const yaRegistradas = new Set(turno.registros.map((r) => r.ordenProduccionId))
  const disponibles = (ordenes.data ?? []).filter((o) => o.estado !== 'Terminada' && !yaRegistradas.has(o.id))

  const {
    register,
    handleSubmit,
    setError,
    control,
    formState: { errors },
  } = useForm<RegistrarProduccionValues>({
    resolver: zodResolver(registrarProduccionSchema),
    defaultValues: { ordenProduccionId: '' },
  })
  const ordenId = useWatch({ control, name: 'ordenProduccionId' })
  const orden = disponibles.find((o) => o.id === ordenId)

  const onSubmit = (values: RegistrarProduccionValues) =>
    registrar.mutate(values, {
      onSuccess: () => toast.success('Producción registrada'),
      onError: (error) =>
        applyServerErrors(error, setError, ['ordenProduccionId', 'piezasTotales', 'piezasBuenas']),
    })

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-3">
      <div className="grid grid-cols-1 gap-3 md:grid-cols-[2fr_1fr_1fr_auto] md:items-start">
        <Field
          label="Orden de producción"
          error={errors.ordenProduccionId?.message}
          hint={
            orden
              ? `${orden.materialCodigo} · ${orden.materialDescripcion} · plan ${formatInt(orden.cantidadPlaneada)} pzs · ciclo ${orden.tiempoCicloIdealSeg} s${orden.tiempoCicloPropio ? ' (propio)' : ''}`
              : undefined
          }
          required
        >
          {(c) => (
            <Select
              {...c}
              {...register('ordenProduccionId')}
              placeholder={
                ordenes.isPending ? 'Cargando órdenes…' : disponibles.length === 0 ? 'Sin órdenes abiertas' : 'Selecciona…'
              }
              options={disponibles.map((o) => ({
                value: o.id,
                label: `${o.numero} · ${o.materialCodigo} (${o.estado === 'EnProceso' ? 'en proceso' : 'liberada'})`,
              }))}
            />
          )}
        </Field>
        <Field label="Piezas totales" error={errors.piezasTotales?.message} required>
          {(c) => (
            <Input {...c} type="number" inputMode="numeric" min={0} step={1} {...register('piezasTotales', { valueAsNumber: true })} />
          )}
        </Field>
        <Field label="Piezas buenas" error={errors.piezasBuenas?.message} required>
          {(c) => (
            <Input {...c} type="number" inputMode="numeric" min={0} step={1} {...register('piezasBuenas', { valueAsNumber: true })} />
          )}
        </Field>
        <Button type="submit" loading={registrar.isPending} className="md:mt-6">
          Registrar
        </Button>
      </div>
      {errors.root?.server && <Alert variant="error">{errors.root.server.message}</Alert>}
      {ordenes.isError && <Alert variant="error">No se pudieron cargar las órdenes de la línea.</Alert>}
    </form>
  )
}
