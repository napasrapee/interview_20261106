# Containerized Business Analytics System

Backend API สำหรับจัดการและวิเคราะห์ข้อมูลโครงการ (Project Monitoring) พัฒนาด้วย .NET 10 แบบ Clean Architecture และ PostgreSQL

> สถานะ: อยู่ระหว่างพัฒนา ขณะนี้มี Database และ `GET /health` ยังไม่มี endpoint ธุรกิจ

## ติดตั้งและเริ่มระบบ

ต้องมี Docker และ Docker Compose v2

```bash
cp .env.example .env
# แก้ทุกค่า change-me ใน .env
docker compose up --build
```

ตรวจสอบได้ที่ `http://localhost:8080/health`

## โครงสร้างโปรเจกต์

| โฟลเดอร์ | เนื้อหา |
|---|---|
| `src/` | Domain, Application, Infrastructure, Api |
| `tests/` | Unit test และ integration test |
| `db/init/` | SQL สำหรับ schema และ seed |
| `scripts/` | สคริปต์ดูแลระบบ |
| `config/` | ไฟล์ตั้งค่า เช่น logrotate |
| `docs/` | เอกสาร BA/SA รูปแบบ `.docx` |

## Build

ต้องมี .NET SDK 10

```bash
dotnet build
dotnet test
```
