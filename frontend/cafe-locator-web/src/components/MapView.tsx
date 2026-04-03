import { useEffect, useMemo, useRef, useState } from 'react'
import {
  CircleF,
  DirectionsRenderer,
  DirectionsService,
  GoogleMap,
  MarkerF,
  useJsApiLoader,
} from '@react-google-maps/api'
import type { CafeItem } from '../types/cafe'

interface MapViewProps {
  mapsApiKey: string
  latitude: number
  longitude: number
  radiusMeters: number
  cafes: CafeItem[]
  selectedPlaceId: string | null
  showRoute: boolean
  routeCafe: CafeItem | null
  onSelect: (placeId: string) => void
}

const mapContainerStyle = {
  width: '100%',
  height: '100%',
}

export function MapView({
  mapsApiKey,
  latitude,
  longitude,
  radiusMeters,
  cafes,
  selectedPlaceId,
  showRoute,
  routeCafe,
  onSelect,
}: MapViewProps) {
  const mapRef = useRef<google.maps.Map | null>(null)
  const [directionsResult, setDirectionsResult] = useState<google.maps.DirectionsResult | null>(null)
  const [routeError, setRouteError] = useState<string | null>(null)
  const center = useMemo(() => ({ lat: latitude, lng: longitude }), [latitude, longitude])
  const routeRequest = useMemo(() => {
    if (!showRoute || routeCafe === null) {
      return null
    }

    return {
      origin: center,
      destination: {
        lat: routeCafe.latitude,
        lng: routeCafe.longitude,
      },
      travelMode: 'WALKING' as google.maps.TravelMode,
    }
  }, [center, routeCafe, showRoute])
  const shouldRequestDirections = routeRequest !== null && directionsResult === null && routeError === null

  const { isLoaded, loadError } = useJsApiLoader({
    id: 'google-map-script',
    googleMapsApiKey: mapsApiKey,
  })

  useEffect(() => {
    if (mapRef.current === null || selectedPlaceId === null) {
      return
    }

    const selectedCafe = cafes.find((cafe) => cafe.placeId === selectedPlaceId)
    if (selectedCafe === undefined) {
      return
    }

    mapRef.current.panTo({ lat: selectedCafe.latitude, lng: selectedCafe.longitude })
  }, [cafes, selectedPlaceId])

  useEffect(() => {
    if (!showRoute || routeCafe === null) {
      setDirectionsResult(null)
      setRouteError(null)
      return
    }

    setDirectionsResult(null)
    setRouteError(null)
  }, [showRoute, routeCafe?.placeId, latitude, longitude])

  if (mapsApiKey.length === 0) {
    return (
      <div className="map-empty">
        Harita gormek icin <code>VITE_GOOGLE_MAPS_API_KEY</code> tanimlayin.
      </div>
    )
  }

  if (loadError !== undefined) {
    return <div className="map-empty">Google Maps yuklenemedi.</div>
  }

  if (isLoaded === false) {
    return <div className="map-empty">Harita yukleniyor...</div>
  }

  return (
    <div className="map-root">
      <GoogleMap
        mapContainerStyle={mapContainerStyle}
        center={center}
        zoom={14}
        onLoad={(map) => {
          mapRef.current = map
        }}
        options={{
          clickableIcons: false,
          disableDefaultUI: true,
          zoomControl: true,
        }}
      >
        {shouldRequestDirections && (
          <DirectionsService
            options={routeRequest}
            callback={(result, status) => {
              if (status === google.maps.DirectionsStatus.OK && result !== null) {
                setDirectionsResult(result)
                setRouteError(null)
                return
              }

              if (status === google.maps.DirectionsStatus.ZERO_RESULTS) {
                setDirectionsResult(null)
                setRouteError('Bu hedefe uygun yaya rotasi bulunamadi.')
                return
              }

              if (status !== google.maps.DirectionsStatus.OK) {
                setDirectionsResult(null)
                setRouteError(getDirectionsErrorMessage(status))
              }
            }}
          />
        )}

        {showRoute && directionsResult !== null && (
          <DirectionsRenderer
            options={{
              directions: directionsResult,
              suppressMarkers: true,
              polylineOptions: {
                strokeColor: '#d64e3b',
                strokeOpacity: 0.85,
                strokeWeight: 5,
              },
            }}
          />
        )}

        <CircleF
          center={center}
          radius={radiusMeters}
          options={{
            fillColor: '#11a38f',
            fillOpacity: 0.1,
            strokeColor: '#11a38f',
            strokeOpacity: 0.9,
            strokeWeight: 1,
          }}
        />

        <MarkerF
          position={center}
          icon={{
            path: google.maps.SymbolPath.CIRCLE,
            fillColor: '#16423d',
            fillOpacity: 1,
            strokeColor: '#ffffff',
            strokeWeight: 2,
            scale: 8,
          }}
        />

        {cafes.map((cafe) => (
          <MarkerF
            key={cafe.placeId}
            position={{ lat: cafe.latitude, lng: cafe.longitude }}
            onClick={() => onSelect(cafe.placeId)}
            icon={{
              path: google.maps.SymbolPath.BACKWARD_CLOSED_ARROW,
              fillColor: cafe.placeId === selectedPlaceId ? '#d64e3b' : '#1f6e64',
              fillOpacity: 0.95,
              strokeColor: '#ffffff',
              strokeWeight: 1.5,
              scale: cafe.placeId === selectedPlaceId ? 7 : 6,
            }}
          />
        ))}
      </GoogleMap>

      {showRoute && routeError !== null && <p className="route-error">{routeError}</p>}
    </div>
  )
}

function getDirectionsErrorMessage(status: google.maps.DirectionsStatus | null): string {
  switch (status) {
    case google.maps.DirectionsStatus.REQUEST_DENIED:
      return 'Rota istegi reddedildi. Google Cloud tarafinda Directions API etkin ve browser key izinleri uygun olmali.'
    case google.maps.DirectionsStatus.OVER_QUERY_LIMIT:
      return 'Directions istegi kota limitine takildi. Biraz sonra tekrar deneyin.'
    case google.maps.DirectionsStatus.NOT_FOUND:
      return 'Baslangic veya hedef konumu bulunamadi.'
    case google.maps.DirectionsStatus.MAX_WAYPOINTS_EXCEEDED:
      return 'Rota istegindeki durak sayisi limiti asildi.'
    case google.maps.DirectionsStatus.INVALID_REQUEST:
      return 'Rota istegi gecersiz parametrelerle gonderildi.'
    case google.maps.DirectionsStatus.UNKNOWN_ERROR:
      return 'Google Directions gecici hata verdi. Lutfen tekrar deneyin.'
    default:
      return `Yol tarifi olusturulamadi (status: ${status ?? 'UNKNOWN'}).`
  }
}
