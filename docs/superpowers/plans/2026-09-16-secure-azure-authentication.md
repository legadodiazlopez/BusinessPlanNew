# Secure Azure Authentication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminar la exposición de credenciales y tarjetas, proteger cada operación por identidad y volver a desplegar Crown Builders en Azure con cuentas nuevas y seguras.

**Architecture:** La API aplicará autenticación por defecto, contratos de entrada/salida separados de EF Core, hash de contraseñas con `PasswordHasher<TUser>` y comprobaciones explícitas de rol/propiedad. Azure SQL se limpiará mediante una migración transaccional y el cliente MAUI conservará perfiles no sensibles en SQLite y la sesión JWT en `SecureStorage`.

**Tech Stack:** .NET 10, ASP.NET Core, JWT Bearer, EF Core 10, Azure SQL, Azure Communication Services SMS, .NET MAUI Blazor Hybrid, SQLite, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-16-secure-azure-authentication-design.md`

## Global Constraints

- Mantener Azure App Service `crownbuilders-portal` detenido hasta completar todas las verificaciones.
- Los datos actuales de Azure son de prueba y están autorizados para eliminación.
- No almacenar ni devolver contraseñas, hashes, tokens FCM, número de tarjeta, vencimiento, CVC o nombre del titular.
- No implementar cobros reales; el flujo de Stripe debe quedar deshabilitado con un mensaje explícito.
- Mantener .NET 10 y las rutas existentes que usa el cliente, salvo endpoints duplicados no utilizados.
- Usar comentarios en español solamente.
- No registrar secretos, credenciales, JWT, códigos SMS ni datos completos de usuarios.

---

## File Map

**API contracts**

- Create: `CrownBuilders.API/Contracts/AuthContracts.cs` — registro, login, respuesta de sesión y verificación de correo.
- Create: `CrownBuilders.API/Contracts/ProfileContracts.cs` — perfiles públicos y comandos de actualización.
- Create: `CrownBuilders.API/Contracts/SmsContracts.cs` — solicitud de código, verificación y prueba de teléfono.
- Create: `CrownBuilders.API/Contracts/ReviewContracts.cs` — creación y lectura segura de reseñas.

**API security and infrastructure**

- Modify: `CrownBuilders.API/Program.cs` — política de autorización por defecto, rate limiting, CORS cerrado y servicios.
- Create: `CrownBuilders.API/Services/UserOwnershipPolicy.cs` — validación única de rol e identificador.
- Create: `CrownBuilders.API/Services/SmsVerificationService.cs` — códigos temporales y pruebas de teléfono de un solo uso.
- Create: `CrownBuilders.API/Services/ISmsSender.cs` — límite testeable para el proveedor SMS.
- Create: `CrownBuilders.API/Services/AzureSmsSender.cs` — envío mediante Azure Communication Services.
- Create: `CrownBuilders.API/Services/SmsOptions.cs` — configuración validada del remitente SMS.

**API behavior and persistence**

- Modify: `CrownBuilders.API/Models/Compania.cs` and `CrownBuilders.API/Models/Trabajador.cs` — `PasswordHash` and no card fields.
- Modify: `CrownBuilders.API/Data/ApplicationDbContext.cs` — field limits and sensitive-data-free model.
- Modify: `CrownBuilders.API/Controllers/AuthController.cs` — hashed login and safe authenticated updates.
- Modify: `CrownBuilders.API/Controllers/PerfilController.cs` — safe registration, directories, profiles, deletes, and FCM ownership.
- Modify: `CrownBuilders.API/Controllers/ResenasController.cs` — authenticated reviews whose sender comes from JWT.
- Modify: `CrownBuilders.API/Controllers/SmsController.cs` — request/verify endpoints without returning codes.
- Modify: `CrownBuilders.API/Controllers/StripeController.cs` — authenticated, intentionally unavailable payment endpoint.
- Create: `CrownBuilders.API/Migrations/20260916000000_SecureAuthenticationAndRemoveCardData.cs`.
- Create: `CrownBuilders.API/Migrations/20260916000000_SecureAuthenticationAndRemoveCardData.Designer.cs`.
- Modify: `CrownBuilders.API/Migrations/ApplicationDbContextModelSnapshot.cs`.

**MAUI client**

- Create: `CrownBuilders/Services/ApiContracts.cs` — client-side request/response types matching the API.
- Modify: `CrownBuilders/Services/ApiService.cs` — typed requests, bearer restoration, sanitized failures.
- Modify: `CrownBuilders/DatabaseHelper.cs` — safe cache schema and one-time sensitive-profile purge.
- Modify: `CrownBuilders/Models/Compania.cs` and `CrownBuilders/Models/Trabajador.cs` — public profile data only.
- Modify: registration, login, and profile edit Razor components to stop collecting or caching sensitive fields.
- Delete: `CrownBuilders/Services/StripeService.cs`.
- Modify: `CrownBuilders/MauiProgram.cs`, `CrownBuilders/Components/_Imports.razor`, and both project files to remove unused Stripe wiring/packages.

**Tests**

- Create focused API security, authentication, profile authorization, review authorization, SMS, migration, and local-cache tests in `CrownBuilders.Tests`.
- Modify outdated tests that construct entities with removed card/password fields.

---

### Task 1: Lock down the API boundary

**Files:**
- Modify: `CrownBuilders.API/Program.cs`
- Modify: `CrownBuilders.API/Controllers/HealthController.cs`
- Modify: `CrownBuilders.Tests/CrownBuilders.Tests.csproj`
- Create: `CrownBuilders.Tests/SecureApiFactory.cs`
- Create: `CrownBuilders.Tests/ApiSecurityBoundaryTests.cs`

**Interfaces:**
- Produces: fallback authorization policy; rate-limit policies named `login` and `registration`; test host `SecureApiFactory`.
- Consumes: existing JWT settings and `ApplicationDbContext`.

- [ ] **Step 1: Add the integration-test dependency**

Add `Microsoft.AspNetCore.Mvc.Testing` version `10.0.9` to `CrownBuilders.Tests/CrownBuilders.Tests.csproj`. Add `public partial class Program;` at the end of `CrownBuilders.API/Program.cs` so the test host can reference the entry point.

- [ ] **Step 2: Create a test host with an isolated database**

Implement `SecureApiFactory : WebApplicationFactory<Program>` that sets Production configuration values for `ConnectionStrings:DefaultConnection`, all JWT fields, replaces the SQL Server registration with a uniquely named EF InMemory database, and creates clients with HTTPS base address and redirects disabled.

- [ ] **Step 3: Write failing boundary tests**

```csharp
[Fact]
public async Task Health_RemainsAnonymous()
{
    using var response = await _client.GetAsync("/api/health");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}

