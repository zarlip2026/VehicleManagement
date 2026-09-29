# Vehicle Management

An ASP.NET Core MVC application for managing vehicles and weight categories. Vehicles are categorised automatically by weight using the latest category settings.

| Layer | Technology |
| --- | --- |
| UI | Razor views, CSS and JavaScript |
| Backend | ASP.NET Core MVC (.NET 8) |
| Data access | Entity Framework Core 8 |
| Database | SQL Server |
| Tests | xUnit business-rule tests |

---

## Setup and Run

Follow these steps in order. Run PowerShell commands from the folder containing `VehicleManagement.slnx`.

### 1. Required software

- **.NET 10 SDK** to build the `.slnx` solution.
- **.NET 8 and ASP.NET Core 8 runtimes** to run the application and EF tool. Installing the .NET 8 SDK supplies both runtimes.
- **SQL Server 2019 or later** — Standard and Express editions are supported.
- Internet access for initial package downloads.
- For the Visual Studio instructions, use a version supporting .NET 10 and `.slnx`, with the **ASP.NET and web development** workload. SSMS is optional.

Double-click **`Install Prerequisites.cmd`**. This file installs prerequisites and restores the EF tool. Complete the installation prompts. It requires WinGet (Microsoft App Installer) and does not install Visual Studio.

If you already have SQL Server under an instance name other than `SQLEXPRESS`, run this instead:

```powershell
.\setup.ps1 -SkipSqlServer
```

### 2. Configuration

Open `VehicleManagement/appsettings.json` and fill in the empty `ConnectionStrings:DefaultConnection` value using your SQL Server instance. Use `VehicleManagementDb` as the database name, as shown below.

The application stops at startup with a clear configuration error if this value is missing or blank.

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=VehicleManagementDb;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True;"
}
```

| SQL Server installation | Server value in JSON |
| --- | --- |
| Default instance on your computer | `localhost` |
| Named instance | `localhost\\INSTANCE_NAME` |
| Express instance named SQLEXPRESS | `.\\SQLEXPRESS` |

`TrustServerCertificate=True` is for local development.

Note: If your machine has an environment variable named `ConnectionStrings__DefaultConnection`, the application will use that value instead of the connection string in `appsettings.json`.

### 3. Database setup

Choose one method below to create the database, tables and initial data.

**From Visual Studio**

1. Open `VehicleManagement.slnx`.
2. Right-click **VehicleManagement** in Solution Explorer and select **Set as Startup Project**.
3. Open **Tools → NuGet Package Manager → Package Manager Console**.
4. Select **VehicleManagement** in the **Default project** dropdown and run:

```powershell
Update-Database
```

**From PowerShell**

```powershell
dotnet tool restore
dotnet ef database update --project VehicleManagement --startup-project VehicleManagement
```

Rerunning the command applies only pending migrations. Migrations do not run automatically when the application starts.

### 4. Build and Run

**From PowerShell**

```powershell
# Build the solution and restore required packages
dotnet build VehicleManagement.slnx -c Release

# Trust the local HTTPS development certificate
dotnet dev-certs https --trust

# Start the application
dotnet run --project VehicleManagement/VehicleManagement.csproj -c Release
```

Open `https://localhost:64690` (the default address in `VehicleManagement/Properties/launchSettings.json`). Press **Ctrl+C** to stop.

**From Visual Studio**

1. Right-click **VehicleManagement** in Solution Explorer and select **Set as Startup Project**.
2. Click **Run** (the green Start button), or press **F5**.
3. Accept the local HTTPS certificate prompt if shown.

| Page | Path |
| --- | --- |
| Home | `/` |
| Vehicles | `/VehiclesManage` |
| Categories | `/CategoriesManage` |

### 5. Automated tests

**From Visual Studio**

1. Right-click **VehicleManagement.UnitTests** in Solution Explorer and select **Run Tests**.
2. Repeat for **VehicleManagement.IntegrationTests**.
3. View the results in **Test → Test Explorer**.

**From PowerShell**

```powershell
dotnet test VehicleManagement.slnx -c Release
```

Unit and integration tests focus on category range rules, overlap and gap prevention, vehicle validation, sorting, category changes affecting vehicles, boundary values and invalid operations. Integration tests check category and vehicle requests through the MVC application. Both projects use isolated in-memory data and do not require SQL Server.

---

### Error logs


Errors, exceptions and business-rule validation failures are written to:

VehicleManagement/Logs/vehiclemanagement-YYYYMMDD.log

Unexpected errors include exception details and stack traces.

Validation failures are logged as warnings.

Entries include a timestamp, severity and request ID when available.

Submitted form values and successful operations are not logged.

Files rotate daily or at 10 MB. The latest 14 files are retained.


---

## Design Notes

### Overall solution architecture

```text
Razor views → MVC controllers → Services → EF Core → SQL Server
```

