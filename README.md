# Containerized Business Analytics System

Backend API สำหรับจัดการและวิเคราะห์ข้อมูลโครงการ (Project Monitoring) พัฒนาด้วย .NET 10 แบบ Clean Architecture และ PostgreSQL

> สถานะ: อยู่ระหว่างพัฒนา ขณะนี้มีเฉพาะโครงโปรเจกต์ ยังไม่มี API และ `docker-compose.yml`

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
