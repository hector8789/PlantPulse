import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Navigate, useLocation } from 'react-router'
import { getApiProblem, problemMessage } from '@/api/problem'
import { Alert, Button, Field, Input } from '@/components/ui'
import { applyServerErrors } from '@/lib/forms'
import { isSessionExpired, useAuthStore, type LogoutReason } from '@/stores/authStore'
import { useLogin } from '../api/useAuth'
import { loginSchema, type LoginFormValues } from '../schemas/loginSchema'

const MOTIVOS: Partial<Record<LogoutReason, string>> = {
  expired: 'Tu sesión expiró. Inicia sesión de nuevo.',
  unauthorized: 'Tu sesión ya no es válida. Inicia sesión de nuevo.',
}

function destinoSeguro(from: unknown): string {
  return typeof from === 'string' && from.startsWith('/') && !from.startsWith('//') ? from : '/'
}

export function LoginPage() {
  const location = useLocation()
  const state: unknown = location.state
  const from = destinoSeguro(
    typeof state === 'object' && state !== null && 'from' in state ? state.from : undefined,
  )
  const accessToken = useAuthStore((s) => s.accessToken)
  const expiresAtUtc = useAuthStore((s) => s.expiresAtUtc)
  const lastLogoutReason = useAuthStore((s) => s.lastLogoutReason)
  const loginMutation = useLogin()

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  })

  // Con sesión válida (también justo después de un login exitoso) se va al destino.
  if (accessToken && !isSessionExpired(expiresAtUtc)) {
    return <Navigate to={from} replace />
  }

  const onSubmit = (values: LoginFormValues) => {
    loginMutation.mutate(values, {
      onError: (error) => {
        if (applyServerErrors(error, setError, ['email', 'password'])) return
        const p = getApiProblem(error)
        setError('root.server', {
          message: p?.status === 401 ? 'Correo o contraseña incorrectos.' : problemMessage(p),
        })
      },
    })
  }

  const motivo = lastLogoutReason ? MOTIVOS[lastLogoutReason] : undefined

  return (
    <main className="flex min-h-dvh items-center justify-center bg-slate-100 px-4 py-10">
      <div className="w-full max-w-sm">
        <div className="mb-6 text-center">
          <p className="text-2xl font-bold tracking-tight text-teal-800">PlantPulse</p>
          <p className="mt-1 text-sm text-slate-500">KPIs de manufactura: OEE, scrap y paros</p>
        </div>
        <div className="rounded-lg bg-white p-6 shadow-sm ring-1 ring-slate-200">
          <h1 className="mb-4 text-lg font-semibold text-slate-900">Iniciar sesión</h1>
          {motivo && (
            <Alert variant="warning" className="mb-4">
              {motivo}
            </Alert>
          )}
          <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
            <Field label="Correo" error={errors.email?.message} required>
              {(c) => (
                <Input {...c} type="email" autoComplete="username" autoFocus {...register('email')} />
              )}
            </Field>
            <Field label="Contraseña" error={errors.password?.message} required>
              {(c) => (
                <Input {...c} type="password" autoComplete="current-password" {...register('password')} />
              )}
            </Field>
            {errors.root?.server && <Alert variant="error">{errors.root.server.message}</Alert>}
            <Button type="submit" loading={loginMutation.isPending} className="w-full">
              Entrar
            </Button>
          </form>
        </div>
        <p className="mt-4 text-center text-xs text-slate-500">
          Demo: supervisor.leon@plantpulse.mx · PlantPulse2026!
        </p>
      </div>
    </main>
  )
}
