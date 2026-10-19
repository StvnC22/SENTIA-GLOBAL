# Limitaciones conocidas y diagnóstico

## Aplicación abre pero muestra cero comentarios

1. Comprobar URL válida por red en módulo o configuración.
2. Confirmar dominio/ruta correctos.
3. Confirmar token Apify en configuración.
4. Verificar que el perfil/post sea público y tenga créditos.
5. Consultar `GET /api/social-comments?...` y leer `message`.
6. Usar `refresh=true` para descartar caché.

Sin URLs, una llamada puede devolver `comments=[]`, `dataSource="live"`,
`message=null`. No significa que el actor funcione; significa que no se seleccionó una
fuente.

## Prueba de integración de X

`ShouldReturnEmptyWhenXHasNoPostsOrReplies` espera `DataSource = demo`, pero el proveedor
actual devuelve `live`. Es una inconsistencia de política, no un fallo de compilación.
Decidir una sola solución: actualizar el test a `live` si ya no se promete demo
automático, o cambiar Composite para devolver demo en ausencia de datos y actualizar
README/UI. No corregir solo una parte.

## Puerto ocupado

El modo desktop prueba de 5388 a 5407. Leer la línea `Sentia Global ->` en la terminal.
Para consultar el proceso sin matar otros proyectos:

```powershell
Get-NetTCPConnection -LocalPort 5388 -ErrorAction SilentlyContinue
```

No terminar todos los procesos `dotnet` indiscriminadamente.

## La base parece perder datos

Es esperado en Development por `EnsureDeletedAsync`. En producción desactivar ese
borrado y usar migraciones/backup.

## Claves no aparecen configuradas

Revisar mismo usuario Windows, directorio Data Protection, application name, purpose,
ruta escribible y que el archivo no se haya copiado desde otra máquina. Un archivo
corrupto hace que el store devuelva `ApiCredentials.Empty` por diseño.

## Gemini no clasifica

Revisar logs de modelo/status, token, cuota y JSON. Aunque Gemini falle, debe ocurrir
fallback local. Si no ocurre, revisar el registro DI de `ISentimentAnalyzer`.

## Apify espera demasiado

Una red puede tardar hasta 3 minutos. El backend serializa redes intencionalmente.
Revisar `CancellationToken`, timeouts y resiliencia antes de aumentarlos.

## Frontend vacío o contrato roto

Verificar nombres/tipos de `SocialListeningDashboard`, consola del navegador, versión de
`app.js` y localStorage. En Development se agrega `Cache-Control: no-store`, pero una
pestaña puede conservar estado local o un job antiguo.

## Publicación EXE falla

Distribuir la carpeta completa, comprobar permisos de `.keys` y SQLite, runtime
`win-x64`, SQLite nativo y `wwwroot`. Comparar primero con `dotnet run`.

## Logs seguros

Registrar plataforma, actor, cantidad, duración y código HTTP. Nunca registrar tokens,
bodies completos con datos personales, payloads de credenciales, cookies ni headers de
autorización.

## Checklist de entrega externa

- [ ] Entregar repo y carpeta `manual`.
- [ ] Explicar Web directo o Aspire.
- [ ] Compartir secretos por canal seguro, nunca por commit.
- [ ] Explicar el borrado de SQLite en Development.
- [ ] Registrar resultado de build/test y fallos conocidos.
- [ ] Verificar endpoints/UI con datos anonimizados.
- [ ] Actualizar manual junto con todo cambio de contrato.
