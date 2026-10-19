# Flujo de monitoreo social y análisis de sentimiento

## Flujo de extremo a extremo

```text
Usuario escribe URLs/prompt
  -> app.js valida y crea un job
  -> GET /api/social-comments
  -> SocialComments.GetSocialComments
  -> GetSocialCommentsQueryHandler
  -> CompositeSocialCommentProvider
       -> ApifySocialCommentProvider
       -> DemoSocialCommentProvider en la rama permitida
  -> ISentimentAnalyzer
       -> GeminiSentimentAnalyzer si existe clave IA
       -> LocalSentimentAnalyzer como fallback
  -> SocialListeningDashboard
  -> app.js fusiona, filtra, guarda historial y renderiza
```

## `SocialCommentDraft`

| Campo | Significado |
|---|---|
| `Id` | Identificador estable |
| `Platform` | `instagram`, `facebook`, `tiktok` o `x` |
| `AuthorName`/`AuthorHandle` | Identidad pública |
| `AuthorUrl` | URL del autor si existe |
| `Text` | Texto original |
| `CommentUrl` | URL del comentario/publicación |
| `PostTitle` | Título/resumen |
| `PostUrl` | URL de publicación |
| `PublishedAt` | Fecha normalizada a `DateTimeOffset` |

`SocialComment` agrega `SentimentResult` con `Label`, `DisplayName` y `Confidence`.

## `CompositeSocialCommentProvider`

1. Consulta primero `ApifySocialCommentProvider`.
2. Si obtiene comentarios, devuelve `DataSource = live`.
3. Si el resultado es `live` o trae un mensaje explícito, lo devuelve sin sustituirlo.
4. En la rama restante consulta `DemoSocialCommentProvider` y devuelve `demo`.

No asumir que “sin credenciales” significa demo: con la implementación actual puede
devolver una respuesta `live` vacía. Si se cambia esta política, actualizar README,
pruebas y UI para distinguir `live`, `demo`, `empty` y `error`.

## `ApifySocialCommentProvider`

Método público:

- `FetchAsync(platform, refresh, cancellationToken, topic, sourceUrls)`: resuelve redes,
  URLs, token, caché, actores, límites y mapeo final.

Métodos internos importantes:

| Función | Uso |
|---|---|
| `ResolveTargetPlatforms` | Decide redes por parámetro, URLs o perfiles |
| `ResolveProfileUrl` | Prioriza URL del request sobre configuración |
| `TryValidateProfileUrl` | Valida dominio y ruta por red |
| `BuildSourceFingerprint` | Parte de la clave de caché |
| `ResolveApifyToken`/`FirstNonEmptyCredential` | Encuentra token aplicable |
| `FetchTikTokAsync` | Videos/comentarios TikTok |
| `FetchInstagramAsync` | Posts/comentarios Instagram |
| `FetchFacebookAsync` | Posts/comentarios Facebook |
| `FetchCommentsForPostsAsync` | Procesa lotes de URLs |
| `FetchProfileCommentsAsync` | Actor común de comentarios |
| `FetchXAsync` | Combina timeline, replies y comentarios X |
| `FetchXRepliesAsync`/`FetchXPostCommentsAsync` | Profundidad X |
| `ExtractSocialPosts`/`ExtractPosts` | Extrae posts de JSON variable |
| `MapTikTokComment`, `MapInstagramComment`, `MapFacebookComment` | Mapeadores principales |
| `MapProfileComment`, `MapXReply`, `MapXComment` | Mapeadores X/perfil |
| `ExtractAuthor` | Normaliza autores variables |
| `ParseTimestamp` | Convierte fechas externas |
| `MatchesTopic`/`Normalize` | Filtra tema sin tildes |
| `TryParseTikTokVideoUrl`/`TryParseXStatusUrl` | Detecta post directo |
| `TruncatePostTitle` | Limita títulos visibles |
| `ExtractMentionHandle`/`ExtractUsernameFromUrl` | Infiere identificadores |

### Protecciones

- Caché por plataforma, tema y huella de URLs.
- Una red a la vez para respetar el plan gratuito.
- Timeout por plataforma de 3 minutos y HttpClient de 6 minutos.
- Límites por post, red y total.
- Deduplicación por `platform:id`.
- Errores acumulados sin ocultar comentarios parciales.

### Limitaciones

- Los actores externos pueden cambiar su JSON sin aviso.
- Perfil privado, URL inválida o permisos insuficientes producen cero comentarios.
- El token puede depender de red/actor.
- Muchos posts consumen créditos y memoria.
- X es la integración más sensible por sus múltiples rutas y formatos.

## Sentimiento

### `GeminiSentimentAnalyzer`

- Lotes de 30 comentarios y máximo 3 lotes paralelos.
- Modelo configurado primero; después `gemini-2.5-flash-lite` y `gemini-2.5-flash`.
- Exige JSON con `id`, `label`, `displayName`, `confidence`.
- Descarta IDs desconocidos y completa faltantes con análisis local.
- Si los modelos fallan, usa `LocalSentimentAnalyzer`.
- `BuildPrompt` limita el prompt personalizado a 1500 caracteres.
- `CalibrateConfidence` baja confianza en textos cortos/ambiguos y la sube para señales
  negativas directas; no es una probabilidad estadística.

### `LocalSentimentAnalyzer`/`SpanishSentimentAnalyzer`

El clasificador local es determinista y por palabras/señales. Sirve para demo y fallback,
pero puede fallar con ironía nueva, contexto cultural, negaciones complejas o coincidencia
de substrings. Cualquier ajuste de listas debe incluir pruebas unitarias.
