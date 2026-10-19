# Catálogo de clases, interfaces, variables y funciones del backend

Este archivo es el índice de símbolos. Los nombres son los que debe usar un ingeniero
para buscar código con `rg`. La responsabilidad se resume para evitar abrir cientos de
archivos sin contexto.

## Composition root

| Símbolo | Archivo | Responsabilidad |
|---|---|---|
| `Program` top-level | `src/Web/Program.cs` | Construye host, Data Protection, DI, middleware, static files, endpoints y arranque |
| `FindAvailablePort` | `src/Web/Program.cs` | Encuentra puerto loopback libre desde 5388 |
| `DependencyInjection.AddApplicationServices` | `src/Application/DependencyInjection.cs` | Registra AutoMapper, validators, MediatR y behaviours |
| `DependencyInjection.AddInfrastructureServices` | `src/Infrastructure/DependencyInjection.cs` | Registra EF, Identity, stores, clientes externos y providers |
| `DependencyInjection.AddWebServices` | `src/Web/DependencyInjection.cs` | Registra OpenAPI, CORS, JSON, exception handler y `IUser` |
| `AddKeyVaultIfConfigured` | `src/Web/DependencyInjection.cs` | Añade Key Vault si existe endpoint |
| `Extensions.AddServiceDefaults` | `src/ServiceDefaults/Extensions.cs` | Health, service discovery, resiliencia y telemetría |
| `Extensions.MapDefaultEndpoints` | `src/ServiceDefaults/Extensions.cs` | Mapea `/health` y `/alive` en Development |
| `AspireExtensions.WithAspNetCoreEnvironment` | `src/AppHost/Extensions.cs` | Ajusta variables del proyecto Aspire |

## Application: contratos base

| Símbolo | Archivo | Uso |
|---|---|---|
| `ICommand`/`ICommand<TResponse>` | `Common/Messaging/ICommand.cs` | Commands con `Result` |
| `IQuery<TResponse>` | `Common/Messaging/IQuery.cs` | Queries de solo lectura |
| `IApplicationDbContext` | `Common/Interfaces/IApplicationDbContext.cs` | Abstracción de EF para handlers |
| `IApiCredentialStore` | `Common/Interfaces/IApiCredentialStore.cs` | Leer/guardar credenciales sin conocer disco |
| `ISocialCommentProvider` | `Common/Interfaces/ISocialCommentProvider.cs` | Contrato de extracción social |
| `ISentimentAnalyzer` | `Common/Interfaces/ISentimentAnalyzer.cs` | Contrato de análisis por lote |
| `IIdentityService` | `Common/Interfaces/IIdentityService.cs` | Usuario, roles y políticas |
| `IUser` | `Common/Interfaces/IUser.cs` | Usuario HTTP actual |
| `SocialCommentFetchResult` | `Common/Interfaces/ISocialCommentProvider.cs` | `DataSource`, comments y message |
| `ApiCredentials` | `Common/Interfaces/IApiCredentialStore.cs` | DTO interno de tokens |
| `PaginatedList<T>` | `Common/Models/PaginatedList.cs` | Paginación reusable |
| `ApplicationError`, `NotFoundError`, `ConflictError` | `Common/Errors/ApplicationError.cs` | Resultados de negocio traducibles a HTTP |
| `AuthorizeAttribute` | `Common/Security/AuthorizeAttribute.cs` | Metadatos de roles/policy para behaviour |

## Application: behaviours MediatR

| Clase | Función `Handle/Process` | Uso y límite |
|---|---|---|
| `UnhandledExceptionBehaviour` | `Handle` | Loguea excepciones y las deja subir; no convierte negocio en éxito |
| `AuthorizationBehaviour` | `Handle` | Lee `AuthorizeAttribute`, usuario, roles/policy; lanza forbidden |
| `ValidationBehaviour` | `Handle` | Ejecuta todos los validators y lanza `ValidationException` |
| `PerformanceBehaviour` | `Handle` | Mide duración; loguea requests lentas |
| `LoggingBehaviour` | `Process` | Loguea request y usuario; no incluir secretos en requests |

