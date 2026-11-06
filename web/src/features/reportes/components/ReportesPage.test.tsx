import { act, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AxiosError, AxiosHeaders, type AxiosResponse } from 'axios'
import { useEffect } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as catalogosApi from '@/features/catalogos/api/catalogosApi'
import { useAuthStore } from '@/stores/authStore'
import { renderWithRouter } from '@/test/utils'
import * as reportesApi from '../api/reportesApi'
import type { EmbedInfo } from '../types'
import { ReportesPage } from './ReportesPage'

// ── Reporte falso de Power BI ──
type Handler = (evento: { detail: unknown }) => void
const embebidos = { montajes: 0 }
const paginas = ['OEE', 'Pareto de paros', 'Scrap', 'Plan vs real'].map((displayName, i) => ({
  displayName,
  isActive: i === 0,
  setActive: vi.fn(async function (this: { isActive: boolean }) {
    paginas.forEach((p) => (p.isActive = false))
    this.isActive = true
  }),
}))
const handlers = new Map<string, Handler>()
const reporte = {
  on: (evento: string, h: Handler) => handlers.set(evento, h),
  off: (evento: string) => handlers.delete(evento),
  getPages: vi.fn(async () => paginas),
  setAccessToken: vi.fn(async () => undefined),
}
const emitir = (evento: string, detail: unknown = {}) => act(() => handlers.get(evento)?.({ detail }))

vi.mock('powerbi-client-react', () => ({
  PowerBIEmbed: ({ getEmbeddedComponent }: { getEmbeddedComponent: (r: unknown) => void }) => {
    // Igual que la librería real: el callback se invoca una vez, cuando se crea el embed.
    useEffect(() => {
      embebidos.montajes++
      getEmbeddedComponent(reporte)
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [])
    return <div data-testid="powerbi" />
  },
}))
vi.mock('../api/reportesApi', () => ({ getReportesPowerBi: vi.fn(), getEmbedInfo: vi.fn() }))
vi.mock('@/features/catalogos/api/catalogosApi', () => ({ getPlantas: vi.fn() }))

const PLANTA = 'p-leon'
const embed = (token: string): EmbedInfo => ({
  reportId: 'rep-1',
  embedUrl: 'https://app.powerbi.com/reportEmbed?reportId=rep-1',
  accessToken: token,
  expiresAtUtc: new Date(Date.now() + 60 * 60_000).toISOString(),
})

function error503(): AxiosError {
  const response = {
    status: 503,
    data: { title: 'Servicio no disponible', servicio: 'Power BI' },
    statusText: '',
    headers: {},
    config: { headers: new AxiosHeaders() },
  } as AxiosResponse
  return new AxiosError('fail', 'ERR', response.config, null, response)
}

const renderPagina = () => renderWithRouter([{ path: '/reportes', element: <ReportesPage /> }], '/reportes')

describe('ReportesPage (Power BI)', () => {
  beforeEach(() => {
    embebidos.montajes = 0
    handlers.clear()
    vi.clearAllMocks()
    paginas.forEach((p, i) => (p.isActive = i === 0))
    useAuthStore.getState().setSession({
      accessToken: 't',
      expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString(),
      user: { id: 'u', nombre: 'Sofía', email: 's@plantpulse.mx', roles: ['Supervisor'], plantaIds: [PLANTA] },
    })
    vi.mocked(catalogosApi.getPlantas).mockResolvedValue([
      { id: PLANTA, codigo: 'LEO', nombre: 'León', zonaHoraria: 'America/Mexico_City', turnos: [] },
    ])
    vi.mocked(reportesApi.getReportesPowerBi).mockResolvedValue([
      { key: 'oee', nombre: 'OEE' },
      { key: 'pareto-paros', nombre: 'Pareto de paros' },
      { key: 'scrap', nombre: 'Scrap' },
      { key: 'plan-vs-real', nombre: 'Plan vs real' },
    ])
  })

  it('pide el embed de la planta activa y cambia de página sin volver a incrustar', async () => {
    vi.mocked(reportesApi.getEmbedInfo).mockResolvedValue(embed('token-1'))
    renderPagina()
    await screen.findByTestId('powerbi')
    expect(reportesApi.getEmbedInfo).toHaveBeenCalledWith('oee', PLANTA)
    emitir('loaded')

    await userEvent.click(screen.getByRole('tab', { name: 'Pareto de paros' }))
    await waitFor(() => expect(paginas[1].setActive).toHaveBeenCalled())
    expect(screen.getByRole('tab', { name: 'Pareto de paros' })).toHaveAttribute('aria-selected', 'true')
    expect(embebidos.montajes).toBe(1)
  })

  it('renueva el token con setAccessToken sin recargar el reporte', async () => {
    vi.mocked(reportesApi.getEmbedInfo).mockResolvedValueOnce(embed('token-1')).mockResolvedValue(embed('token-2'))
    renderPagina()
    await screen.findByTestId('powerbi')
    emitir('loaded')

    // Power BI avisa que el token venció → se pide otro y se aplica en caliente.
    emitir('error', { message: 'TokenExpired', level: 6 })
    await waitFor(() => expect(reporte.setAccessToken).toHaveBeenCalledWith('token-2'))
    expect(embebidos.montajes).toBe(1)
  })

  it('sincroniza la pestaña cuando el usuario navega dentro del reporte', async () => {
    vi.mocked(reportesApi.getEmbedInfo).mockResolvedValue(embed('token-1'))
    renderPagina()
    await screen.findByTestId('powerbi')
    emitir('loaded')
    emitir('pageChanged', { newPage: { displayName: 'Scrap' } })
    await waitFor(() =>
      expect(screen.getByRole('tab', { name: 'Scrap' })).toHaveAttribute('aria-selected', 'true'),
    )
  })

  it('muestra un error fatal del embed con opción de reintentar', async () => {
    vi.mocked(reportesApi.getEmbedInfo).mockResolvedValue(embed('token-1'))
    renderPagina()
    await screen.findByTestId('powerbi')
    emitir('error', { message: 'Capacidad no disponible', level: 6 })
    expect(await screen.findByText('No se pudo mostrar el reporte')).toBeInTheDocument()
    expect(screen.getByText('Capacidad no disponible')).toBeInTheDocument()
  })

  it('con 503 del API explica que Power BI no está disponible', async () => {
    vi.mocked(reportesApi.getEmbedInfo).mockRejectedValue(error503())
    renderPagina()
    expect(await screen.findByText('Power BI no está disponible')).toBeInTheDocument()
    expect(screen.queryByTestId('powerbi')).not.toBeInTheDocument()
  })
})
