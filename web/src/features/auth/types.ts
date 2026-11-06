import type { Guid, IsoUtc, Rol } from '@/types/domain'

export interface LoginRequest {
  email: string
  password: string
}

export interface LoginResponse {
  accessToken: string
  expiresAtUtc: IsoUtc
  user: {
    id: Guid
    nombre: string
    email: string
    roles: Rol[]
    plantaIds: Guid[]
  }
}

