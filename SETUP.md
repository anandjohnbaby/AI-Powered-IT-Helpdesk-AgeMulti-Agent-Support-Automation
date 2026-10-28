# HelpBot (final version: custom login pages + own JWT) – setup notes

All source files are copied verbatim from last_and_final.txt. The .sln/.csproj files and the Angular
scaffold are created by CLI commands in the guide (sections 4 and 9), so create them first, then copy
the files from this zip over the generated ones.

IMPORTANT – delete leftovers from earlier versions, or the build will fail:
- src/Helpdesk.Api/CurrentUser.cs (old, project root) and src/Helpdesk.Api/Controllers/MeController.cs
- helpdesk-ui/src/app/core: session.service.ts, user.interceptor.ts, approver.guard.ts
- any old helpdesk.db
- Class1.cs, WeatherForecast.cs, Controllers/WeatherForecastController.cs
No Keycloak JS library is needed anymore (you can `npm uninstall keycloak-js`).

Backend packages: Microsoft.SemanticKernel, EntityFrameworkCore, Sqlite, Docker.DotNet, JwtBearer, Swashbuckle.AspNetCore
(see section 4 of the guide) and `<NoWarn>$(NoWarn);SKEXP0001;SKEXP0010</NoWarn>` in Helpdesk.Application.csproj.

Run:
```bash
docker compose down -v && docker compose up -d     # fresh Keycloak with the new realm, wait ~60s
cd src/Helpdesk.Api
dotnet user-secrets init
dotnet user-secrets set "Llm:ApiKey" "gsk_your_key"
dotnet user-secrets set "Keycloak:ClientSecret" "helpdesk-backend-secret"
dotnet user-secrets set "Jwt:SigningKey" "<random base64, 32+ chars>"
dotnet run --urls http://localhost:5000
# new terminal
cd helpdesk-ui && npm start
```
Employees: http://localhost:4200/login (alice / Alice@123, bob / Bob@123)
IT staff:  http://localhost:4200/it/login (itadmin / Admin@123)
