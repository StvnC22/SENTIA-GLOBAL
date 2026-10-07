# AnalisisSentimiento

## Sentia Global

El panel web reúne comentarios públicos de Instagram, Facebook, TikTok y X, los clasifica como
positivos, neutrales o negativos y permite buscar, ordenar, filtrar y exportar la vista
actual en CSV. Para iniciar el MVP:

```powershell
aspire start --apphost src\AppHost\AppHost.csproj
aspire wait webapi
```

La versión actual usa datos demostrativos y un clasificador lingüístico local para que
el flujo completo funcione sin credenciales. La recolección de comentarios reales
requiere registrar una aplicación y configurar los tokens oficiales de cada plataforma.
El botón **Configurar API keys** permite guardarlos cifrados en el equipo; el panel solo
consulta si cada clave está configurada y nunca devuelve su valor al navegador.

### Ejecutable para Windows

Ejecuta `PUBLICAR_EXE.cmd` para crear una versión autónoma en
`dist\SentiaGlobal\SentiaGlobal.exe`. Al abrir el EXE, el servidor local se inicia y el panel
se abre automáticamente en el navegador predeterminado. Mantén la ventana del programa
abierta mientras usas el panel y ciérrala para detener la aplicación.

El proyecto fue generado usando el [Clean.Architecture.Solution.Template](https://github.com/jasontaylordev/CleanArchitecture) versión 10.8.0.

## Requisitos previos

Antes de empezar a trabajar en el proyecto, instala lo siguiente en tu equipo.

### 1. .NET SDK

Instala la versión del SDK indicada en `global.json` (o superior dentro del mismo canal) desde la [página oficial de descargas de .NET](https://dotnet.microsoft.com/download).

Verifica la instalación:

```bash
dotnet --version
```

Con el SDK ya instalado, confía el certificado HTTPS de desarrollo (necesario para correr el AppHost/Aspire dashboard sin advertencias de certificado):

```bash
dotnet dev-certs https --trust
```

Puedes verificar que quedó confiado con `dotnet dev-certs https --check --trust`, y regenerarlo con `dotnet dev-certs https --clean` seguido de `dotnet dev-certs https --trust` si da problemas.

### 2. .NET Aspire

Instala (o actualiza) los workloads/herramientas de Aspire siguiendo la [guía oficial de instalación de .NET Aspire](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling). Esto incluye el CLI `aspire`, necesario para ejecutar el `AppHost` y para el servidor MCP configurado en este repo (ver más abajo).

Verifica la instalación:

```bash
aspire --version
```

### 3. CSharpier (formateo de código)

Instala [CSharpier](https://csharpier.com/) como herramienta global de .NET para formatear el código de forma consistente:

```bash
dotnet tool install -g csharpier
```

Para formatear toda la solución:

```bash
dotnet csharpier .
```

Si usas VS Code, instala también la [extensión de CSharpier](https://marketplace.visualstudio.com/items?itemName=csharpier.csharpier-vscode) para formatear automáticamente al guardar.

### 4. Docker o Podman (contenedores)

Algunos recursos de Aspire (por ejemplo, bases de datos o servicios en contenedor) requieren un motor de contenedores. Instala **uno** de los dos siguiendo la documentación oficial:

- [Instalar Docker](https://docs.docker.com/get-started/get-docker/)
- [Instalar Podman](https://podman.io/docs/installation)

### 5. Visual Studio Code + C# Dev Kit

Si usas VS Code, instala la extensión [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) (incluye C# y soporte para .NET Aspire).

#### Configurar F5 (debug) con el AppHost en HTTPS

Para depurar correctamente todo el sistema (Aspire dashboard + servicios), configura el debugger de VS Code apuntando al proyecto `AppHost` con el perfil HTTPS:

1. Abre la paleta de comandos (`Ctrl+Shift+P`) y ejecuta **"Debug: Add Configuration..."**.
2. Selecciona **C#**.
3. Elige el proyecto `AppHost` (`src/AppHost/AppHost.csproj`).
4. Selecciona el perfil de lanzamiento `https`.
5. Guarda la configuración generada en `.vscode/launch.json`.

Con esto, al presionar `F5` se levantará el AppHost en HTTPS junto con el resto de servicios orquestados por Aspire.

### 6. MCP de Aspire

Este repositorio ya incluye configurado el servidor MCP de Aspire (`.vscode/mcp.json` y `.mcp.json`), que expone `aspire agent mcp`. No requiere configuración adicional: basta con tener el CLI `aspire` instalado (paso 2) para que el agente pueda usarlo.

## Compilar (Build)

Ejecuta `dotnet build` para compilar la solución.

## Ejecutar (Run)

Para ejecutar la aplicación:

```bash
dotnet run --project .\src\AppHost
```

Se abrirá automáticamente el dashboard de Aspire, mostrando las URLs y los logs de la aplicación.

Si Aspire o el certificado HTTPS aún no están configurados, puedes probar el panel web directamente
por HTTP (no necesita el dashboard ni un certificado de desarrollo):

```bash
dotnet run --project .\src\Web --no-open
```

Luego abre `http://127.0.0.1:5176`. Esta modalidad conserva la consulta de redes, los contadores,
la clasificación y la configuración de API keys; solo omite el dashboard de Aspire.

## Estilos de código y formateo

La plantilla incluye soporte de [EditorConfig](https://editorconfig.org/) para mantener un estilo de código consistente entre distintos editores/IDEs. El archivo **.editorconfig** define los estilos aplicables a esta solución.

Además, usa [CSharpier](https://csharpier.com/) (ver requisitos previos) para formatear automáticamente el código antes de hacer commit.

## Dependencias desactualizadas

Para revisar si hay paquetes NuGet desactualizados en la solución, usa la herramienta `outdated` de .NET:

```bash
dotnet outdated
```

Si no la tienes instalada:

```bash
dotnet tool install -g dotnet-outdated-tool
```

## Scaffolding de código

La plantilla incluye soporte para generar (scaffold) nuevos comandos y consultas.

Ejecuta los comandos desde la carpeta `.\src\Application\`.

Crear un nuevo comando:

```bash
dotnet new ca-usecase --name CreateTodoList --feature-name TodoLists --usecase-type command --return-type int
```

Crear una nueva consulta:

```bash
dotnet new ca-usecase -n GetTodos -fn TodoLists -ut query -rt TodosVm
```

Si aparece el error *"No templates or subcommands found matching: 'ca-usecase'."*, instala la plantilla e inténtalo de nuevo:

```bash
dotnet new install Clean.Architecture.Solution.Template::10.8.0
```

## Pruebas (Test)

La solución contiene pruebas unitarias, de integración, funcionales y **de arquitectura**.

Para ejecutar las pruebas:

```bash
dotnet test
```

El proyecto `tests/ArchitectureTests` hace cumplir por código las reglas de capas y de mensajería CQRS descritas en `CLAUDE.md` (dirección de dependencias, uso de `ICommand`/`ICommand<T>`/`IQuery<T>`, handlers `internal`, endpoints dependiendo de `ISender`, etc.). Ejecútalo antes de dar por terminado un caso de uso nuevo o un cambio de arquitectura:

```bash
dotnet test tests\ArchitectureTests\ArchitectureTests.csproj
```

## Configuración, secrets y publicación

Todo recurso (base de datos, API externa, parámetro, secret) se declara en `src/AppHost/Program.cs`: es el modelo de Aspire el que genera el manifiesto usado para publicar la aplicación, así que nada de configuración de recursos debe hacerse por fuera de Aspire ni hardcodeado en los proyectos.

### Gestión de secrets

En desarrollo local se usan **User Secrets** de .NET sobre el proyecto `AppHost`:

```bash
dotnet user-secrets init --project src\AppHost
dotnet user-secrets set "Parameters:NombreDelSecret" "valor-real" --project src\AppHost
```

El secret declarado así se pasa al recurso que lo necesite desde `src/AppHost/Program.cs` (`builder.AddParameter("NombreDelSecret", secret: true)`). El archivo con los valores reales vive fuera del repo, en el perfil del usuario, y nunca se commitea.

### Dónde va cada dato de configuración

- **Secrets, claves de producto, contraseñas, tokens y cualquier información sensible** → User Secrets (desarrollo) o Azure Key Vault (producción). Nunca en un archivo del repositorio.
- **Nombres de conexión, URLs y demás variables no sensibles** → `appsettings.Development.json`.
- **Configuración de producción** → no se edita a mano en el repo; el proceso de puesta en producción hace el llenado de `appsettings.json` al desplegar.

**Regla de paridad:** toda variable que exista en `appsettings.Development.json` debe existir también en `appsettings.json`, con el mismo nombre, pero **con valor vacío** (`""`) — ese placeholder es lo que el proceso de despliegue llena por variable substitution. Ejemplo: `ConnectionStrings:AnalisisSentimientoDb` está vacío en `appsettings.json` y tiene el valor real de desarrollo en `appsettings.Development.json`.

## Estilo de código y arquitectura

Además de lo indicado en "Estilos de código y formateo", este proyecto sigue estas convenciones (detalladas con ejemplos en `CLAUDE.md`):

- **Primary constructors**: toda clase cuyo constructor solo asigna parámetros a campos (Handlers, behaviours, servicios) se declara con primary constructor, sin campo `private readonly` ni constructor explícito.
- **Validators, DTOs y mappers en el archivo del feature**: el Validator, los DTO/ViewModel y el perfil de AutoMapper de un Command/Query van en el mismo archivo que el record y su Handler, no en archivos separados.
- **DDD**: se usan principios de Domain-Driven Design en lo posible. La lógica de negocio se vuelca principalmente a `src/Domain` (entidades con factory methods y métodos de comportamiento, propiedades con `private set`) y solo en segundo plano a `src/Application` (orquestación y reglas que requieren infraestructura, como validar unicidad contra la base de datos).
- **Commands retornan Result**: todo Command usa [FluentResults](https://github.com/altmann/FluentResults) (`Result`/`Result<T>`) en vez de lanzar excepciones para fallos de negocio esperables, con tipos de Error propios (`NotFoundError`, `ConflictError`). Los endpoints traducen un Result fallido a `ProblemDetails` con `ProblemDetailsResultExtensions.ToProblemHttpResult()`, la contraparte de `ProblemDetailsExceptionHandler` para errores que no son excepciones.
- **Consultas paginadas con `PaginatedList<T>`**: toda Query que devuelva un listado potencialmente grande debe preferir el contenedor genérico `Application/Common/Models/PaginatedList.cs` en vez de devolver la colección completa.
- **SmartEnum en vez de `enum`**: los enumeradores se modelan con [Ardalis.SmartEnum](https://github.com/ardalis/SmartEnum), no con `enum` de C#.
- **Base de datos: eliminar y recrear, sin migraciones**: patrón obligatorio mientras el proyecto es nuevo — no se usan EF Core Migrations.

## Archivos permitidos en el repositorio

No se deben subir archivos binarios de documentos (PDF, DOCX, DOC, XLSX, XLS, PPTX, PPT, ZIP, etc.) a este repositorio. Solo se aceptan **imágenes usadas como logos** (PNG, JPG/JPEG, SVG, ICO, WEBP). El `.gitignore` ya bloquea estos formatos de documento.

Cualquier caso particular o excepción a esta regla debe hablarse antes con el **administrador de DIIT**.

## Compartir la aplicación

Envía la carpeta completa `dist\SentiaGlobal` (no solo el EXE). La persona receptora puede
abrir `SentiaGlobal.exe` directamente en Windows: no necesita instalar .NET, Docker ni Aspire.

### Fuentes globales

Las URLs de Instagram, Facebook, TikTok y X no están fijadas a una institución. Se configuran
en `src/Web/appsettings.Development.json` dentro de `SocialListening:Profiles`, según las cuentas
o fuentes públicas que se quieran monitorear. El tema escrito en el panel filtra los comentarios
obtenidos; no sustituye la URL de la fuente.
La primera ejecución crea SQLite y `.keys` junto al ejecutable. Las API keys pueden quedar vacías;
el panel funciona con los comentarios demostrativos incluidos.

## Ayuda

Para saber más sobre la plantilla, visita el [sitio web del proyecto](https://cleanarchitecture.jasontaylor.dev). Allí encontrarás guías adicionales, podrás solicitar nuevas funcionalidades, reportar errores y conversar con otros usuarios de la plantilla.
