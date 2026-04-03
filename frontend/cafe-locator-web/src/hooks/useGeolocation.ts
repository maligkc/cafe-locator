import { useCallback, useState } from 'react'

type GeolocationStatus = 'idle' | 'requesting' | 'granted' | 'denied' | 'error'

export interface GeolocationState {
  status: GeolocationStatus
  latitude: number | null
  longitude: number | null
  errorMessage: string | null
}

const initialState: GeolocationState = {
  status: 'idle',
  latitude: null,
  longitude: null,
  errorMessage: null,
}

export function useGeolocation() {
  const [state, setState] = useState<GeolocationState>(initialState)

  const requestLocation = useCallback(() => {
    if ('geolocation' in navigator === false) {
      setState({
        status: 'error',
        latitude: null,
        longitude: null,
        errorMessage: 'Tarayiciniz konum ozelligini desteklemiyor.',
      })
      return
    }

    setState((prevState) => ({
      ...prevState,
      status: 'requesting',
      errorMessage: null,
    }))

    navigator.geolocation.getCurrentPosition(
      (position) => {
        setState({
          status: 'granted',
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          errorMessage: null,
        })
      },
      (error) => {
        const status = error.code === error.PERMISSION_DENIED ? 'denied' : 'error'
        setState({
          status,
          latitude: null,
          longitude: null,
          errorMessage: error.message,
        })
      },
      {
        enableHighAccuracy: true,
        timeout: 10000,
        maximumAge: 30000,
      },
    )
  }, [])

  return {
    geolocation: state,
    requestLocation,
  }
}
