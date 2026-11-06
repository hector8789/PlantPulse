import { createBrowserRouter, Navigate } from 'react-router'
import { PageSpinner } from '@/components/ui'
import { AppLayout } from '@/components/layout/AppLayout'
import { LoginPage } from '@/features/auth/components/LoginPage'
import { RequireAuth } from '@/features/auth/components/RequireAuth'
import { RequireRole } from '@/features/auth/components/RequireRole'
import { NotFoundPage } from '@/pages/NotFoundPage'
import { SinPermisoPage } from '@/pages/SinPermisoPage'
import { ROLES_CAPTURA } from '@/types/domain'

// Las páginas se cargan bajo demanda para que el login y el layout carguen rápido.
const oee = () => import('@/features/kpis/components/OeePage').then((m) => ({ Component: m.OeePage }))
const turnos = () => import('@/features/produccion/components/TurnosPage').then((m) => ({ Component: m.TurnosPage }))
const abrirTurno = () =>
  import('@/features/produccion/components/AbrirTurnoPage').then((m) => ({ Component: m.AbrirTurnoPage }))
const turnoDetalle = () =>
  import('@/features/produccion/components/TurnoDetallePage').then((m) => ({ Component: m.TurnoDetallePage }))
const reportes = () => import('@/features/reportes/components/ReportesPage').then((m) => ({ Component: m.ReportesPage }))

export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  {
    element: <RequireAuth />,
    children: [
      {
        element: <AppLayout />,
        HydrateFallback: () => <PageSpinner />,
        children: [
          { index: true, element: <Navigate to="/oee" replace /> },
          { path: 'oee', lazy: oee },
          { path: 'turnos', lazy: turnos },
          {
            element: <RequireRole roles={ROLES_CAPTURA} />,
            children: [{ path: 'turnos/nuevo', lazy: abrirTurno }],
          },
          { path: 'turnos/:id', lazy: turnoDetalle },
          { path: 'reportes', lazy: reportes },
          { path: 'sin-permiso', element: <SinPermisoPage /> },
          { path: '*', element: <NotFoundPage /> },
        ],
      },
    ],
  },
])
