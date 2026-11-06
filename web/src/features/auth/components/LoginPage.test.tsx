import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AxiosError, AxiosHeaders, type AxiosResponse } from 'axios'
import { Navigate } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/stores/authStore'
import { renderWithRouter } from '@/test/utils'
import * as authApi from '../api/authApi'
import { LoginPage } from './LoginPage'

vi.mock('../api/authApi', () => ({ login: vi.fn() }))

function respuesta(status: number, data: unknown): AxiosError {
  const response = { status, data, statusText: '', headers: {}, config: { headers: new AxiosHeaders() } } as AxiosResponse
  return new AxiosError('fail', 'ERR', response.config, null, response)
}

const routes = [
  { path: '/login', element: <LoginPage /> },
  { path: '/', element: <Navigate to="/oee" replace /> },
  { path: '/oee', element: <p>Panel OEE</p> },
]

describe('LoginPage', () => {
  beforeEach(() => {
    useAuthStore.getState().logout()
    vi.mocked(authApi.login).mockReset()
  })

  it('valida en el cliente antes de llamar al API', async () => {
    renderWithRouter(routes, '/login')
    await userEvent.click(screen.getByRole('button', { name: 'Entrar' }))
    expect(await screen.findByText('Escribe tu correo.')).toBeInTheDocument()
    expect(authApi.login).not.toHaveBeenCalled()
  })

  it('muestra el 422 del API en el campo (PascalCase normalizado)', async () => {
    vi.mocked(authApi.login).mockRejectedValue(respuesta(422, { title: 'Validación', errors: { Email: ['Usuario bloqueado.'] } }))
    renderWithRouter(routes, '/login')
    await userEvent.type(screen.getByLabelText(/Correo/), 'admin@plantpulse.mx')
    await userEvent.type(screen.getByLabelText(/Contraseña/), 'x')
    await userEvent.click(screen.getByRole('button', { name: 'Entrar' }))
    expect(await screen.findByText('Usuario bloqueado.')).toBeInTheDocument()
  })

  it('muestra credenciales incorrectas con 401', async () => {
    vi.mocked(authApi.login).mockRejectedValue(respuesta(401, { title: 'Unauthorized' }))
    renderWithRouter(routes, '/login')
    await userEvent.type(screen.getByLabelText(/Correo/), 'admin@plantpulse.mx')
    await userEvent.type(screen.getByLabelText(/Contraseña/), 'mala')
    await userEvent.click(screen.getByRole('button', { name: 'Entrar' }))
    expect(await screen.findByText('Correo o contraseña incorrectos.')).toBeInTheDocument()
  })

  it('con login exitoso guarda la sesión y entra', async () => {
    vi.mocked(authApi.login).mockResolvedValue({
      accessToken: 't',
      expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString(),
      user: { id: 'u', nombre: 'Ana', email: 'a@plantpulse.mx', roles: ['Supervisor'], plantaIds: ['p1'] },
    })
    renderWithRouter(routes, '/login')
    await userEvent.type(screen.getByLabelText(/Correo/), 'a@plantpulse.mx')
    await userEvent.type(screen.getByLabelText(/Contraseña/), 'PlantPulse2026!')
    await userEvent.click(screen.getByRole('button', { name: 'Entrar' }))
    // Sin state.from el destino es "/", que redirige a /oee.
    expect(await screen.findByText('Panel OEE')).toBeInTheDocument()
    expect(useAuthStore.getState().plantaActivaId).toBe('p1')
  })
})
