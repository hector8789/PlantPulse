import { Alert, OeeBadge } from '@/components/ui'
import { formatInt, formatMinutes, formatPercent } from '@/lib/format'
import { sinProduccion } from '@/lib/oee'
import type { Oee } from '@/types/domain'

function Kpi({ label, valor, detalle }: { label: string; valor: string; detalle?: string }) {
  return (
    <div className="rounded-md bg-slate-50 px-3 py-2.5 ring-1 ring-slate-200">
      <dt className="text-xs font-medium tracking-wide text-slate-500 uppercase">{label}</dt>
      <dd className="mt-0.5 text-lg font-semibold text-slate-900 tabular-nums">{valor}</dd>
      {detalle && <dd className="text-xs text-slate-500">{detalle}</dd>}
    </div>
  )
}

/** Desglose del OEE: disponibilidad × desempeño × calidad, con tiempos y piezas. */
export function OeeResumen({ oee }: { oee: Oee }) {
  const sinDatos = sinProduccion(oee)
  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-3">
        <span className="text-sm font-medium text-slate-600">OEE</span>
        <span className="text-3xl font-bold text-slate-900 tabular-nums">{formatPercent(oee.oee)}</span>
        <OeeBadge oee={oee.oee} sinDatos={sinDatos} soloNivel />
      </div>
      <dl className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-6">
        <Kpi
          label="Disponibilidad"
          valor={formatPercent(oee.disponibilidad)}
          detalle={`${formatMinutes(oee.tiempoOperativoMin)} de ${formatMinutes(oee.tiempoPlanificadoMin)}`}
        />
        <Kpi label="Desempeño" valor={formatPercent(oee.desempeno)} />
        <Kpi
          label="Calidad"
          valor={formatPercent(oee.calidad)}
          detalle={`${formatInt(oee.piezasBuenas)} de ${formatInt(oee.piezasTotales)} pzs`}
        />
        <Kpi label="Scrap" valor={`${formatInt(oee.scrap)} pzs`} />
        <Kpi label="Paros no planif." valor={formatMinutes(oee.parosNoPlanificadosMin)} />
        <Kpi label="Paros planif." valor={formatMinutes(oee.parosPlanificadosMin)} />
      </dl>
      {oee.desempenoExcedido && (
        <Alert variant="warning" title="Revisar ciclo ideal o conteo">
          El desempeño calculado pasó de 100 % y se limitó: las piezas capturadas superan lo posible con el tiempo
          de ciclo ideal de la orden.
        </Alert>
      )}
    </div>
  )
}
