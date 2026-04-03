import { useEffect, useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { AxiosError } from 'axios'
import { fetchNearbyCafes } from './api/cafes'
import { CafeList } from './components/CafeList'
import { FiltersPanel, type FiltersState } from './components/FiltersPanel'
import { MapView } from './components/MapView'
import { useGeolocation } from './hooks/useGeolocation'
import { appEnv } from './lib/env'
import type { SortBy } from './types/cafe'
import './App.css'

const defaultFilters: FiltersState = {
  radiusMeters: 1500,
  minRating: 0,
  openNow: false,
  sortBy: 'Distance',
}

function App() {
  const { geolocation, requestLocation } = useGeolocation()
  const [filters, setFilters] = useState<FiltersState>(defaultFilters)
  const [selectedPlaceId, setSelectedPlaceId] = useState<string | null>(null)
  const [isRouteVisible, setIsRouteVisible] = useState(false)

  const hasLocation = geolocation.latitude !== null && geolocation.longitude !== null

  const query = useMemo(() => {
    if (hasLocation === false) {
      return null
    }

    return {
      latitude: geolocation.latitude!,
      longitude: geolocation.longitude!,
      radiusMeters: filters.radiusMeters,
      minRating: Number(filters.minRating.toFixed(1)),
      openNow: filters.openNow,
      sortBy: filters.sortBy as SortBy,
      limit: 20,
    }
  }, [filters, geolocation.latitude, geolocation.longitude, hasLocation])

  const cafesQuery = useQuery({
    queryKey: ['cafes-nearby', query],
    queryFn: () => fetchNearbyCafes(query!),
    enabled: query !== null,
    staleTime: 60_000,
    gcTime: 5 * 60_000,
    retry: 1,
  })

  useEffect(() => {
    const firstCafe = cafesQuery.data?.cafes[0]
    if (firstCafe !== undefined) {
      setSelectedPlaceId((current) => current ?? firstCafe.placeId)
    }
  }, [cafesQuery.data?.cafes])

  useEffect(() => {
    if (selectedPlaceId === null) {
      return
    }

    const element = document.querySelector<HTMLElement>(`[data-place-id="${selectedPlaceId}"]`)
    element?.scrollIntoView({ block: 'nearest', behavior: 'smooth' })
  }, [selectedPlaceId])

  const cafes = cafesQuery.data?.cafes ?? []
  const selectedCafe = useMemo(
    () => cafes.find((cafe) => cafe.placeId === selectedPlaceId) ?? null,
    [cafes, selectedPlaceId],
  )
  const backendErrorDetail = useMemo(() => {
    if (cafesQuery.error instanceof AxiosError) {
      const detail = cafesQuery.error.response?.data?.detail
      if (typeof detail === 'string' && detail.length > 0) {
        return detail
      }
    }

    return null
  }, [cafesQuery.error])

  return (
    <div className="app">
      <header className="hero">
        <div>
          <p className="eyebrow">Cafe Radar</p>
          <h1>Yakindaki kafeleri saniyeler icinde bul</h1>
          <p>
            Konumuna gore yakin kafeleri puan ve mesafeye gore sirala, harita ve listeyi ayni anda
            takip et.
          </p>
        </div>
        <button onClick={requestLocation} className="primary-btn">
          {geolocation.status === 'requesting' ? 'Konum aliniyor...' : 'Konumu Paylas'}
        </button>
      </header>

      <FiltersPanel
        filters={filters}
        onChange={(next) => {
          setSelectedPlaceId(null)
          setIsRouteVisible(false)
          setFilters(next)
        }}
        disabled={hasLocation === false}
      />

      {hasLocation === false && (
        <section className="state-card">
          <h2>Konum izni gerekli</h2>
          <p>
            Uygulama, yakin kafeleri gosterebilmek icin konum bilgine ihtiyac duyar. Tarayicidan konum
            izni ver ve tekrar dene.
          </p>
          {geolocation.errorMessage !== null && <p className="error-text">{geolocation.errorMessage}</p>}
        </section>
      )}

      {hasLocation && cafesQuery.isPending && (
        <section className="state-card">
          <h2>Yakin kafeler aranıyor</h2>
          <p>Filtrelerine uygun mekanlar Google Places uzerinden cekiliyor...</p>
        </section>
      )}

      {hasLocation && cafesQuery.isError && (
        <section className="state-card">
          <h2>Bir hata olustu</h2>
          <p>{backendErrorDetail ?? 'Veriler alinamadi. API anahtari, kota veya ag problemi olabilir.'}</p>
        </section>
      )}

      {hasLocation && cafesQuery.isSuccess && cafes.length === 0 && (
        <section className="state-card">
          <h2>Sonuc bulunamadi</h2>
          <p>Yaricapi arttirabilir ya da minimum puani dusurebilirsin.</p>
        </section>
      )}

      {hasLocation && cafes.length > 0 && (
        <section className="results-layout">
          <aside>
            <div className="results-meta">
              <span>{cafes.length} sonuc</span>
              <span>{cafesQuery.data?.fromCache ? 'Cache' : 'Canli veri'}</span>
            </div>
            {selectedCafe !== null && (
              <button
                className="route-btn"
                onClick={() => setIsRouteVisible((currentState) => !currentState)}
              >
                {isRouteVisible ? 'Yol Tarifini Gizle' : 'Yol Tarifi Olustur'}
              </button>
            )}
            <CafeList
              cafes={cafes}
              selectedPlaceId={selectedPlaceId}
              onSelect={(placeId) => {
                setSelectedPlaceId(placeId)
                setIsRouteVisible(false)
              }}
            />
          </aside>

          <div className="map-panel">
            <MapView
              mapsApiKey={appEnv.googleMapsApiKey}
              latitude={geolocation.latitude!}
              longitude={geolocation.longitude!}
              radiusMeters={filters.radiusMeters}
              cafes={cafes}
              selectedPlaceId={selectedPlaceId}
              showRoute={isRouteVisible}
              routeCafe={selectedCafe}
              onSelect={(placeId) => {
                setSelectedPlaceId(placeId)
                setIsRouteVisible(false)
              }}
            />
          </div>
        </section>
      )}
    </div>
  )
}

export default App
