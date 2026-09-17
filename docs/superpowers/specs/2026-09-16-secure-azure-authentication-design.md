# Diseño de seguridad para autenticación y despliegue en Azure

## Objetivo

Corregir la exposición de datos sensibles antes de volver a iniciar `crownbuilders-portal` en Azure. La aplicación debe permitir registro e inicio de sesión seguros, proteger perfiles y operaciones por identidad, y dejar de almacenar datos de tarjetas o contraseñas en texto plano.

## Estado y restricciones

- Azure App Service permanecerá detenido durante la corrección.
- Los registros existentes en Azure son datos de prueba y el usuario autorizó eliminarlos.
- Se conservará la arquitectura actual: aplicación .NET MAUI/Blazor Hybrid, API ASP.NET Core, JWT, EF Core y Azure SQL.
- No se implementará un sistema de pagos nuevo en este alcance. El formulario que recoge tarjetas quedará deshabilitado hasta integrar Stripe correctamente.
- No se harán refactorizaciones ajenas a autenticación, autorización, exposición de datos, almacenamiento local y despliegue seguro.

## Superficie pública y privada

La API usará una política de autorización denegada por defecto; cada excepción pública llevará autorización anónima explícita. Las únicas operaciones anónimas serán:

- `GET /api/health`.
- Registro de trabajador y compañía.
- Inicio de sesión de trabajador y compañía.
- Comprobación de correo necesario para el registro.
- Envío o validación de SMS requerido por el registro, con limitación de solicitudes.

Necesitarán un JWT válido:

- Listados y perfiles de trabajadores y compañías.
- Actualización y eliminación de cuentas.
- Registro o actualización del token FCM.
- Conversaciones, mensajes y reseñas.
- Cualquier operación de suscripción o pago que permanezca accesible.

Los endpoints que actúan sobre una cuenta comprobarán el identificador y tipo del JWT. Un trabajador no podrá actuar como compañía, y ningún usuario podrá modificar o eliminar una cuenta distinta. Una operación sin sesión devolverá `401`; una operación autenticada sin permiso devolverá `403`.

## Contratos de la API

Los controladores dejarán de recibir o devolver directamente entidades de EF Core. Se usarán contratos separados para:

- Registro.
- Inicio de sesión.
- Edición del perfil propio.
- Perfil público de trabajador.
- Perfil público de compañía.
- Actualización del token FCM.
- Respuesta de autenticación.

Los perfiles públicos contendrán solo datos necesarios para el directorio y la comunicación, por ejemplo nombre, oficio o especialidad, ciudad, foto, experiencia, disponibilidad y datos de contacto que el producto ya muestra. Nunca incluirán:

- `PasswordHash`.
- Contraseña recibida durante el registro o inicio de sesión.
- `FcmToken`.
- Número de tarjeta, CVC, vencimiento o nombre del titular.
- Tokens JWT o configuración interna.

Las consultas proyectarán directamente a estos contratos para que los campos sensibles no se carguen ni serialicen accidentalmente.

## Contraseñas y autenticación

La columna `Password` será sustituida por `PasswordHash` en trabajadores y compañías. El servidor usará `PasswordHasher<TUser>` de ASP.NET Core:

1. El registro valida la contraseña y guarda solamente su hash.
2. El inicio de sesión busca al usuario por correo y verifica el hash.
3. Una respuesta fallida no indicará si falló el correo o la contraseña.
4. El valor de la contraseña no se registrará en logs ni se devolverá a la aplicación.

No habrá compatibilidad con contraseñas antiguas porque todos los usuarios actuales son de prueba y se eliminarán antes de habilitar la nueva versión.

El JWT conservará los claims actuales de identificador y tipo de usuario. La autorización reutilizará esos claims mediante una función pequeña y común para validar propiedad, evitando repetir comparaciones inconsistentes en cada controlador.

## Datos de pago

Se eliminarán del modelo y de Azure SQL:

- `NombreTitular`.
- `NumeroTarjeta`.
- `FechaExpiracion`.
- `CVC`.

La aplicación móvil dejará de solicitarlos, enviarlos y guardarlos. También se purgarán sus copias locales. `FormaPago` y `MontoPago` podrán conservarse únicamente si las pantallas actuales los necesitan como información no sensible; no representan una autorización de cobro.

El flujo de tarjeta quedará deshabilitado con un mensaje claro. Una integración futura deberá utilizar Stripe Checkout, Payment Sheet o tokens equivalentes para que los datos de tarjeta nunca atraviesen esta API ni se almacenen en Azure SQL o SQLite.

## Almacenamiento local

Los modelos locales y las transformaciones dejarán de persistir:

- Contraseñas.
- Datos de tarjeta.
- CVC.
- Token FCM como parte del perfil visible o serializable.

Se añadirá una limpieza única y acotada de las filas locales de perfil que puedan contener esos valores. No se borrarán otros datos del dispositivo fuera de esas tablas. La sesión conservará solo la información mínima necesaria y el JWT mediante el mecanismo existente, revisado para evitar que aparezca en logs.

