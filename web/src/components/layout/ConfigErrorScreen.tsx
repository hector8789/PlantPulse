/** Se muestra cuando falta o es inválida la configuración de .env.local. */
export function ConfigErrorScreen({ message }: { message: string }) {
  return (
    <main className="flex min-h-dvh items-center justify-center bg-slate-100 px-4">
      <div role="alert" className="w-full max-w-xl rounded-lg bg-white p-6 shadow-sm ring-1 ring-red-200">
        <p className="text-sm font-semibold tracking-wide text-red-700 uppercase">Configuración inválida</p>
        <h1 className="mt-1 text-lg font-semibold text-slate-900">PlantPulse no puede iniciar</h1>
        <p className="mt-3 text-sm text-slate-700">{message}</p>
      </div>
    </main>
  )
}
