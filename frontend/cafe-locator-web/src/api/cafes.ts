import axios from 'axios'
import { appEnv } from '../lib/env'
import type { NearbyCafeQuery, NearbyCafeResponse } from '../types/cafe'

const api = axios.create({
  baseURL: appEnv.apiBaseUrl,
  timeout: 10000,
})

export async function fetchNearbyCafes(query: NearbyCafeQuery): Promise<NearbyCafeResponse> {
  const response = await api.get<NearbyCafeResponse>('/api/v1/cafes/nearby', {
    params: query,
  })

  return response.data
}

export function buildPhotoUrl(photoProxyUrl: string | null): string | null {
  if (photoProxyUrl === null) {
    return null
  }

  if (photoProxyUrl.startsWith('http')) {
    return photoProxyUrl
  }

  return `${appEnv.apiBaseUrl}${photoProxyUrl}`
}
