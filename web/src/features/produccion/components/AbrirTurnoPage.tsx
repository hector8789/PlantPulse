import { zodResolver } from '@hookform/resolvers/zod'
import { useMemo } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate } from 'react-router'
import { Alert, Button, Card, EmptyState, Field, Input, PageHeader, Select } from '@/components/ui'
import { usePermisos } from '@/features/auth/api/usePermisos'
import { useLineas } from '@/features/catalogos/api/useCatalogos'
import { usePlantaActiva } from '@/features/catalogos/api/usePlantaActiva'
import type { Planta } from '@/features/catalogos/types'
import { hoyEnZona, sumarDias } from '@/lib/dates'
import { applyServerErrors } from '@/lib/forms'
import { toast } from '@/stores/toastStore'
import { getApiProblem } from '@/api/problem'
import { notificarErrorDeMutacion } from '@/app/queryClient'
import { useAbrirTurno, useBuscarTurnoExistente } from '../api/useTurnosLinea'
import { abrirTurnoSchema, DIAS_CAPTURA_RETROACTIVA, type AbrirTurnoValues } from '../schemas/produccionSchemas'

function AbrirTurnoForm({ planta }: { planta: Planta }) {
  const navigate = useNavigate()
  const lineas = useLineas(planta.id)
  const abrir = useAbrirTurno()
  const buscarExistente = useBuscarTurnoExistente()
  const hoy = hoyEnZona(planta.zonaHoraria)
  const minimo = sumarDias(hoy, -DIAS_CAPTURA_RETROACTIVA)
  const schema = useMemo(() => abrirTurnoSchema(hoy, minimo), [hoy, minimo])

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<AbrirTurnoValues>({
    resolver: zodResolver(schema),
    defaultValues: { lineaId: '', turnoId: '', fechaOperativa: hoy },
  })

  const onSubmit = (values: AbrirTurnoValues) =>
    abrir.mutate(values, {
      onSuccess: (turno) => {
        toast.success('Turno abierto', `${turno.lineaCodigo} · ${turno.turnoNombre} · ${turno.fechaOperativa}`)
        navigate(`/turnos/${turno.id}`)
      },
      onError: async (error) => {
        if (applyServerErrors(error, setError, ['lineaId', 'turnoId', 'fechaOperativa'])) return
        if (getApiProblem(error)?.codigo === 'turno_linea.ya_existe') {
          const existente = await buscarExistente(planta.id, values).catch(() => null)
          if (existente) {
            toast.info('Ese turno ya estaba abierto', 'Te llevamos a su captura.')
            navigate(`/turnos/${existente.id}`)
            return
          }
        }
        notificarErrorDeMutacion(error)
      },
    })

  const turnos = [...planta.turnos].sort((a, b) => a.numero - b.numero)
  const lineasActivas = (lineas.data ?? []).filter((l) => l.activa)

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex max-w-xl flex-col gap-4">
      <Field label="Línea" error={errors.lineaId?.message} required>
        {(c) => (
          <Select
            {...c}
            {...register('lineaId')}
            placeholder={lineas.isPending ? 'Cargando líneas…' : 'Selecciona…'}
            options={lineasActivas.map((l) => ({ value: l.id, label: `${l.codigo} · ${l.nombre}` }))}
          />
        )}
      </Field>
      <Field label="Turno" error={errors.turnoId?.message} required>
        {(c) => (
          <Select
            {...c}
            {...register('turnoId')}
            placeholder="Selecciona…"
            options={turnos.map((t) => ({
              value: t.id,
              label: `${t.numero} · ${t.nombre} (${t.horaInicio}–${t.horaFin})`,
            }))}
          />
        )}
      </Field>
      <Field
        label="Fecha operativa"
        error={errors.fechaOperativa?.message}
        hint={`Fecha de inicio del turno (el nocturno cuenta para el día en que empieza). Desde ${minimo} hasta hoy.`}
        required
      >
        {(c) => <Input {...c} type="date" min={minimo} max={hoy} {...register('fechaOperativa')} />}
      </Field>
      {errors.root?.server && <Alert variant="error">{errors.root.server.message}</Alert>}
      {lineas.isError && <Alert variant="error">No se pudieron cargar las líneas.</Alert>}
      <div className="flex gap-2">
        <Button type="submit" loading={abrir.isPending}>
          Abrir turno
        </Button>
        <Link
          to="/turnos"
          className="inline-flex h-10 items-center rounded-md px-4 text-sm font-medium text-slate-700 hover:bg-slate-100"
        >
          Cancelar
        </Link>
      </div>
    </form>
  )
}

export function AbrirTurnoPage() {
  const { plantaActiva } = usePlantaActiva()
  const permisos = usePermisos()
  return (
    <>
      <PageHeader
        title="Abrir turno"
        description={plantaActiva ? `${plantaActiva.codigo} · ${plantaActiva.nombre}` : undefined}
      />
      <Card>
        {!plantaActiva ? (
          <EmptyState title="Selecciona una planta" />
        ) : !permisos.puedeCapturarEnPlanta(plantaActiva.id) ? (
          <Alert variant="warning" title="Sin permiso">
            No puedes capturar en esta planta.
          </Alert>
        ) : (
          // key: al cambiar de planta se reinicia el formulario
          <AbrirTurnoForm key={plantaActiva.id} planta={plantaActiva} />
        )}
      </Card>
    </>
  )
}
