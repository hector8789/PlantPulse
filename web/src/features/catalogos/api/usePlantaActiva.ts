import { useEffect, useMemo } from 'react'
import { isAdmin, useAuthStore } from '@/stores/authStore'
import type { Planta } from '../types'
import { usePlantas } from './useCatalogos'

/**
 * Plantas que el usuario puede ver y la planta activa del header.
 * Admin ve todas las plantas; los demás roles solo las de `plantaIds`.
 */
export function usePlantaActiva() {
  const user = useAuthStore((s) => s.user)
  const plantaActivaId = useAuthStore((s) => s.plantaActivaId)
  const setPlantaActiva = useAuthStore((s) => s.setPlantaActiva)
  const plantasQuery = usePlantas(user !== null)

  const permitidas = useMemo<Planta[]>(() => {
    const todas = plantasQuery.data ?? []
    if (!user) return []
    if (isAdmin(user)) return todas
    return todas.filter((p) => user.plantaIds.includes(p.id))
  }, [plantasQuery.data, user])

  const plantaActiva = permitidas.find((p) => p.id === plantaActivaId) ?? permitidas[0] ?? null

  // Si la planta guardada ya no es válida, se corrige a la primera permitida.
  useEffect(() => {
    if (plantaActiva && plantaActiva.id !== plantaActivaId) setPlantaActiva(plantaActiva.id)
  }, [plantaActiva, plantaActivaId, setPlantaActiva])

  return {
    plantas: permitidas,
    plantaActiva,
    setPlantaActiva,
    isLoading: plantasQuery.isPending,
    error: plantasQuery.error,
    refetch: plantasQuery.refetch,
  }
}
