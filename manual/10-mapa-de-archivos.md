# Mapa de archivos por responsabilidad

Este índice permite saltar directamente al lugar correcto. No es necesario revisar las
30 mil pestañas imaginables del sistema: buscar el área y luego el símbolo.

## Raíz

| Archivo | Uso |
|---|---|
| `global.json` | SDK .NET requerido |
| `Directory.Packages.props` | Versiones centralizadas de NuGet |
| `Directory.Build.props` | Propiedades comunes de MSBuild |
| `AnalisisSentimiento.slnx` | Solución |
| `README.md` | Guía general y publicación |
| `CLAUDE.md` | Reglas de arquitectura y estilo del repositorio |
| `PUBLICAR_EXE.cmd` | Publicación Windows |
| `aspire.config.json`, `.mcp.json` | Configuración de Aspire/MCP |

## Domain

| Ruta | Uso |
|---|---|
| `src/Domain/Common/BaseEntity.cs` | Id y eventos de dominio |
| `src/Domain/Common/BaseAuditableEntity.cs` | Auditoría |
| `src/Domain/Common/BaseEvent.cs` | Evento MediatR |
| `src/Domain/Common/ValueObject.cs` | Igualdad de value objects |
| `src/Domain/Entities/TodoList.cs` | Lista y agregación de items |
| `src/Domain/Entities/TodoItem.cs` | Item, prioridad y completado |
| `src/Domain/ValueObjects/Colour.cs` | Colores válidos |
| `src/Domain/Enums/PriorityLevel.cs` | SmartEnum de prioridad |
| `src/Domain/Events/TodoItemCompletedEvent.cs` | Evento de completado |
| `src/Domain/Constants/Roles.cs` | Nombre Administrator |
| `src/Domain/Exceptions/UnsupportedColourException.cs` | Error de color |

## Application

| Ruta | Uso |
|---|---|
| `src/Application/DependencyInjection.cs` | Registro de MediatR, validators y behaviours |
| `src/Application/Common/Behaviours/*` | Pipeline logging, auth, validation, rendimiento y errores |
| `src/Application/Common/Interfaces/*` | Puertos hacia infraestructura |
| `src/Application/Common/Messaging/*` | ICommand/IQuery |
| `src/Application/Common/Models/*` | DTOs y paginación |
| `src/Application/Common/Errors/*` | Errores de negocio |
| `src/Application/Common/Exceptions/*` | Excepciones de aplicación |
| `src/Application/Common/Security/*` | Metadata de autorización |
| `src/Application/SocialListening/Queries/GetSocialComments/*` | Consulta y sentimiento |
| `src/Application/ApiCredentials/*` | Estado y guardado de credenciales |
| `src/Application/TodoLists/*` | CRUD y consulta de listas |
| `src/Application/TodoItems/*` | CRUD, detalle y evento de items |
| `src/Application/WeatherForecasts/*` | Feature heredado de plantilla |

## Infrastructure

| Ruta | Uso |
|---|---|
| `src/Infrastructure/DependencyInjection.cs` | Composition root de infraestructura |
| `src/Infrastructure/Data/ApplicationDbContext.cs` | Contexto EF/Identity |
| `src/Infrastructure/Data/ApplicationDbContextInitialiser.cs` | Borrado, creación y seed |
| `src/Infrastructure/Data/Configurations/*` | Mapeo EF de entidades |
| `src/Infrastructure/Data/Interceptors/*` | Auditoría y domain events |
| `src/Infrastructure/Identity/*` | Usuario y servicios Identity |
| `src/Infrastructure/Configuration/LocalAppStorage.cs` | Rutas locales escribibles |
| `src/Infrastructure/Configuration/EncryptedFileApiCredentialStore.cs` | Cifrado de tokens |
| `src/Infrastructure/SocialListening/Configuration/*` | Options de redes/Apify/Gemini |
| `src/Infrastructure/SocialListening/Apify/ApifyClient.cs` | Cliente HTTP Apify |
| `src/Infrastructure/SocialListening/Apify/ApifySocialCommentProvider.cs` | Extracción real |
| `src/Infrastructure/SocialListening/Apify/CompositeSocialCommentProvider.cs` | Política live/demo |
| `src/Infrastructure/SocialListening/Apify/DemoSocialCommentProvider.cs` | Dataset demo fijo |
| `src/Infrastructure/SocialListening/Sentiment/GeminiSentimentAnalyzer.cs` | IA externa y fallback |
| `src/Infrastructure/SocialListening/Sentiment/LocalSentimentAnalyzer.cs` | Fallback local |

## Web y frontend

| Ruta | Uso |
|---|---|
| `src/Web/Program.cs` | Host, middleware y arranque |
| `src/Web/DependencyInjection.cs` | Servicios Web/OpenAPI/CORS/JSON |
| `src/Web/Endpoints/*` | API Minimal por grupos |
| `src/Web/Infrastructure/*` | Discovery, metadata, errores y seguridad OpenAPI |
| `src/Web/Services/CurrentUser.cs` | IUser desde claims |
| `src/Web/Serialization/AppJsonSerializerContext.cs` | Source generation JSON |
| `src/Web/appsettings.json` | Defaults y placeholders |
| `src/Web/appsettings.Development.json` | SQLite y límites de Desarrollo |
| `src/Web/Properties/launchSettings.json` | Perfiles HTTP/HTTPS |
| `src/Web/wwwroot/index.html` | Markup |
| `src/Web/wwwroot/app.js` | Estado y comportamiento |
| `src/Web/wwwroot/styles.css` | Presentación |

## Aspire y Shared

| Ruta | Uso |
|---|---|
| `src/AppHost/Program.cs` | SQLite, Web y environment Aspire |
| `src/AppHost/Extensions.cs` | Extensiones del recurso |
| `src/AppHost/appsettings*.json` | Configuración del host |
| `src/ServiceDefaults/Extensions.cs` | Health, OTEL, discovery y resiliencia |
| `src/Shared/Services.cs` | Nombres `WebApi`, `Database`, etc. |

## Tests

| Proyecto | Enfoque |
|---|---|
| `tests/Domain.UnitTests` | Value objects y SmartEnum |
| `tests/Application.UnitTests` | Handlers/behaviours/sentimiento |
| `tests/Infrastructure.IntegrationTests` | Apify/Gemini/cifrado |
| `tests/Application.FunctionalTests` | API y flujo completo |
| `tests/Web.UnitTests` | Problem Details y Web |
| `tests/ArchitectureTests` | Capas, visibilidad, CQRS y convenciones |
| `tests/TestAppHost` | AppHost aislado para funcionales |

## Búsquedas rápidas

```powershell
rg -n "NombreDelSimbolo" src tests
rg -n "SocialListening|ApiCredentials|ConnectionStrings" src
rg -n "MapGet|MapPost|MapPut|MapPatch|MapDelete" src\Web\Endpoints
rg -n "PositiveTerms|NegativeTerms|Analyze" src tests
rg -n "localStorage|state\.jobs|fetch\(" src\Web\wwwroot\app.js
```
