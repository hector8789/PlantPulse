import { useState } from 'react'
import { Link } from 'react-router'
import { FiltrosLineaFechas, type FiltrosValor } from '@/components/filters/FiltrosLineaFechas'
import {
  Card,
  EmptyState,
  ExportarExcelButton,
  ErrorState,
  OeeBadge,
  PageHeader,
  PageSpinner,
  Table,
  TBody,
  Td,
  Th,
  THead,
  Tr,
} from '@/components/ui'
import { usePermisos } from '@/features/auth/api/usePermisos'
import { useLineas } from '@/features/catalogos/api/useCatalogos'
import { usePlantaActiva } from '@/features/catalogos/api/usePlantaActiva'
import { SincronizarErpButton } from '@/features/catalogos/components/SincronizarErpButton'
import { hoyEnZona, sumarDias } from '@/lib/dates'
import { formatDateTime, formatFechaOperativa, formatInt, formatTime, ZONA_DEFAULT } from '@/lib/format'
import { sinProduccion } from '@/lib/oee'
import { columnasOee } from '@/features/kpis/excelOee'
import { hoja } from '@/lib/excel'
import { validarRango } from '@/lib/rangoFechas'
import { useTurnosLinea } from '../api/useTurnosLinea'
import type { TurnoLineaResumen, TurnosLineaFiltro } from '../types'
import { EstadoTurnoBadge } from './EstadoTurnoBadge'

const MAX_DIAS_TURNOS = 62

function contarParos(paros: TurnoLineaResumen['paros']): number {
  return Array.isArray(paros) ? paros.length : paros
}

