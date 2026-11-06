import { useState } from 'react'
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
import { useLineas } from '@/features/catalogos/api/useCatalogos'
import { usePlantaActiva } from '@/features/catalogos/api/usePlantaActiva'
import { hoyEnZona, sumarDias } from '@/lib/dates'
import { formatFechaOperativa, formatInt, formatPercent, ZONA_DEFAULT } from '@/lib/format'
import { sinProduccion } from '@/lib/oee'
import { hoja } from '@/lib/excel'
import { validarRango } from '@/lib/rangoFechas'
import { columnasOee } from '../excelOee'
import { useOeeKpi } from '../api/useOeeKpi'
import type { KpiFiltro, OeeKpiResponse, OeePorLinea, OeePorLineaTurno } from '../types'
import { OeeResumen } from './OeeResumen'

const MAX_DIAS_KPI = 92

function FilaOee({ fila, turnoNumero }: { fila: OeePorLinea; turnoNumero?: number }) {
  const { oee } = fila
  return (
    <Tr>
      <Td className="font-medium">{fila.lineaCodigo}</Td>
      {turnoNumero !== undefined && <Td align="center">{turnoNumero}</Td>}
      <Td align="right">{formatInt(fila.turnos)}</Td>
      <Td align="right">{formatPercent(oee.disponibilidad)}</Td>
      <Td align="right">{formatPercent(oee.desempeno)}</Td>
      <Td align="right">{formatPercent(oee.calidad)}</Td>
      <Td align="right">{formatInt(oee.scrap)}</Td>
      <Td align="right">
        <OeeBadge oee={oee.oee} sinDatos={sinProduccion(oee)} />
      </Td>
    </Tr>
  )
}

function TablaOee({ filas, conTurno }: { filas: (OeePorLinea | OeePorLineaTurno)[]; conTurno: boolean }) {
  return (
    <Table>
      <THead>
        <tr>
          <Th>Línea</Th>
          {conTurno && <Th align="center">Turno</Th>}
          <Th align="right">Turnos</Th>
          <Th align="right">Disponib.</Th>
          <Th align="right">Desempeño</Th>
          <Th align="right">Calidad</Th>
          <Th align="right">Scrap (pzs)</Th>
          <Th align="right">OEE</Th>
        </tr>
      </THead>
      <TBody>
        {filas.map((f) => (
          <FilaOee
            key={'turnoNumero' in f ? `${f.lineaId}-${f.turnoNumero}` : f.lineaId}
            fila={f}
            turnoNumero={'turnoNumero' in f ? f.turnoNumero : undefined}
          />
        ))}
      </TBody>
    </Table>
  )
}

export function OeePage() {
  const { plantaActiva } = usePlantaActiva()
  const zona = plantaActiva?.zonaHoraria ?? ZONA_DEFAULT
  const hoy = hoyEnZona(zona)
  const [filtros, setFiltros] = useState<FiltrosValor>(() => ({
    lineaId: '',
    desde: sumarDias(hoyEnZona(ZONA_DEFAULT), -6),
    hasta: hoyEnZona(ZONA_DEFAULT),
  }))
  const [plantaFiltro, setPlantaFiltro] = useState(plantaActiva?.id)
  // Al cambiar de planta, la línea seleccionada ya no aplica.
  if (plantaActiva?.id !== plantaFiltro) {
    setPlantaFiltro(plantaActiva?.id)
    setFiltros((f) => ({ ...f, lineaId: '' }))
  }

  const lineas = useLineas(plantaActiva?.id ?? null)
  const errorRango = validarRango(filtros.desde, filtros.hasta, MAX_DIAS_KPI)
  const filtro: KpiFiltro | null =
    plantaActiva && !errorRango
      ? {
          plantaId: plantaActiva.id,
          desde: filtros.desde,
          hasta: filtros.hasta,
          ...(filtros.lineaId ? { lineaId: filtros.lineaId } : {}),
        }
      : null
  const kpi = useOeeKpi(filtro)

  return (
    <>
      <PageHeader
        title="OEE por línea y turno"
        description={plantaActiva ? `${plantaActiva.codigo} · ${plantaActiva.nombre}` : undefined}
        actions={
          kpi.data && kpi.data.turnos > 0 && plantaActiva ? (
            <ExportarExcelButton
              nombreArchivo={`PlantPulse_OEE_${plantaActiva.codigo}_${kpi.data.desde}_${kpi.data.hasta}`}
              hojas={() => {
                const d = kpi.data
                return [
                  hoja(
                    'Total',
                    [
                      { header: 'Planta', valor: () => plantaActiva.codigo },
                      { header: 'Desde', valor: () => d.desde },
                      { header: 'Hasta', valor: () => d.hasta },
                      { header: 'Turnos', valor: () => d.turnos, formato: 'entero' },
                      ...columnasOee<OeeKpiResponse>((f) => f.total),
                    ],
                    [d],
                  ),
                  hoja(
                    'Por línea y turno',
                    [
                      { header: 'Línea', valor: (f) => f.lineaCodigo },
                      { header: 'Turno', valor: (f) => f.turnoNumero, formato: 'entero' },
                      { header: 'Turnos', valor: (f) => f.turnos, formato: 'entero' },
                      ...columnasOee<OeePorLineaTurno>((f) => f.oee),
                    ],
                    d.porLineaTurno,
                  ),
                  hoja(
                    'Por línea',
                    [
                      { header: 'Línea', valor: (f) => f.lineaCodigo },
                      { header: 'Turnos', valor: (f) => f.turnos, formato: 'entero' },
                      ...columnasOee<OeePorLinea>((f) => f.oee),
                    ],
                    d.porLinea,
                  ),
                ]
              }}
            />
          ) : undefined
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

        {!plantaActiva ? (
          <EmptyState title="Selecciona una planta">Elige una planta en la barra superior.</EmptyState>
        ) : kpi.isPending ? (
          <PageSpinner label="Calculando OEE…" />
        ) : kpi.isError ? (
          <ErrorState error={kpi.error} onRetry={() => void kpi.refetch()} />
        ) : kpi.data.turnos === 0 ? (
          <EmptyState title="Sin turnos en el periodo">
            No hay turnos capturados entre {formatFechaOperativa(kpi.data.desde)} y{' '}
            {formatFechaOperativa(kpi.data.hasta)}.
          </EmptyState>
        ) : (
          <div className={kpi.isPlaceholderData ? 'opacity-60 transition-opacity' : undefined}>
            <div className="flex flex-col gap-5">
              <Card
                title="Total del periodo"
                description={`${formatFechaOperativa(kpi.data.desde)} – ${formatFechaOperativa(kpi.data.hasta)} · ${formatInt(kpi.data.turnos)} turnos`}
              >
                <OeeResumen oee={kpi.data.total} />
              </Card>
              <Card title="Por línea y turno">
                <TablaOee filas={kpi.data.porLineaTurno} conTurno />
              </Card>
              <Card title="Por línea">
                <TablaOee filas={kpi.data.porLinea} conTurno={false} />
              </Card>
            </div>
          </div>
        )}
      </div>
    </>
  )
}
