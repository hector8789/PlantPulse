import { z } from 'zod'

const ENV_HELP =
  'Crea el archivo .env.local en la raíz del proyecto (junto a package.json) con la línea ' +
  'VITE_API_URL=https://localhost:7177/api (sin comillas) y reinicia npm run dev.'

const envSchema = z.object({
  VITE_API_URL: z
    .string({ error: 'VITE_API_URL no está definida.' })
    .trim()
    .min(1, 'VITE_API_URL está vacía.')
    .pipe(z.url({ protocol: /^https?$/, error: 'VITE_API_URL no es una URL http(s) válida.' })),
})

const parsed = envSchema.safeParse(import.meta.env)

/** Mensaje de configuración inválida; main.tsx lo muestra en pantalla en lugar de dejarla en blanco. */
export const envError: string | null = parsed.success
  ? null
  : `${parsed.error.issues.map((i) => i.message).join(' ')} Valor recibido: ${JSON.stringify(
      import.meta.env.VITE_API_URL ?? null,
    )}. ${ENV_HELP}`

export const env = {
  apiUrl: parsed.success ? parsed.data.VITE_API_URL.replace(/\/+$/, '') : '',
} as const
