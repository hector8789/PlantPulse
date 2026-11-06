import { Link, useParams } from 'react-router'
import { Alert, Card, ErrorState, ExportarExcelButton, PageSpinner } from '@/components/ui'
import { usePermisos } from '@/features/auth/api/usePermisos'
import { usePlantas } from '@/features/catalogos/api/useCatalogos'
import { OeeResumen } from '@/features/kpis/components/OeeResumen'
import { columnasOee } from '@/features/kpis/excelOee'
import { ParosSection } from '@/features/paros/components/ParosSection'
import { hoja } from '@/lib/excel'
import { formatDateTime, formatFechaOperativa, formatTime, ZONA_DEFAULT } from '@/lib/format'
import { TIPO_PARO_LABEL } from '@/types/domain'
import type { ParoRegistrado, RegistroProduccion, TurnoLinea } from '../types'
import { useTurnoLinea } from '../api/useTurnosLinea'
import { EstadoTurnoBadge } from './EstadoTurnoBadge'
import { ProduccionSection } from './ProduccionSection'
import { TurnoAcciones } from './TurnoAcciones'

export function TurnoDetallePage() {
  const { id } = useParams<{ id: string }>()
  const turnoQuery = useTurnoLinea(id)
  const plantas = usePlantas()
  const permisos = usePermisos()

  const volver = (
    <Link to="/turnos" className="text-sm font-medium text-teal-700 hover:underline">
      ← Turnos
    </Link>
  )

  if (turnoQuery.isPending) return <PageSpinner label="Cargando turno…" />
  if (turnoQuery.isError) {
    return (
      <div className="flex flex-col gap-4">
        {volver}
        <ErrorState error={turnoQuery.error} onRetry={() => void turnoQuery.refetch()} title="No se pudo cargar el turno" />
      </div>
    )
  }

  const turno = turnoQuery.data
  const planta = plantas.data?.find((p) => p.id === turno.plantaId)
  const zona = planta?.zonaHoraria ?? ZONA_DEFAULT
  const editable = turno.estado === 'Abierto' && permisos.puedeCapturarEnPlanta(turno.plantaId)

  return (
    <div className="flex flex-col gap-5">
      <div>
        {volver}
        <div className="mt-2 flex flex-wrap items-start justify-between gap-3">
          <div>
            <h1 className="flex flex-wrap items-center gap-2 text-xl font-semibold text-slate-900 sm:text-2xl">
              {turno.lineaCodigo} · {turno.turnoNombre}
              <EstadoTurnoBadge estado={turno.estado} />
            </h1>
            <p className="mt-1 text-sm text-slate-500">
              {planta ? `${planta.codigo} · ` : ''}
              {formatFechaOperativa(turno.fechaOperativa)} · {formatTime(turno.inicioUtc, zona)}–
              {formatTime(turno.finUtc, zona)}
              {turno.cerradoEnUtc && ` · cerrado ${formatDateTime(turno.cerradoEnUtc, zona)}`}
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <ExportarExcelButton
              nombreArchivo={`PlantPulse_Turno_${turno.lineaCodigo}_${turno.fechaOperativa}_T${turno.turnoNumero}`}
              hojas={() => [
                hoja<TurnoLinea>(
                  'OEE',
                  [
                    { header: 'Línea', valor: (t) => t.lineaCodigo },
                    { header: 'Fecha operativa', valor: (t) => t.fechaOperativa },
                    { header: 'Turno', valor: (t) => t.turnoNombre },
                    { header: 'Estado', valor: (t) => t.estado },
                    ...columnasOee<TurnoLinea>((t) => t.oee),
                  ],
                  [turno],
                ),
                hoja<RegistroProduccion>(
                  'Producción',
                  [
                    { header: 'Orden', valor: (r) => r.ordenNumero },
                    { header: 'Material', valor: (r) => r.materialCodigo },
                    { header: 'Ciclo ideal (s)', valor: (r) => r.tiempoCicloIdealSeg, formato: 'decimal' },
                    { header: 'Piezas totales', valor: (r) => r.piezasTotales, formato: 'entero' },
                    { header: 'Piezas buenas', valor: (r) => r.piezasBuenas, formato: 'entero' },
                    { header: 'Scrap', valor: (r) => r.scrap, formato: 'entero' },
                    { header: 'Registrado', valor: (r) => formatDateTime(r.registradoEnUtc, zona), ancho: 18 },
                    { header: 'Corregido', valor: (r) => (r.modificadoEnUtc ? formatDateTime(r.modificadoEnUtc, zona) : ''), ancho: 18 },
                  ],
                  turno.registros,
                ),
                hoja<ParoRegistrado>(
                  'Paros',
                  [
                    { header: 'Causa', valor: (p) => p.causaCodigo },
                    { header: 'Descripción', valor: (p) => p.causaDescripcion, ancho: 28 },
                    { header: 'Tipo', valor: (p) => TIPO_PARO_LABEL[p.tipo] },
                    { header: 'Inicio', valor: (p) => formatDateTime(p.inicioUtc, zona), ancho: 18 },
                    { header: 'Fin', valor: (p) => formatDateTime(p.finUtc, zona), ancho: 18 },
                    { header: 'Duración (min)', valor: (p) => p.duracionMin, formato: 'decimal' },
                    { header: 'Comentario', valor: (p) => p.comentario ?? '', ancho: 40 },
                  ],
                  turno.paros,
                ),
              ]}
            />
            <TurnoAcciones turno={turno} zona={zona} />
          </div>
        </div>
      </div>

      {turno.estado === 'Cerrado' && (
        <Alert variant="info">
          Turno cerrado: no admite cambios.{permisos.puedeReabrir ? ' Puedes reabrirlo para corregirlo.' : ' Solo un Admin puede reabrirlo.'}
        </Alert>
      )}
      {turno.estado === 'Abierto' && !permisos.puedeCapturarEnPlanta(turno.plantaId) && (
        <Alert variant="info">Vista de solo lectura.</Alert>
      )}

      <Card title="OEE del turno">
        <OeeResumen oee={turno.oee} />
      </Card>
      <ProduccionSection turno={turno} editable={editable} zona={zona} />
      <ParosSection turno={turno} editable={editable} zona={zona} />
    </div>
  )
}
