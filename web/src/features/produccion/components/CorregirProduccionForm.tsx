import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Alert, Button, Field, Input } from '@/components/ui'
import { applyServerErrors } from '@/lib/forms'
import { toast } from '@/stores/toastStore'
import type { Guid } from '@/types/domain'
import { useCorregirProduccion } from '../api/useTurnosLinea'
import { corregirProduccionSchema, type CorregirProduccionValues } from '../schemas/produccionSchemas'
import type { RegistroProduccion } from '../types'

interface Props {
  turnoId: Guid
  registro: RegistroProduccion
  onDone: () => void
}

export function CorregirProduccionForm({ turnoId, registro, onDone }: Props) {
  const corregir = useCorregirProduccion(turnoId)
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<CorregirProduccionValues>({
    resolver: zodResolver(corregirProduccionSchema),
    defaultValues: { piezasTotales: registro.piezasTotales, piezasBuenas: registro.piezasBuenas },
  })

  const onSubmit = (values: CorregirProduccionValues) =>
    corregir.mutate(
      { registroId: registro.id, body: values },
      {
        onSuccess: () => {
          toast.success('Producción corregida', `Orden ${registro.ordenNumero}`)
          onDone()
        },
        onError: (error) => applyServerErrors(error, setError, ['piezasTotales', 'piezasBuenas']),
      },
    )

  return (
    <form
      onSubmit={handleSubmit(onSubmit)}
      noValidate
      className="flex flex-col gap-3 rounded-md bg-amber-50/60 p-3 ring-1 ring-amber-200"
      aria-label={`Corregir orden ${registro.ordenNumero}`}
    >
      <p className="text-sm font-medium text-slate-800">Corregir orden {registro.ordenNumero}</p>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-[1fr_1fr_auto_auto] sm:items-start">
        <Field label="Piezas totales" error={errors.piezasTotales?.message} required>
          {(c) => (
            <Input {...c} type="number" inputMode="numeric" min={0} step={1} autoFocus {...register('piezasTotales', { valueAsNumber: true })} />
          )}
        </Field>
        <Field label="Piezas buenas" error={errors.piezasBuenas?.message} required>
          {(c) => (
            <Input {...c} type="number" inputMode="numeric" min={0} step={1} {...register('piezasBuenas', { valueAsNumber: true })} />
          )}
        </Field>
        <Button type="submit" loading={corregir.isPending} className="sm:mt-6">
          Guardar
        </Button>
        <Button variant="ghost" onClick={onDone} className="sm:mt-6">
          Cancelar
        </Button>
      </div>
      {errors.root?.server && <Alert variant="error">{errors.root.server.message}</Alert>}
    </form>
  )
}
