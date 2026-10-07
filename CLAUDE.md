# CLAUDE.md

Este archivo ofrece guía a Claude Code (claude.ai/code) al trabajar con código en este repositorio.

## Resumen del proyecto

Esta solución fue generada a partir del [Clean.Architecture.Solution.Template](https://github.com/jasontaylordev/CleanArchitecture) (v10.8.0) y sigue el patrón Clean Architecture de Jason Taylor con CQRS/MediatR, orquestado mediante **.NET Aspire**. El framework de destino es `net10.0` (SDK fijado en `global.json`, `rollForward: latestFeature`).

Consulta también el `README.md` para los requisitos previos (instalación de .NET, Aspire, CSharpier, Docker/Podman, C# Dev Kit y configuración del F5/debug).

## UI/UX: Fluent 2 obligatorio

Cualquier aplicación, framework de UX o cliente de este repositorio (web, desktop o
móvil) debe seguir el lenguaje de diseño **Fluent 2** de Microsoft — principios de
diseño, tipografía, accesibilidad (WCAG 2.1 AA), tono de contenido y patrones de
carga/espera. Librería oficial recomendada para web: **[`@fluentui/react-components`](https://react.fluentui.dev/)**
(Fluent UI React v9). Antes de construir o revisar cualquier pantalla o componente,
invoca el skill `fluent-ui-2` (`.claude/skills/fluent-ui-2/SKILL.md`) — no construyas
UI a mano ni mezcles otro design system sin pasar antes por ese skill.

## Mensajes Commit.
Mensajes obligatoriamente en español. Estructura: Título corto, línea en blanco, y una lista detallada de cambios con viñetas. Usa un emoji relevante al inicio de cada viñeta. Explica brevemente qué cambió en cada archivo y por qué.

## Formateo obligatorio tras generar código C#
Al final de **cada** generación o edición de código C#, ejecutar:
```bash
csharpier format .
```
antes de dar la tarea por terminada (y antes de cualquier commit).

## Todo cambio se comprueba con test

Todo cambio funcional, de dominio o de infraestructura debe comprobarse con una prueba automatizada antes de darse por terminado — no basta con que el código compile o con probarlo manualmente. La prueba más importante es la **prueba funcional a través de MediatR** (`Application.FunctionalTests`, ver sección [Pruebas](#pruebas)): ejercita el caso de uso real vía `ISender`/`TestApp.SendAsync` de punta a punta (Handler, validadores, `IApplicationDbContext`, base de datos), por lo que es la que más fielmente reproduce el comportamiento en producción.

- Cambio en un Command/Query nuevo o existente → agregar/actualizar su prueba en `Application.FunctionalTests` (una clase de prueba por caso de uso, reflejando la carpeta `Commands`/`Queries` del slice en `Application`).
- Cambio de lógica de dominio (factory methods, métodos de comportamiento de una entidad) → complementar con `Domain.UnitTests`.
- Cambio en Infrastructure (configuraciones EF Core, interceptors, servicios de Identity) → complementar con `Infrastructure.IntegrationTests`.
- Cambio en helpers puros de `src/Web/Infrastructure` → complementar con `Web.UnitTests`.
- Cambio de arquitectura (nuevo Command/Query, Handler, endpoint) → correr también `tests/ArchitectureTests` (ver reglas en [Pruebas](#pruebas)).

Ejecutar `dotnet test` (o el proyecto/filtro específico afectado) y confirmar que pasa antes de considerar el trabajo terminado.

## Muy importante: minimizar comentarios

Los comentarios en el código deben mantenerse **al mínimo**. Es preferible un nombre de método, variable, clase o parámetro largo y explícito que ejemplifique claramente lo que hace, a un comentario largo explicándolo. Antes de escribir un comentario, intenta primero renombrar para que el código se explique solo; si aun así hace falta un comentario, que sea de una línea y explique el **porqué** (una decisión no obvia), nunca el **qué** (lo que el código ya dice por sí mismo).

## Política de archivos binarios

Este repositorio **no debe contener archivos binarios de documentos** (PDF, DOCX, DOC, XLSX, XLS, PPTX, PPT, ZIP, etc.). La única excepción son **imágenes usadas como logos** (PNG, JPG/JPEG, SVG, ICO, WEBP). El `.gitignore` bloquea estos formatos de documento a nivel de repositorio (ver sección añadida al final del archivo).

Cualquier necesidad de excepción a esta regla (adjuntar un PDF, una plantilla DOCX, etc.) **debe hablarse primero con el administrador de DIIT** — no agregar el archivo ni ajustar el `.gitignore` por cuenta propia.

## Todo pasa por Aspire

Cualquier recurso nuevo (base de datos, API externa, cola, storage, parámetro/secret, variable de entorno) se declara y conecta **en `src/AppHost/Program.cs`**, nunca hardcodeado o configurado a mano por fuera del modelo de Aspire. Esto no es solo una preferencia de organización: el manifiesto que Aspire genera a partir de ese modelo es lo que impulsa la publicación (`aspire deploy` / Azure Container Apps) y el llenado de configuración en cada entorno — si un recurso no está en el AppHost, no existe para el proceso de publicación. Ver [Orquestación con Aspire](#orquestación-con-aspire) y [Configuración, certificados y secrets](#configuración-certificados-y-secrets) más abajo.

## Comandos habituales

Compilar:
```bash
dotnet build
```

Ejecutar toda la app (AppHost de Aspire — abre el dashboard con URLs/logs de todos los servicios):
```bash
dotnet run --project .\src\AppHost
```

Ejecutar todas las pruebas:
```bash
dotnet test
```

Ejecutar un solo proyecto de pruebas:
```bash
dotnet test tests\Domain.UnitTests\Domain.UnitTests.csproj
```

Ejecutar una sola prueba (filtro de NUnit, funciona con `dotnet test`):
```bash
dotnet test --filter "FullyQualifiedName~CreateTodoItemTests"
```

Formatear el código con CSharpier (obligatorio tras generar/editar código C#, ver más arriba):
```bash
csharpier format .
```

Revisar paquetes NuGet desactualizados:
```bash
dotnet outdated
```

Generar (scaffold) un nuevo command o query (debe ejecutarse desde `src\Application\`; requiere la plantilla `dotnet new ca-usecase` — instálala con `dotnet new install Clean.Architecture.Solution.Template::10.8.0` si falta):
```bash
dotnet new ca-usecase --name CreateTodoList --feature-name TodoLists --usecase-type command --return-type int
dotnet new ca-usecase -n GetTodos -fn TodoLists -ut query -rt TodosVm
```

## Arquitectura

Estructura de la solución (`AnalisisSentimiento.slnx`); las dependencias fluyen hacia adentro según Clean Architecture:

- **`src/Domain`** — Entidades, value objects, eventos de dominio, enums, excepciones. Sin dependencias de otras capas.
- **`src/Application`** — Casos de uso CQRS (commands/queries de MediatR), validadores (FluentValidation), perfiles de AutoMapper y las abstracciones `IApplicationDbContext` / `IIdentityService` / `IUser`. Depende únicamente de Domain.
- **`src/Infrastructure`** — EF Core (`ApplicationDbContext`, proveedor SQLite), ASP.NET Core Identity, e implementaciones de las interfaces definidas en Application.
- **`src/Web`** — Host de Minimal API. Endpoints, configuración de OpenAPI/Scalar, manejo de excepciones.
- **`src/AppHost`** — Orquestador de .NET Aspire; es el punto de entrada real al ejecutar con F5/`dotnet run`. Configura el recurso SQLite y el proyecto `Web` como recurso de Aspire.
- **`src/ServiceDefaults`** — Defaults de servicio compartidos de Aspire (telemetría, health checks, resiliencia) referenciados por los proyectos orquestados por AppHost.
- **`src/Shared`** — Constantes transversales compartidas entre AppHost y otros proyectos (p. ej. `Services.cs` define los nombres de recursos como `Services.Database`, `Services.WebApi` usados para conectar referencias de Aspire y leer connection strings).

### Organización de features en Application (vertical slices)

Dentro de `src/Application`, el código se agrupa por feature, no por capa técnica. Cada caso de uso es un único archivo que combina el request de MediatR, el handler y (si aplica) el mapeo:

```
Application/{NombreFeature}/Commands/{NombreCasoDeUso}/{NombreCasoDeUso}.cs
Application/{NombreFeature}/Queries/{NombreCasoDeUso}/{NombreCasoDeUso}.cs
Application/{NombreFeature}/EventHandlers/{NombreHandler}.cs
```

Ese único archivo contiene el record, el Handler y, si aplica, el Validator y los DTO/ViewModel/perfil de AutoMapper de ese caso de uso — ver [Validators, DTOs y mappers viven en el archivo del feature](#validators-dtos-y-mappers-viven-en-el-archivo-del-feature) más abajo.

Los handlers dependen de `IApplicationDbContext` (nunca directamente de `ApplicationDbContext`) y se ejecutan a través del pipeline de MediatR — ver `src/Application/DependencyInjection.cs` para la cadena de `IPipelineBehavior` registrada (en orden: pre-procesador de logging, excepciones no controladas, autorización, validación, rendimiento). Para agregar comportamiento transversal a los requests, se agrega un behaviour genérico abierto ahí, no en cada handler individual.

#### Convención CQRS: `ICommand` / `ICommand<T>` / `IQuery<T>`

No hay un proyecto `Contracts` separado: el record del Command/Query y su Handler viven en el mismo archivo, dentro de `Application` (ver estructura de arriba). Aun así, todo request de MediatR en `Application` — salvo los relacionados con `Users` (Identity/Entra ID, que no pasan por MediatR) — debe tipar su intención usando los marcadores de `Application/Common/Messaging`:

- `ICommand` / `ICommand<TResponse>` — operación de escritura. Equivalen a `IRequest<Result>`/`IRequest<Result<TResponse>>`: **todo Command retorna el patrón Result** (ver sección siguiente), nunca `void`/`int`/etc. directo.
- `IQuery<TResponse>` — operación de solo lectura, retorna `TResponse` directo (no envuelve en Result).

Nunca implementes `IRequest`/`IRequest<T>` de MediatR directamente en un Command o Query; usa siempre el marcador correspondiente. Además:

- Un mismo tipo no puede implementar `ICommand`/`ICommand<T>` **y** `IQuery<T>` a la vez.
- El Handler de un `ICommand`/`ICommand<T>` debe vivir bajo un namespace que contenga `.Commands.`; el de un `IQuery<T>`, bajo uno que contenga `.Queries.` (ya lo garantiza la estructura de carpetas estándar).
- Todo Handler (`IRequestHandler<,>`/`IRequestHandler<>`) debe ser **`internal`**, nunca `public`: es un detalle de implementación del slice, solo se invoca vía `ISender.Send`/`TestApp.SendAsync`, nunca se referencia directo desde Web ni desde los tests.

Estas cuatro reglas se verifican automáticamente en `tests/ArchitectureTests` (`MessagingPoliciesTests`, `VisibilityTests`) — correr `dotnet test tests\ArchitectureTests\ArchitectureTests.csproj` antes de dar por terminado un caso de uso nuevo.

#### Commands retornan Result (FluentResults)

Los Commands usan [FluentResults](https://github.com/altmann/FluentResults) (`global using FluentResults;` en `Application`/`Infrastructure`/`Web`) en vez de lanzar excepciones para fallos de negocio esperables (p. ej. "la entidad no existe"). Patrón en un Handler:

```csharp
internal class UpdateTodoItemCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateTodoItemCommand, Result>
{
    public async Task<Result> Handle(UpdateTodoItemCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.TodoItems.FindAsync([request.Id], cancellationToken);

        if (entity is null)
        {
            return Result.Fail(new NotFoundError($"TodoItem {request.Id} was not found."));
        }

        entity.Update(request.Title, request.Done);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
```

- `ICommand` → Handler retorna `Result`; `ICommand<TResponse>` → Handler retorna `Result<TResponse>` (`Result.Ok(value)` en éxito).
- Los errores de negocio se representan con tipos en `Application/Common/Errors/ApplicationError.cs` (`NotFoundError`, `ConflictError`, ambos con un `ApplicationErrorType` — ver [SmartEnum](#smartenum-de-ardalis-en-vez-de-enum) más abajo), no con strings sueltos ni excepciones. Se agregan nuevos tipos de Error ahí a medida que aparecen nuevos casos.
- Esto **no reemplaza** el resto del pipeline basado en excepciones: `ValidationBehaviour` (FluentValidation) sigue lanzando `ValidationException` para requests con forma inválida, y `AuthorizationBehaviour` sigue lanzando `UnauthorizedAccessException`/`ForbiddenAccessException`. El patrón Result aplica específicamente al **resultado de negocio** que devuelve el Handler.
- Los endpoints de Web traducen `Result`/`Result<T>` a HTTP con `result.ToProblemHttpResult()` (`Web/Infrastructure/ProblemDetailsResultExtensions.cs`) cuando `IsFailed` — ver [Endpoints Web](#endpoints-web-registro-por-convención) más abajo.

#### Validators, DTOs y mappers viven en el archivo del feature

Para no "regar" la lógica de un caso de uso en varios archivos, el Validator (FluentValidation), los DTO/ViewModel de salida y el perfil de AutoMapper de un Command/Query van **en el mismo archivo** que el record y su Handler — no en `XxxValidator.cs`, `XxxDto.cs` ni `XxxVm.cs` separados. Ver `CreateTodoItem.cs`, `UpdateTodoList.cs` o `GetTodos.cs` como referencia: record, Handler, Validator (si aplica) y DTOs/VM (si aplica) uno debajo del otro en el mismo archivo. La única excepción son los modelos realmente compartidos entre features (p. ej. `Application/Common/Models/LookupDto.cs`), que sí quedan en `Common`.

Nota de estilo: un Validator que necesita ejecutar lógica imperativa en el constructor (`RuleFor(...)`, como en `CreateTodoListCommandValidator`) mantiene un constructor explícito en vez de primary constructor — un primary constructor no tiene cuerpo donde colocar esas llamadas. Ver más abajo.

### Primary constructors

`.editorconfig` marca `csharp_style_prefer_primary_constructors` en `warning`: toda clase cuyo constructor solo asigna parámetros a campos (Handlers, `IPipelineBehavior`, servicios de Infrastructure/Web, interceptors de EF Core, etc.) debe declararse con **primary constructor** y usar el parámetro directamente en el cuerpo de la clase, sin declarar el campo `private readonly` ni el constructor explícito.

```csharp
// Antes
public class CreateTodoItemCommandHandler : IRequestHandler<CreateTodoItemCommand, int>
{
    private readonly IApplicationDbContext _context;

    public CreateTodoItemCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<int> Handle(...) { _context.TodoItems.Add(...); ... }
}

// Ahora
internal class CreateTodoItemCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateTodoItemCommand, int>
{
    public async Task<int> Handle(...) { context.TodoItems.Add(...); ... }
}
```

Excepción: cuando el constructor necesita ejecutar lógica imperativa además de asignar campos (por ejemplo, un Validator con `RuleFor(...)` en el cuerpo, o algo que valide/transforme un parámetro antes de guardarlo), se mantiene un constructor explícito — un primary constructor no tiene "cuerpo" donde ejecutar esas sentencias.

### DDD: la lógica de negocio vive en Domain

Se usan principios de DDD en lo posible. La lógica de negocio (invariantes, reglas de construcción/transición de una entidad) debe volcarse **principalmente a `src/Domain`** y solo en segundo plano a `src/Application`:

- Las entidades (`TodoItem`, `TodoList`) no se construyen con `new Entidad { Prop = valor, ... }` desde fuera del dominio: exponen un **factory method estático** (`TodoItem.Create(...)`, `TodoList.Create(...)`) y **métodos de comportamiento** (`entity.Update(...)`, `entity.UpdateDetail(...)`, `entity.Rename(...)`, `entity.UpdateColour(...)`, `list.AddItem(...)`) que encapsulan cómo cambia el estado. Las propiedades mutables son `{ get; private set; }`; el constructor sin parámetros es `private` (solo para materialización de EF Core).
- Los Handlers de Application, en consecuencia, no asignan propiedades de entidades directamente — llaman a los métodos de dominio. Application se limita a orquestar (cargar la entidad, invocar el método de dominio, guardar) y a lo que necesita infraestructura para resolverse (p. ej. la regla "el título debe ser único" en `CreateTodoListCommandValidator` vive en Application porque requiere consultar la base de datos, no porque sea una excepción a la regla).
- Si una colección navegación necesita exponerse de solo lectura (`IReadOnlyCollection<T>`) mientras EF Core sigue pudiendo materializarla, se usa un campo de respaldo (`private readonly List<T> _items`) configurado explícitamente en el `IEntityTypeConfiguration` con `.Navigation(...).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field)` — ver `TodoListConfiguration`.

### Endpoints Web (registro por convención)

Las clases en `src/Web/Endpoints/*.cs` implementan `IEndpointGroup` (método estático `Map(RouteGroupBuilder)`). `WebApplicationExtensions.MapEndpoints` (`src/Web/Infrastructure`) inspecciona el assembly por reflexión al iniciar, registra automáticamente cada implementación de `IEndpointGroup` bajo `/api/{NombreClase}` (sobreescribible con una propiedad estática `RoutePrefix`) y la etiqueta en OpenAPI con el nombre de la clase. No existe una lista manual de registro de endpoints que mantener — basta con agregar una nueva clase `IEndpointGroup`. Los endpoints llaman a `ISender.Send(command/query)`.

Para un Command, el endpoint debe declarar `ProblemHttpResult` como variante del `Results<...>` de retorno y ramificar sobre `result.IsSuccess`, delegando el camino de error a `result.ToProblemHttpResult()`:

```csharp
public static async Task<Results<NoContent, BadRequest, ProblemHttpResult>> UpdateTodoItem(
    ISender sender, int id, UpdateTodoItemCommand command)
{
    if (id != command.Id) return TypedResults.BadRequest();

    var result = await sender.Send(command);

    return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemHttpResult();
}
```

`src/Web/Infrastructure/ProblemDetailsResultExtensions.cs` es el contraparte de `ProblemDetailsExceptionHandler` para el patrón Result: donde `ProblemDetailsExceptionHandler` traduce **excepciones** a `ProblemDetails` (`IExceptionHandler`, intercepta lo lanzado), `ProblemDetailsResultExtensions.ToProblemHttpResult()` traduce un `Result`/`Result<T>` **fallido** a `ProblemDetails` (no es un `IExceptionHandler` porque un Result fallido no es una excepción — es un valor de retorno normal que el propio endpoint debe consultar). Mapea `ApplicationErrorType` a código HTTP (`NotFound` → 404, `Conflict` → 409, cualquier otro → 400).

### Acceso a datos

`ApplicationDbContext` (Infrastructure) usa SQLite, configurado a través del connection string `Services.Database` que provee Aspire. Siempre se aplican dos `ISaveChangesInterceptor`: `AuditableEntityInterceptor` (sella los campos de auditoría Created/Modified) y `DispatchDomainEventsInterceptor` (publica los eventos de dominio encolados a través de MediatR en `SaveChanges`). Las configuraciones de entidades están en `Infrastructure/Data/Configurations`.

#### Base de datos: eliminar y recrear, sin migraciones

El patrón obligatorio de este repo es **eliminar la base de datos y volver a crearla** en cada arranque en desarrollo (`ApplicationDbContextInitialiser.InitialiseAsync`, vía `EnsureDeletedAsync` + `EnsureCreatedAsync`) — no se usan EF Core Migrations. Esto es intencional mientras el proyecto es nuevo y el esquema todavía cambia seguido: no agregues `dotnet ef migrations add`/carpeta `Migrations` a menos que se decida explícitamente abandonar este patrón.

#### Consultas paginadas: `PaginatedList<T>`

Toda Query que retorne una colección potencialmente grande debe preferir paginación usando el contenedor genérico `Application/Common/Models/PaginatedList.cs` en vez de devolver la lista completa:

```csharp
Lists = await PaginatedList<TodoListDto>.CreateAsync(
    context.TodoLists.AsNoTracking().ProjectTo<TodoListDto>(mapper.ConfigurationProvider),
    request.PageNumber,
    request.PageSize,
    cancellationToken
);
```

`PaginatedList<T>` expone `Items`, `PageNumber`, `TotalPages`, `TotalCount`, `HasPreviousPage`, `HasNextPage`. `GetTodosQuery` es una excepción deliberada (agrega listas+items en un único VM pequeño y acotado, no un listado paginable), pero cualquier Query nueva que devuelva un listado debe usar este contenedor por defecto.

### SmartEnum de Ardalis en vez de `enum`

Los enumeradores del dominio/aplicación se modelan con [`Ardalis.SmartEnum`](https://github.com/ardalis/SmartEnum) en vez de `enum` de C# — ver `Domain/Enums/PriorityLevel.cs` y `Application/Common/Errors/ApplicationError.cs` (`ApplicationErrorType`):

```csharp
public sealed class PriorityLevel : SmartEnum<PriorityLevel>
{
    public static readonly PriorityLevel None = new(nameof(None), 0);
    public static readonly PriorityLevel Low = new(nameof(Low), 1);

    private PriorityLevel(string name, int value) : base(name, value) { }
}
```

Al ser una clase, el default implícito es `null`, no el primer valor — toda propiedad de este tipo debe inicializarse explícitamente (`= PriorityLevel.None`). Para persistirlo con EF Core se configura una conversión explícita en el `IEntityTypeConfiguration` (`.HasConversion(p => p.Value, v => PriorityLevel.FromValue(v))` — ver `TodoItemConfiguration`).

### Orquestación con Aspire

`src/AppHost/Program.cs` es el punto de entrada real (`DefaultStartup` en el `.slnx`): declara un entorno de Azure Container App, un recurso SQLite nombrado vía `Services.Database`, y el proyecto `Web` como recurso de Aspire con una URL de Scalar API Reference. `tests/TestAppHost` es un AppHost paralelo y mínimo usado por `Application.FunctionalTests` (vía `Aspire.Hosting.Testing`) para levantar la app + SQLite en pruebas funcionales sin los recursos cloud del AppHost completo.

Este repositorio ya trae configurado el servidor MCP de Aspire (`.vscode/mcp.json` y `.mcp.json`, ambos apuntan a `aspire agent mcp`), por lo que las herramientas MCP de Aspire están disponibles sin configuración adicional siempre que el CLI `aspire` esté instalado.

## Configuración, certificados y secrets

### Certificados HTTPS de desarrollo

Una vez instalado el SDK de .NET (ver `README.md`), confía el certificado HTTPS de desarrollo para que el AppHost, el dashboard de Aspire y las llamadas entre servicios no muestren advertencias de certificado:

```bash
dotnet dev-certs https --trust
```

Verificar que quedó confiado:

```bash
dotnet dev-certs https --check --trust
```

Si el certificado quedó en mal estado (advertencias persistentes, error de confianza), regenéralo:

```bash
dotnet dev-certs https --clean
dotnet dev-certs https --trust
```

### Gestión de secrets

En desarrollo local, los secrets se gestionan con **User Secrets** de .NET sobre el proyecto `AppHost` (es el proyecto que declara los recursos/parámetros del modelo de Aspire — ver "Todo pasa por Aspire"):

```bash
dotnet user-secrets init --project src\AppHost
dotnet user-secrets set "Parameters:NombreDelSecret" "valor-real" --project src\AppHost
```

Cada parámetro secreto debe declararse en `src/AppHost/Program.cs` con `builder.AddParameter("NombreDelSecret", secret: true)` y pasarse al recurso que lo necesite (`.WithEnvironment(...)`, `.WithReference(...)`, etc.). El archivo `secrets.json` real vive fuera del repo (perfil de usuario, `%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json` en Windows) y **nunca se commitea**.

En producción, el equivalente es **Azure Key Vault**: `src/Web/DependencyInjection.cs` (`AddKeyVaultIfConfigured`) agrega Key Vault como proveedor de configuración cuando existe la variable `AZURE_KEY_VAULT_ENDPOINT`, inyectada por el propio proceso de publicación de Aspire — no se edita a mano.

### Dónde va cada dato de configuración

Regla de reparto entre archivos, para no mezclar secretos con configuración normal:

| Dato | Dónde va |
| --- | --- |
| Secrets, API keys, claves de producto, contraseñas, tokens, cualquier información sensible | **User Secrets** (dev) / **Azure Key Vault** (prod) — nunca en un archivo del repo |
| Nombres de conexión, URLs, flags y demás variables no sensibles para desarrollo local | `appsettings.Development.json` |
| Configuración de producción | **No se edita a mano en el repo** — el proceso de puesta en producción hace el llenado de `appsettings.json`/variables de entorno al desplegar |

`appsettings.json` de cada proyecto debe mantenerse como plantilla mínima (claves esperadas, sin valores reales de producción); los valores concretos de cada entorno los provee Aspire (User Secrets en dev, Key Vault/env vars en prod).

**Regla de paridad de claves:** toda variable que exista en `appsettings.Development.json` debe existir también en `appsettings.json`, con el mismo nombre/estructura pero **con valor vacío** (`""`). Ese placeholder vacío es lo que el proceso de puesta en producción llena por variable substitution al desplegar — si falta la clave en `appsettings.json`, la substitution no tiene dónde escribir. Ejemplo ya aplicado en el repo: `src/Web/appsettings.json` tiene `ConnectionStrings:AnalisisSentimientoDb` en `""`, mientras que `src/Web/appsettings.Development.json` trae el valor real de desarrollo (SQLite local).

## Pruebas

- **`Domain.UnitTests`** / **`Application.UnitTests`** / **`Web.UnitTests`** — NUnit + Moq + Shouldly, sin dependencias externas. `Web.UnitTests` cubre helpers puros de `src/Web/Infrastructure` (p. ej. `ProblemDetailsResultExtensionsTests`) que no requieren levantar un host HTTP.
- **`Infrastructure.IntegrationTests`** — ejercita Infrastructure contra una base de datos real.
- **`Application.FunctionalTests`** — levanta la app de extremo a extremo mediante `TestAppHost`/`WebApiFactory` (`Aspire.Hosting.Testing` + `Microsoft.AspNetCore.Mvc.Testing`), resetea la base de datos entre pruebas con `Respawn` (`Infrastructure/DatabaseResetter.cs`), y valida a través de los handlers de MediatR (`TestBase`), reflejando la estructura de vertical slices de `src/Application` (una clase de prueba por command/query).
- **`ArchitectureTests`** — NUnit + ArchUnitNET, sin base de datos. Hace cumplir por código las reglas de la sección "Arquitectura" de arriba y **debe pasar antes de considerar terminado cualquier cambio de arquitectura o caso de uso nuevo**:
  - `LayerTests` — dirección de dependencias entre Domain/Application/Infrastructure/Web (p. ej. Domain no depende de nada; Web no depende de `ApplicationDbContext`/`IApplicationDbContext` directamente).
  - `MessagingPoliciesTests` — todo request en Application (salvo `Users`) debe tipar `ICommand`/`ICommand<T>`/`IQuery<T>`, nunca `IRequest`/`IRequest<T>` crudo; un tipo no puede ser Command y Query a la vez; el Handler de un Command vive bajo `.Commands.` y el de un Query bajo `.Queries.`.
  - `VisibilityTests` — todo `IRequestHandler<,>`/`IRequestHandler<>` en Application debe ser `internal`.
  - `WebAndSlicePoliciesTests` — todo `IEndpointGroup` en Web debe depender de `ISender` (excepto `Users`, que envuelve `MapIdentityApi` directo); Web no debe depender de EF Core/`DbContext`/`DbSet`.
  - `TestConventionsTests` — en `Application.FunctionalTests`, los tests bajo una carpeta `Commands` no deben usar `TestApp.SendAsync` con un Query y viceversa.