export function TurnosPage() {
  const { plantaActiva } = usePlantaActiva()
  const permisos = usePermisos()
  const zona = plantaActiva?.zonaHoraria ?? ZONA_DEFAULT
  const hoy = hoyEnZona(zona)
  const [filtros, setFiltros] = useState<FiltrosValor>(() => ({
    lineaId: '',
    desde: sumarDias(hoyEnZona(ZONA_DEFAULT), -6),
    hasta: hoyEnZona(ZONA_DEFAULT),
  }))
  const [plantaFiltro, setPlantaFiltro] = useState(plantaActiva?.id)
  if (plantaActiva?.id !== plantaFiltro) {
    setPlantaFiltro(plantaActiva?.id)
    setFiltros((f) => ({ ...f, lineaId: '' }))
  }

  const lineas = useLineas(plantaActiva?.id ?? null)
  const errorRango = validarRango(filtros.desde, filtros.hasta, MAX_DIAS_TURNOS)
  const filtro: TurnosLineaFiltro | null =
    plantaActiva && !errorRango
      ? {
          plantaId: plantaActiva.id,
          desde: filtros.desde,
          hasta: filtros.hasta,
          ...(filtros.lineaId ? { lineaId: filtros.lineaId } : {}),
        }
      : null
  const turnos = useTurnosLinea(filtro)
  const puedeCapturar = plantaActiva !== null && permisos.puedeCapturarEnPlanta(plantaActiva.id)

  const ordenados = [...(turnos.data ?? [])].sort(
    (a, b) =>
      b.fechaOperativa.localeCompare(a.fechaOperativa) ||
      a.lineaCodigo.localeCompare(b.lineaCodigo) ||
      a.turnoNumero - b.turnoNumero,
  )

  return (
    <>
      <PageHeader
        title="Turnos y captura"
        description="Producción y paros por línea y turno"
        actions={
          <>
            {ordenados.length > 0 && plantaActiva && filtro && (
              <ExportarExcelButton
                nombreArchivo={`PlantPulse_Turnos_${plantaActiva.codigo}_${filtro.desde}_${filtro.hasta}`}
                hojas={() => [
                  hoja<TurnoLineaResumen>(
                    'Turnos',
                    [
                      { header: 'Fecha operativa', valor: (t) => t.fechaOperativa },
                      { header: 'Línea', valor: (t) => t.lineaCodigo },
                      { header: 'Turno', valor: (t) => t.turnoNumero, formato: 'entero' },
                      { header: 'Inicio', valor: (t) => formatDateTime(t.inicioUtc, zona), ancho: 18 },
                      { header: 'Fin', valor: (t) => formatDateTime(t.finUtc, zona), ancho: 18 },
                      { header: 'Estado', valor: (t) => t.estado },
                      { header: 'Paros', valor: (t) => contarParos(t.paros), formato: 'entero' },
                      ...columnasOee<TurnoLineaResumen>((t) => t.oee),
                    ],
                    ordenados,
                  ),
                ]}
              />
            )}
            {puedeCapturar && plantaActiva && (
            <>
              <SincronizarErpButton plantaId={plantaActiva.id} />
              <Link
                to="/turnos/nuevo"
                className="inline-flex h-10 items-center rounded-md bg-teal-700 px-4 text-sm font-medium text-white hover:bg-teal-800 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-teal-700"
              >
                Abrir turno
              </Link>
            </>
            )}
          </>
        }
      />
      <div className="flex flex-col gap-5">
        <Card>
          <FiltrosLineaFechas
            valor={filtros}
            onChange={setFiltros}
            lineas={lineas.data ?? []}
            lineasCargando={lineas.isPending && plantaActiva !== null}
            max={hoy}
            error={errorRango}
          />
        </Card>
        <Card title="Turnos">
          {!plantaActiva ? (
            <EmptyState title="Selecciona una planta" />
          ) : turnos.isPending ? (
            <PageSpinner label="Cargando turnos…" />
          ) : turnos.isError ? (
            <ErrorState error={turnos.error} onRetry={() => void turnos.refetch()} />
          ) : ordenados.length === 0 ? (
            <EmptyState title="No hay turnos en el periodo">
              {puedeCapturar ? 'Usa "Abrir turno" para empezar la captura.' : 'Prueba con otro rango de fechas.'}
            </EmptyState>
          ) : (
            <div className={turnos.isPlaceholderData ? 'opacity-60' : undefined}>
              <Table>
                <THead>
                  <tr>
                    <Th>Fecha operativa</Th>
                    <Th>Línea</Th>
                    <Th align="center">Turno</Th>
                    <Th>Horario</Th>
                    <Th>Estado</Th>
                    <Th align="right">Piezas</Th>
                    <Th align="right">Paros</Th>
                    <Th align="right">OEE</Th>
                    <Th>
                      <span className="sr-only">Acciones</span>
                    </Th>
                  </tr>
                </THead>
                <TBody>
                  {ordenados.map((t) => (
                    <Tr key={t.id}>
                      <Td>{formatFechaOperativa(t.fechaOperativa)}</Td>
                      <Td className="font-medium">{t.lineaCodigo}</Td>
                      <Td align="center">{t.turnoNumero}</Td>
                      <Td className="text-slate-600">
                        {formatTime(t.inicioUtc, zona)} – {formatTime(t.finUtc, zona)}
                      </Td>
                      <Td>
                        <EstadoTurnoBadge estado={t.estado} />
                      </Td>
                      <Td align="right">{formatInt(t.oee.piezasTotales)}</Td>
                      <Td align="right">{formatInt(contarParos(t.paros))}</Td>
                      <Td align="right">
                        <OeeBadge oee={t.oee.oee} sinDatos={sinProduccion(t.oee)} />
                      </Td>
                      <Td align="right">
                        <Link
                          to={`/turnos/${t.id}`}
                          className="text-sm font-medium text-teal-700 hover:underline"
                          aria-label={`Ver turno ${t.turnoNumero} de ${t.lineaCodigo} del ${t.fechaOperativa}`}
                        >
                          {puedeCapturar && t.estado === 'Abierto' ? 'Capturar' : 'Ver'}
                        </Link>
                      </Td>
                    </Tr>
                  ))}
                </TBody>
              </Table>
            </div>
          )}
        </Card>
      </div>
    </>
  )
}
