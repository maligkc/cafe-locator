import type { SortBy } from '../types/cafe'

export interface FiltersState {
  radiusMeters: number
  minRating: number
  openNow: boolean
  sortBy: SortBy
}

interface FiltersPanelProps {
  filters: FiltersState
  onChange: (next: FiltersState) => void
  disabled?: boolean
}

export function FiltersPanel({ filters, onChange, disabled = false }: FiltersPanelProps) {
  return (
    <section className="filters-panel" aria-label="Filtreler">
      <div className="field">
        <label htmlFor="radius">Yaricap</label>
        <input
          id="radius"
          type="range"
          min={300}
          max={5000}
          step={100}
          value={filters.radiusMeters}
          onChange={(event) =>
            onChange({
              ...filters,
              radiusMeters: Number(event.target.value),
            })
          }
          disabled={disabled}
        />
        <span>{filters.radiusMeters} m</span>
      </div>

      <div className="field">
        <label htmlFor="rating">Minimum Puan</label>
        <input
          id="rating"
          type="range"
          min={0}
          max={5}
          step={0.5}
          value={filters.minRating}
          onChange={(event) =>
            onChange({
              ...filters,
              minRating: Number(event.target.value),
            })
          }
          disabled={disabled}
        />
        <span>{filters.minRating.toFixed(1)}</span>
      </div>

      <label className="switch">
        <input
          type="checkbox"
          checked={filters.openNow}
          onChange={(event) =>
            onChange({
              ...filters,
              openNow: event.target.checked,
            })
          }
          disabled={disabled}
        />
        <span>Sadece acik olanlar</span>
      </label>

      <div className="field">
        <label htmlFor="sortBy">Siralama</label>
        <select
          id="sortBy"
          value={filters.sortBy}
          onChange={(event) =>
            onChange({
              ...filters,
              sortBy: event.target.value as SortBy,
            })
          }
          disabled={disabled}
        >
          <option value="Distance">Mesafe</option>
          <option value="Rating">Puan</option>
        </select>
      </div>
    </section>
  )
}
