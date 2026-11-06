import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Alert, Button, Field, Input, Select, Textarea } from '@/components/ui'
import { useCausasParo } from '@/features/catalogos/api/useCatalogos'
import type { TurnoLinea } from '@/features/produccion/types'
import { localAUtcIso, utcIsoALocal } from '@/lib/dates'
import { applyServerErrors } from '@/lib/forms'
import { toast } from '@/stores/toastStore'
import { CATEGORIA_PARO_LABEL, TIPO_PARO_LABEL } from '@/types/domain'
import { useRegistrarParo } from '../api/useParos'
import { COMENTARIO_MAX, paroSchema, type ParoFormValues } from '../schemas/paroSchema'

export function ParoForm({ turno, zona }: { turno: TurnoLinea; zona: string }) {
  const causas = useCausasParo()
  const registrar = useRegistrarParo(turno.id)
  const inicioTurno = utcIsoALocal(turno.inicioUtc, zona)
  const finTurno = utcIsoALocal(turno.finUtc, zona)
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors },
  } = useForm<ParoFormValues>({
    // Se reconstruye el schema al validar para que "ahora" sea el momento real del envío.
    resolver: (values, ctx, options) =>
      zodResolver(paroSchema({ inicioTurno, finTurno, ahora: utcIsoALocal(new Date().toISOString(), zona) }))(
        values,
        ctx,
        options,
      ),
    defaultValues: { causaParoId: '', inicio: inicioTurno, fin: '', comentario: '' },
  })

  const onSubmit = (values: ParoFormValues) =>
    registrar.mutate(
      {
        causaParoId: values.causaParoId,
        inicioUtc: localAUtcIso(values.inicio, zona),
        finUtc: localAUtcIso(values.fin, zona),
        ...(values.comentario ? { comentario: values.comentario } : {}),
      },
      {
        onSuccess: () => {
          toast.success('Paro registrado')
          reset({ causaParoId: '', inicio: values.fin, fin: '', comentario: '' })
        },
        onError: (error) =>
          applyServerErrors(error, setError, ['causaParoId', 'inicio', 'fin', 'comentario']),
      },
    )

  const activas = (causas.data ?? [])
    .filter((c) => c.activa)
    .sort((a, b) => a.tipo.localeCompare(b.tipo) || a.codigo.localeCompare(b.codigo))

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-3">
      <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
        <Field label="Causa" error={errors.causaParoId?.message} required className="md:col-span-3 lg:col-span-1">
          {(c) => (
            <Select
              {...c}
              {...register('causaParoId')}
              placeholder={causas.isPending ? 'Cargando causas…' : 'Selecciona…'}
              options={activas.map((ca) => ({
                value: ca.id,
                label: `${ca.codigo} · ${ca.descripcion} — ${TIPO_PARO_LABEL[ca.tipo]}, ${CATEGORIA_PARO_LABEL[ca.categoria] ?? ca.categoria}`,
              }))}
            />
          )}
        </Field>
        <Field label="Inicio" error={errors.inicio?.message} required>
          {(c) => <Input {...c} type="datetime-local" min={inicioTurno} max={finTurno} {...register('inicio')} />}
        </Field>
        <Field label="Fin" error={errors.fin?.message} required>
          {(c) => <Input {...c} type="datetime-local" min={inicioTurno} max={finTurno} {...register('fin')} />}
        </Field>
      </div>
      <Field label="Comentario" error={errors.comentario?.message} hint={`Opcional, máximo ${COMENTARIO_MAX} caracteres.`}>
        {(c) => <Textarea {...c} rows={2} maxLength={COMENTARIO_MAX} {...register('comentario')} />}
      </Field>
      {errors.root?.server && <Alert variant="error">{errors.root.server.message}</Alert>}
      {causas.isError && <Alert variant="error">No se pudo cargar el catálogo de causas.</Alert>}
      <div>
        <Button type="submit" loading={registrar.isPending}>
          Registrar paro
        </Button>
      </div>
    </form>
  )
}