## Migración y limpieza de Azure SQL

Se creará una migración EF Core que pueda revisarse como SQL antes de ejecutarla. La operación será transaccional cuando SQL Server lo permita y seguirá este orden:

1. Eliminar `Resenas`.
2. Eliminar `Mensajes`.
3. Eliminar `Conversaciones`.
4. Eliminar `Trabajadores`.
5. Eliminar `Companias`.
6. Renombrar las columnas `Password` a `PasswordHash`.
7. Eliminar las cuatro columnas de tarjeta de `Companias`.
8. Registrar la migración en `__EFMigrationsHistory` mediante EF Core.

Antes de ejecutar se confirmará el servidor y la base de datos de destino. Se aprovechará el respaldo automático de Azure SQL como recuperación; además, se validarán conteos antes y después. No se eliminarán tablas ni la base de datos completa.

## CORS, errores y abuso

La política CORS permisiva actual se eliminará. Como el cliente principal es MAUI y no depende del control CORS del navegador, la API no anunciará acceso para orígenes arbitrarios. Si se identifica un consumidor web real, se configurará una lista explícita de orígenes mediante variables de entorno.

Los controladores devolverán códigos HTTP coherentes y mensajes genéricos al cliente. Los detalles de excepciones se escribirán únicamente en logs estructurados y sin secretos, credenciales, tokens ni datos personales innecesarios.

Los endpoints públicos tendrán limitación por dirección IP: inicio de sesión permitirá hasta 10 solicitudes por minuto; registro, comprobación de correo y SMS permitirán hasta 5 solicitudes por minuto. Al superar el límite responderán `429`. Las entradas se validarán por longitud y formato para reducir abuso y respuestas inesperadas.

## Componentes afectados

- Configuración de autenticación, autorización, CORS y rate limiting en `CrownBuilders.API/Program.cs`.
- Entidades, contexto y una nueva migración EF Core.
- Contratos de entrada y salida de autenticación y perfiles.
- `AuthController` y `PerfilController`.
- Controladores de mensajes y reseñas para exigir autenticación y validar participantes.
- Controladores de SMS y Stripe para limitar acceso y no exponer excepciones.
- Servicios y páginas MAUI que registran, autentican, editan perfiles o guardan datos localmente.
- Pruebas enfocadas en seguridad y configuración de despliegue.

## Estrategia de pruebas

Las pruebas mínimas cubrirán:

- Registro guarda un hash y nunca la contraseña original.
- Inicio de sesión acepta una contraseña correcta y rechaza una incorrecta.
- Respuestas de perfiles y directorios no contienen nombres ni valores sensibles.
- Endpoints privados sin JWT devuelven `401`.
- Un usuario intentando modificar o eliminar otra cuenta recibe `403`.
- Un JWT de tipo incorrecto recibe `403`.
- El propietario puede leer, actualizar y eliminar su cuenta.
- Mensajes y conversaciones solo son accesibles por sus participantes.
- La migración deja vacías las tablas autorizadas, conserva la estructura necesaria y elimina las columnas sensibles.
- La aplicación limpia las copias locales sensibles sin borrar datos ajenos.
- La API publica en modo Release y la aplicación Android compila en modo Release.

La comprobación final en Azure se hará con cuentas nuevas creadas después de la migración.

## Secuencia de despliegue

1. Mantener App Service detenido.
2. Implementar y ejecutar todas las pruebas locales.
3. Generar y revisar el script idempotente de migración.
4. Confirmar que el destino sea `crownbuilders-server/CrownBuildersDB`.
5. Ejecutar la limpieza y migración en Azure SQL.
6. Publicar la API segura en `crownbuilders-portal`.
7. Iniciar App Service.
8. Comprobar salud pública y `401` en rutas privadas sin sesión.
9. Crear cuentas nuevas de trabajador y compañía.
10. Validar inicio de sesión, perfiles, permisos, mensajes y reseñas.

Si falla una comprobación de seguridad, autenticación o base de datos, se detendrá nuevamente App Service antes de investigar.

## Criterios de aceptación

- Ninguna contraseña se almacena o compara como texto plano.
- La API y la aplicación no almacenan datos completos de tarjeta ni CVC.
- Ninguna respuesta JSON contiene contraseña/hash, token FCM o datos de tarjeta.
- Todos los perfiles y directorios requieren autenticación.
- Todas las mutaciones validan rol y propiedad.
- Los datos de prueba autorizados se eliminan de Azure SQL.
- Las pruebas de seguridad enfocadas, publicación Release y compilación Android Release terminan correctamente.
- Azure solo se reactiva después de completar las comprobaciones anteriores.

## Fuera de alcance

- Implementar cobros reales o suscripciones completas con Stripe.
- Recuperación de contraseñas y verificación avanzada de identidad.
- Migrar la autenticación a Microsoft Entra External ID u otro proveedor externo.
- Rediseñar la interfaz general de la aplicación.
