# Containerized Business Analytics System

Backend API สำหรับจัดการและวิเคราะห์ข้อมูลโครงการ (Project Monitoring) พัฒนาด้วย .NET 10 แบบ Clean Architecture และ PostgreSQL 18 รันด้วย Docker Compose พร้อมรายงานความคืบหน้าแยกตามแผนก และสคริปต์ดูแลระบบบน Linux

## สิ่งที่ต้องมี

- Docker และ Docker Compose v2
- สำหรับสคริปต์ใน `scripts/`: Bash และ `curl` (บน Windows ให้ใช้ WSL2 และเปิด WSL Integration ใน Docker Desktop)

## ติดตั้งและเริ่มระบบ

```bash
docker compose up
```

คำสั่งเดียวนี้ใช้ได้ทันทีหลัง clone โดยไม่ต้องตั้งค่าอะไรเพิ่ม เมื่อระบบพร้อมจะเรียก `http://localhost:8080/health` ได้ ครั้งแรกจะใช้เวลาเพิ่มสำหรับการดาวน์โหลด image และ build

เมื่อเริ่มครั้งแรก ระบบจะ:

- สุ่มรหัสผ่าน Database, JWT secret และรหัสผ่าน Admin ลง volume `app_secrets` จึงไม่มีความลับอยู่ใน repository
- สร้างตารางและข้อมูลตัวอย่างอัตโนมัติจาก `db/init`
- สร้างบัญชี Admin `admin@example.com` ดูรหัสผ่านได้ด้วยคำสั่ง:

```bash
docker compose exec api cat /run/app-secrets/Admin__Password
```

ถ้าต้องการรันเบื้องหลังและรอจนพร้อมใช้ `docker compose up -d --wait` แล้วตรวจด้วย `./scripts/healthcheck.sh` และถ้าดึงโค้ดใหม่มาให้ใช้ `docker compose up --build` เพื่อไม่ให้ใช้ image เก่า

### ลองใช้งาน

```bash
ADMIN_PASSWORD=$(docker compose exec -T api cat /run/app-secrets/Admin__Password)
TOKEN=$(curl -s -H 'Content-Type: application/json' \
  -d "{\"email\":\"admin@example.com\",\"password\":\"$ADMIN_PASSWORD\"}" \
  http://localhost:8080/api/v1/auth/login | sed 's/.*"accessToken":"\([^"]*\)".*/\1/')

curl -H "Authorization: Bearer $TOKEN" http://localhost:8080/api/v1/reports/department-progress
```

### หยุดระบบ

```bash
docker compose down      # หยุดระบบ ข้อมูลยังอยู่
docker compose down -v   # ลบข้อมูลทั้งหมด รวมถึงไฟล์ backup และความลับ
```

## โครงสร้างโปรเจกต์

| โฟลเดอร์ | เนื้อหา |
|---|---|
| `src/` | Domain, Application, Infrastructure, Api |
| `tests/` | Unit test และ integration test |
| `db/init/` | SQL สำหรับ schema, index และ seed |
| `scripts/` | `backup.sh`, `healthcheck.sh`, `resource_usage.sh`, `rotate_logs.sh` และ `init-secrets.sh` ซึ่ง Compose เรียกใช้เอง |
| `config/` | `logrotate.conf` |
| `postman/` | Postman collection และ environment |
| `docs/` | เอกสาร BA และ SA รูปแบบ `.docx` พร้อม ER diagram และ data dictionary |

## การตั้งค่า (ไม่บังคับ)

ระบบทำงานได้โดยไม่มีไฟล์ `.env` ถ้าต้องการกำหนดค่าเองให้คัดลอก `.env.example` เป็น `.env` แล้วแก้ค่าที่ต้องการ

| ตัวแปร | ค่าเริ่มต้น | คำอธิบาย |
|---|---|---|
| `POSTGRES_DB`, `POSTGRES_USER` | `project_monitoring`, `analytics` | ชื่อฐานข้อมูลและผู้ใช้ |
| `POSTGRES_PASSWORD` | สุ่มอัตโนมัติ | รหัสผ่านฐานข้อมูล |
| `API_PORT` | `8080` | พอร์ตบนเครื่อง |
| `JWT_SECRET` | สุ่มอัตโนมัติ | ต้องยาวอย่างน้อย 32 ไบต์ |
| `JWT_ISSUER`, `JWT_AUDIENCE` | `project-monitoring`, `project-monitoring-clients` | ค่าของ JWT |
| `JWT_EXPIRES_MINUTES` | `60` | อายุของ token |
| `ADMIN_EMAIL` | `admin@example.com` | Admin เริ่มต้น ใช้เมื่อยังไม่มีบัญชีนี้ |
| `ADMIN_PASSWORD` | สุ่มอัตโนมัติ | รหัสผ่าน Admin ห้ามใช้ `change-me` |
| `BACKUP_RETENTION_DAYS` | `7` | จำนวนวันที่เก็บ backup |
| `LOG_RETENTION_DAYS` | `30` | จำนวนวันที่เก็บแถวใน `system_logs` |

