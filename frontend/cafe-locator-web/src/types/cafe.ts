export type SortBy = 'Distance' | 'Rating'

export interface NearbyCafeQuery {
  latitude: number
  longitude: number
  radiusMeters: number
  minRating: number
  openNow: boolean
  sortBy: SortBy
  limit: number
}

export interface CafeItem {
  placeId: string
  name: string
  address: string
  latitude: number
  longitude: number
  rating: number | null
  userRatingCount: number | null
  isOpenNow: boolean | null
  distanceMeters: number
  photoName: string | null
  photoProxyUrl: string | null
}

export interface NearbyCafeResponse {
  cafes: CafeItem[]
  totalCount: number
  generatedAtUtc: string
  fromCache: boolean
  query: NearbyCafeQuery
}