El orden de registro en `AddApplicationServices` importa. Cambiarlo modifica qué
behaviour ve excepciones y validaciones.

## Features de Application

### Credenciales

| Símbolo | Uso |
|---|---|
| `GetApiCredentialStatusQuery` + handler | Lee store y devuelve solo estados booleanos/máscaras |
| `ApiCredentialStatus.From` | Transforma credenciales sensibles en DTO seguro |
| `SaveApiCredentialsCommand` | Request de guardado; `ToString` está diseñado para no exponer valores |
| `SaveApiCredentialsCommandValidator` | Valida longitud/formato de entrada |
| `SaveApiCredentialsCommandHandler` | Fusiona valores nuevos con actuales, guarda y devuelve status |
| `KeepCurrentWhenBlank` | Evita borrar una clave cuando el formulario envía máscara/vacío |
| `LooksLikeMaskedPlaceholder` | Detecta la máscara del frontend |

### Social listening

| Símbolo | Uso |
|---|---|
| `GetSocialCommentsQuery` | Plataforma, refresh, topic, prompt y URLs |
| `GetSocialCommentsQueryHandler.Handle` | Provider -> analyzer -> `SocialListeningDashboard` |
| `SpanishSentimentAnalyzer.Analyze` | Fallback determinista por señales españolas |
| `SocialListeningDashboard` | Respuesta agregada |
| `SocialComment` | Comentario listo para UI |
| `SentimentResult` | Label, nombre y confianza |
| `SentimentSummary` | Total y distribución |

### Todo de la plantilla

| Feature | Símbolos principales | Responsabilidad |
|---|---|---|
| Listas | `GetTodosQuery`, `CreateTodoListCommand`, `UpdateTodoListCommand`, `DeleteTodoListCommand` | CRUD, mapeo y unicidad de título |
| Items | `CreateTodoItemCommand`, `UpdateTodoItemCommand`, `UpdateTodoItemDetailCommand`, `DeleteTodoItemCommand` | CRUD, prioridad, nota y completado |
| Evento | `TodoItemCompletedEvent`, `LogTodoItemCompleted.Handle` | Publica/loguea completado |
| Weather | `GetWeatherForecastsQuery`, handler | Endpoint de plantilla protegido |

Los handlers de commands devuelven `Result`/`Result<T>`, buscan entidades, llaman métodos
de dominio y hacen `SaveChangesAsync`. No duplicar reglas de entidad en endpoints.

## Domain

| Símbolo | Archivo | Contrato |
|---|---|---|
| `BaseEntity` | `Domain/Common/BaseEntity.cs` | `Id` y colección de domain events |
| `BaseAuditableEntity` | `Domain/Common/BaseAuditableEntity.cs` | Created/Modified y usuario |
| `BaseEvent` | `Domain/Common/BaseEvent.cs` | Base de notificaciones MediatR |
| `ValueObject` | `Domain/Common/ValueObject.cs` | Igualdad por componentes |
| `TodoList.Create/Rename/UpdateColour/AddItem` | `Domain/Entities/TodoList.cs` | Invariantes y composición de lista |
| `TodoItem.Create/Update/UpdateDetail` | `Domain/Entities/TodoItem.cs` | Invariantes, Done y evento completado |
| `Colour.From`, colores estáticos | `Domain/ValueObjects/Colour.cs` | Value object y colores permitidos |
| `PriorityLevel` | `Domain/Enums/PriorityLevel.cs` | SmartEnum None/Low/Medium/High |
| `Roles.Administrator` | `Domain/Constants/Roles.cs` | Nombre canónico de rol |
| `UnsupportedColourException` | `Domain/Exceptions/...` | Error de color inválido |

Usar factories/métodos de entidad. Los constructores privados existen para EF y no son
la vía de creación de negocio.

## Infrastructure: datos e Identity