ความลับและชื่อฐานข้อมูลมีผลเฉพาะตอนเริ่มครั้งแรก ถ้าจะเปลี่ยนภายหลังต้องใช้ `docker compose down -v` ซึ่งจะลบข้อมูลทั้งหมด ถ้าค่าไม่ถูกต้อง API จะไม่เริ่มทำงาน ดูสาเหตุได้ด้วย `docker compose logs api`

Database ไม่เปิดพอร์ตออกนอก Docker ถ้าต้องการดูข้อมูลให้ใช้:

```bash
docker compose exec db sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
```

## API

Base URL คือ `http://localhost:8080` และ endpoint ทั้งหมดขึ้นต้นด้วย `/api/v1` ยกเว้น `/health` ส่ง token ด้วย header `Authorization: Bearer <token>` ซึ่งมีอายุ 60 นาที

| Endpoint | สิทธิ์ขั้นต่ำ | คำอธิบาย |
|---|---|---|
| `GET /health` | ไม่ต้องใช้ token | ตอบ `200` หรือ `503` เมื่อ Database ไม่พร้อม |
| `POST /auth/login` | ไม่ต้องใช้ token | Login จำกัด 10 ครั้งต่อนาทีต่อ IP |
| `GET /auth/me` | member | ข้อมูลผู้ใช้ปัจจุบัน |
| `GET /departments`, `GET /departments/{id}` | member | ดูแผนก |
| `POST`, `PUT /{id}`, `DELETE /{id}` ของ `/departments` | admin | จัดการแผนก |
| `GET /users`, `GET /users/{id}` | manager | ดูผู้ใช้ กรองด้วย `departmentId`, `role` |
| `POST`, `PUT /{id}`, `DELETE /{id}` ของ `/users` | admin | จัดการผู้ใช้ |
| `GET /projects`, `GET /projects/{id}` | member | ดูโครงการ กรองด้วย `departmentId`, `status` |
| `POST`, `PUT /{id}`, `DELETE /{id}` ของ `/projects` | manager | จัดการโครงการ การลบจะลบงานในโครงการด้วย |
| `GET /tasks`, `GET /tasks/{id}` | member | ดูงาน กรองด้วย `projectId`, `assigneeUserId`, `status`, `overdue` |
| `POST`, `PUT /{id}`, `DELETE /{id}` ของ `/tasks` | manager | จัดการงาน |
| `PATCH /tasks/{id}/status` | member | member แก้ได้เฉพาะงานของตนเอง |
| `GET /reports/department-progress` | manager | ความคืบหน้าตามแผนก |
| `GET /reports/project-summary` | manager | สรุปรายโครงการ กรองด้วย `departmentId` |
| `GET /reports/user-workload` | manager | ภาระงานรายผู้ใช้ กรองด้วย `departmentId` |
| `GET /reports/{report}/export?format=json\|csv` | manager | ส่งออกรายงาน |
| `GET /logs` | admin | System logs กรองด้วย `level`, `eventType`, `from`, `to` |

รายการแบบแบ่งหน้ารับ `page` และ `pageSize` (สูงสุด 100) และตอบ `items`, `page`, `pageSize`, `totalItems`, `totalPages`

ใน `PUT` ถ้าไม่ส่ง `role`, `isActive`, `status`, `priority` หรือ `password` ระบบจะคงค่าเดิม

**ความคืบหน้า** คือ `งานที่เสร็จ / งานทั้งหมด × 100` ยึดแผนกของโครงการ และไม่นับโครงการที่ยกเลิก

### ตัวอย่าง Request

```bash
curl -X POST http://localhost:8080/api/v1/projects \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"departmentId":1,"code":"PRJ-100","name":"โครงการใหม่","status":"active",
       "startDate":"2026-11-01","endDate":"2027-01-31","budget":250000}'
```

### Error

Error ทุกกรณีเป็น `application/problem+json` ตาม RFC 9457:

```json
{
  "type": "about:blank",
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more fields are invalid.",
  "code": "validation_error",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "errors": { "email": ["'Email' is not a valid email address."] }
}
```

