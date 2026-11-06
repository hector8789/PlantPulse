import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { getApiProblem, problemMessage } from '@/api/problem'
import { Alert, Button, Card, EmptyState, ErrorState, PageHeader, PageSpinner } from '@/components/ui'
import { usePlantaActiva } from '@/features/catalogos/api/usePlantaActiva'
import { cn } from '@/lib/cn'
import type { Guid } from '@/types/domain'
import { reportesKeys } from '../api/reportesKeys'
import { useEmbedInfo, useReportesPowerBi } from '../api/useReportesPowerBi'
import { keyDePagina, PAGINA_POR_KEY } from '../types'
import { PowerBiReport } from './PowerBiReport'

type Alcance = 'planta' | 'todas'

/** Mensaje claro cuando el API no puede generar el embed. */
function EmbedError({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  const p = getApiProblem(error)
  if (p?.status === 503) {
    return (
      <Alert
        variant="warning"
        title="Power BI no está disponible"
        actions={
          <Button size="sm" variant="secondary" onClick={onRetry}>
            Reintentar
          </Button>
        }
      >
        El servicio no pudo generar el reporte: falta capacidad o configuración del workspace, o Power BI no
        respondió. Mientras tanto, las cifras al día están en{' '}
        <Link to="/oee" className="font-medium underline">
          OEE
        </Link>
        .
      </Alert>
    )
  }
  if (p?.status === 404) {
    return <Alert variant="error" title="Reporte no encontrado">{problemMessage(p)}</Alert>
  }
  return <ErrorState error={error} onRetry={onRetry} title="No se pudo preparar el reporte" />
}

export function ReportesPage() {
  const qc = useQueryClient()
  const [params, setParams] = useSearchParams()
  const { plantas, plantaActiva } = usePlantaActiva()
  const [alcance, setAlcance] = useState<Alcance>('planta')
  const [intento, setIntento] = useState(0)
  const reportes = useReportesPowerBi()

  const lista = reportes.data ?? []
  const keyEnUrl = params.get('r')
  const keyActiva = lista.find((r) => r.key === keyEnUrl)?.key ?? lista[0]?.key ?? null
  const plantaId: Guid | null = alcance === 'planta' ? (plantaActiva?.id ?? null) : null
  const listoParaPedir = keyActiva !== null && (alcance === 'todas' || plantaId !== null)
  const embed = useEmbedInfo(listoParaPedir ? keyActiva : null, plantaId)

  const seleccionar = (key: string) => setParams({ r: key }, { replace: true })
  const renovarToken = () =>
    void qc.invalidateQueries({ queryKey: reportesKeys.embed(keyActiva ?? '', plantaId) })
  const reintentar = async () => {
    await qc.resetQueries({ queryKey: reportesKeys.embed(keyActiva ?? '', plantaId) })
    setIntento((n) => n + 1)
  }

  return (
    <>
      <PageHeader
        title="Reportes Power BI"
        description="Cada usuario ve solo sus plantas: el filtro lo aplica Power BI con el token (RLS)."
        actions={
          plantas.length > 1 ? (
            <div role="group" aria-label="Plantas del reporte" className="inline-flex rounded-md ring-1 ring-slate-300">
              {(
                [
                  ['planta', plantaActiva ? `Solo ${plantaActiva.codigo}` : 'Planta activa'],
                  ['todas', 'Todas mis plantas'],
                ] as const
              ).map(([valor, etiqueta]) => (
                <button
                  key={valor}
                  type="button"
                  aria-pressed={alcance === valor}
                  onClick={() => setAlcance(valor)}
                  className={cn(
                    'h-9 px-3 text-sm font-medium first:rounded-l-md last:rounded-r-md focus-visible:outline-2 focus-visible:outline-teal-600',
                    alcance === valor ? 'bg-teal-700 text-white' : 'bg-white text-slate-700 hover:bg-slate-50',
                  )}
                >
                  {etiqueta}
                </button>
              ))}
            </div>
          ) : undefined
        }
      />

      <Card>
        {reportes.isPending ? (
          <PageSpinner label="Cargando reportes…" />
        ) : reportes.isError ? (
          <EmbedError error={reportes.error} onRetry={() => void reportes.refetch()} />
        ) : lista.length === 0 ? (
          <EmptyState title="No hay reportes publicados" />
        ) : (
          <div className="flex flex-col gap-4">
            <div role="tablist" aria-label="Reportes" className="flex flex-wrap gap-1 border-b border-slate-200">
              {lista.map((r) => (
                <button
                  key={r.key}
                  role="tab"
                  type="button"
                  aria-selected={r.key === keyActiva}
                  onClick={() => seleccionar(r.key)}
                  className={cn(
                    '-mb-px border-b-2 px-3 py-2 text-sm font-medium focus-visible:outline-2 focus-visible:outline-teal-600',
                    r.key === keyActiva
                      ? 'border-teal-700 text-teal-800'
                      : 'border-transparent text-slate-600 hover:text-slate-900',
                  )}
                >
                  {r.nombre}
                </button>
              ))}
            </div>

            <p className="text-xs text-slate-500">
              Power BI se actualiza solo después de cerrar turnos (agrupado, hasta 8 veces al día), así que un
              turno recién cerrado puede tardar en aparecer. Las cifras al momento están en{' '}
              <Link to="/oee" className="font-medium text-teal-700 hover:underline">
                OEE
              </Link>
              .
            </p>

            {embed.isError && embed.data && (
              <Alert variant="warning">
                No se pudo renovar el acceso al reporte ({problemMessage(getApiProblem(embed.error))}). Se
                reintentará en unos segundos.
              </Alert>
            )}

            <div role="tabpanel">
              {alcance === 'planta' && !plantaActiva ? (
                <EmptyState title="Selecciona una planta" />
              ) : embed.isPending ? (
                <PageSpinner label="Preparando reporte…" />
              ) : embed.isError && !embed.data ? (
                <EmbedError error={embed.error} onRetry={() => void reintentar()} />
              ) : embed.data ? (
                <PowerBiReport
                  // Otro reporte, otra URL, otro alcance (RLS) o un reintento → se vuelve a incrustar.
                  key={`${embed.data.reportId}|${embed.data.embedUrl}|${plantaId ?? 'todas'}|${intento}`}
                  info={embed.data}
                  pagina={keyActiva ? PAGINA_POR_KEY[keyActiva] : undefined}
                  onPaginaCambiada={(displayName) => {
                    const key = keyDePagina(displayName)
                    if (key && key !== keyActiva && lista.some((r) => r.key === key)) seleccionar(key)
                  }}
                  onTokenInvalido={renovarToken}
                  onReintentar={() => void reintentar()}
                />
              ) : null}
            </div>
          </div>
        )}
      </Card>
    </>
  )
}
