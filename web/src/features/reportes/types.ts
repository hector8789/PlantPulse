import type { IsoUtc } from '@/types/domain'

/** GET /api/powerbi/reportes */
export interface ReportePowerBi {
  key: string
  nombre: string
}

/** GET /api/powerbi/embed/{reportKey}?plantaId= */
export interface EmbedInfo {
  reportId: string
  embedUrl: string
  accessToken: string
  expiresAtUtc: IsoUtc
}

/**
 * Hoy las 4 keys apuntan al mismo reporte con 4 páginas. Este mapa dice qué página abrir para cada key.
 * Si una key no aparece aquí, se deja la página que el reporte tenga por defecto.
 */
export const PAGINA_POR_KEY: Readonly<Record<string, string>> = {
  oee: 'OEE',
  'pareto-paros': 'Pareto de paros',
  scrap: 'Scrap',
  'plan-vs-real': 'Plan vs real',
}

export function keyDePagina(displayName: string): string | undefined {
  return Object.entries(PAGINA_POR_KEY).find(([, pagina]) => pagina === displayName)?.[0]
}
