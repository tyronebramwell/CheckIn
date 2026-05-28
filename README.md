# Charity Event Registration & Attendance System

A privacy-by-design, local network system for managing charity event registrations and attendance using .NET 9, PostgreSQL, and Docker.

---

## 🚀 Quick Start (Docker)

1. **Configure Environment:**
   Update the `.env` file with your preferred database path and certificate password.

2. **Generate SSL Certificate:** (See the [SSL Setup Guide](#-ssl-setup-guide-mandatory) below)

3. **Launch System:**
   ```bash
   docker-compose up -d --build
   ```

4. **Access:**
   - **Website (UI):** [http://localhost:8080](http://localhost:8080)
   - **Swagger API Docs:** [https://localhost:5001](https://localhost:5001)
   - **Default Admin Login:** 
     - **Username:** `admin`
     - **Password:** `admin123`

---

## 🔐 SSL Setup Guide (Mandatory)

Because this system handles sensitive member and medical data, **HTTPS is strictly enforced**. You must generate and convert a local SSL certificate for the Docker containers to run correctly.

### Step 1: Generate the PFX Certificate
In the root of the project, create a `certs` folder and generate the .NET development certificate. 
**Important:** Use the password `Bra09094626!` to match the provided configuration, or update your `.env` accordingly.

```bash
mkdir certs
dotnet dev-certs https -ep ./certs/aspnetapp.pfx -p Bra09094626!
dotnet dev-certs https --trust
```

### Step 2: Convert to CRT/KEY for the UI Container
The Nginx server used for the website requires standard certificate and key files. Use **OpenSSL** (available in Git Bash on Windows or Terminal on Mac) to extract them:

```bash
openssl pkcs12 -in ./certs/aspnetapp.pfx -clcerts -nokeys -out ./certs/aspnetapp.crt -passin pass:Bra09094626!
openssl pkcs12 -in ./certs/aspnetapp.pfx -nocerts -nodes -out ./certs/aspnetapp.key -passin pass:Bra09094626!
```

---

## 🏁 Windows Build Fixes

If you encounter build errors on Windows, run these commands in PowerShell:

1. **Install Blazor Workload:**
   ```powershell
   dotnet workload install blazor-wasm
   ```

2. **Clean & Rebuild:**
   ```powershell
   dotnet clean CheckIn.sln
   Get-ChildItem -Recurse -Include bin,obj | Remove-Item -Recurse -Force
   dotnet build CheckIn.sln
   ```

---

## 🛠 Features

- **Master Roster:** Securely store member details, guardian contacts, and notes.
- **Real-Time Manifest:** Instant access to a list of all members currently checked in.
- **User Management:** Granular permissions (`CanViewData`, `CanAddUsers`, `CanManageVolunteers`).
- **Privacy First:** Marketing preferences are isolated from operational safety data.
- **Secure by Default:** 
  - Basic Authentication over mandatory HTTPS.
  - BCrypt password hashing.
  - Local database isolation.

---

## 📂 Project Structure

- `CheckInApi/`: ASP.NET Core Minimal API.
- `CheckInCommon/`: Shared Class Library for Data Models (POCOs).
- `CheckIn/`: Blazor WebAssembly Frontend.
- `docker-compose.yml`: Infrastructure orchestration (API + PostgreSQL).

---

## 📡 API Endpoints Summary

### Auth
- `POST /api/auth/login`: Validate credentials and retrieve permissions.

### Volunteers (Admins Only)
- `GET /api/volunteers`: List all system users.
- `POST /api/volunteers`: Register a new volunteer.
- `PUT /api/volunteers/{id}/password`: Update passwords (Self or Admin).
- `PUT /api/volunteers/{id}/permissions`: Update access levels.

### Members & Attendance
- `POST /api/members`: Register a new member.
- `GET /api/members`: Search the roster.
- `POST /api/attendance/check-in`: Clock-in a member.
- `PUT /api/attendance/check-out`: Clock-out a member.
- `GET /api/attendance/active`: Live safety manifest.
