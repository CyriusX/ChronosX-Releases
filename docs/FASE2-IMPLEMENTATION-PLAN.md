# TimeTrack MVP — Fase 2: Plano de Implementação Completo

> **Projeto:** Time Track MVP (Fase 2) — Evidências, Screenshots, Websites, Governança e Retenção
> **Stack:** C#/.NET 10 · React/WebView2 · S3-compatible Storage (MinIO) · Neon Postgres · Hangfire
> **Arquitetura:** Clean Architecture + DDD + SOLID + Composition Pattern
> **Data:** 2026-04-23 · **Target:** 2026-06-30

---

## Sumário

1. [Visão Geral e Status Atual](#1-visão-geral-e-status-atual)
2. [Fase 1 — Fundação (CX-181 + CX-179)](#2-fase-1--fundação)
3. [Fase 2 — Captura no Agent (CX-182 + CX-183 + CX-184)](#3-fase-2--captura-no-agent)
4. [Fase 3 — Consumo Backend (CX-180 + CX-190)](#4-fase-3--consumo-backend)
5. [Fase 4 — Frontend de Evidências (CX-189 + CX-191)](#5-fase-4--frontend-de-evidências)
6. [Fase 5 — Governança e Auditoria (CX-192 + CX-193 + CX-194 + CX-195)](#6-fase-5--governança-e-auditoria)
7. [Fase 6 — Retenção e Validação Final (CX-196 + CX-197 + CX-198)](#7-fase-6--retenção-e-validação-final)
8. [Apêndice A — Setup do MinIO](#apêndice-a--setup-do-minio)
9. [Apêndice B — Diagrama do Pipeline Completo](#apêndice-b--diagrama-do-pipeline-completo)
10. [Apêndice C — Estrutura de Pastas Esperada](#apêndice-c--estrutura-de-pastas-esperada)

---

## 1. Visão Geral e Status Atual

### Milestones

| Milestone | Target | Progresso | Tasks |
|---|---|---|---|
| F2-E1 — Infra de Storage | 27/04/2026 | 0% | 3 tasks (CX-179, CX-180, CX-181) |
| F2-E2 — Screenshots Agent | 11/05/2026 | 0% | 3 tasks (CX-182, CX-183, CX-184) |
| F2-E3 — Websites por Domínio | 25/05/2026 | **100%** | 4 tasks (CX-185 a CX-188) DONE |
| F2-E4 — Timeline + Evidências | 08/06/2026 | 0% | 3 tasks (CX-189, CX-190, CX-191) |
| F2-E5 — Governança & Auditoria | 22/06/2026 | 0% | 4 tasks (CX-192 a CX-195) |
| F2-E6 — Retenção & Limpeza | 30/06/2026 | 0% | 3 tasks (CX-196, CX-197, CX-198) |

### Regras de Arquitetura (seguir em TODA implementação)

- **Entidades** com construtor privado + factory method `Create()`
- **Configurações EF Core** em `Persistence/Configurations/`
- **Multi-tenancy** via global query filter em `TimeTrackDbContext.ConfigureMultiTenantFilters()`
- **Repositórios** com interface em `Domain/Interfaces/Repositories/` e implementação em `Infrastructure/Repositories/`
- **Controllers** usam MediatR (ISender), nunca injetam repositórios diretamente
- **Jobs Hangfire** com interface em `Jobs/Interfaces/` + implementação em `Jobs/`
- **Migrations** EF Core versionadas, sem dados seedados em produção
- **DTOs** em `Application/{Feature}/DTOs/`
- **Commands/Queries** em `Application/{Feature}/Commands/` e `Queries/`

---

## 2. Fase 1 — Fundação

> **Objetivo:** Configurar o bucket S3 (MinIO), criar tabelas de evidências e políticas de screenshot.
> **Milestone:** F2-E1 — Infra de Storage
> **Tasks Linear:** CX-181 (Schema Políticas) + CX-179 (Setup S3)
> **Estimativa:** 8 pontos (3 + 5)

> **NOTA DE ARQUITETURA (2026-04-23):** O setup original previa acesso direto ao S3 via `AWSSDK.S3`.
> Isso foi **alterado** — o TimeTrack agora se comunica com o microserviço de Media existente
> (`AppRelationshipMedia`, porta 3003) via HTTP. O Media Service já possui MinIO, presigned URLs,
> processamento de thumbnails e fila BullMQ. O TimeTrack apenas registra metadata local
> (`evidence_items`) e delega todo o storage para o Media Service via `IMediaServiceClient`.
>
> **Arquivos implementados:**
> - `IMediaServiceClient` (Domain) — interface de comunicação com o Media Service
> - `MediaServiceClient` (Infrastructure) — HTTP client com `HttpClientFactory`
> - `MediaServiceConfiguration` (Infrastructure) — bind de `appsettings.json`
> - `EvidenceItem` com campo `ExternalMediaId` — vincula ao ID do Media Service

### 2.1 CX-181 — Schema de Políticas de Evidência por Org/Equipe

Esta task adiciona campos de screenshot na tabela `org_policies` existente.

#### 2.1.1 — Alterar entidade `OrgPolicy` (Domain)

**Arquivo:** `src/backend/TimeTrack.Backend.Domain/Entities/OrgPolicy.cs`

Adicionar propriedades:

```csharp
// Evidence policy fields
public bool ScreenshotsEnabled { get; private set; } = false;
public int ScreenshotIntervalMinutes { get; private set; } = 5;
public string ScreenshotExcludedAppsJson { get; private set; } = "[]";
public int EvidenceRetentionDays { get; private set; } = 30;
public bool WebsiteTrackingEnabled { get; private set; } = true;
```

Adicionar no método `Create()` os defaults acima.

Adicionar no método `Update()` parâmetros para atualizar esses campos.

Adicionar método helper `GetScreenshotExcludedApps()` (similar ao `GetAppExclusions()`).

Adicionar método dedicado:

```csharp
public void UpdateEvidencePolicy(
    bool? screenshotsEnabled,
    int? screenshotIntervalMinutes,
    string? screenshotExcludedAppsJson,
    int? evidenceRetentionDays,
    bool? websiteTrackingEnabled)
{
    // Validar ranges:
    // screenshotIntervalMinutes: 1-60
    // evidenceRetentionDays: 7-90
    // screenshotExcludedAppsJson: max 50 itens
}
```

#### 2.1.2 — Criar Migration EF Core

```bash
dotnet ef migrations add AddEvidencePolicyFields \
  --project src/backend/TimeTrack.Backend.Infrastructure \
  --startup-project src/backend/TimeTrack.Api
```

Campos a adicionar na tabela `org_policies`:

| Campo | Tipo | Default | Constraint |
|---|---|---|---|
| `screenshots_enabled` | BOOLEAN | false | NOT NULL |
| `screenshot_interval_minutes` | INTEGER | 5 | NOT NULL, CHECK (1-60) |
| `screenshot_excluded_apps_json` | TEXT | '[]' | NOT NULL |
| `evidence_retention_days` | INTEGER | 30 | NOT NULL, CHECK (7-90) |
| `website_tracking_enabled` | BOOLEAN | true | NOT NULL |

#### 2.1.3 — Atualizar DTOs de Policy

**Arquivo:** `src/backend/TimeTrack.Backend.Application/Policies/DTOs/`

Criar ou estender:

```csharp
public sealed record EvidencePolicyResponse(
    bool ScreenshotsEnabled,
    int ScreenshotIntervalMinutes,
    List<string> ScreenshotExcludedApps,
    int EvidenceRetentionDays,
    bool WebsiteTrackingEnabled
);

public sealed record UpdateEvidencePolicyRequest(
    bool? ScreenshotsEnabled = null,
    int? ScreenshotIntervalMinutes = null,
    List<string>? ScreenshotExcludedApps = null,
    int? EvidenceRetentionDays = null,
    bool? WebsiteTrackingEnabled = null
);
```

#### 2.1.4 — Criar Query/Command para Evidence Policy

**Query:** `GetEvidencePolicyQuery` → retorna `EvidencePolicyResponse`
**Command:** `UpdateEvidencePolicyCommand` → recebe `UpdateEvidencePolicyRequest`, retorna `EvidencePolicyResponse`

Seguir padrão existente em `Application/Policies/Commands/` e `Queries/`.

#### 2.1.5 — Adicionar Endpoint no Controller

**Arquivo:** `src/backend/TimeTrack.Api/Controllers/OrgPoliciesController.cs`

```csharp
[HttpGet("evidence")]
public async Task<ActionResult<EvidencePolicyResponse>> GetEvidencePolicy(
    [FromRoute] Guid orgId, CancellationToken ct)

[HttpPut("evidence")]
[Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
public async Task<ActionResult<EvidencePolicyResponse>> UpdateEvidencePolicy(
    [FromRoute] Guid orgId, [FromBody] UpdateEvidencePolicyRequest request, CancellationToken ct)
```

#### 2.1.6 — Atualizar Cache de Políticas no Agent (SQLite)

**Arquivo:** `src/agent/TimeTrack.Agent.Infrastructure/Persistence/SqliteContext.cs`

Na tabela `org_policies_cache`, adicionar colunas:
- `screenshots_enabled INTEGER NOT NULL DEFAULT 0`
- `screenshot_interval_minutes INTEGER NOT NULL DEFAULT 5`
- `screenshot_excluded_apps_json TEXT NOT NULL DEFAULT '[]'`
- `evidence_retention_days INTEGER NOT NULL DEFAULT 30`
- `website_tracking_enabled INTEGER NOT NULL DEFAULT 1`

Na `RunMigrationsAsync()`, adicionar ALTER TABLE para colunas existentes.

#### 2.1.7 — Critérios de Aceite CX-181

- [ ] Migration versionada adiciona os 5 campos em `org_policies`
- [ ] Defaults aplicados para orgs existentes
- [ ] `GET /api/v1/orgs/{orgId}/policies/evidence` retorna campos de evidência
- [ ] `PUT /api/v1/orgs/{orgId}/policies/evidence` atualiza (Admin/Gestor only)
- [ ] Validação de ranges (interval 1-60, retention 7-90)
- [ ] Agent SQLite cache atualizado com novos campos
- [ ] Integrado ao ciclo de sync do Agent

---

### 2.2 CX-179 — Setup Object Storage S3-compatível + Presigned Upload

Esta é a **task âncora** — configura o bucket MinIO, tabelas de evidências e presigned URLs.

#### 2.2.1 — Setup do MinIO (Infraestrutura)

> Ver [Apêndice A](#apêndice-a--setup-do-minio) para instruções detalhadas de instalação.

Configurar MinIO via Docker:

```yaml
# docker-compose.yml (adicionar ao projeto)
services:
  minio:
    image: minio/minio:latest
    container_name: timetrack-minio
    ports:
      - "9000:9000"   # API S3
      - "9001:9001"   # Console Web
    environment:
      MINIO_ROOT_USER: timetrack-admin
      MINIO_ROOT_PASSWORD: ${MINIO_ROOT_PASSWORD}
    command: server /data --console-address ":9001"
    volumes:
      - minio_data:/data
    restart: unless-stopped

volumes:
  minio_data:
```

Criar bucket `timetrack-evidence` (privado, sem acesso público).

Criar IAM user com Access Key/Secret Key específico para o TimeTrack.

Política IAM: acesso apenas ao bucket `timetrack-evidence` com permissões `s3:PutObject`, `s3:GetObject`, `s3:DeleteObject`.

#### 2.2.2 — Adicionar Pacote NuGet

```bash
cd src/backend/TimeTrack.Backend.Infrastructure
dotnet add package AWSSDK.S3
dotnet add package AWSSDK.Extensions.NETCore.Setup
```

> O `AWSSDK.S3` é 100% compatível com MinIO via `ServiceURL` customizado.

#### 2.2.3 — Configuração no appsettings.json

**Arquivo:** `src/backend/TimeTrack.Api/appsettings.json`

Adicionar seção:

```json
{
  "Storage": {
    "Provider": "MinIO",
    "Endpoint": "http://localhost:9000",
    "AccessKey": "timetrack-access-key",
    "SecretKey": "timetrack-secret-key",
    "BucketName": "timetrack-evidence",
    "Region": "us-east-1",
    "PresignedUrlTtlMinutes": 5,
    "ForcePathStyle": true
  }
}
```

> **ForcePathStyle = true** é obrigatório para MinIO (não usa virtual-hosted-style).

#### 2.2.4 — Criar Value Object de Configuração

**Arquivo:** `src/backend/TimeTrack.Backend.Application/Common/Interfaces/IStorageConfiguration.cs`

```csharp
public interface IStorageConfiguration
{
    string Endpoint { get; }
    string AccessKey { get; }
    string SecretKey { get; }
    string BucketName { get; }
    string Region { get; }
    int PresignedUrlTtlMinutes { get; }
    bool ForcePathStyle { get; }
}
```

**Implementação:** `src/backend/TimeTrack.Backend.Infrastructure/Services/StorageConfiguration.cs`
- Bind da seção `"Storage"` do `IConfiguration`

#### 2.2.5 — Criar Interface do Storage Service

**Arquivo:** `src/backend/TimeTrack.Backend.Domain/Interfaces/Services/IObjectStorageService.cs`

```csharp
public interface IObjectStorageService
{
    Task<string> GeneratePresignedUploadUrlAsync(
        string storageKey, string contentType, int ttlMinutes, CancellationToken ct);

    Task<string> GeneratePresignedDownloadUrlAsync(
        string storageKey, int ttlMinutes, CancellationToken ct);

    Task DeleteObjectAsync(string storageKey, CancellationToken ct);

    Task<bool> ObjectExistsAsync(string storageKey, CancellationToken ct);

    Task<long> GetObjectSizeAsync(string storageKey, CancellationToken ct);

    Task<IAsyncEnumerable<string>> ListOrphanKeysAsync(
        HashSet<string> knownKeys, CancellationToken ct);
}
```

#### 2.2.6 — Implementar ObjectStorageService

**Arquivo:** `src/backend/TimeTrack.Backend.Infrastructure/Services/ObjectStorageService.cs`

```csharp
public sealed class ObjectStorageService : IObjectStorageService
{
    private readonly AmazonS3Client _client;
    private readonly IStorageConfiguration _config;

    public ObjectStorageService(IStorageConfiguration config)
    {
        _config = config;

        var awsConfig = new AmazonS3Config
        {
            ServiceURL = config.Endpoint,
            ForcePathStyle = config.ForcePathStyle,
            RegionEndpoint = RegionEndpoint.GetBySystemName(config.Region)
        };

        _client = new AmazonS3Client(
            config.AccessKey,
            config.SecretKey,
            awsConfig);
    }

    // Implementar cada método usando _client:
    // GetPreSignedURL com HttpMethod.PUT (upload) ou GET (download)
    // DeleteObjectAsync, ListObjectsV2Async, etc.
}
```

Chave de storage multi-tenant: `evidence/{org_id}/{yyyy-MM-dd}/{evidence_id}.jpg`

#### 2.2.7 — Criar Entidade `EvidenceItem` (Domain)

**Arquivo:** `src/backend/TimeTrack.Backend.Domain/Entities/EvidenceItem.cs`

```csharp
public sealed class EvidenceItem
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid OrgId { get; private set; }
    public Guid DeviceId { get; private set; }
    public string EvidenceType { get; private set; } = "screenshot";
    public string StorageKey { get; private set; } = string.Empty;
    public DateTime CapturedAt { get; private set; }
    public string AppName { get; private set; } = string.Empty;
    public string? WindowTitleHash { get; private set; }
    public long FileSizeBytes { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Navigation
    public User? User { get; private set; }
    public Device? Device { get; private set; }
    public Organization? Organization { get; private set; }

    private EvidenceItem() { }

    public static EvidenceItem Create(
        Guid id, Guid userId, Guid orgId, Guid deviceId,
        string evidenceType, string storageKey,
        DateTime capturedAt, string appName,
        string? windowTitleHash, long fileSizeBytes) { ... }

    public void SoftDelete() { IsDeleted = true; DeletedAt = DateTime.UtcNow; }
}
```

#### 2.2.8 — Criar Entidade `StorageKey` (Domain)

**Arquivo:** `src/backend/TimeTrack.Backend.Domain/Entities/StorageKey.cs`

```csharp
public sealed class StorageKey
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public string Bucket { get; private set; } = string.Empty;
    public string Key { get; private set; } = string.Empty;
    public string Region { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private StorageKey() { }

    public static StorageKey Create(Guid orgId, string bucket, string key, string region) { ... }
}
```

#### 2.2.9 — Criar Configurações EF Core

**Arquivo:** `src/backend/TimeTrack.Backend.Infrastructure/Persistence/Configurations/EvidenceItemConfiguration.cs`

```csharp
public sealed class EvidenceItemConfiguration : IEntityTypeConfiguration<EvidenceItem>
{
    public void Configure(EntityTypeBuilder<EvidenceItem> builder)
    {
        builder.ToTable("evidence_items");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EvidenceType).IsRequired().HasMaxLength(50);
        builder.Property(e => e.StorageKey).IsRequired().HasMaxLength(500);
        builder.Property(e => e.AppName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.WindowTitleHash).HasMaxLength(128);
        builder.Property(e => e.FileSizeBytes).IsRequired();
        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.HasIndex(e => new { e.OrgId, e.CapturedAt });
        builder.HasIndex(e => new { e.UserId, e.CapturedAt });
        builder.HasIndex(e => e.StorageKey).IsUnique();
        builder.HasIndex(e => new { e.IsDeleted, e.CapturedAt });

        // Relationships
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Device)
            .WithMany()
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

**Arquivo:** `src/backend/TimeTrack.Backend.Infrastructure/Persistence/Configurations/StorageKeyConfiguration.cs`

Similar pattern para `storage_keys`.

#### 2.2.10 — Registrar no DbContext

**Arquivo:** `src/backend/TimeTrack.Backend.Infrastructure/Persistence/TimeTrackDbContext.cs`

Adicionar:

```csharp
public DbSet<EvidenceItem> EvidenceItems => Set<EvidenceItem>();
public DbSet<StorageKey> StorageKeys => Set<StorageKey>();
```

Em `ConfigureMultiTenantFilters()`:

```csharp
modelBuilder.Entity<EvidenceItem>()
    .HasQueryFilter(e => !_currentUser.IsAuthenticated || e.OrgId == _currentUser.OrgId);

modelBuilder.Entity<StorageKey>()
    .HasQueryFilter(s => !_currentUser.IsAuthenticated || s.OrgId == _currentUser.OrgId);
```

#### 2.2.11 — Criar Repositório

**Interface:** `src/backend/TimeTrack.Backend.Domain/Interfaces/Repositories/IEvidenceItemRepository.cs`

```csharp
public interface IEvidenceItemRepository
{
    Task<EvidenceItem?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<EvidenceItem>> GetByPeriodAsync(
        Guid orgId, Guid? userId, DateTime startDate, DateTime endDate,
        string? evidenceType, int limit, int offset, CancellationToken ct);
    Task AddAsync(EvidenceItem item, CancellationToken ct);
    Task UpdateAsync(EvidenceItem item, CancellationToken ct);
    Task<int> CountByOrgAsync(Guid orgId, CancellationToken ct);
    Task<long> SumFileSizeByOrgAsync(Guid orgId, CancellationToken ct);
    Task<IReadOnlyList<EvidenceItem>> GetExpiredAsync(
        Guid orgId, DateTime cutoffDate, int batchSize, CancellationToken ct);
}
```

**Implementação:** `src/backend/TimeTrack.Backend.Infrastructure/Repositories/EvidenceItemRepository.cs`

Seguir padrão dos repositórios existentes.

#### 2.2.12 — Criar Migration

```bash
dotnet ef migrations add AddEvidenceItemsAndStorageKeys \
  --project src/backend/TimeTrack.Backend.Infrastructure \
  --startup-project src/backend/TimeTrack.Api
```

#### 2.2.13 — Criar DTOs e Commands

**Pasta:** `src/backend/TimeTrack.Backend.Application/Evidence/`

```
Evidence/
├── DTOs/
│   ├── PresignedUploadUrlResponse.cs
│   ├── PresignedDownloadUrlResponse.cs
│   ├── EvidenceItemResponse.cs
│   └── RequestPresignedUploadUrlRequest.cs
├── Commands/
│   ├── RequestPresignedUploadUrlCommand.cs
│   └── RequestPresignedDownloadUrlCommand.cs
└── Queries/
    └── GetEvidenceByIdQuery.cs
```

**RequestPresignedUploadUrlCommand:**
- Input: `file_name`, `evidence_type`, `content_type`, `device_id`, `app_name`, `captured_at`
- Gera `evidence_id` (UUID)
- Gera `storage_key`: `evidence/{org_id}/{yyyy-MM-dd}/{evidence_id}.jpg`
- Cria registro `EvidenceItem` (sem `file_size_bytes` ainda — atualizado pelo Agent após upload)
- Cria registro `StorageKey`
- Gera presigned URL PUT com TTL 5min
- Retorna `{ upload_url, evidence_id, storage_key }`

**RequestPresignedDownloadUrlCommand:**
- Input: `evidence_id`
- Busca `EvidenceItem` por ID
- Registra acesso no `AuditLog` (action: `evidence.view` ou `evidence.download`)
- Gera presigned URL GET com TTL 5min
- Retorna `{ download_url, evidence_item }`

#### 2.2.14 — Criar Controller

**Arquivo:** `src/backend/TimeTrack.Api/Controllers/EvidenceController.cs`

```csharp
[ApiController]
[Route("api/v1/evidence")]
[Authorize]
[RequireSubscription]
[EnableRateLimiting(RateLimitingExtensions.PolicyNames.Default)]
public sealed class EvidenceController : ControllerBase
{
    // POST /api/v1/evidence/upload-url
    [HttpPost("upload-url")]
    [ProducesResponseType(typeof(PresignedUploadUrlResponse), 200)]
    public async Task<ActionResult<PresignedUploadUrlResponse>> RequestUploadUrl(
        [FromBody] RequestPresignedUploadUrlRequest request, CancellationToken ct)

    // GET /api/v1/evidence/{id}/download-url
    [HttpGet("{id:guid}/download-url")]
    [ProducesResponseType(typeof(PresignedDownloadUrlResponse), 200)]
    public async Task<ActionResult<PresignedDownloadUrlResponse>> RequestDownloadUrl(
        [FromRoute] Guid id, CancellationToken ct)
}
```

#### 2.2.15 — Registrar DI

**Arquivo:** `src/backend/TimeTrack.Backend.Infrastructure/Extensions/InfrastructureServiceCollectionExtensions.cs`

Adicionar:

```csharp
// Evidence Storage
services.AddScoped<IEvidenceItemRepository, EvidenceItemRepository>();
services.AddSingleton<IObjectStorageService, ObjectStorageService>();
services.AddSingleton<IStorageConfiguration, StorageConfiguration>();
```

No `HangfireConfiguration.cs` — registrar jobs de evidência (Fase 3).

#### 2.2.16 — Health Check do Storage

**Arquivo:** adicionar health check para MinIO connectivity.

```csharp
// Em Program.cs ou HealthCheck configuration:
builder.Services.AddHealthChecks()
    .AddCheck<S3StorageHealthCheck>("minio-storage");
```

O `S3StorageHealthCheck` tenta `ListObjectsV2` com max-keys=1 no bucket.

#### 2.2.17 — Critérios de Aceite CX-179

- [ ] Bucket MinIO configurado e acessível via SDK (`AWSSDK.S3`)
- [ ] `POST /api/v1/evidence/upload-url` retorna presigned URL válida
- [ ] `GET /api/v1/evidence/{id}/download-url` retorna presigned URL com TTL 5min
- [ ] Tabela `evidence_items` criada via migration versionada
- [ ] Tabela `storage_keys` criada via migration versionada
- [ ] Bucket privado — sem acesso público direto
- [ ] Upload direto do Agent funciona com presigned URL (PUT)
- [ ] Multi-tenancy: isolamento por `org_id` no prefixo da chave
- [ ] Health check do MinIO no `/health`
- [ ] Query filters de multi-tenancy para EvidenceItem e StorageKey

---

## 3. Fase 2 — Captura no Agent

> **Objetivo:** Implementar captura de screenshot, fila de upload com criptografia e scheduler.
> **Milestone:** F2-E2 — Screenshots Agent
> **Tasks Linear:** CX-182 + CX-183 + CX-184
> **Estimativa:** 14 pontos (6 + 5 + 3)

### 3.1 CX-182 — Windows Graphics Capture

> **Depende de:** CX-181 (política de apps excluídos)

#### 3.1.1 — Criar Interface

**Arquivo:** `src/agent/TimeTrack.Agent.Domain/Services/IScreenshotCapture.cs`

```csharp
public interface IScreenshotCapture
{
    Task<ScreenshotResult?> CaptureActiveWindowAsync(CancellationToken ct);
}

public sealed record ScreenshotResult(
    byte[] JpegData,
    int Width,
    int Height,
    string ActiveAppName,
    string? ActiveWindowTitleHash,
    long FileSizeBytes
);
```

#### 3.1.2 — Implementar WindowsGraphicsCaptureService

**Arquivo:** `src/agent/TimeTrack.Agent.Infrastructure/Services/WindowsGraphicsCaptureService.cs`

Implementação usando WinRT interop:

```csharp
public sealed class WindowsGraphicsCaptureService : IScreenshotCapture
{
    // Requisitos:
    // 1. Obter HWND da janela ativa via GetForegroundWindow() (P/Invoke)
    // 2. Criar GraphicsCaptureItem a partir do HWND
    // 3. Direct3D11CaptureFramePool → SoftwareBitmap → JPEG (quality 60%)
    // 4. Thread de baixa prioridade
    // 5. Fallback para System.Drawing se Win < 10 1903

    // Performance targets:
    // CPU < 2%, Memória < 50MB adicional, Captura < 500ms, JPEG < 200KB
}
```

**Dependências NuGet:**

```bash
# No projeto do Agent
dotnet add package Microsoft.WindowsAppSDK
dotnet add package Microsoft.Graphics.Win2D
```

**Regras de captura:**
1. Apenas janela ativa (nunca tela inteira)
2. Verificar app excluído → skip silencioso
3. Compressão JPEG 60% (antes de I/O)
4. Thread priority = `Lowest`
5. Pular se `UserIdleDetector.IsIdle`
6. Respeitar barra de captura do Windows (obrigatório da API)
7. Hash SHA-256 do window_title (nunca armazenar título em claro)

#### 3.1.3 — Criar Serviço de Criptografia Local

**Arquivo:** `src/agent/TimeTrack.Agent.Infrastructure/Services/LocalEncryptionService.cs`

```csharp
public interface ILocalEncryptionService
{
    byte[] Encrypt(byte[] plaintext, out byte[] iv);
    byte[] Decrypt(byte[] ciphertext, byte[] iv);
}

// Implementação: AES-256-GCM com chave derivada via DPAPI (Windows)
// Chave master protegida via DPAPI: CurrentUser scope
// IV aleatório por arquivo (12 bytes para GCM)
```

#### 3.1.4 — Critérios de Aceite CX-182

- [ ] `IScreenshotCapture` implementado com `Windows.Graphics.Capture`
- [ ] Captura apenas janela ativa (não tela inteira)
- [ ] Apps excluídos são ignorados silenciosamente
- [ ] JPEG 60% com tamanho < 200KB
- [ ] Thread de baixa prioridade
- [ ] Não captura durante idle do usuário
- [ ] CPU < 2%, memória < 50MB
- [ ] Fallback graceful se API não disponível

---

### 3.2 CX-184 — Scheduler de Screenshots

> **Depende de:** CX-182 (captura) + CX-181 (políticas)

#### 3.2.1 — Criar ScreenshotScheduler (IHostedService)

**Arquivo:** `src/agent/TimeTrack.AgentService/Workers/ScreenshotScheduler.cs`

```csharp
public sealed class ScreenshotScheduler : BackgroundService
{
    private readonly IScreenshotCapture _capture;
    private readonly IEvidenceUploadQueue _uploadQueue;
    private readonly IOptionsMonitor<EvidencePolicy> _policy;
    private readonly IUserIdleDetector _idleDetector;

    // Lógica principal:
    // 1. PeriodicTimer com intervalo de _policy.ScreenshotIntervalMinutes
    // 2. Antes de cada captura, verificar:
    //    - ScreenshotsEnabled == true
    //    - !UserIdleDetector.IsIdle
    //    - Agent não está em pausa
    //    - App ativo NÃO está na lista de excluídos
    //    - CPU do processo < 5%
    // 3. Se todas as condições ok: capturar → enfileirar upload
    // 4. Se não: log do motivo do skip e aguardar próximo ciclo
    // 5. Re-carrega política a cada sync (via IOptionsMonitor)
}
```

#### 3.2.2 — Criar Options de EvidencePolicy

**Arquivo:** `src/agent/TimeTrack.Agent.Domain/Configuration/EvidencePolicy.cs`

```csharp
public sealed class EvidencePolicy
{
    public bool ScreenshotsEnabled { get; set; } = false;
    public int ScreenshotIntervalMinutes { get; set; } = 5;
    public List<string> ScreenshotExcludedApps { get; set; } = new();
    public int EvidenceRetentionDays { get; set; } = 30;
}
```

#### 3.2.3 — Critérios de Aceite CX-184

- [ ] `ScreenshotScheduler` dispara capturas no intervalo configurado
- [ ] Suspende capturas quando usuário idle
- [ ] Suspende quando tracking pausado
- [ ] Pula captura quando app excluído está ativo
- [ ] Respeita `screenshots_enabled` — para/inicia em tempo real
- [ ] CPU > 5% adia captura sem acumular
- [ ] Mudanças de política aplicadas sem restart

---

### 3.3 CX-183 — Fila de Upload de Evidências (Outbox)

> **Depende de:** CX-179 (presigned URLs) + CX-182 (captura)

#### 3.3.1 — Criar Tabela SQLite no Agent

**Arquivo:** `src/agent/TimeTrack.Agent.Infrastructure/Persistence/SqliteContext.cs`

Adicionar no `InitializeSchemaAsync()`:

```sql
CREATE TABLE IF NOT EXISTS evidence_upload_queue (
    id TEXT PRIMARY KEY,
    local_path TEXT NOT NULL,
    evidence_type TEXT NOT NULL DEFAULT 'screenshot',
    captured_at TEXT NOT NULL,
    app_name TEXT NOT NULL,
    window_title_hash TEXT,
    attempt_count INTEGER NOT NULL DEFAULT 0,
    next_attempt_utc TEXT NOT NULL,
    uploaded_at TEXT,
    file_size_bytes INTEGER NOT NULL DEFAULT 0,
    status TEXT NOT NULL DEFAULT 'pending',  -- pending, uploading, uploaded, failed
    created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX IF NOT EXISTS ix_evidence_queue_status ON evidence_upload_queue(status, next_attempt_utc);
```

#### 3.3.2 — Criar Interface do Upload Queue

**Arquivo:** `src/agent/TimeTrack.Agent.Domain/Services/IEvidenceUploadQueue.cs`

```csharp
public interface IEvidenceUploadQueue
{
    Task EnqueueAsync(ScreenshotResult screenshot, CancellationToken ct);
    Task<IReadOnlyList<PendingEvidenceItem>> GetPendingAsync(int limit, CancellationToken ct);
    Task MarkAsUploadedAsync(string id, CancellationToken ct);
    Task MarkAsFailedAsync(string id, string error, CancellationToken ct);
    Task CleanupOldEntriesAsync(int olderThanDays, CancellationToken ct);
}
```

#### 3.3.3 — Criar EvidenceUploadWorker (IHostedService)

**Arquivo:** `src/agent/TimeTrack.AgentService/Workers/EvidenceUploadWorker.cs`

```
Fluxo:
1. Roda a cada 30 segundos (PeriodicTimer)
2. Busca itens: status = 'pending' AND next_attempt_utc <= now AND attempt_count < 3
3. Para cada item:
   a. Solicitar presigned URL: POST /api/v1/evidence/upload-url
   b. Descriptografar arquivo local (se criptografado)
   c. PUT direto para a presigned URL (HttpClient)
   d. Se sucesso: MarkAsUploaded + deletar arquivo local
   e. Se falha: incrementar attempt_count + backoff (1min, 2min, 4min)
4. Após 3 falhas: status = 'failed', logar erro
```

**Retry com backoff exponencial:**
- Tentativa 1: imediato
- Tentativa 2: após 1 minuto
- Tentativa 3: após 2 minutos
- Após 3 falhas: marcar como `failed`

**Pasta temporária:** `%LOCALAPPDATA%/TimeTrack/evidence_queue/`

**Cleanup:** arquivos órfãos com mais de 24h são removidos na inicialização.

#### 3.3.4 — Critérios de Aceite CX-183

- [ ] Tabela `evidence_upload_queue` criada no SQLite
- [ ] `EvidenceUploadWorker` roda como IHostedService separado do SyncWorker
- [ ] Fluxo: captura → criptografa → enfileira → upload → deleta local
- [ ] Criptografia AES-256-GCM com DPAPI
- [ ] Retry com backoff exponencial (máx 3 tentativas)
- [ ] Cleanup de arquivos órfãos
- [ ] Upload via presigned URL (PUT)
- [ ] Não interfere com o SyncWorker principal

---

## 4. Fase 3 — Consumo Backend

> **Objetivo:** Jobs de retenção Hangfire + endpoint de consulta de evidências.
> **Milestone:** F2-E1 (parcial) + F2-E4 (parcial)
> **Tasks Linear:** CX-180 + CX-190
> **Estimativa:** 7 pontos (4 + 3)

### 4.1 CX-180 — Jobs de Retenção e Limpeza (Hangfire)

> **Depende de:** CX-179 (storage service + tabela evidence_items)

#### 4.1.1 — Criar Interface do Job

**Arquivo:** `src/backend/TimeTrack.Backend.Infrastructure/Jobs/Interfaces/IEvidenceRetentionJob.cs`

```csharp
public interface IEvidenceRetentionJob
{
    Task ExecuteAsync();
}

public interface IStorageQuotaCheckJob
{
    Task ExecuteAsync();
}

public interface IOrphanCleanupJob
{
    Task ExecuteAsync();
}
```

#### 4.1.2 — Implementar EvidenceRetentionJob

**Arquivo:** `src/backend/TimeTrack.Backend.Infrastructure/Jobs/EvidenceRetentionJob.cs`

```
Fluxo (diariamente às 03:00 UTC):
1. Buscar todas as orgs ativas
2. Para cada org:
   a. Obter evidence_retention_days da política
   b. Buscar evidence_items onde captured_at < cutoff e is_deleted = false
   c. Para cada item (batch de 500):
      - Deletar arquivo do S3 (IObjectStorageService.DeleteObjectAsync)
      - Deletar registro do storage_keys
      - Hard delete do evidence_item
   d. Log de cada item deletado para auditoria
```

#### 4.1.3 — Implementar StorageQuotaCheckJob

**Arquivo:** `src/backend/TimeTrack.Backend.Infrastructure/Jobs/StorageQuotaCheckJob.cs`

```
Fluxo (diariamente às 04:00 UTC):
1. Buscar todas as orgs ativas
2. Para cada org:
   a. Calcular storage_used_bytes (SUM de file_size_bytes em evidence_items)
   b. Verificar se > 80% da storage_quota_gb
   c. Se sim: marcar storage_warning = true (campo a adicionar na Organization)
   d. Se não: marcar storage_warning = false
```

#### 4.1.4 — Implementar OrphanCleanupJob

**Arquivo:** `src/backend/TimeTrack.Backend.Infrastructure/Jobs/OrphanCleanupJob.cs`

```
Fluxo (semanalmente, domingo às 02:00 UTC):
1. Listar todas as storage_keys conhecidas do banco
2. Usar IObjectStorageService.ListOrphanKeysAsync para encontrar objetos sem metadata
3. Deletar arquivos órfãos do S3
4. Log de cada arquivo removido
```

#### 4.1.5 — Registrar Jobs no Hangfire

**Arquivo:** `src/backend/TimeTrack.Backend.Infrastructure/Jobs/Configuration/HangfireConfiguration.cs`

```csharp
// Em AddHangfireJobs():
services.AddScoped<IEvidenceRetentionJob, EvidenceRetentionJob>();
services.AddScoped<IStorageQuotaCheckJob, StorageQuotaCheckJob>();
services.AddScoped<IOrphanCleanupJob, OrphanCleanupJob>();

// Em ConfigureRecurringJobs():
RecurringJob.AddOrUpdate<IEvidenceRetentionJob>(
    "evidence-retention-job",
    job => job.ExecuteAsync(),
    "0 3 * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

RecurringJob.AddOrUpdate<IStorageQuotaCheckJob>(
    "storage-quota-check",
    job => job.ExecuteAsync(),
    "0 4 * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

RecurringJob.AddOrUpdate<IOrphanCleanupJob>(
    "orphan-cleanup",
    job => job.ExecuteAsync(),
    "0 2 * * 0", // Domingo às 02:00
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
```

#### 4.1.6 — Critérios de Aceite CX-180

- [ ] `EvidenceRetentionJob` roda diariamente e deleta itens expirados + arquivos S3
- [ ] Respeita `evidence_retention_days` diferente por org
- [ ] `StorageQuotaCheckJob` alerta quando org ultrapassa 80%
- [ ] `OrphanCleanupJob` remove arquivos S3 sem metadata
- [ ] Logging estruturado (Serilog) em todos os jobs
- [ ] Jobs registrados no Dashboard Hangfire

---

### 4.2 CX-190 — Endpoint de Evidências por Período

> **Depende de:** CX-179 (tabela evidence_items)

#### 4.2.1 — Criar Query

**Arquivo:** `src/backend/TimeTrack.Backend.Application/Evidence/Queries/GetEvidenceByPeriodQuery.cs`

```csharp
public sealed record GetEvidenceByPeriodQuery(
    Guid? UserId,
    DateTime StartDate,
    DateTime EndDate,
    string? EvidenceType,
    int Page = 1,
    int PageSize = 20
) : IRequest<PagedResult<EvidenceItemResponse>>;
```

#### 4.2.2 — Criar Handler

O handler deve:
1. Validar que o requester tem permissão (RBAC)
2. Buscar evidence_items com filtros
3. Gerar presigned download URLs para cada item (thumbnails)
4. Retornar paginado

**RBAC:**
- **Colaborador:** vê apenas próprias evidências
- **Gestor:** vê evidências da equipe
- **Admin:** vê todas da org

#### 4.2.3 — Adicionar Endpoint

**Arquivo:** `src/backend/TimeTrack.Api/Controllers/EvidenceController.cs`

```csharp
// GET /api/v1/evidence?userId=&startDate=&endDate=&type=screenshot&page=1&pageSize=20
[HttpGet]
[ProducesResponseType(typeof(PagedResult<EvidenceItemResponse>), 200)]
public async Task<ActionResult<PagedResult<EvidenceItemResponse>>> GetEvidence(
    [FromQuery] Guid? userId,
    [FromQuery] DateTime startDate,
    [FromQuery] DateTime endDate,
    [FromQuery] string? type,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken ct = default)
```

#### 4.2.4 — Critérios de Aceite CX-190

- [ ] `GET /api/v1/evidence` com filtros de período, user e tipo
- [ ] Presigned URLs para thumbnail e download na response
- [ ] RBAC: colaborador vê só as próprias, admin vê todas
- [ ] Paginação com metadados

---

## 5. Fase 4 — Frontend de Evidências

> **Objetivo:** Timeline enriquecida + modal de visualização de screenshot.
> **Milestone:** F2-E4 — Timeline + Evidências
> **Tasks Linear:** CX-189 + CX-191
> **Estimativa:** 8 pontos (5 + 3)

### 5.1 CX-189 — Timeline Enriquecida com Evidências

> **Depende de:** CX-190 (endpoint de evidências)

#### 5.1.1 — Expandir componente de Timeline existente

Adicionar indicadores visuais nos blocos de timeline:

- Ícone de câmera nos blocos que têm screenshot associado
- Clique no ícone abre o modal de visualização (CX-191)
- Filtro por "Com screenshot" / "Sem screenshot"
- Badge com contagem de evidências no período

#### 5.1.2 — Integrar com endpoint de evidências

Chamar `GET /api/v1/evidence` para o período visível na timeline e cruzar com activity_sessions por timestamp.

#### 5.1.3 — Critérios de Aceite CX-189

- [ ] Ícone de câmera em blocos com screenshot
- [ ] Filtro por tipo de evidência
- [ ] Badge com contagem
- [ ] Transição suave para modal

---

### 5.2 CX-191 — Modal de Visualização de Evidência

> **Depende de:** CX-190 (presigned download URL)

#### 5.2.1 — Implementar Modal

- Fullscreen com fundo escuro
- Imagem carregada via presigned URL
- Metadados ao lado: app_name, captured_at, file_size
- Navegação: anterior/próximo
- Botão de download (Admin/Gestor)
- Zoom in/out

#### 5.2.2 — Critérios de Aceite CX-191

- [ ] Modal fullscreen com imagem e metadados
- [ ] Navegação entre evidências
- [ ] Download para Admin/Gestor
- [ ] Zoom e controle de visualização

---

## 6. Fase 5 — Governança e Auditoria

> **Objetivo:** Audit log, painel admin de políticas, auditoria e direito ao esquecimento.
> **Milestone:** F2-E5 — Governança & Auditoria
> **Tasks Linear:** CX-192 + CX-193 + CX-194 + CX-195
> **Estimativa:** 13 pontos (3 + 4 + 3 + 3)

### 6.1 CX-192 — Audit Log de Acesso a Evidências

#### 6.1.1 — Extender AuditLog existente

A entidade `AuditLog` já existe com `Action`, `EntityType`, `EntityId`. Usar:
- `Action`: `evidence.view`, `evidence.download`, `evidence.delete`
- `EntityType`: `evidence_item`
- `EntityId`: ID do EvidenceItem
- `OldValues`/`NewValues`: serializar detalhes relevantes

#### 6.1.2 — Registrar acesso em toda interação com presigned URL

No `RequestPresignedDownloadUrlCommand` (já criado na Fase 1), garantir que o audit log está sendo registrado.

#### 6.1.3 — Criar endpoint de consulta

```csharp
// GET /api/v1/audit/evidence?userId=&action=&startDate=&endDate=&page=1
```

Apenas Admin/Gestor pode consultar.

#### 6.1.4 — Critérios de Aceite CX-192

- [ ] Cada acesso a evidência gera registro no `audit_log`
- [ ] Ações: `evidence.view`, `evidence.download`, `evidence.delete`
- [ ] Endpoint de consulta com filtros
- [ ] Apenas Admin/Gestor pode consultar

---

### 6.2 CX-193 — Frontend Admin: Painel de Políticas de Evidência

#### 6.2.1 — Adicionar seção "Evidências" no Admin Panel

- Toggle `screenshots_enabled`
- Slider para `screenshot_interval_minutes` (1-60 min)
- Lista editável de `screenshot_excluded_apps`
- Slider para `evidence_retention_days` (7-90 dias)
- Toggle `website_tracking_enabled`
- Preview das mudanças antes de salvar

#### 6.2.2 — Critérios de Aceite CX-193

- [ ] Seção "Evidências" no Admin Panel
- [ ] Todos os campos editáveis com validação de ranges
- [ ] Preview antes de salvar
- [ ] Feedback de sucesso/erro

---

### 6.3 CX-195 — Exclusão de Evidências pelo Colaborador

#### 6.3.1 — Criar endpoint de soft delete

```csharp
// DELETE /api/v1/evidence/{id}
[HttpDelete("{id:guid}")]
public async Task<ActionResult> DeleteEvidence(
    [FromRoute] Guid id, CancellationToken ct)
```

**Comportamento:**
- Colaborador: pode deletar apenas próprias evidências
- Admin: pode deletar de qualquer membro
- Marca `is_deleted = true`, `deleted_at = now()`
- Arquivo no S3 **não** é deletado imediatamente
- Hard delete assíncrono pelo `EvidenceRetentionJob` (limpa arquivos com `is_deleted = true` há > 7 dias)

#### 6.3.2 — Critérios de Aceite CX-195

- [ ] Colaborador pode deletar próprias evidências (soft delete)
- [ ] Admin pode deletar de qualquer membro
- [ ] Hard delete assíncrono pelo job de retenção
- [ ] Audit log registrado para cada deleção

---

### 6.4 CX-194 — Frontend Admin: Painel de Auditoria

#### 6.4.1 — Adicionar aba "Auditoria" no Admin Panel

- Tabela com logs de acesso a evidências
- Filtros: usuário, período, ação
- Exportação CSV
- Colunas: timestamp, actor, target user, ação, IP

#### 6.4.2 — Critérios de Aceite CX-194

- [ ] Tabela de logs com paginação
- [ ] Filtros funcionais
- [ ] Exportação CSV
- [ ] Apenas Admin/Gestor tem acesso

---

## 7. Fase 6 — Retenção e Validação Final

> **Objetivo:** Quotas de storage, indicador no admin e testes E2E.
> **Milestone:** F2-E6 — Retenção & Limpeza
> **Tasks Linear:** CX-196 + CX-198 + CX-197
> **Estimativa:** 9 pontos (3 + 2 + 4)

### 7.1 CX-196 — Sistema de Quotas de Storage por Org

#### 7.1.1 — Adicionar campos na Organization

**Arquivo:** `src/backend/TimeTrack.Backend.Domain/Entities/Organization.cs`

```csharp
public decimal StorageQuotaGb { get; private set; } = 10;
public long StorageUsedBytes { get; private set; } = 0;
public bool StorageWarning { get; private set; } = false;
```

Migration + Configuration + Query filters.

#### 7.1.2 — Implementar verificação de quota no upload

No `RequestPresignedUploadUrlCommand`:
- Verificar se `storage_used_bytes + file_size > storage_quota_gb * 1024^3`
- Se sim: retornar erro 403 `Storage quota exceeded`

#### 7.1.3 — Atualizar storage_used_bytes após upload

Criar endpoint para o Agent confirmar o upload:

```csharp
// POST /api/v1/evidence/{id}/confirm-upload
[HttpPost("{id:guid}/confirm-upload")]
public async Task<ActionResult> ConfirmUpload(
    [FromRoute] Guid id, [FromBody] ConfirmUploadRequest request, CancellationToken ct)
```

Atualizar `storage_used_bytes` da org.

#### 7.1.4 — Critérios de Aceite CX-196

- [ ] Quota padrão de 10GB por org
- [ ] Bloqueio de upload ao atingir 100%
- [ ] Alerta automático ao atingir 80%
- [ ] `storage_used_bytes` atualizado após cada upload

---

### 7.2 CX-198 — Frontend: Indicador de Storage no Admin Panel

#### 7.2.1 — Adicionar card de storage

- Barra de progresso visual
- Texto: "X.X GB usados de Y.Y GB (ZZ%)"
- Cor: verde (<60%), amarelo (60-80%), vermelho (>80%)
- Link para configurações de retenção

#### 7.2.2 — Critérios de Aceite CX-198

- [ ] Card de storage visível no Admin Panel
- [ ] Barra de progresso com cores dinâmicas
- [ ] Atualização automática

---

### 7.3 CX-197 — Validação E2E da Retenção Automática

#### 7.3.1 — Testes de Integração com Testcontainers

**Arquivo:** `tests/backend/TimeTrack.Backend.Tests.Integration/Evidence/`

```
Cenários:
1. Fluxo completo: Captura → Upload → Armazenamento → Retenção
2. Quota: Upload bloqueado ao atingir 100%
3. Retenção: Items expirados são deletados automaticamente
4. Orfãos: Arquivos S3 sem metadata são removidos
5. Soft delete: Colaborador pode deletar própria evidência
6. RBAC: Permissões corretas por role
```

#### 7.3.2 — Critérios de Aceite CX-197

- [ ] Testcontainers com MinIO + Postgres
- [ ] Fluxo completo testado end-to-end
- [ ] Todos os cenários acima cobertos
- [ ] Pipeline CI passa

---

## Apêndice A — Setup do MinIO

### Instalação via Docker (Desenvolvimento)

```bash
docker run -d \
  --name timetrack-minio \
  -p 9000:9000 \
  -p 9001:9001 \
  -e MINIO_ROOT_USER=timetrack-admin \
  -e MINIO_ROOT_PASSWORD=changeme-strong-password \
  -v minio_data:/data \
  minio/minio:latest server /data --console-address ":9001"
```

### Criar Bucket via MinIO Console

1. Acessar `http://localhost:9001`
2. Login com root credentials
3. Criar bucket `timetrack-evidence`
4. Access: **Private** (nenhuma política pública)

### Criar IAM User para a Aplicação

```bash
# Via MinIO Client (mc)
mc alias set local http://localhost:9000 timetrack-admin changeme-strong-password

# Criar user
mc admin user add local timetrack-app changeme-app-secret

# Criar policy
mc admin policy create local timetrack-evidence-policy - <<'EOF'
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "s3:PutObject",
        "s3:GetObject",
        "s3:DeleteObject",
        "s3:ListBucket"
      ],
      "Resource": [
        "arn:aws:s3:::timetrack-evidence",
        "arn:aws:s3:::timetrack-evidence/*"
      ]
    }
  ]
}
EOF

# Associar policy ao user
mc admin policy attach local timetrack-evidence-policy --user timetrack-app
```

### Produção (Easypanel/Cloud)

Para produção, usar:
- **Cloudflare R2** (zero egress, S3-compatible) — recomendado
- **MinIO standalone** no Easypanel
- **AWS S3** (se preferência por AWS)

O `AWSSDK.S3` funciona com todos via configuração de `ServiceURL`.

---

## Apêndice B — Diagrama do Pipeline Completo

```
┌─────────────────────────────────────────────────────────────────────┐
│                        AGENT (Windows Desktop)                      │
│                                                                     │
│  ┌──────────────┐    ┌──────────────────┐    ┌──────────────────┐  │
│  │  Screenshot   │    │  Local Encrypt    │    │  Upload Queue    │  │
│  │  Scheduler    │───>│  AES-256-GCM     │───>│  (SQLite outbox) │  │
│  │  (1-60 min)   │    │  + DPAPI          │    │                  │  │
│  └──────────────┘    └──────────────────┘    └────────┬─────────┘  │
│                                                        │            │
│  ┌──────────────┐                                     │            │
│  │  Evidence     │                                     │            │
│  │  Upload       │<────────────────────────────────────┘            │
│  │  Worker       │                                                  │
│  │  (30s poll)   │                                                  │
│  └──────┬───────┘                                                   │
│         │                                                           │
└─────────┼───────────────────────────────────────────────────────────┘
          │ 1. POST /evidence/upload-url → presigned PUT URL
          │ 2. PUT presigned URL → upload direto para S3
          │ 3. Confirmação
          │
          ▼
┌─────────────────────────────────────────────────────────────────────┐
│                        BACKEND (ASP.NET Core)                       │
│                                                                     │
│  ┌──────────────┐    ┌──────────────────┐    ┌──────────────────┐  │
│  │  Evidence     │    │  Object Storage   │    │  PostgreSQL      │  │
│  │  Controller   │───>│  Service          │───>│  evidence_items  │  │
│  │              │    │  (AWSSDK.S3)       │    │  storage_keys    │  │
│  └──────────────┘    └──────────────────┘    │  audit_log        │  │
│                                              └──────────────────┘  │
│  ┌──────────────┐    ┌──────────────────┐                          │
│  │  Hangfire     │    │  MinIO / R2       │                          │
│  │  Jobs         │───>│  (S3 bucket)      │                          │
│  │              │    │                    │                          │
│  │  • Retention  │    │  evidence/         │                          │
│  │  • Quota      │    │  {org_id}/         │                          │
│  │  • Orphan     │    │  {date}/           │                          │
│  │    cleanup    │    │  {id}.jpg          │                          │
│  └──────────────┘    └──────────────────┘                          │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────────────────────────┐
│                        FRONTEND (React/WebView2)                    │
│                                                                     │
│  ┌──────────────┐    ┌──────────────────┐    ┌──────────────────┐  │
│  │  Timeline     │    │  Evidence Modal   │    │  Admin Panel     │  │
│  │  Enriquecida  │    │  (fullscreen)     │    │                  │  │
│  │              │    │                    │    │  • Políticas     │  │
│  │  Camera icon │    │  • Zoom           │    │  • Auditoria     │  │
│  │  on blocks   │    │  • Navigation     │    │  • Storage       │  │
│  └──────────────┘    └──────────────────┘    └──────────────────┘  │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Apêndice C — Estrutura de Pastas Esperada

### Backend — Arquivos Novos

```
src/backend/
├── TimeTrack.Backend.Domain/
│   ├── Entities/
│   │   ├── EvidenceItem.cs                          # NOVO
│   │   └── StorageKey.cs                            # NOVO
│   └── Interfaces/
│       ├── Repositories/
│       │   └── IEvidenceItemRepository.cs           # NOVO
│       └── Services/
│           └── IObjectStorageService.cs             # NOVO
│
├── TimeTrack.Backend.Application/
│   ├── Common/Interfaces/
│   │   └── IStorageConfiguration.cs                 # NOVO
│   └── Evidence/                                    # NOVA PASTA
│       ├── DTOs/
│       │   ├── EvidenceItemResponse.cs
│       │   ├── PresignedUploadUrlResponse.cs
│       │   ├── PresignedDownloadUrlResponse.cs
│       │   ├── RequestPresignedUploadUrlRequest.cs
│       │   ├── ConfirmUploadRequest.cs
│       │   ├── UpdateEvidencePolicyRequest.cs
│       │   └── EvidencePolicyResponse.cs
│       ├── Commands/
│       │   ├── RequestPresignedUploadUrlCommand.cs
│       │   ├── RequestPresignedDownloadUrlCommand.cs
│       │   ├── ConfirmUploadCommand.cs
│       │   ├── DeleteEvidenceCommand.cs
│       │   └── UpdateEvidencePolicyCommand.cs
│       └── Queries/
│           ├── GetEvidenceByPeriodQuery.cs
│           ├── GetEvidenceByIdQuery.cs
│           └── GetEvidencePolicyQuery.cs
│
├── TimeTrack.Backend.Infrastructure/
│   ├── Persistence/
│   │   └── Configurations/
│   │       ├── EvidenceItemConfiguration.cs          # NOVO
│   │       └── StorageKeyConfiguration.cs            # NOVO
│   ├── Repositories/
│   │   └── EvidenceItemRepository.cs                 # NOVO
│   ├── Services/
│   │   ├── ObjectStorageService.cs                   # NOVO
│   │   └── StorageConfiguration.cs                   # NOVO
│   └── Jobs/
│       ├── EvidenceRetentionJob.cs                    # NOVO
│       ├── StorageQuotaCheckJob.cs                    # NOVO
│       ├── OrphanCleanupJob.cs                        # NOVO
│       └── Interfaces/
│           ├── IEvidenceRetentionJob.cs               # NOVO
│           ├── IStorageQuotaCheckJob.cs               # NOVO
│           └── IOrphanCleanupJob.cs                   # NOVO
│
├── TimeTrack.Api/
│   └── Controllers/
│       └── EvidenceController.cs                      # NOVO
│
└── Migrations/
    └── <timestamp>_AddEvidencePolicyFields.cs         # NOVO
    └── <timestamp>_AddEvidenceItemsAndStorageKeys.cs  # NOVO
    └── <timestamp>_AddOrganizationStorageFields.cs    # NOVO
```

### Agent — Arquivos Novos

```
src/agent/
├── TimeTrack.Agent.Domain/
│   ├── Services/
│   │   ├── IScreenshotCapture.cs                     # NOVO
│   │   ├── ILocalEncryptionService.cs                # NOVO
│   │   └── IEvidenceUploadQueue.cs                   # NOVO
│   └── Configuration/
│       └── EvidencePolicy.cs                         # NOVO
│
├── TimeTrack.Agent.Infrastructure/
│   └── Services/
│       ├── WindowsGraphicsCaptureService.cs           # NOVO
│       ├── LocalEncryptionService.cs                  # NOVO
│       └── EvidenceUploadQueue.cs                     # NOVO
│
└── TimeTrack.AgentService/
    └── Workers/
        ├── ScreenshotScheduler.cs                     # NOVO
        └── EvidenceUploadWorker.cs                    # NOVO
```

---

## Ordem de Execução Recomendada (Resumo)

```
SEMANA 1-2 (Fase 1 — Fundação)
  ├── CX-181: Schema de Políticas de Evidência       [3 pts]  ← SEM DEPENDÊNCIA
  └── CX-179: Setup MinIO/S3 + Presigned URLs         [5 pts]  ← SEM DEPENDÊNCIA

SEMANA 3-4 (Fase 2 — Captura no Agent)
  ├── CX-182: Windows Graphics Capture                [6 pts]  ← dep: CX-181
  ├── CX-184: Scheduler de Screenshots                [3 pts]  ← dep: CX-181, CX-182
  └── CX-183: Fila de Upload + Criptografia           [5 pts]  ← dep: CX-179, CX-182

SEMANA 5 (Fase 3 — Consumo Backend)
  ├── CX-180: Jobs de Retenção Hangfire               [4 pts]  ← dep: CX-179
  └── CX-190: Endpoint de Evidências por Período      [3 pts]  ← dep: CX-179

SEMANA 6 (Fase 4 — Frontend de Evidências)
  ├── CX-189: Timeline Enriquecida                    [5 pts]  ← dep: CX-190
  └── CX-191: Modal de Visualização                   [3 pts]  ← dep: CX-190

SEMANA 7 (Fase 5 — Governança)
  ├── CX-192: Audit Log de Evidências                 [3 pts]  ← dep: CX-190
  ├── CX-193: Painel de Políticas (Admin)             [4 pts]  ← dep: CX-181
  ├── CX-195: Exclusão pelo Colaborador               [3 pts]  ← dep: CX-190
  └── CX-194: Painel de Auditoria (Admin)             [3 pts]  ← dep: CX-192

SEMANA 8-9 (Fase 6 — Retenção e Validação)
  ├── CX-196: Quotas de Storage                       [3 pts]  ← dep: CX-179
  ├── CX-198: Indicador de Storage (Frontend)         [2 pts]  ← dep: CX-196
  └── CX-197: Testes E2E (Testcontainers)             [4 pts]  ← dep: TUDO
```

**Total: 16 tasks | 56 pontos | ~9 semanas**

---

> Documento gerado em 2026-04-23. Consulte as issues no Linear para specs completas de cada task.
