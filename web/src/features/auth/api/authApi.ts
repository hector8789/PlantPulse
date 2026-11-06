import { httpClient } from '@/api/httpClient'
import type { LoginRequest, LoginResponse } from '../types'

export async function login(body: LoginRequest): Promise<LoginResponse> {
  const { data } = await httpClient.post<LoginResponse>('/auth/login', body)
  return data
}

