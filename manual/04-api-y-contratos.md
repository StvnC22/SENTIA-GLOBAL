# API HTTP y contratos

## Convenciones

- Base local habitual: `http://127.0.0.1:5388`.
- Los endpoints se descubren por clases `IEndpointGroup` en `src/Web/Endpoints`.
- El prefijo por defecto sería `/api/{NombreDeClase}`; algunos grupos declaran
  `RoutePrefix`.
- Los comandos/consultas viajan por MediatR.
- Los fallos de negocio se devuelven como Problem Details mediante
  `ProblemDetailsResultExtensions.ToProblemHttpResult()`.

## Health y archivos

| Método | Ruta | Auth | Uso |
|---|---|---|---|
| GET | `/` | No | `index.html` |
| GET | `/health` | No, solo Development | Readiness |
| GET | `/alive` | No, solo Development | Liveness |
| GET | `/openapi/v1.json` | No, fuera de desktop | Documento OpenAPI |
| GET | `/scalar` | No, fuera de desktop | UI Scalar |

## Social comments

`GET /api/social-comments`

| Parámetro | Tipo | Descripción |
|---|---|---|
| `platform` | string | Red individual; vacío = todas las redes recibidas |
| `refresh` | bool | Evita caché de Apify si `true` |
| `topic` | string | Términos requeridos en texto/título |
| `prompt` | string | Instrucción de sentimiento; máximo efectivo 1500 caracteres |
| `instagramUrl` | URL | Fuente específica Instagram |
| `facebookUrl` | URL | Fuente específica Facebook |
| `tiktokUrl` | URL | Fuente específica TikTok |
| `xUrl` | URL | Fuente específica X |

Respuesta `SocialListeningDashboard`:

```json
{
  "comments": [],
  "summary": { "total": 0, "positive": 0, "neutral": 0, "negative": 0 },
  "updatedAt": "2026-10-07T00:00:00Z",
  "dataSource": "live",
  "message": null
}
```

`dataSource` puede ser `live` o `demo`. No usarlo como indicador de éxito por sí solo:
comprobar `comments` y `message`.

## Credenciales

`GET /api/api-credentials` devuelve `ApiCredentialStatus`: solo si cada valor está
configurado y, si corresponde, una máscara. Nunca devuelve tokens.

`PUT /api/api-credentials` recibe `SaveApiCredentialsCommand`, cifra y guarda localmente
y devuelve el estado. Fuera de desktop debe llamarse por HTTPS; nunca registrar el body.

## Identity y rutas de plantilla

`Users.MapIdentityApi<ApplicationUser>()` registra rutas estándar de Identity API.
`POST /api/Users/logout` exige autorización.

| Grupo | Rutas | Auth |
|---|---|---|
| `TodoLists` | GET/POST `/api/TodoLists`, PUT/DELETE `/api/TodoLists/{id}` | Sí |
| `TodoItems` | POST/PUT/DELETE `/api/TodoItems`, PATCH `/api/TodoItems/UpdateDetail/{id}` | Sí |
| `WeatherForecasts` | GET `/api/WeatherForecasts` | Sí |
| `Users` | Identity API y logout | Según operación |

Son rutas heredadas de Clean Architecture y no el flujo principal social. No eliminarlas
sin revisar tests funcionales y de arquitectura.

## Contratos que no se deben romper

- `comments`, `summary`, `updatedAt`, `dataSource`, `message`.
- `comment.id`, `platform`, `authorName`, `authorHandle`, `authorUrl`, `text`,
  `commentUrl`, `postTitle`, `postUrl`, `publishedAt`, `sentiment`.
- `sentiment.label`: `positive`, `neutral`, `negative`.
- `sentiment.displayName`: `Positivo`, `Neutral`, `Negativo`.
- `sentiment.confidence`: entero 1-100.

## Agregar un endpoint

1. Crear/editar clase en `src/Web/Endpoints` implementando `IEndpointGroup`.
2. Declarar `RoutePrefix` solo si hace falta.
3. Crear request/handler en Application si hay lógica.
4. Aplicar autorización a nivel de grupo.
5. Traducir `Result` a `ProblemDetails`.
6. Añadir prueba funcional y actualizar OpenAPI/manual.
