import { useId } from 'react'
import { usePlantaActiva } from '@/features/catalogos/api/usePlantaActiva'

export function PlantaSelector() {
  const id = useId()
  const { plantas, plantaActiva, setPlantaActiva, isLoading, error } = usePlantaActiva()

  if (isLoading) return <span className="text-sm text-slate-500">Cargando plantas…</span>
  if (error) return <span className="text-sm text-red-600">No se cargaron las plantas</span>
  if (plantas.length === 0) return <span className="text-sm text-amber-700">Sin plantas asignadas</span>

  return (
    <div className="flex items-center gap-2">
      <label htmlFor={id} className="text-sm text-slate-500">
        Planta
      </label>
      <select
        id={id}
        value={plantaActiva?.id ?? ''}
        onChange={(e) => setPlantaActiva(e.target.value)}
        disabled={plantas.length === 1}
        className="h-9 rounded-md border-0 bg-white py-1 pr-8 pl-3 text-sm font-medium text-slate-900 ring-1 ring-slate-300 ring-inset focus:ring-2 focus:ring-teal-600 focus:outline-none disabled:bg-slate-50"
      >
        {plantas.map((p) => (
          <option key={p.id} value={p.id}>
            {p.codigo} · {p.nombre}
          </option>
        ))}
      </select>
    </div>
  )
}
