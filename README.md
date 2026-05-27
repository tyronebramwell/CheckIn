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

Because this system handles sensitive member and medical data, **HTTPS is strictly enforced**. You must generate a local SSL certificate for the Docker containers to run correctly.

### Step 1: Create the Certificates Directory
In the root of the project, create a folder to hold your certificate:
```bash
mkdir certs
```

### Step 2: Generate the Certificate
Run the following command. **Important:** Replace `your_secure_password` with the same password you have set in the `CERT_PASSWORD` field of your `.env` file.

```bash
dotnet dev-certs https -ep ./certs/aspnetapp.pfx -p your_secure_password
```

### Step 3: Trust the Development Certificate
To avoid browser security warnings on your local machine, tell your OS to trust the .NET development certificates:
```bash
dotnet dev-certs https --trust
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

- `SignUpApi/`: ASP.NET Core Minimal API.
- `SignUpCommon/`: Shared Class Library for Data Models (POCOs).
- `SignUp/`: Blazor WebAssembly Frontend.
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
