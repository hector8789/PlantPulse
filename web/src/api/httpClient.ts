import axios from 'axios'
import { env } from '@/lib/env'
import { useAuthStore } from '@/stores/authStore'

export const httpClient = axios.create({
  baseURL: env.apiUrl,
  headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
  timeout: 20_000,
})

httpClient.interceptors.request.use((config) => {
  const token = useAuthStore.getState().accessToken
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

httpClient.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    // 401 con sesión activa = token vencido o inválido → logout. El login (sin token) maneja su propio 401.
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      const { accessToken, logout } = useAuthStore.getState()
      if (accessToken) logout('unauthorized')
    }
    return Promise.reject(error)
  },
)

export { getApiProblem, type ApiProblem } from './problem'
