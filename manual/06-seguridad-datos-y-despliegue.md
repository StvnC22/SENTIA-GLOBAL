# Seguridad, datos y despliegue

## Persistencia

`ApplicationDbContext` hereda de `IdentityDbContext<ApplicationUser>` y expone
`TodoLists`, `TodoItems` y las tablas estándar de Identity. Los comentarios sociales no
forman parte del DbContext: viven en memoria, caché y localStorage.

La base se crea con `EnsureCreated`, no con migraciones. En Development,
`InitialiseDatabaseAsync` llama a `EnsureDeletedAsync` antes de `EnsureCreatedAsync`.
Cada reinicio elimina usuarios, roles, Todo y cualquier extensión futura de SQLite.
Es aceptable para plantilla/demo, no para producción.

Antes de producción se necesita una estrategia de migraciones/upgrade idempotente,
backup y seed controlado.

## Protección de claves

`EncryptedFileApiCredentialStore`:

1. Resuelve ruta mediante `ApiCredentials:Path` o `LocalAppStorage`.
2. Serializa `ApiCredentials` a JSON.
3. Protege JSON con `IDataProtector` y propósito
   `EmocionesULEAM.ApiCredentials.v1`.
4. Escribe `.tmp` y reemplaza el archivo final.
5. Usa `SemaphoreSlim` para escrituras concurrentes.

En Windows desktop, `Program.cs` agrega `ProtectKeysWithDpapi()`. Copiar solo el archivo
de credenciales a otra máquina no garantiza que pueda descifrarse.

`LocalAppStorage` prueba LocalApplicationData, ApplicationData, Temp y `.emotions-uleam`
junto al ejecutable. Si todos fallan usa el último candidato, pero guardar credenciales
puede fallar por permisos.

## Identity

El inicializador crea en Desarrollo el rol `Administrator` y el usuario
`administrator@localhost` con contraseña de desarrollo `Administrator1!`. Es una semilla
visible en código: eliminar, cambiar o aislar antes de publicar. Nunca usarla en
producción.

## CORS, HTTPS y health

- `app.UseCors` permite cualquier método, header y origen; es demasiado abierto para
  producción.
- Fuera de desktop se usa redirección HTTPS y HSTS.
- Health endpoints solo se mapean en Development.
- OpenAPI/Scalar se agregan fuera del modo desktop.

## Publicación

`Web.csproj` tiene perfil `WindowsExe` con `win-x64`, self-contained, single-file,
trimming parcial y globalización invariante. El trimming puede revelar dependencias por
reflection/serialización; probar el EXE, no solo `dotnet build`.

## Integraciones externas

| Servicio | Base URL | Timeout | Datos enviados |
|---|---|---:|---|
| Apify | `https://api.apify.com/v2/` | 6 min HttpClient; 3 min/red | URLs y parámetros |
| Gemini | `https://generativelanguage.googleapis.com/` | 60 s | IDs y textos |
| Key Vault | endpoint configurado | Azure SDK | Configuración/secretos |

No enviar texto de comentarios a Gemini sin confirmar la política de datos de la
organización. Sin Gemini se usa el clasificador local.