[Theory]
[InlineData("/api/perfil/listar-trabajadores")]
[InlineData("/api/perfil/listar-companias")]
[InlineData("/api/resenas/1/trabajador")]
public async Task PrivateRoutes_RejectAnonymousCallers(string route)
{
    using var response = await _client.GetAsync(route);
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}

[Fact]
public async Task UnknownBrowserOrigin_IsNotGrantedCorsAccess()
{
    using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
    request.Headers.Add("Origin", "https://attacker.example");
    using var response = await _client.SendAsync(request);
    Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
}

[Fact]
public async Task LoginLimiter_ReturnsTooManyRequestsAfterTenCalls()
{
    for (var requestNumber = 1; requestNumber <= 10; requestNumber++)
    {
        using var accepted = await _client.PostAsJsonAsync(
            "/api/auth/login-trabajador",
            new { Correo = "nobody@example.com", Password = "incorrecta" });
        Assert.NotEqual(HttpStatusCode.TooManyRequests, accepted.StatusCode);
    }

    using var rejected = await _client.PostAsJsonAsync(
        "/api/auth/login-trabajador",
        new { Correo = "nobody@example.com", Password = "incorrecta" });
    Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
}
```

- [ ] **Step 4: Run the tests and confirm the insecure behavior**

Run: `dotnet test CrownBuilders.Tests/CrownBuilders.Tests.csproj --filter FullyQualifiedName~ApiSecurityBoundaryTests --no-restore`

Expected: at least the anonymous private-route assertions fail before the fallback policy is configured.

- [ ] **Step 5: Implement the security boundary**

Configure a fallback policy requiring an authenticated user. Mark only health and the approved public methods with `[AllowAnonymous]`. Add fixed-window limiters partitioned by remote IP: `login` permits 10 requests/minute and `registration` permits 5 requests/minute, both returning `429`. Remove `PermitirTodo`; only register/use CORS when `Cors:AllowedOrigins` contains explicit origins.

Middleware order must be:

```csharp
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseCors("ConfiguredOrigins"); // Solo cuando existen orígenes configurados.
app.UseAuthentication();
app.UseAuthorization();
```

- [ ] **Step 6: Verify and commit**

Run the Task 1 filter again and expect all tests to pass.

```powershell
git add CrownBuilders.API/Program.cs CrownBuilders.API/Controllers/HealthController.cs CrownBuilders.Tests/CrownBuilders.Tests.csproj CrownBuilders.Tests/SecureApiFactory.cs CrownBuilders.Tests/ApiSecurityBoundaryTests.cs
git commit -m "security: require authentication by default"
```

---

### Task 2: Replace the public mock SMS code with one-time verification

**Files:**
- Create: `CrownBuilders.API/Contracts/SmsContracts.cs`
- Create: `CrownBuilders.API/Services/ISmsSender.cs`
- Create: `CrownBuilders.API/Services/AzureSmsSender.cs`
- Create: `CrownBuilders.API/Services/SmsOptions.cs`
- Create: `CrownBuilders.API/Services/SmsVerificationService.cs`
- Modify: `CrownBuilders.API/Controllers/SmsController.cs`
- Modify: `CrownBuilders.API/Program.cs`
- Modify: `CrownBuilders.API/appsettings.json`
- Create: `CrownBuilders.Tests/SmsVerificationServiceTests.cs`

**Interfaces:**
- Produces: `Task RequestCodeAsync(string phone, CancellationToken)`, `string VerifyCode(string phone, string code)`, and `bool ConsumeProof(string phone, string proofToken)`.
- Produces: `RequestSmsCodeRequest`, `VerifySmsCodeRequest`, `VerifySmsCodeResponse`.
- Consumes: `ISmsSender.SendAsync(string phone, string message, CancellationToken)`.

- [ ] **Step 1: Write failing SMS tests with a fake sender**

Cover these exact cases:

```csharp
[Fact] public async Task RequestCode_DoesNotExposeCodeInItsResult();
[Fact] public async Task VerifyCode_ReturnsSingleUseProofForCorrectCode();
[Fact] public async Task VerifyCode_RejectsExpiredOrIncorrectCode();
[Fact] public async Task ConsumeProof_AcceptsTokenOnceOnly();
```

Use `TimeProvider` and `IMemoryCache`; the fake sender captures the outbound message so the test can extract the generated six-digit code without production code exposing it.

- [ ] **Step 2: Run tests to verify missing types fail compilation**

Run: `dotnet test CrownBuilders.Tests/CrownBuilders.Tests.csproj --filter FullyQualifiedName~SmsVerificationServiceTests --no-restore`

Expected: FAIL because the SMS contracts and service do not exist.

- [ ] **Step 3: Implement cryptographic, expiring verification**

Generate the code with `RandomNumberGenerator.GetInt32(0, 1_000_000)`. Store only a SHA-256 digest and expiry for five minutes in `IMemoryCache`. On successful verification, remove the code and create a random 32-byte proof token valid for ten minutes. Compare digests with `CryptographicOperations.FixedTimeEquals` and remove the proof when `ConsumeProof` succeeds.

- [ ] **Step 4: Connect Azure SMS and the controller**

`AzureSmsSender` must read `Sms:ConnectionString` and `Sms:FromPhoneNumber`, create `SmsClient`, and send only the server-generated message. `POST /api/sms/enviar` accepts a phone number and returns `{ exitoso: true }`; `POST /api/sms/verificar` returns the proof token only after a correct code. Both endpoints use `[AllowAnonymous]` and `[EnableRateLimiting("registration")]`; neither response nor log contains the code.

Add empty non-secret keys to `appsettings.json` and require production values through:

```text
Sms__ConnectionString
Sms__FromPhoneNumber
```

- [ ] **Step 5: Verify and commit**

Run the Task 2 filter and expect all tests to pass.

```powershell
git add CrownBuilders.API/Contracts/SmsContracts.cs CrownBuilders.API/Services/ISmsSender.cs CrownBuilders.API/Services/AzureSmsSender.cs CrownBuilders.API/Services/SmsOptions.cs CrownBuilders.API/Services/SmsVerificationService.cs CrownBuilders.API/Controllers/SmsController.cs CrownBuilders.API/Program.cs CrownBuilders.API/appsettings.json CrownBuilders.Tests/SmsVerificationServiceTests.cs
git commit -m "security: add one-time SMS verification"
```

---

### Task 3: Hash passwords and separate registration/login contracts

**Files:**
- Create: `CrownBuilders.API/Contracts/AuthContracts.cs`
- Modify: `CrownBuilders.API/Models/Compania.cs`
- Modify: `CrownBuilders.API/Models/Trabajador.cs`
- Modify: `CrownBuilders.API/Controllers/AuthController.cs`
- Modify: `CrownBuilders.API/Controllers/PerfilController.cs`
- Modify: `CrownBuilders.API/Program.cs`
- Create: `CrownBuilders.Tests/AuthenticationSecurityTests.cs`

**Interfaces:**
- Produces: `LoginRequest(string Correo, string Password)` and existing-shape `AuthResponse` without entity fields.
- Produces: `RegisterWorkerRequest` and `RegisterCompanyRequest`, each containing `PhoneVerificationToken` and transient `Password`.
- Consumes: `SmsVerificationService.ConsumeProof(phone, token)`.

- [ ] **Step 1: Define contract tests before changing entities**

Tests must assert:

```csharp
[Fact] public async Task RegisterWorker_StoresHashInsteadOfPlaintext();
[Fact] public async Task RegisterCompany_StoresHashInsteadOfPlaintext();
[Fact] public async Task LoginWorker_VerifiesHashAndReturnsJwt();
[Fact] public async Task LoginCompany_RejectsWrongPasswordWithoutLeakingDetails();
[Fact] public async Task Registration_RejectsMissingOrConsumedPhoneProof();
```

Use EF InMemory and `PasswordHasher<Trabajador>` / `PasswordHasher<Compania>`. Seed valid phone proofs through the SMS service and its fake sender rather than bypassing its public contract.

- [ ] **Step 2: Run tests and confirm failure**

Run: `dotnet test CrownBuilders.Tests/CrownBuilders.Tests.csproj --filter FullyQualifiedName~AuthenticationSecurityTests --no-restore`

Expected: FAIL because passwords are still compared and stored as plaintext.

- [ ] **Step 3: Replace entity password fields**

Change both API entities from `Password` to `PasswordHash`. Set maximum length 512 in `ApplicationDbContext`. Register both framework hashers:

```csharp
builder.Services.AddScoped<IPasswordHasher<Trabajador>, PasswordHasher<Trabajador>>();
builder.Services.AddScoped<IPasswordHasher<Compania>, PasswordHasher<Compania>>();
```

- [ ] **Step 4: Implement safe registration**

Normalize email with `Trim().ToLowerInvariant()`, validate required lengths, consume the phone proof, map explicit fields from the request, and compute `PasswordHash`. Return only registration ID and normalized email. Duplicate email returns `409`; invalid input or phone proof returns `400`; unexpected failures return a generic `500` while structured logs contain only operation and correlation context.

- [ ] **Step 5: Implement safe login**

Look up by normalized email, call `VerifyHashedPassword`, and return the same generic `401` payload for missing users and bad passwords. Successful responses contain only `Exitoso`, `Mensaje`, `Id`, `Nombre`, `Correo`, `Tipo`, and JWT. Apply `[AllowAnonymous]` and `[EnableRateLimiting("login")]`.

- [ ] **Step 6: Verify and commit**

Run the Task 3 filter and `JwtTokenServiceTests`; expect all tests to pass.

```powershell
git add CrownBuilders.API/Contracts/AuthContracts.cs CrownBuilders.API/Models/Compania.cs CrownBuilders.API/Models/Trabajador.cs CrownBuilders.API/Data/ApplicationDbContext.cs CrownBuilders.API/Controllers/AuthController.cs CrownBuilders.API/Controllers/PerfilController.cs CrownBuilders.API/Program.cs CrownBuilders.Tests/AuthenticationSecurityTests.cs
git commit -m "security: hash account passwords"
```

---

### Task 4: Return safe profiles and enforce ownership

**Files:**
- Create: `CrownBuilders.API/Contracts/ProfileContracts.cs`
- Create: `CrownBuilders.API/Services/UserOwnershipPolicy.cs`
- Modify: `CrownBuilders.API/Controllers/AuthController.cs`
- Modify: `CrownBuilders.API/Controllers/PerfilController.cs`
- Replace: `CrownBuilders.Tests/WorkerProfileAuthorizationTests.cs`
- Create: `CrownBuilders.Tests/ProfileSecurityTests.cs`

**Interfaces:**
- Produces: `WorkerProfileResponse`, `CompanyProfileResponse`, `UpdateWorkerProfileRequest`, `UpdateCompanyProfileRequest`, and `FcmTokenRequest`.
- Produces: `UserOwnershipPolicy.CanManage(AuthenticatedUser user, string requiredType, int resourceId) -> bool`.
- Consumes: `AuthenticatedUserAccessor.FromPrincipal(ClaimsPrincipal)`.

- [ ] **Step 1: Write failing profile tests**

Cover directory serialization and ownership:

```csharp
[Fact] public async Task WorkerDirectory_DoesNotSerializePasswordHashOrFcmToken();
[Fact] public async Task CompanyDirectory_DoesNotSerializeCardFieldsPasswordHashOrFcmToken();
[Fact] public async Task UpdateCompany_ReturnsForbidForAnotherCompany();
[Fact] public async Task DeleteWorker_ReturnsForbidForAnotherWorker();
[Fact] public async Task UpdateFcmToken_ReturnsForbidForAnotherAccount();
[Fact] public async Task OwnerCanReadUpdateAndDeleteOwnProfile();
```

- [ ] **Step 2: Run tests and confirm exposure/authorization failures**

Run: `dotnet test CrownBuilders.Tests/CrownBuilders.Tests.csproj --filter "FullyQualifiedName~ProfileSecurityTests|FullyQualifiedName~WorkerProfileAuthorizationTests" --no-restore`

Expected: FAIL because entity objects are returned and company/delete/token ownership is missing.

- [ ] **Step 3: Project queries into safe response records**

Use `.AsNoTracking().Select(...)` for lists and individual profiles. Worker responses include profile/contact/work fields only. Company responses include profile/contact fields plus `FormaPago` and `MontoPago`; they exclude every password/hash, FCM, and card property.

- [ ] **Step 4: Apply role and ownership checks**

Use `[Authorize]` for directories and reads, role-specific `[Authorize(Roles = "trabajador")]` or `[Authorize(Roles = "compania")]` for mutation routes, and `UserOwnershipPolicy` before database work. Update tracked entities field-by-field from update contracts; never set `EntityState.Modified` on request objects.

- [ ] **Step 5: Remove duplicate unsafe Auth reads**

Delete unused `Auth/trabajadores`, `Auth/companias`, `Auth/perfil-trabajador/{id}`, and `Auth/perfil-compania/{id}` actions after verifying the MAUI client uses the `Perfil/...` equivalents. Keep the current update route names to avoid an unnecessary client route change.

- [ ] **Step 6: Verify and commit**

Run the Task 4 filter and expect all tests to pass.

```powershell
git add CrownBuilders.API/Contracts/ProfileContracts.cs CrownBuilders.API/Services/UserOwnershipPolicy.cs CrownBuilders.API/Controllers/AuthController.cs CrownBuilders.API/Controllers/PerfilController.cs CrownBuilders.Tests/WorkerProfileAuthorizationTests.cs CrownBuilders.Tests/ProfileSecurityTests.cs
git commit -m "security: protect profile data and ownership"
```

---

### Task 5: Secure reviews and disable incomplete payments

**Files:**
- Create: `CrownBuilders.API/Contracts/ReviewContracts.cs`
- Modify: `CrownBuilders.API/Controllers/ResenasController.cs`
- Modify: `CrownBuilders.API/Controllers/StripeController.cs`
- Modify: `CrownBuilders.API/Controllers/GarantiasController.cs`
- Modify: `CrownBuilders.API/CrownBuilders.API.csproj`
- Create: `CrownBuilders.Tests/ReviewAuthorizationTests.cs`
- Create: `CrownBuilders.Tests/DisabledPaymentTests.cs`

**Interfaces:**
- Produces: `CreateReviewRequest(int ReceptorId, string TipoReceptor, int Calificacion1, int Calificacion2, int Calificacion3, string? Comentario)`.
- Produces: authenticated Stripe endpoint that always returns `503` with a non-sensitive message.
- Consumes: authenticated user identity from JWT.

- [ ] **Step 1: Write failing review/payment tests**

Assert that the review sender ID is ignored from client input because it no longer exists in the request, a company can review an existing worker as itself, a worker cannot review another worker, ratings outside 1–5 return `400`, and unauthenticated calls return `401`. Assert an authenticated company gets `503` from Stripe while anonymous callers get `401`.

- [ ] **Step 2: Run tests and confirm failure**

Run: `dotnet test CrownBuilders.Tests/CrownBuilders.Tests.csproj --filter "FullyQualifiedName~ReviewAuthorizationTests|FullyQualifiedName~DisabledPaymentTests" --no-restore`

Expected: FAIL because reviews trust `EmisorId` and Stripe still calls the external API.

- [ ] **Step 3: Secure review creation and reads**

Add `[Authorize]` to `ResenasController`. Derive `EmisorId` from the JWT, normalize `TipoReceptor` to `trabajador`/`compania`, require the opposite sender type, verify the recipient exists, validate three ratings in range 1–5 and limit comments to 1000 characters. Map responses explicitly and return generic errors.

- [ ] **Step 4: Disable Stripe and protect guarantees**

Replace Stripe execution with `[Authorize(Roles = "compania")]` and `503 Service Unavailable` containing `"Los pagos todavía no están habilitados."`. Remove `Stripe.net` from the API project. Rely on the fallback authorization policy for guarantees and replace console output of request bodies with structured operation-only logging.

- [ ] **Step 5: Verify and commit**

Run Task 5 tests plus existing `MensajesControllerTests` and `ConversationAccessPolicyTests`; expect all to pass.

```powershell
git add CrownBuilders.API/Contracts/ReviewContracts.cs CrownBuilders.API/Controllers/ResenasController.cs CrownBuilders.API/Controllers/StripeController.cs CrownBuilders.API/Controllers/GarantiasController.cs CrownBuilders.API/CrownBuilders.API.csproj CrownBuilders.Tests/ReviewAuthorizationTests.cs CrownBuilders.Tests/DisabledPaymentTests.cs
git commit -m "security: authorize reviews and disable payments"
```

---

### Task 6: Create the destructive-but-authorized Azure SQL migration

**Files:**
- Create: `CrownBuilders.API/Migrations/20260916000000_SecureAuthenticationAndRemoveCardData.cs`
- Create: `CrownBuilders.API/Migrations/20260916000000_SecureAuthenticationAndRemoveCardData.Designer.cs`
- Modify: `CrownBuilders.API/Migrations/ApplicationDbContextModelSnapshot.cs`
- Create: `CrownBuilders.Tests/SecureSchemaTests.cs`

**Interfaces:**
- Produces: EF migration ID `20260916000000_SecureAuthenticationAndRemoveCardData`.
- Consumes: the updated `Compania` and `Trabajador` EF models from Task 3.

- [ ] **Step 1: Write failing model metadata tests**

```csharp
[Fact]
public void AccountModel_ContainsOnlyPasswordHash()
{
    Assert.NotNull(_context.Model.FindEntityType(typeof(Trabajador))!.FindProperty("PasswordHash"));
    Assert.Null(_context.Model.FindEntityType(typeof(Trabajador))!.FindProperty("Password"));
}

