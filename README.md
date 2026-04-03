# Cafe Locator

Konuma gore yakin kafeleri bulan, filtreleyen, listede ve haritada senkron gosteren modern bir web uygulamasi.

## Ozellik Ozeti

- Kullanici konum izni ile yakin kafe arama
- Filtreler: yaricap, minimum puan, sadece acik olanlar
- Siralama: mesafe veya puan
- Liste + harita senkron secim
- Secili kafe icin tek tusla yol tarifi olusturma
- Kafe fotograflari (Google Places photo proxy)
- Loading, error, empty state
- Backend cache, validation, rate limiting, logging
- Docker ve GitHub Actions CI

## Teknoloji Yigini

- Backend: .NET 10, ASP.NET Core Web API
- Frontend: React + TypeScript + Vite
- Harita: Google Maps JavaScript API
- Mekan verisi: Google Places API (New)
- Frontend veri yonetimi: TanStack Query

## Mimari Kisa Ozet

- Frontend sadece backend ile konusur.
- Backend Google Places API'ye gider.
- Places server key backendde tutulur.
- Fotograflar backend `/api/v1/cafes/photo` endpointi uzerinden proxylenir.

## Proje Yapisi

```txt
.
├─ backend/CafeLocator.Api
│  ├─ Controllers
│  ├─ Services
│  ├─ Models
│  ├─ Options
│  └─ Program.cs
├─ frontend/cafe-locator-web
│  ├─ src
│  ├─ vite.config.ts
│  └─ package.json
├─ docker-compose.yml
└─ .github/workflows/ci.yml
```

## Calistirma (Local)

### 1) Backend

```bash
cd backend/CafeLocator.Api
export GooglePlaces__ApiKey="YOUR_BACKEND_PLACES_KEY"
dotnet run --no-launch-profile --urls http://localhost:5011
```

### 2) Frontend

`frontend/cafe-locator-web/.env.local`:

```env
VITE_API_BASE_URL=http://localhost:5011
VITE_GOOGLE_MAPS_API_KEY=YOUR_BROWSER_MAPS_KEY
```

```bash
cd frontend/cafe-locator-web
npm install
npm run dev
```

## Docker ile Calistirma

Root klasorde `.env` olustur:

```env
GOOGLE_PLACES_API_KEY=YOUR_BACKEND_PLACES_KEY
VITE_GOOGLE_MAPS_API_KEY=YOUR_BROWSER_MAPS_KEY
VITE_API_BASE_URL=http://localhost:5010
WEB_ORIGIN=http://localhost:5173
```

Sonra:

```bash
docker compose up --build
```

## Google API Key Kurallari (Onemli)

### Backend key (Places)

- Places API (New) aktif olmali
- Application restriction: local testte `None`, productionda `IP addresses`
- Bu key frontendde kullanilmaz

### Frontend key (Maps + Directions)

- Application restriction: `HTTP referrers`
- Local referrerlar:
  - `http://localhost:5173/*`
  - `http://localhost:5174/*`
  - `http://127.0.0.1:5173/*`
  - `http://127.0.0.1:5174/*`
- API restrictions:
  - Maps JavaScript API
  - Directions API

## Guvenlik ve Paylasim Notlari

- Gercek keyleri asla repoya commitleme.
- Sadece `.env.example` dosyalari repoda kalmali.
- `.env`, `.env.local`, `*.pem`, `*.key`, `*.pfx` dosyalari `.gitignore` ile engellendi.
- Sohbette paylasilan eski keyleri rotate etmen onerilir.

## GitHub'a Hazirlama

```bash
# root'ta
cd "/Users/mali/Projects/Claude Projects/kafe-projesi"

# 1) repo baslat (eger daha once yapilmadiysa)
git init

# 2) kontrol
find . -name '.env*' -not -name '*.example'

# 3) dosyalari ekle
git add .

# 4) staged kontrolu
git status

# 5) commit
git commit -m "Initial commit: Cafe Locator MVP"
```

Sonra GitHub'da bos repo olusturup remote ekleyip push et:

```bash
git remote add origin <YOUR_GITHUB_REPO_URL>
git branch -M main
git push -u origin main
```

## CI

GitHub Actions workflow:

- Backend restore + build
- Frontend install + build

Dosya: `.github/workflows/ci.yml`
