# Configuración, variables y secretos

## Orden conceptual

ASP.NET Core combina `appsettings.json`, `appsettings.Development.json`, variables de
entorno, User Secrets y, fuera de desktop, opcionalmente Key Vault. Las variables usan
`__` para representar `:`; por ejemplo `SocialListening__Gemini__Model` equivale a
`SocialListening:Gemini:Model`.

Los valores sensibles no deben entrar en archivos del repositorio. El panel los guarda
mediante `PUT /api/api-credentials`; la implementación escribe un archivo cifrado local.

## Configuración general

| Clave | Tipo | Uso | Impacto |
|---|---:|---|---|
| `ConnectionStrings:AnalisisSentimientoDb` | string | SQLite | EF Core/Identity |
| `ApiCredentials:Path` | string | Ruta alternativa de credenciales cifradas | Store local |
| `AZURE_KEY_VAULT_ENDPOINT` | URL | Key Vault fuera de desktop | Azure Identity |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | URL | Exporter OpenTelemetry | Aspire/telemetría |

## `SocialListening`

| Clave | Tipo | Función | Trade-off |
|---|---:|---|---|
| `Profiles.instagram` | URL | Perfil/fuente Instagram por defecto | Vacío = no consultar |
| `Profiles.facebook` | URL | Página/perfil Facebook | Vacío = no consultar |
| `Profiles.tiktok` | URL | Perfil/video TikTok | Vacío = no consultar |
| `Profiles.x` | URL | Perfil/post X | Vacío = no consultar |
| `MaxPostsPerProfile` | int | Tope genérico de posts | Más datos/costo |
| `InstagramMaxPosts` | int | Tope Instagram | Más cobertura/latencia |
| `FacebookMaxPosts` | int | Tope Facebook | Más cobertura/latencia |
| `MaxCommentsPerPost` | int | Tope genérico por post | Limita respuestas |
| `InstagramMaxCommentsPerPost` | int | Comentarios IG por post | Más memoria |
| `FacebookMaxCommentsPerPost` | int | Comentarios FB por post | Más memoria |
| `MaxCommentsPerPlatform` | int | Tope final por red | Protege UI/Apify |
| `MaxTotalComments` | int | Tope global | Se reparte entre redes |
| `ApifyMemoryMb` | int | Memoria solicitada al actor | Depende del plan |
| `XMaxPosts` | int | Posts X revisados | Determina profundidad |
| `XMaxCommentPages` | int | Páginas de respuestas X | Más llamadas |
| `XRecentPostsBoost` | int | Profundidad extra reciente | Más costo |
| `CommentUrlBatchSize` | int | URLs por lote IG/FB | Balancea payload/fallos |
| `CacheMinutes` | int | Caché de Apify | `refresh=true` la evita |

## Actores Apify

Las claves `SocialListening:Actors:*` son identificadores, no tokens:

- `InstagramPosts`: `apify/instagram-scraper`.
- `InstagramComments`: `apify/instagram-comment-scraper`.
- `FacebookPosts`: `apify/facebook-posts-scraper`.
- `FacebookComments`: `apify/facebook-comments-scraper`.
- `TikTokPosts`: `clockworks/tiktok-scraper`.
- `TikTokComments`: `clockworks/tiktok-comments-scraper`.
- `XPosts`: `apidojo/tweet-scraper`.
- `XTimeline`: `igolaizola/x-twitter-scraper`.
- `XComments`: `iron-crawler/twitter-comments`.
- `ProfileComments`: `tri_angle/social-media-sentiment-analysis-tool`.

Cambiar un actor puede romper el mapeo JSON aunque compile. Revisar los mapeadores,
campos esperados y pruebas de integración después de cualquier cambio.

## Gemini

| Clave | Tipo | Uso |
|---|---:|---|
| `SocialListening:Gemini:Model` | string | Modelo primario; actualmente `gemini-2.5-flash` |

La API key no vive aquí: se guarda como `ArtificialIntelligence` en el archivo cifrado.

## Credenciales locales

| Campo lógico | Proveedor | Lo que ve el frontend |
|---|---|---|
| `Instagram` | Apify/Instagram | Estado y máscara |
| `Facebook` | Apify/Facebook | Estado y máscara |
| `TikTok` | Apify/TikTok | Estado y máscara |
| `X` | Apify/X | Estado y máscara |
| `ArtificialIntelligence` | Gemini | Estado y máscara |
| `Apify` | Token común Apify | Estado y máscara |

`ResolveApifyToken` define la prioridad efectiva. No registrar el contenido de
`ApiCredentials` en logs.

## Sincronización obligatoria

Una nueva clave debe actualizar `appsettings.json`, `appsettings.Development.json`, la
clase Options, pruebas y este manual. La paridad de nombres evita que Desarrollo
funcione y publicación falle por una clave ausente.
