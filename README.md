# QRMenu — Digital Menu Platform

A full-stack ASP.NET Core 8 web application for restaurant QR menus.

## Quick Start (Local)

```bash
# Open QrMenu.sln in Visual Studio 2022 and press F5
# OR run from terminal:
cd QrMenu.Web
dotnet run
```

Open http://localhost:5000

**Demo accounts:**
| Role | Email | Password |
|------|-------|----------|
| Admin | admin@qrmenu.com | Admin@123456! |
| Owner | owner@demo.com | Owner@123456! |

---

## Deploy to Railway (Recommended)

### Step 1 — Push to GitHub

```bash
git init
git add .
git commit -m "Initial commit"
git remote add origin https://github.com/YOUR_USERNAME/qrmenu.git
git push -u origin main
```

### Step 2 — Create Railway project

1. Go to [railway.app](https://railway.app) and sign up
2. Click **New Project → Deploy from GitHub repo**
3. Select your `qrmenu` repository
4. Railway detects the Dockerfile automatically

### Step 3 — Add a Volume (for SQLite persistence)

1. In Railway dashboard → your service → **Volumes**
2. Click **+ Add Volume**
3. Set mount path: `/data`
4. This keeps your database across deployments

### Step 4 — Set environment variables

In Railway → your service → **Variables**, add:

| Variable | Value |
|----------|-------|
| `ConnectionStrings__DefaultConnection` | `Data Source=/data/qrmenu.db` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Smtp__Host` | `smtp.gmail.com` |
| `Smtp__Port` | `587` |
| `Smtp__Username` | `your-email@gmail.com` |
| `Smtp__Password` | `your-16-char-app-password` |
| `Smtp__FromEmail` | `your-email@gmail.com` |
| `Smtp__FromName` | `QRMenu` |

> SMTP variables are optional. Without them the app works but won't send emails.

### Step 5 — Deploy

Click **Deploy** in Railway. Your app will be live at:
`https://your-app-name.up.railway.app`

---

## Environment Variables Reference

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__DefaultConnection` | SQLite path or PostgreSQL connection string |
| `ASPNETCORE_ENVIRONMENT` | `Development` or `Production` |
| `PORT` | Auto-set by Railway — do not set manually |
| `Smtp__Host` | SMTP server (smtp.gmail.com for Gmail) |
| `Smtp__Port` | SMTP port (587 for TLS) |
| `Smtp__Username` | Email address |
| `Smtp__Password` | App password (not your regular password) |
| `Smtp__FromEmail` | Sender email address |
| `Smtp__FromName` | Sender display name |

---

## Switch to PostgreSQL (Optional, for production)

1. Add PostgreSQL service in Railway dashboard
2. Railway auto-sets `DATABASE_URL` variable
3. Update `QrMenu.Infrastructure.csproj`:
```xml
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
```
4. Update `Program.cs`:
```csharp
// Replace UseSqlite with:
options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
```
5. Set variable: `ConnectionStrings__DefaultConnection` = your PostgreSQL connection string

---

## Project Structure

```
QrMenu.sln
├── QrMenu.Domain          → Entities
├── QrMenu.Application     → DTOs, Interfaces  
├── QrMenu.Infrastructure  → EF Core, Services
├── QrMenu.Web             → ASP.NET Core MVC
└── QrMenu.Tests           → xUnit Tests
```
