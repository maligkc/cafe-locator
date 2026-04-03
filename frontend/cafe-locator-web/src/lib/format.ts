export function formatDistance(meters: number): string {
  if (meters < 1000) {
    return `${meters} m`
  }

  return `${(meters / 1000).toFixed(1)} km`
}

export function formatRating(rating: number | null, votes: number | null): string {
  if (rating === null) {
    return 'Puan yok'
  }

  if (votes === null) {
    return `${rating.toFixed(1)} / 5`
  }

  return `${rating.toFixed(1)} / 5 (${votes})`
}