| Area | Responsibility |
| --- | --- |
| Views / wwwroot | Render pages and provide styles and browser scripts |
| Controllers | Handle HTTP requests, validate submitted forms and call services |
| Services | Apply business rules and access data through EF Core |
| Models | Define entities and input validation |
| ViewModels | Bind vehicle forms with nullable numeric inputs so missing values show required-field errors; database fields remain non-nullable |
| Data | Configure the DbContext and database mappings |
| Program.cs | Register dependencies and configure routing and middleware |

### Database design

| Table | Main fields |
| --- | --- |
| Vehicles | Owner name, manufacturer name, manufacture year and weight in kilograms |
| Categories | Name, unique minimum weight and required binary icon |
| Manufacturers | Unique manufacturer name |

Weights use `decimal(18,2)`. Vehicles do not store a category ID. The migration seeds Mazda, Mercedes, Honda, Ferrari and Toyota.

### Category boundary rules

Users enter only a **minimum weight** for each category. They do not enter a maximum weight. Categories are ordered by their minimum weight, and each category ends where the next one begins.

The minimum is inclusive; the next category's minimum is exclusive. The last category covers all remaining valid weights.

| Category | Minimum entered by user | Resulting weight range |
| --- | --- | --- |
| Light | 0.01 kg | `0.01 ≤ weight < 500 kg` |
| Medium | 500 kg | `500 ≤ weight < 2500 kg` |
| Heavy | 2500 kg | `2500 kg ≤ weight`, up to the vehicle weight limit |

This design prevents gaps and overlaps without asking users to maintain two boundaries. When a minimum changes, the neighbouring range adjusts automatically. It also reduces validation logic because the application does not need separate checks for overlapping ranges or gaps between independently entered minimums and maximums.

The application still enforces these rules:

- Minimum weights must be valid and unique.
- The first category must start at **0.01 kg** to cover every valid vehicle weight.
- Deleting the first category resets the next category's minimum to **0.01 kg**.
- The last remaining category cannot be deleted, keeping vehicles from displaying N/A.

Exactly **500 kg** belongs to Medium, and exactly **2500 kg** belongs to Heavy. Each valid vehicle weight belongs to exactly one category.

### Category calculation approach

Each vehicle is automatically assigned to the category whose weight range contains its weight. For example, a **700 kg** vehicle belongs to **Medium**.

To find the category, the application checks category minimums from highest to lowest and selects the first minimum that does not exceed the vehicle's weight.

The application uses current category settings whenever vehicles are read. Changing a category minimum therefore updates the displayed categories of existing vehicles without changing their records.

### Important assumptions

- Vehicle weight is **0.01–1,000,000 kg**, with at most two decimal places.
- Manufacture year is between **1880 and the server's current year**.
- Manufacturer changes are rare and managed by a developer through migrations or controlled database updates. Vehicles retain the selected manufacturer name.

### Design decisions

- **Agreed UI approach:** use Razor MVC for this application.
- **Agreed manufacturer storage:** keep manufacturers in a database table, maintained by a developer.
- **Retain the last category:** prevent deletion of the last remaining category so every valid vehicle weight continues to map to a category instead of displaying N/A.
- **Positive vehicle weights:** zero is invalid. With at most two decimal places, the smallest valid vehicle weight is 0.01 kg. The agreed first category minimum is also 0.01 kg, covering every valid vehicle weight. A category threshold of zero could also cover positive weights; it would not make a zero-weight vehicle valid.

- **Single Responsibility:** controllers handle requests; services handle business rules.
- **Dependency Inversion:** controllers depend on `ICategoryService` and `IVehicleService`, supplied through dependency injection.
- **Simple data access:** services use `DbContext` directly without an additional repository layer.
- **Image display:** detect the stored image format when rendering icons, so PNG, JPG/JPEG, WebP, GIF, BMP and AVIF use the correct media type without changing existing database records.
- **Validation:** client and server validation handle input rules. Vehicle saves check the manufacturer against the database list; EF parameterises normal database queries.
- **Error handling:** injected `ILogger` records unexpected errors. Users receive a generic error and request ID. Antiforgery checks protect form submissions.

### Known limitations

- Authentication and authorization are not implemented.
- Concurrent category edits could invalidate category rules.
- Browser-only icon validation accepts PNG, JPG/JPEG, WebP, GIF, BMP and AVIF files up to 1 MB (1,000,000 bytes). File type and size are not enforced on the server.
- In-memory tests do not verify SQL Server constraints, migrations or transactions.
- Browser validation depends on CDN scripts; server validation remains available.

### Further production improvements

- Separate the backend API from the client application to allow independent development and support web, mobile and other clients across platforms.
- Containerize the server for consistent deployment across development, testing and production environments.
- Add authentication and authorization.
- Protect concurrent category updates and add deeper image validation.
- Introduce managed secrets, trusted certificates, monitoring, backups and reviewed database deployments.
