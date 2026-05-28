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

Because this system handles sensitive member and medical data, **HTTPS is strictly enforced**. You must provide an SSL certificate for the Docker containers to run correctly.

### Provide your Custom Certificate
In the root of the project, create a `certs` folder and place your `cert.pem` and `key.pem` files inside it.

```bash
mkdir certs
# Copy your cert.pem and key.pem into this folder
```

The Nginx web server and the backend API will automatically load `cert.pem` and `key.pem` from the `./certs` directory when the containers are started.

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

## 🐳 Docker on Windows (WSL2)

If you are using Docker Desktop with the WSL2 backend, please keep the following in mind:

1.  **File System Performance:** For the fastest build times, it is highly recommended to clone this repository into your **WSL Home Directory** (e.g., `~/projects/CheckIn`) rather than on the Windows mount (`/mnt/c/...`).
2.  **Build Context (.dockerignore):** I have included a `.dockerignore` file. This is **critical** on Windows to prevent local `bin` and `obj` folders from being copied into the Linux containers, which will cause the build to fail with "Multiple assemblies" or "Platform mismatch" errors.
3.  **Line Endings:** Ensure your git is configured to handle line endings correctly (`git config --global core.autocrlf input`) to prevent issues with the Nginx configuration files.

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
