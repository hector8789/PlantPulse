import { useQuery } from '@tanstack/react-query'
import { httpClient } from '@/api/httpClient'

export interface ApiHealth {
  service: string
  status: string
  timestampUtc: string
}

export const healthKeys = {
  all: ['health'] as const,
}

async function getHealth(): Promise<ApiHealth> {
  const { data } = await httpClient.get<ApiHealth>('/health')
  return data
}

export function useApiHealth() {
  return useQuery({
    queryKey: healthKeys.all,
    queryFn: getHealth,
    refetchInterval: 60_000,
    retry: false,
  })
}
