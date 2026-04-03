import { z } from 'zod'

const envSchema = z.object({
  VITE_API_BASE_URL: z.string().url().optional(),
  VITE_GOOGLE_MAPS_API_KEY: z.string().optional(),
})

const parsed = envSchema.safeParse(import.meta.env)

if (parsed.success === false) {
  throw new Error('Gecersiz .env yapilandirmasi')
}

export const appEnv = {
  apiBaseUrl: parsed.data.VITE_API_BASE_URL ?? '',
  googleMapsApiKey: parsed.data.VITE_GOOGLE_MAPS_API_KEY ?? '',
}
