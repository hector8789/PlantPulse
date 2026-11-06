import { Link } from 'react-router'

export function NotFoundPage() {
  return (
    <div className="flex min-h-[60vh] flex-col items-center justify-center px-4 text-center">
      <p className="text-5xl font-bold text-teal-700">404</p>
      <h1 className="mt-3 text-xl font-semibold text-slate-900">Página no encontrada</h1>
      <p className="mt-1 text-sm text-slate-500">La dirección no existe o se movió.</p>
      <Link to="/" className="mt-6 text-sm font-medium text-teal-700 underline-offset-4 hover:underline">
        Volver al inicio
      </Link>
    </div>
  )
}
