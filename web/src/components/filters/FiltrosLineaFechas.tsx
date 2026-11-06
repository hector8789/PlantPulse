import { Field, Input, Select } from '@/components/ui'
import type { Linea } from '@/features/catalogos/types'

export interface FiltrosValor {
  lineaId: string
  desde: string
  hasta: string
}

interface Props {
  valor: FiltrosValor
  onChange: (valor: FiltrosValor) => void
  lineas: Linea[]
  lineasCargando?: boolean
  /** Fecha máxima seleccionable (hoy en la planta) */
  max: string
  error?: string | null
}

export function FiltrosLineaFechas({ valor, onChange, lineas, lineasCargando, max, error }: Props) {
  const set = (parcial: Partial<FiltrosValor>) => onChange({ ...valor, ...parcial })
  return (
    <div className="flex flex-col gap-2">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <Field label="Línea">
          {(c) => (
            <Select
              {...c}
              value={valor.lineaId}
              onChange={(e) => set({ lineaId: e.target.value })}
              placeholder={lineasCargando ? 'Cargando…' : 'Todas las líneas'}
              options={lineas.map((l) => ({
                value: l.id,
                label: `${l.codigo} · ${l.nombre}${l.activa ? '' : ' (inactiva)'}`,
              }))}
            />
          )}
        </Field>
        <Field label="Desde">
          {(c) => (
            <Input {...c} type="date" value={valor.desde} max={max} onChange={(e) => set({ desde: e.target.value })} />
          )}
        </Field>
        <Field label="Hasta">
          {(c) => (
            <Input {...c} type="date" value={valor.hasta} max={max} onChange={(e) => set({ hasta: e.target.value })} />
          )}
        </Field>
      </div>
      {error && (
        <p role="alert" className="text-sm text-red-600">
          {error}
        </p>
      )}
    </div>
  )
}
