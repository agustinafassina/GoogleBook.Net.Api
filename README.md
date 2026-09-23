# 📚 GoogleBook.Api
A .NET 10 API that searches [Google Books](https://developers.google.com/books) by text or tag, keeps matches in an in-memory catalog, and can fill in author details from Wikidata.

## 🧩 Architecture
![API architecture](api-diagram.png)

## ✅ Requirements
- 🔷 .NET 10 SDK
- 🐳 Docker, only if you want to run the container
- 🔑 Google Books API key is optional. Without one, calls use the public quota

## 📁 Solution
- `GoogleBook.Api` - HTTP host, `BookController`, and Swagger
- `GoogleBook.Services` - catalog search, ingest, and tagging
- `GoogleBook.Repository` - in-memory book store, plus the Google Books and Wikidata clients
- `GoogleBook.Models` - DTOs and external API models
- `GoogleBook.Unittests` - tests (project `GoogleBook.Api.Tests`)

## ▶️ Run locally
Set `GameApi:ApiKey` in `GoogleBook.Api/appsettings.json`, or set the `GameApi__ApiKey` environment variable, then:

```bash
dotnet run --project GoogleBook.Api
dotnet test
```

The process listens on **10000** (`http://0.0.0.0:10000`). It reads `PORT` and falls back to 10000, so the ports in `GoogleBook.Api/Properties/launchSettings.json` are not used.

- Swagger: `http://localhost:10000/swagger`
- Health: `GET /health` (`http://localhost:10000/health`)

Book routes expect the `X-API-Key` header. In Swagger, click **Authorize** and paste the `GameApi:ApiKey` value. If that key is missing, those routes return 503.

## ⚙️ Configuration
Settings live in `GoogleBook.Api/appsettings.json`.

```json
{
  "ExternalApis": {
    "GoogleBooks": { "BaseUrl": "https://www.googleapis.com/", "ApiKey": "" },
    "Wikidata": { "BaseUrl": "https://query.wikidata.org/" }
  },
  "GameApi": { "ApiKey": "your-api-key" }
}
```

`GameApi:ApiKey` is required (`GameApi__ApiKey` in the environment). `ExternalApis:GoogleBooks:ApiKey` is optional (`ExternalApis__GoogleBooks__ApiKey`).

## 🔗 Endpoints
Base path `api/v1/book`. Send `X-API-Key` on every route below. `/health` does not use that header.

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/v1/book` | Local catalog. Optional query: `?author=Name` |
| `GET` | `/api/v1/book/{id}` | One stored book |
| `GET` | `/api/v1/book/tags` | Tags present in the catalog |
| `GET` | `/api/v1/book/by-tag/{tag}` | Filter the local catalog |
| `GET` | `/api/v1/book/google/search` | Search Google Books by `query`. Does not persist |
| `GET` | `/api/v1/book/google/by-tag/{tag}` | Search Google Books by tag. Does not persist |
| `POST` | `/api/v1/book/ingest/google` | Search Google Books and store the new results |
| `POST` | `/api/v1/book/{id}/tags` | Add tags to a stored book |
| `POST` | `/api/v1/book/{id}/enrich` | Enrich authors via Wikidata |

`/by-tag/{tag}` reads the in-memory catalog, which stays empty until you ingest or add tags. `/google/by-tag/{tag}` queries Google Books live (`subject:{tag}`, then fallbacks). Google search routes take `maxResults` (default 20) and `autoTag` (default true). When `autoTag` is true, Google Books categories are copied onto the books as tags.

Ingest body: `query` (required), `maxResults` (1-40, default 10), `tags`, `autoTag` (default true), `enrichAuthors` (default false). Try the routes from Swagger.

## 🐳 Docker
```bash
docker build -t googlebook-api:latest .
docker run -d -p 10000:10000 -e "GameApi__ApiKey=your-api-key" --name googlebook-api googlebook-api:latest
```

The container listens on **10000**. Swagger is `http://localhost:10000/swagger`, and the health check is `GET /health`.

If the host sets `PORT` (Render does), the app binds to that port instead. Do not put `:10000` on the public URL. Swagger stays on in Production.
