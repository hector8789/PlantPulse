import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { models, type Embed, type Report, type service } from 'powerbi-client'
import { PowerBIEmbed } from 'powerbi-client-react'
import { Alert, Button, Spinner } from '@/components/ui'
import { tokenVigente } from '../lib/tokenRenewal'
import type { EmbedInfo } from '../types'

interface Props {
  /** Datos del embed. El componente se monta con key={reportId|embedUrl|alcance}: si cambian, se vuelve a incrustar. */
  info: EmbedInfo
  /** displayName de la página que se debe mostrar */
  pagina?: string
  /** El usuario cambió de página desde la navegación del propio reporte */
  onPaginaCambiada?: (displayName: string) => void
  /** Power BI avisó que el token venció o es inválido: hay que pedir otro */
  onTokenInvalido?: () => void
  /** Reintentar tras un error fatal: el padre pide un token nuevo y vuelve a montar el componente */
  onReintentar: () => void
}

const ESPERA_LENTA_MS = 30_000

type Estado = { tipo: 'cargando' } | { tipo: 'listo' } | { tipo: 'error'; mensaje: string; detalle?: string }

function isRecord(v: unknown): v is Record<string, unknown> {
  return typeof v === 'object' && v !== null
}

/** Lee el detalle de un evento de Power BI sin asumir su forma. */
function detalleDe(evento: service.ICustomEvent<unknown> | undefined): Record<string, unknown> {
  return isRecord(evento?.detail) ? evento.detail : {}
}

function esErrorDeToken(detalle: Record<string, unknown>): boolean {
  const texto = `${String(detalle.message ?? '')} ${String(detalle.errorCode ?? '')}`.toLowerCase()
  return texto.includes('tokenexpired') || texto.includes('token expired') || texto.includes('invalidtoken')
}

export function PowerBiReport({ info, pagina, onPaginaCambiada, onTokenInvalido, onReintentar }: Props) {
  const reportRef = useRef<Report | null>(null)
  const cargadoRef = useRef(false)
  const tokenAplicadoRef = useRef(info.accessToken)
  const [estado, setEstado] = useState<Estado>({ tipo: 'cargando' })

  // Callbacks más recientes para los handlers, que se registran una sola vez.
  const callbacks = useRef({ pagina, onPaginaCambiada, onTokenInvalido })
  useLayoutEffect(() => {
    callbacks.current = { pagina, onPaginaCambiada, onTokenInvalido }
  })

  // powerbi-client-react vuelve a incrustar si embedConfig cambia (lo compara con isEqual),
  // así que se fija al montar. El token nuevo se aplica con setAccessToken (efecto de abajo).
  const [embedConfig] = useState<models.IReportEmbedConfiguration>(() => ({
    type: 'report',
    id: info.reportId,
    embedUrl: info.embedUrl,
    accessToken: info.accessToken,
    tokenType: models.TokenType.Embed,
    settings: {
      panes: { filters: { visible: false }, pageNavigation: { visible: true } },
      background: models.BackgroundType.Transparent,
      localeSettings: { language: 'es-MX', formatLocale: 'es-MX' },
    },
  }))

  const activarPagina = async (displayName: string | undefined) => {
    const report = reportRef.current
    if (!report || !cargadoRef.current || !displayName) return
    try {
      const pages = await report.getPages()
      const destino = pages.find((p) => p.displayName === displayName)
      if (destino && !destino.isActive) await destino.setActive()
    } catch {
      // Si la página no existe se queda la actual; no es un error para el usuario.
    }
  }

  // Los eventos se registran directo en el reporte (no con la prop eventHandlers) para leer
  // siempre los callbacks más recientes desde las refs.
  const registrarEventos = (report: Report) => {
    report.off('loaded')
    report.off('rendered')
    report.off('pageChanged')
    report.off('error')
    report.on('loaded', () => {
      cargadoRef.current = true
      void activarPagina(callbacks.current.pagina)
    })
    report.on('rendered', () => setEstado((e) => (e.tipo === 'error' ? e : { tipo: 'listo' })))
    report.on('pageChanged', (evento: service.ICustomEvent<unknown>) => {
      const nueva = detalleDe(evento).newPage
      if (isRecord(nueva) && typeof nueva.displayName === 'string') {
        callbacks.current.onPaginaCambiada?.(nueva.displayName)
      }
    })
    report.on('error', (evento: service.ICustomEvent<unknown>) => {
      const d = detalleDe(evento)
      if (esErrorDeToken(d)) {
        callbacks.current.onTokenInvalido?.()
        return
      }
      // Power BI emite errores menores de visuales; solo los fatales impiden ver el reporte.
      if (d.level !== models.TraceType.Fatal) return
      setEstado({
        tipo: 'error',
        mensaje: typeof d.message === 'string' ? d.message : 'Error de Power BI',
        detalle: typeof d.detailedMessage === 'string' ? d.detailedMessage : undefined,
      })
    })
  }

  // Si Power BI no termina de cargar (red, capacidad pausada), se ofrece reintentar en vez de girar sin fin.
  const [lento, setLento] = useState(false)
  useEffect(() => {
    if (estado.tipo !== 'cargando') return
    const id = window.setTimeout(() => setLento(true), ESPERA_LENTA_MS)
    return () => window.clearTimeout(id)
  }, [estado.tipo])

  // Renovación: aplica el token nuevo sin recargar el reporte.
  useEffect(() => {
    const report = reportRef.current
    if (!report || info.accessToken === tokenAplicadoRef.current || !tokenVigente(info.expiresAtUtc)) return
    tokenAplicadoRef.current = info.accessToken
    report.setAccessToken(info.accessToken).catch(() => callbacks.current.onTokenInvalido?.())
  }, [info.accessToken, info.expiresAtUtc])

  // Cambio de pestaña dentro del mismo reporte: solo se activa la página.
  useEffect(() => {
    void activarPagina(pagina)
  }, [pagina])

  return (
    <div className="relative">
      {estado.tipo === 'error' && (
        <Alert
          variant="error"
          title="No se pudo mostrar el reporte"
          className="mb-3"
          actions={
            <Button size="sm" variant="secondary" onClick={onReintentar}>
              Reintentar
            </Button>
          }
        >
          {estado.mensaje}
          {estado.detalle && <span className="mt-1 block text-xs opacity-80">{estado.detalle}</span>}
        </Alert>
      )}
      {estado.tipo === 'cargando' && (
        <div className="absolute inset-0 z-10 flex flex-col items-center justify-center gap-3 rounded-md bg-white/80 px-4 text-center text-slate-600">
          <div className="flex items-center gap-3">
            <Spinner className="size-5" label="Cargando reporte…" />
            <span aria-hidden="true">Cargando reporte de Power BI…</span>
          </div>
          {lento && (
            <div role="status" className="flex flex-col items-center gap-2 text-sm">
              <p>Está tardando más de lo normal. Revisa tu conexión o que la capacidad de Power BI esté activa.</p>
              <Button size="sm" variant="secondary" onClick={onReintentar}>
                Reintentar
              </Button>
            </div>
          )}
        </div>
      )}
      <PowerBIEmbed
        embedConfig={embedConfig}
        cssClassName="h-[70vh] min-h-[480px] w-full overflow-hidden rounded-md ring-1 ring-slate-200 [&>iframe]:border-0"
        getEmbeddedComponent={(embed: Embed) => {
          const report = embed as Report
          if (reportRef.current === report) return
          reportRef.current = report
          registrarEventos(report)
        }}
      />
    </div>
  )
}