| HTTP | `code` | เกิดเมื่อ |
|---|---|---|
| 400 | `validation_error`, `malformed_request` | ข้อมูลไม่ถูกต้อง หรือ JSON อ่านไม่ได้ |
| 401 | `unauthorized`, `invalid_credentials` | Token ไม่ถูกต้อง หรือ login ไม่สำเร็จ |
| 403 | `forbidden` | สิทธิ์ไม่พอ |
| 404 | `not_found` | ไม่พบข้อมูล |
| 405 | `method_not_allowed` | HTTP method ไม่รองรับ |
| 409 | `conflict` | ค่าซ้ำ หรือข้อมูลยังถูกอ้างอิงอยู่ |
| 415 | `unsupported_media_type` | Body ไม่ใช่ JSON |
| 429 | `rate_limited` | Login ถี่เกินกำหนด |
| 500 | `internal_error` | ข้อผิดพลาดที่ไม่ได้คาดไว้ |
| 503 | `database_unavailable` | Database เชื่อมต่อไม่ได้ |

### Postman

Import ไฟล์ทั้งสองในโฟลเดอร์ `postman/` ใส่ `adminPassword` ที่ได้จากคำสั่งในหัวข้อการติดตั้งลงใน environment แล้วรันทั้ง collection ตามลำดับ collection นี้ลบข้อมูลที่สร้างเองหลังรันเสร็จ ส่วนการตรวจค่ารายงานคาดว่าข้อมูลตัวอย่างยังไม่ถูกแก้ไข

## สคริปต์ดูแลระบบ

| สคริปต์ | หน้าที่ | Exit code |
|---|---|---|
| `scripts/backup.sh` | `pg_dump` + `gzip` ลง volume `db_backups` ตรวจไฟล์ และลบไฟล์เกินกำหนด | `0` สำเร็จ, `1` ล้มเหลวหรือ `db` ไม่ทำงาน, `2` เรียก Docker ไม่ได้หรือค่าผิด |
| `scripts/healthcheck.sh` | ตรวจสถานะ container และเรียก `/health` | `0` ปกติ, `1` มีบริการไม่พร้อม, `2` เรียก Docker ไม่ได้ |
| `scripts/resource_usage.sh` | แสดง CPU, Memory, Network และ Disk จาก `docker stats` | `0` สำเร็จ, `1` มีบริการไม่ทำงาน, `2` เรียก Docker ไม่ได้ |
| `scripts/rotate_logs.sh` | หมุน log และลบ `system_logs` ที่เกินอายุ โดย `--simulate` ใช้จำลองการทำงาน | `0` สำเร็จ, `1` ล้มเหลวหรือ `db` ไม่ทำงาน, `2` ไม่มี logrotate หรือ Docker, หรือค่าผิด |

### กู้คืนข้อมูล

```bash
docker compose exec db ls -l /backups
docker compose exec db sh -c 'createdb -U "$POSTGRES_USER" restored'
docker compose exec -T db sh -c 'gunzip -c /backups/<ไฟล์> | psql -U "$POSTGRES_USER" -d restored'
```

คำสั่งนี้กู้คืนเข้าฐานข้อมูลใหม่ชื่อ `restored` เพื่อตรวจก่อนใช้งาน ส่วนการคัดลอกไฟล์ออกจาก volume ใช้ `docker compose cp db:/backups/<ไฟล์> .`

### Log rotation

ป้องกัน log โตจน Disk เต็ม 3 ส่วน:

- **Log ของ container:** จำกัดไฟล์ละ 10 MB สูงสุด 3 ไฟล์ ตั้งค่าใน `docker-compose.yml`
- **Log ของสคริปต์ใน `logs/`:** หมุนทุกวัน หรือทันทีที่เกิน 10 MB เมื่อรัน `rotate_logs.sh` เก็บ 7 รอบและบีบอัด ตั้งค่าใน `config/logrotate.conf`
- **ตาราง `system_logs`:** ลบแถวที่เก่ากว่า `LOG_RETENTION_DAYS` เมื่อรัน `rotate_logs.sh`

ดูการทำงานจริงได้ด้วยการจำลอง ซึ่งสร้าง log ปริมาณมากแล้วแสดงขนาดก่อนและหลัง โดยไม่แตะ log จริงและข้อมูลในระบบ:

```bash
./scripts/rotate_logs.sh --simulate
```

### ตั้งเวลาด้วย cron

```cron
0 2 * * * cd /path/to/project && mkdir -p logs && ./scripts/backup.sh >> logs/backup.log 2>&1
0 * * * * cd /path/to/project && mkdir -p logs && ./scripts/rotate_logs.sh >> logs/rotate.log 2>&1
```

## การทดสอบ

ต้องมี .NET SDK 10 และเปิด Docker ไว้ เพราะ integration test จะเปิด PostgreSQL ชั่วคราวด้วย Testcontainers

```bash
dotnet test
```

## เอกสารออกแบบ

- `docs/BA.docx`: ขอบเขต, requirements, user stories, use cases และเกณฑ์ตรวจรับ
- `docs/SA.docx`: สถาปัตยกรรม, ER diagram, data dictionary, API specification, diagram การทำงาน, deployment และแผนทดสอบ
