# Configuration

All secrets have been removed from `iptv.Api/appsettings.json` and must be supplied
through environment variables. ASP.NET Core binds `Section__Key` (double underscore)
environment variables onto the matching configuration section automatically, so no code
change is needed to read them.

## Required environment variables

| Environment variable | Configuration key | Notes |
|---|---|---|
| `MonjoSettings__ConnectionString` | `MonjoSettings:ConnectionString` | MongoDB connection string (e.g. `mongodb+srv://user:pass@host/?appName=...` or `mongodb://localhost:27017`). **Never** commit the real Atlas string. |
| `MonjoSettings__DatabaseName` | `MonjoSettings:DatabaseName` | Optional; defaults to `IpTvDb` from appsettings. |
| `JwtServiceSettings__SignatureKey` | `JwtServiceSettings:SignatureKey` | JWT signing key. |
| `JwtServiceSettings__EncryptionKey` | `JwtServiceSettings:EncryptionKey` | JWT encryption key (16 chars). |
| `JwtServiceSettings__ClientInfo__<client>` | `JwtServiceSettings:ClientInfo:<client>` | Client id → pre-shared secret map. Example: `JwtServiceSettings__ClientInfo__testtest=<secret>`. |
| `ApplicationPoolSettings__Applications__0__ApplicationId` | `ApplicationPoolSettings:Applications:0:ApplicationId` | Application pool entry (index `0`). |
| `ApplicationPoolSettings__Applications__0__PreSharedKey` | `ApplicationPoolSettings:Applications:0:PreSharedKey` | |
| `ApplicationPoolSettings__Applications__0__MasterSignature` | `ApplicationPoolSettings:Applications:0:MasterSignature` | |

## Optional feature flags

| Environment variable | Configuration key | Default | Notes |
|---|---|---|---|
| `ProviderSeed__Enabled` | `ProviderSeed:Enabled` | `true` | When true, the startup seeder inserts the free IPTV providers (only if a provider with the same `Name` does not already exist) and seeds the Channel Registry. |

## Local development example (bash)

```bash
export MonjoSettings__ConnectionString="mongodb://localhost:27017"
export MonjoSettings__DatabaseName="IpTvDb"
export JwtServiceSettings__SignatureKey="<dev-signature-key>"
export JwtServiceSettings__EncryptionKey="<16-char-key>"
export ApplicationPoolSettings__Applications__0__ApplicationId="<app-id>"
export ApplicationPoolSettings__Applications__0__PreSharedKey="<psk>"
export ApplicationPoolSettings__Applications__0__MasterSignature="<sig>"
dotnet run --project iptv.Api
```

## Owner action items (secrets committed to a public repo must be rotated)

The following values were previously committed in plaintext and **must be rotated** by the
owner — removing them from the current file does not remove them from git history:

- MongoDB Atlas password for user `magictv9044` (and ideally rotate the whole cluster user).
- `JwtServiceSettings:SignatureKey`
- `JwtServiceSettings:EncryptionKey`
- `JwtServiceSettings:ClientInfo` entries (e.g. `testtest`)
- `ApplicationPoolSettings:Applications[*]` `PreSharedKey` and `MasterSignature`
