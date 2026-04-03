import { useState } from 'react'
import { buildPhotoUrl } from '../api/cafes'
import { formatDistance, formatRating } from '../lib/format'
import type { CafeItem } from '../types/cafe'

interface CafeListProps {
  cafes: CafeItem[]
  selectedPlaceId: string | null
  onSelect: (placeId: string) => void
}

export function CafeList({ cafes, selectedPlaceId, onSelect }: CafeListProps) {
  const [brokenPhotoIds, setBrokenPhotoIds] = useState<Record<string, boolean>>({})

  return (
    <ul className="cafe-list" aria-label="Kafe listesi">
      {cafes.map((cafe) => {
        const photoUrl = buildPhotoUrl(cafe.photoProxyUrl)
        const showFallback = photoUrl === null || brokenPhotoIds[cafe.placeId] === true
        const selected = selectedPlaceId === cafe.placeId

        return (
          <li key={cafe.placeId}>
            <button
              className={`cafe-card${selected ? ' selected' : ''}`}
              onClick={() => onSelect(cafe.placeId)}
              data-place-id={cafe.placeId}
            >
              <div className="cafe-card-media">
                {showFallback ? (
                  <div className="photo-fallback">Foto yok</div>
                ) : (
                  <img
                    src={photoUrl}
                    alt={cafe.name}
                    loading="lazy"
                    onError={() =>
                      setBrokenPhotoIds((prevState) => ({
                        ...prevState,
                        [cafe.placeId]: true,
                      }))
                    }
                  />
                )}
              </div>

              <div className="cafe-card-body">
                <header>
                  <h3>{cafe.name}</h3>
                  <span>{formatDistance(cafe.distanceMeters)}</span>
                </header>
                <p>{cafe.address}</p>
                <div className="meta">
                  <span>{formatRating(cafe.rating, cafe.userRatingCount)}</span>
                  <span>
                    {cafe.isOpenNow === null
                      ? 'Durum bilinmiyor'
                      : cafe.isOpenNow
                        ? 'Acik'
                        : 'Kapali'}
                  </span>
                </div>
              </div>
            </button>
          </li>
        )
      })}
    </ul>
  )
}