[Theory]
[InlineData("NombreTitular")]
[InlineData("NumeroTarjeta")]
[InlineData("FechaExpiracion")]
[InlineData("CVC")]
public void CompanyModel_DoesNotContainCardField(string property)
{
    Assert.Null(_context.Model.FindEntityType(typeof(Compania))!.FindProperty(property));
}

[Fact]
public void Migration_DeletesAuthorizedDataBeforeDroppingSensitiveColumns()
{
    var source = File.ReadAllText(FindWorkspaceFile(
        "CrownBuilders.API/Migrations/20260916000000_SecureAuthenticationAndRemoveCardData.cs"));
    Assert.True(source.IndexOf("DELETE FROM [Resenas]", StringComparison.Ordinal) <
                source.IndexOf("DropColumn", StringComparison.Ordinal));
    Assert.Contains("PasswordHash", source, StringComparison.Ordinal);
    Assert.Contains("NumeroTarjeta", source, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run tests and verify the snapshot is stale**

Run: `dotnet test CrownBuilders.Tests/CrownBuilders.Tests.csproj --filter FullyQualifiedName~SecureSchemaTests --no-restore`

Expected: FAIL because the migration file does not exist and the snapshot still shows old columns.

- [ ] **Step 3: Implement the migration in the authorized order**

The `Up` migration must execute deletes before schema changes:

```csharp
migrationBuilder.Sql("DELETE FROM [Resenas];");
migrationBuilder.Sql("DELETE FROM [Mensajes];");
migrationBuilder.Sql("DELETE FROM [Conversaciones];");
migrationBuilder.Sql("DELETE FROM [Trabajadores];");
migrationBuilder.Sql("DELETE FROM [Companias];");
migrationBuilder.RenameColumn("Password", "Trabajadores", "PasswordHash");
migrationBuilder.RenameColumn("Password", "Companias", "PasswordHash");
```

Then drop `NombreTitular`, `NumeroTarjeta`, `FechaExpiracion`, and `CVC`. The `Down` method re-adds those columns with empty defaults and renames both hash columns back; it explicitly does not restore deleted test data.

- [ ] **Step 4: Generate and inspect SQL without connecting to Azure**

Run:

```powershell
dotnet ef migrations script 20260915045859_MakeCompanyProfilePhotoOptional 20260916000000_SecureAuthenticationAndRemoveCardData --idempotent --project CrownBuilders.API/CrownBuilders.API.csproj --output CrownBuilders.API/bin/Release/azure-security-migration.sql
Select-String -Path CrownBuilders.API/bin/Release/azure-security-migration.sql -Pattern "DELETE FROM|PasswordHash|DROP COLUMN|__EFMigrationsHistory"
```

Expected: all five deletes occur before renames/drops, all four card columns are dropped, and the migration history insert is present.

- [ ] **Step 5: Verify and commit**

Run Task 6 tests and `dotnet build CrownBuilders.API/CrownBuilders.API.csproj -c Release --no-restore`; expect success.

```powershell
git add CrownBuilders.API/Migrations/20260916000000_SecureAuthenticationAndRemoveCardData.cs CrownBuilders.API/Migrations/20260916000000_SecureAuthenticationAndRemoveCardData.Designer.cs CrownBuilders.API/Migrations/ApplicationDbContextModelSnapshot.cs CrownBuilders.Tests/SecureSchemaTests.cs
git commit -m "security: remove test users and card columns"
```

---

### Task 7: Move the MAUI client to safe API contracts

**Files:**
- Create: `CrownBuilders/Services/ApiContracts.cs`
- Modify: `CrownBuilders/Services/ApiService.cs`
- Modify: `CrownBuilders/Models/Compania.cs`
- Modify: `CrownBuilders/Models/Trabajador.cs`
- Create: `CrownBuilders.Tests/ClientApiContractTests.cs`

**Interfaces:**
- Produces: client `RegisterWorkerRequest`, `RegisterCompanyRequest`, `WorkerProfileResponse`, `CompanyProfileResponse`, `UpdateWorkerProfileRequest`, `UpdateCompanyProfileRequest`, and SMS request/verify types matching API JSON names.
- Consumes: existing `AuthResponse`, JWT `SecureStorage`, and protected API routes.

- [ ] **Step 1: Write failing client contract tests**

Use reflection and JSON serialization to assert profile models do not define or serialize `Password`, `PasswordHash`, `FcmToken`, `NombreTitular`, `NumeroTarjeta`, `FechaExpiracion`, or `CVC`. Use a fake `HttpMessageHandler`, call `SetAuthToken("jwt-de-prueba")`, and assert protected profile requests include `Authorization: Bearer`; registration JSON contains transient `Password` plus `PhoneVerificationToken` only in the registration request.

- [ ] **Step 2: Run tests and confirm sensitive properties fail assertions**

Run: `dotnet test CrownBuilders.Tests/CrownBuilders.Tests.csproj --filter FullyQualifiedName~ClientApiContractTests --no-restore`

Expected: FAIL because current client profile models carry password, FCM, and card fields.

- [ ] **Step 3: Add typed client contracts and update ApiService**

Change registration methods to accept registration request records, profile methods to return safe response models, and update methods to accept update request records. Call `RestoreAuthTokenAsync()` before every protected request. Convert server failures into short user-facing messages based on status code; do not append raw response bodies or exception details.

- [ ] **Step 4: Reduce profile models to display/cache data**

Remove all sensitive properties from `CrownBuilders/Models/Compania.cs` and `CrownBuilders/Models/Trabajador.cs`. Keep payment unit and amount because they describe job compensation, not card authorization.

- [ ] **Step 5: Verify and commit**

Run Task 7 tests and expect all to pass.

```powershell
git add CrownBuilders/Services/ApiContracts.cs CrownBuilders/Services/ApiService.cs CrownBuilders/Models/Compania.cs CrownBuilders/Models/Trabajador.cs CrownBuilders.Tests/ClientApiContractTests.cs
git commit -m "security: use safe mobile API contracts"
```

---

### Task 8: Purge sensitive SQLite profile caches once

**Files:**
- Modify: `CrownBuilders/DatabaseHelper.cs`
- Create: `CrownBuilders/Services/LocalProfileCacheMigration.cs`
- Create: `CrownBuilders.Tests/LocalProfileCacheSecurityTests.cs`

**Interfaces:**
- Produces: `LocalProfileCacheMigration.ApplyAsync(SQLiteAsyncConnection database)` and SQLite `PRAGMA user_version = 2`.
- Consumes: safe client profile models from Task 7.

- [ ] **Step 1: Write failing local-cache tests**

Tests must create a temporary SQLite database with legacy `Trabajador` and `PerfilCompania` rows, run the migration helper, and assert only those two profile tables are rebuilt while a seeded `Mensaje` row remains. Inspect `PRAGMA table_info` to assert all sensitive columns are absent.

- [ ] **Step 2: Run tests and confirm legacy columns remain**

Run: `dotnet test CrownBuilders.Tests/CrownBuilders.Tests.csproj --filter FullyQualifiedName~LocalProfileCacheSecurityTests --no-restore`

Expected: FAIL because the current table classes and database still contain sensitive columns.

- [ ] **Step 3: Implement a focused, testable cache migration**

`LocalProfileCacheMigration.ApplyAsync` checks `PRAGMA user_version`. Below version 2, it preserves only non-sensitive profile fields in memory, rebuilds `Trabajador` and `PerfilCompania` using safe schemas inside a transaction, restores safe fields, and sets `PRAGMA user_version = 2` only after success. `DatabaseHelper.InitAsync` invokes it before normal cache use. Do not touch `Mensaje`, `Garantia`, or `Asistencia`.

- [ ] **Step 4: Remove offline password validation and sensitive mappings**

Delete `ValidarCredencialesTrabajadorAsync`, `ValidarCredencialesCompaniaAsync`, and all mapping assignments for password, FCM, or card values. Authentication remains online-only; offline mode may display cached directories but cannot create a session.

- [ ] **Step 5: Verify and commit**

Run Task 8 tests plus `DataLoadingOptimizationTests`; expect all to pass.

```powershell
git add CrownBuilders/DatabaseHelper.cs CrownBuilders/Services/LocalProfileCacheMigration.cs CrownBuilders.Tests/LocalProfileCacheSecurityTests.cs
git commit -m "security: purge sensitive mobile cache"
```

---

### Task 9: Update registration, login, profile, and review screens

**Files:**
- Modify: `CrownBuilders/Components/Pages/RegistroCompania.Razor`
- Modify: `CrownBuilders/Components/Pages/RegistroTrabajador.Razor`
- Modify: `CrownBuilders/Components/LoginCompania.razor`
- Modify: `CrownBuilders/Components/Pages/LoginTrabajador.razor`
- Modify: `CrownBuilders/Components/Pages/EditarPerfilCompania.Razor`
- Modify: `CrownBuilders/Components/Pages/EditarPerfilTrabajador.razor`
- Modify: `CrownBuilders/Components/Pages/Mensajeriatrabajor.razor`
- Modify: `CrownBuilders/Components/Pages/PortalCompania.razor`
- Modify: `CrownBuilders/Services/CompanyRegistrationValidation.cs`
- Delete: `CrownBuilders/Services/StripeService.cs`
- Modify: `CrownBuilders/MauiProgram.cs`
- Modify: `CrownBuilders/Components/_Imports.razor`
- Modify: `CrownBuilders/CrownBuilders.csproj`
- Modify: `CrownBuilders.Tests/CompanyRegistrationValidationTests.cs`
- Modify: `CrownBuilders.Tests/PriceMappingTests.cs`
- Create: `CrownBuilders.Tests/SensitiveUiRemovalTests.cs`

**Interfaces:**
- Consumes: safe API contracts and one-time `PhoneVerificationToken` from Tasks 2 and 7.
- Produces: registration and edit forms with no card collection or cached password.

- [ ] **Step 1: Write failing source-level regression tests**

Scan production Razor/C# outside `bin`/`obj` and assert no card field names, hard-coded SMS code `123456`, `@bind` to a profile password, or registration of `StripeService` remains. Allow the word `Password` only in transient login/registration request contracts and private form variables.

- [ ] **Step 2: Run tests and confirm current UI violations**

Run: `dotnet test CrownBuilders.Tests/CrownBuilders.Tests.csproj --filter FullyQualifiedName~SensitiveUiRemovalTests --no-restore`

Expected: FAIL on company registration/edit, both local profile mappings, and the hard-coded verification code.

- [ ] **Step 3: Update both registration flows**

Request SMS using only the normalized phone, submit the entered code to receive `PhoneVerificationToken`, then send the typed registration request. Do not prefill codes. Keep password in a private component variable only until the request completes, then clear it in `finally`. Do not save the password in SQLite.

- [ ] **Step 4: Remove card and password-edit controls**

Delete cardholder, number, expiry, and CVC controls/validation. Remove password fields from both profile-edit pages; password change/recovery remains outside this scope. Add the message `Los pagos con tarjeta estarán disponibles próximamente mediante Stripe.` where the card form existed.

- [ ] **Step 5: Preserve authenticated API usage for reviews**

Replace standalone static review `HttpClient` instances with `ApiService` methods so JWT restoration occurs. Send `CreateReviewRequest` without `EmisorId`; use the protected API for rating averages and surface `401` by returning to the relevant login page.

- [ ] **Step 6: Remove Stripe client wiring and obsolete validators**

Delete `StripeService`, its DI registration, `@using Stripe`, both `Stripe.net` package references, and card/CVC/expiration validator methods/tests. Keep email and phone validation plus compensation display tests.

- [ ] **Step 7: Verify and commit**

Run Task 9 tests, `CompanyProfileIdentityTests`, and `PriceMappingTests`; expect all current assertions to pass after updating obsolete fixtures.

```powershell
git add CrownBuilders/Components/Pages/RegistroCompania.Razor CrownBuilders/Components/Pages/RegistroTrabajador.Razor CrownBuilders/Components/LoginCompania.razor CrownBuilders/Components/Pages/LoginTrabajador.razor CrownBuilders/Components/Pages/EditarPerfilCompania.Razor CrownBuilders/Components/Pages/EditarPerfilTrabajador.razor CrownBuilders/Components/Pages/Mensajeriatrabajor.razor CrownBuilders/Components/Pages/PortalCompania.razor CrownBuilders/Services/CompanyRegistrationValidation.cs CrownBuilders/Services/StripeService.cs CrownBuilders/MauiProgram.cs CrownBuilders/Components/_Imports.razor CrownBuilders/CrownBuilders.csproj CrownBuilders.Tests/CompanyRegistrationValidationTests.cs CrownBuilders.Tests/PriceMappingTests.cs CrownBuilders.Tests/SensitiveUiRemovalTests.cs
git commit -m "security: remove sensitive payment and profile inputs"
```

---

### Task 10: Complete local security verification

**Files:**
- Modify only files required by failures directly caused by Tasks 1–9.
- Do not change unrelated legacy tests merely to make the suite green.

**Interfaces:**
- Consumes: all implementation tasks.
- Produces: verified Release artifacts and a reviewed migration script.

- [ ] **Step 1: Run focused security tests**

```powershell
dotnet test CrownBuilders.Tests/CrownBuilders.Tests.csproj --filter "FullyQualifiedName~Security|FullyQualifiedName~Authorization|FullyQualifiedName~SmsVerification|FullyQualifiedName~SecureSchema|FullyQualifiedName~SensitiveUiRemoval|FullyQualifiedName~AzureDeploymentConfiguration" --no-restore
```

Expected: all focused tests pass.

- [ ] **Step 2: Run the full test suite and classify any legacy failures**

Run: `dotnet test CrownBuilders.Tests/CrownBuilders.Tests.csproj --no-restore`

Expected: no new failure attributable to this plan. Record existing bUnit/Windows runtime failures separately; do not hide or delete them.

- [ ] **Step 3: Publish the API locally**

Run: `dotnet publish CrownBuilders.API/CrownBuilders.API.csproj -c Release --no-restore -o .codex-api-output-security`

Expected: exit code 0 and `CrownBuilders.API.dll` exists in the output.

- [ ] **Step 4: Build Android Release**

Run: `dotnet build CrownBuilders/CrownBuilders.csproj -f net10.0-android -c Release --no-restore`

Expected: exit code 0. Existing package advisory warnings may be reported but no compilation error is allowed.

- [ ] **Step 5: Run secret and sensitive-field scans**

```powershell
rg -n "NumeroTarjeta|FechaExpiracion|NombreTitular|\bCVC\b|Password\s*=\s*table\.Column|Password ==|AllowAnyOrigin|ServerCertificateCustomValidationCallback|123456" CrownBuilders.API CrownBuilders -g "*.cs" -g "*.razor" -g "*.json"
```

Expected: no production match except migration `Down` compatibility code and transient `Password` request properties; every remaining match is manually reviewed.

- [ ] **Step 6: Review the complete diff**

Run: `git --no-pager diff --check` and `git --no-pager diff --stat`. Review authentication, migration, and deletion targets line-by-line before deployment.

---

### Task 11: Apply Azure configuration, migration, and deployment

**Files:**
- Use generated local artifact: `CrownBuilders.API/bin/Release/azure-security-migration.sql`
- Use publish profile already associated with Azure resource `crownbuilders-portal`.

**Interfaces:**
- Consumes: Azure SQL target `crownbuilders-server/CrownBuildersDB` and App Service `crownbuilders-portal`.
- Produces: secure running deployment with fresh accounts only.

- [ ] **Step 1: Confirm the Web App is still stopped**

In Azure Portal, open App Services → `crownbuilders-portal` → Overview. Status must be `Stopped`. Do not continue if it is running.

- [ ] **Step 2: Add the two SMS settings without exposing values**

In App Service → Settings → Environment variables → App settings, add `Sms__ConnectionString` and `Sms__FromPhoneNumber` from the Azure Communication Services resource. Keep existing JWT, Stripe, Firebase, and ASP.NET settings unchanged; Stripe settings may remain unused until a future payment implementation.

- [ ] **Step 3: Verify the SQL target and backup state**

Open Azure SQL → `CrownBuildersDB` and confirm the server shown is `crownbuilders-server.database.windows.net`. Confirm the database is Online and note the latest automatic restore point before executing SQL.

- [ ] **Step 4: Execute the reviewed migration script**

Open Query editor for `CrownBuildersDB`, paste the contents of `azure-security-migration.sql`, and run it once. Expected: success with migration `20260916000000_SecureAuthenticationAndRemoveCardData` present in `__EFMigrationsHistory`.

- [ ] **Step 5: Validate cleanup and schema**

Run:

```sql
SELECT 'Companias' AS Tabla, COUNT(*) AS Registros FROM dbo.Companias
UNION ALL SELECT 'Trabajadores', COUNT(*) FROM dbo.Trabajadores
UNION ALL SELECT 'Conversaciones', COUNT(*) FROM dbo.Conversaciones
UNION ALL SELECT 'Mensajes', COUNT(*) FROM dbo.Mensajes
UNION ALL SELECT 'Resenas', COUNT(*) FROM dbo.Resenas;

SELECT TABLE_NAME, COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('Companias', 'Trabajadores')
  AND COLUMN_NAME IN ('Password', 'PasswordHash', 'NombreTitular', 'NumeroTarjeta', 'FechaExpiracion', 'CVC')
ORDER BY TABLE_NAME, COLUMN_NAME;
```

Expected: every count is 0; only `PasswordHash` appears for each account table.

- [ ] **Step 6: Publish while the app remains stopped**

Publish the Release profile `crownbuilders-portal - Zip Deploy.pubxml` from Visual Studio. Confirm deployment success, but do not start the app until publication completes.

- [ ] **Step 7: Start and smoke-test public/private behavior**

Start App Service, then verify:

```powershell
Invoke-RestMethod 'https://crownbuilders-portal-drbqbfbphpe7hude.centralus-01.azurewebsites.net/api/health'
try { Invoke-WebRequest 'https://crownbuilders-portal-drbqbfbphpe7hude.centralus-01.azurewebsites.net/api/perfil/listar-trabajadores' } catch { $_.Exception.Response.StatusCode.value__ }
```

Expected: health returns `{"status":"healthy"}` and the protected directory returns `401`.

- [ ] **Step 8: Create fresh accounts and test ownership**

Use the mobile app to create one worker and one company with real SMS verification. Log in as each, confirm directories/profile/messages/reviews work, and confirm a token for one account cannot update or delete the other account (`403`). Inspect JSON responses to ensure no sensitive property names appear.

- [ ] **Step 9: Apply the stop condition if any check fails**

If health, authentication, schema, ownership, messaging, or sensitive-field checks fail, stop `crownbuilders-portal` immediately and preserve logs plus correlation IDs for diagnosis. Do not restore the old insecure build.

- [ ] **Step 10: Record final evidence and commit any deployment-only documentation update**

Record test commands, migration ID, deployment timestamp, health result, and authorization results in the existing deployment specification package without copying secrets. Commit only reviewed source/documentation changes.
