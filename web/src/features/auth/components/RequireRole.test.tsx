import { screen } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/stores/authStore'
import { renderWithRouter } from '@/test/utils'
import type { Rol } from '@/types/domain'
import { RequireAuth } from './RequireAuth'
import { RequireRole } from './RequireRole'

function sesion(roles: Rol[]) {
  useAuthStore.getState().setSession({
    accessToken: 'token',
    expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString(),
    user: { id: 'u', nombre: 'Prueba', email: 'p@plantpulse.mx', roles, plantaIds: ['p1'] },
  })
}

const routes = [
  { path: '/login', element: <p>Login</p> },
  { path: '/sin-permiso', element: <p>Sin permiso</p> },
  {
    element: <RequireAuth />,
    children: [
      {
        element: <RequireRole roles={['Admin', 'Supervisor']} />,
        children: [{ path: '/turnos/nuevo', element: <p>Abrir turno</p> }],
      },
    ],
  },
]

describe('rutas protegidas', () => {
  beforeEach(() => useAuthStore.getState().logout())

  it('sin sesión manda al login', () => {
    renderWithRouter(routes, '/turnos/nuevo')
    expect(screen.getByText('Login')).toBeInTheDocument()
  })

  it('con rol Consulta muestra sin permiso', () => {
    sesion(['Consulta'])
    renderWithRouter(routes, '/turnos/nuevo')
    expect(screen.getByText('Sin permiso')).toBeInTheDocument()
  })

  it('con rol Supervisor entra', () => {
    sesion(['Supervisor'])
    renderWithRouter(routes, '/turnos/nuevo')
    expect(screen.getByText('Abrir turno')).toBeInTheDocument()
  })

  it('con token vencido manda al login', () => {
    useAuthStore.getState().setSession({
      accessToken: 'token',
      expiresAtUtc: new Date(Date.now() - 1000).toISOString(),
      user: { id: 'u', nombre: 'P', email: 'p@x.mx', roles: ['Admin'], plantaIds: [] },
    })
    renderWithRouter(routes, '/turnos/nuevo')
    expect(screen.getByText('Login')).toBeInTheDocument()
  })
})
