-- Demo data with known report results. Seed users have no password, so they cannot log in;
-- the API creates the first admin from .env.
--
-- Expected department-progress report:
--   HR: 4 tasks, 3 done, 0 overdue                    -> 75.00%
--   IT: 10 tasks, 4 done, 3 in progress, 1 overdue    -> 40.00%
--   FN: cancelled project excluded, 1 empty project   ->  0%
--   MK: no projects                                   ->  0%
BEGIN;

INSERT INTO departments (code, name) VALUES
  ('IT', 'เทคโนโลยีสารสนเทศ'),
  ('HR', 'ทรัพยากรบุคคล'),
  ('FN', 'การเงิน'),
  ('MK', 'การตลาด');

INSERT INTO users (department_id, full_name, email, role)
SELECT d.id, v.full_name, v.email, v.role
FROM (VALUES
  ('IT', 'สมชาย ใจดี',    'somchai@example.com', 'manager'),
  ('IT', 'วิภา รักงาน',    'wipa@example.com',    'member'),
  ('IT', 'ธนา มั่นคง',     'thana@example.com',   'member'),
  ('HR', 'นภา สดใส',      'napa@example.com',    'manager'),
  ('HR', 'กมล ตั้งใจ',     'kamon@example.com',   'member'),
  ('FN', 'ปรีชา รอบคอบ',  'preecha@example.com', 'manager')
) AS v (dept, full_name, email, role)
JOIN departments d ON d.code = v.dept;

INSERT INTO projects (department_id, owner_user_id, code, name, description, status, start_date, end_date, budget)
SELECT d.id, u.id, v.code, v.name, v.description, v.status, v.start_date::date, v.end_date::date, v.budget
FROM (VALUES
  ('IT', 'somchai@example.com', 'PRJ-IT-01', 'ย้ายระบบขึ้น Container', 'ปรับระบบให้รันบน Docker', 'active', '2026-09-01', '2027-01-31', 250000.00),
  ('IT', 'somchai@example.com', 'PRJ-IT-02', 'ระบบรายงานผู้บริหาร', 'รายงานความคืบหน้ารายแผนก', 'active', '2026-10-01', '2027-03-31', 180000.00),
  ('HR', 'napa@example.com', 'PRJ-HR-01', 'ปรับปรุงการรับพนักงาน', 'ลดขั้นตอนการรับพนักงานใหม่', 'active', '2026-08-01', '2026-12-31', 90000.00),
  ('FN', 'preecha@example.com', 'PRJ-FN-01', 'เปลี่ยนระบบบัญชี', 'ยกเลิกเนื่องจากปรับแผนงบประมาณ', 'cancelled', '2026-06-01', '2026-12-31', 500000.00),
  ('FN', 'preecha@example.com', 'PRJ-FN-02', 'วางแผนงบประมาณปีถัดไป', 'อยู่ระหว่างเตรียมการ', 'planned', '2027-01-01', '2027-06-30', 60000.00)
) AS v (dept, owner, code, name, description, status, start_date, end_date, budget)
JOIN departments d ON d.code = v.dept
JOIN users u ON u.email = v.owner;

-- Due dates are relative to the load date so that only the intended task is overdue.
INSERT INTO tasks (project_id, assignee_user_id, title, status, priority, due_date, completed_at)
SELECT p.id, u.id, v.title, v.status, v.priority,
       CURRENT_DATE + v.due_in_days,
       CASE WHEN v.status = 'done' THEN now() END
FROM (VALUES
  ('PRJ-IT-01', 'wipa@example.com',  'วิเคราะห์ระบบเดิม',       'done',        'high',   -30),
  ('PRJ-IT-01', 'wipa@example.com',  'เขียน Dockerfile',       'done',        'high',   -20),
  ('PRJ-IT-01', 'thana@example.com', 'เขียน Docker Compose',   'done',        'medium', -10),
  ('PRJ-IT-01', 'thana@example.com', 'ทดสอบบนเครื่องสะอาด',    'in_progress', 'high',   -3),
  ('PRJ-IT-01', 'wipa@example.com',  'ตั้งค่า Log Rotation',    'in_progress', 'medium', 14),
  ('PRJ-IT-01', 'thana@example.com', 'จัดทำคู่มือการติดตั้ง',    'todo',        'low',    30),
  ('PRJ-IT-02', 'wipa@example.com',  'รวบรวมความต้องการ',      'done',        'high',   -15),
  ('PRJ-IT-02', 'thana@example.com', 'ออกแบบรายงาน',           'in_progress', 'high',   10),
  ('PRJ-IT-02', 'wipa@example.com',  'เขียน SQL รายงาน',        'todo',        'medium', 20),
  ('PRJ-IT-02', NULL,                'ส่งออกไฟล์ CSV',          'todo',        'medium', 40),
  ('PRJ-HR-01', 'kamon@example.com', 'ทบทวนขั้นตอนเดิม',        'done',        'medium', -40),
  ('PRJ-HR-01', 'kamon@example.com', 'ออกแบบแบบฟอร์มใหม่',      'done',        'medium', -25),
  ('PRJ-HR-01', 'kamon@example.com', 'อบรมผู้สัมภาษณ์',          'done',        'low',    -5),
  ('PRJ-HR-01', 'napa@example.com',  'ประเมินผลหลังใช้งาน',      'todo',        'low',    25),
  ('PRJ-FN-01', 'preecha@example.com', 'เปรียบเทียบผู้ให้บริการ', 'done',        'high',   -60),
  ('PRJ-FN-01', 'preecha@example.com', 'ย้ายข้อมูลบัญชี',        'todo',        'high',   -20)
) AS v (project, assignee, title, status, priority, due_in_days)
JOIN projects p ON p.code = v.project
LEFT JOIN users u ON u.email = v.assignee;

COMMIT;