| Símbolo | Uso |
|---|---|
| `ApplicationDbContext.OnModelCreating` | Identity + configuraciones de assembly |
| `ApplicationDbContextInitialiser.InitialiseAsync` | Borra/crea base en Development |
| `ApplicationDbContextInitialiser.TrySeedAsync` | Rol, usuario y Todo inicial |
| `TodoListConfiguration.Configure` | Longitud, Colour owned y colección por field |
| `TodoItemConfiguration.Configure` | Longitud y conversión SmartEnum |
| `AuditableEntityInterceptor.UpdateEntities` | Rellena auditoría en Added/Modified |
| `AuditableEntityInterceptor.HasChangedOwnedEntities` | Detecta cambios de value objects |
| `DispatchDomainEventsInterceptor.DispatchDomainEvents` | Publica eventos tras SaveChanges |
| `ApplicationUser` | Subclase de IdentityUser |
| `IdentityService` | UserManager/roles/policies |
| `IdentityResultExtensions.ToApplicationResult` | Convierte errores Identity a FluentResults |

## Infrastructure: configuración e integraciones

| Símbolo | Uso |
|---|---|
| `LocalAppStorage.GetAppDataDirectory` | Escoge carpeta escribible |
| `GetKeysDirectory`/`GetCredentialsFilePath` | Rutas de Data Protection y tokens |
| `EncryptedFileApiCredentialStore.GetAsync` | Descifra o devuelve Empty |
| `EncryptedFileApiCredentialStore.SaveAsync` | Cifra, escribe temporal y reemplaza |
| `ApifyClient` | Ejecuta actor y lee dataset de Apify |
| `ApifySocialCommentProvider.FetchAsync` | Orquestación completa de extracción |
| `CompositeSocialCommentProvider.FetchAsync` | Política live/demo |
| `DemoSocialCommentProvider.FetchAsync` | Dataset fijo y filtro por tema |
| `GeminiSentimentAnalyzer.AnalyzeBatchAsync` | Gemini por lotes y fallback |
| `LocalSentimentAnalyzer.AnalyzeBatchAsync` | Análisis local por lote |

## Web infrastructure

| Símbolo | Uso |
|---|---|
| `WebApplicationExtensions.MapEndpoints` | Descubre grupos por reflection |
| `EndpointRouteBuilderExtensions.MapGet/MapPost/MapPut/MapPatch/MapDelete` | Añaden nombre y metadata común |
| `ProblemDetailsResultExtensions.ToProblemHttpResult` | Result -> HTTP Problem Details |
| `ProblemDetailsExceptionHandler.TryHandleAsync` | Excepción -> Problem Details |
| `BearerSecuritySchemeTransformer` | Documenta bearer en OpenAPI |
| `IdentityApiOperationTransformer` | Mejora metadata OpenAPI de Identity |
| `ApiExceptionOperationTransformer` | Documenta errores conocidos |
| `MethodInfoExtensions.IsAnonymous` | Filtra métodos anónimos en discovery |
| `CurrentUser.Id/Roles` | Claims del HttpContext |

## Variables estáticas y constantes de alto impacto

| Variable | Ubicación | Significado |
|---|---|---|
| `preferredPort = 5388` | `Web/Program.cs` | Puerto inicial desktop |
| `ProtectorPurpose` | `EncryptedFileApiCredentialStore` | Parte del contrato criptográfico |
| `Platforms` | `ApifySocialCommentProvider` | Redes reconocidas |
| `BatchSize = 30` | `GeminiSentimentAnalyzer` | Tamaño de lote IA |
| `MaxParallelBatches = 3` | `GeminiSentimentAnalyzer` | Paralelismo Gemini |
| `PositiveTerms`/`NegativeTerms` | `GetSocialComments.cs` | Diccionarios del fallback local |
| `ApplicationFolderName` | `LocalAppStorage` | Carpeta heredada `EmocionesULEAM` |
| `_items`, `_done`, `_domainEvents` | Domain entities | Estado encapsulado; no exponer setters sin revisar invariantes |
