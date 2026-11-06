import type { Guid } from '@/types/domain'

export const reportesKeys = {
  all: ['powerbi'] as const,
  lista: () => [...reportesKeys.all, 'reportes'] as const,
  embed: (reportKey: string, plantaId: Guid | null) =>
    [...reportesKeys.all, 'embed', reportKey, plantaId ?? 'todas'] as const,
}
