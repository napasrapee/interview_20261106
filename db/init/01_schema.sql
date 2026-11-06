-- Schema for the Project Monitoring system. Runs once, on an empty data volume.
-- Descriptions live in the database through COMMENT ON, so \d+ in psql shows them.
BEGIN;

CREATE FUNCTION set_updated_at() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  NEW.updated_at := now();
  RETURN NEW;
END;
$$;

CREATE TABLE departments (
  id         BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  code       VARCHAR(20)  NOT NULL CONSTRAINT uq_departments_code UNIQUE,
  name       VARCHAR(150) NOT NULL CONSTRAINT uq_departments_name UNIQUE,
  created_at TIMESTAMPTZ  NOT NULL DEFAULT now(),
  updated_at TIMESTAMPTZ  NOT NULL DEFAULT now()
);

CREATE TABLE users (
  id            BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  department_id BIGINT       NOT NULL
                CONSTRAINT fk_users_department REFERENCES departments (id) ON DELETE RESTRICT,
  full_name     VARCHAR(150) NOT NULL,
  email         VARCHAR(255) NOT NULL,
  password_hash VARCHAR(255),
  role          VARCHAR(20)  NOT NULL DEFAULT 'member'
                CONSTRAINT ck_users_role CHECK (role IN ('admin', 'manager', 'member')),
  is_active     BOOLEAN      NOT NULL DEFAULT true,
  created_at    TIMESTAMPTZ  NOT NULL DEFAULT now(),
  updated_at    TIMESTAMPTZ  NOT NULL DEFAULT now()
);

CREATE TABLE projects (
  id            BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  department_id BIGINT       NOT NULL
                CONSTRAINT fk_projects_department REFERENCES departments (id) ON DELETE RESTRICT,
  owner_user_id BIGINT
                CONSTRAINT fk_projects_owner REFERENCES users (id) ON DELETE RESTRICT,
  code          VARCHAR(30)  NOT NULL CONSTRAINT uq_projects_code UNIQUE,
  name          VARCHAR(200) NOT NULL,
  description   TEXT,
  status        VARCHAR(20)  NOT NULL DEFAULT 'planned'
                CONSTRAINT ck_projects_status
                CHECK (status IN ('planned', 'active', 'on_hold', 'completed', 'cancelled')),
  start_date    DATE,
  end_date      DATE,
  budget        NUMERIC(14, 2) CONSTRAINT ck_projects_budget CHECK (budget >= 0),
  created_at    TIMESTAMPTZ  NOT NULL DEFAULT now(),
  updated_at    TIMESTAMPTZ  NOT NULL DEFAULT now(),
  CONSTRAINT ck_projects_dates CHECK (end_date >= start_date)
);

CREATE TABLE tasks (
  id               BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  project_id       BIGINT       NOT NULL
                   CONSTRAINT fk_tasks_project REFERENCES projects (id) ON DELETE CASCADE,
  assignee_user_id BIGINT
                   CONSTRAINT fk_tasks_assignee REFERENCES users (id) ON DELETE RESTRICT,
  title            VARCHAR(200) NOT NULL,
  description      TEXT,
  status           VARCHAR(20)  NOT NULL DEFAULT 'todo'
                   CONSTRAINT ck_tasks_status CHECK (status IN ('todo', 'in_progress', 'done')),
  priority         VARCHAR(10)  NOT NULL DEFAULT 'medium'
                   CONSTRAINT ck_tasks_priority CHECK (priority IN ('low', 'medium', 'high')),
  due_date         DATE,
  completed_at     TIMESTAMPTZ,
  created_at       TIMESTAMPTZ  NOT NULL DEFAULT now(),
  updated_at       TIMESTAMPTZ  NOT NULL DEFAULT now(),
  CONSTRAINT ck_tasks_completed_at CHECK ((status = 'done') = (completed_at IS NOT NULL))
);

CREATE TABLE system_logs (
  id          BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  level       VARCHAR(10) NOT NULL
              CONSTRAINT ck_system_logs_level CHECK (level IN ('info', 'warning', 'error')),
  event_type  VARCHAR(50) NOT NULL,
  message     TEXT        NOT NULL,
  http_method VARCHAR(10),
  path        VARCHAR(500),
  status_code INTEGER,
  duration_ms INTEGER     CONSTRAINT ck_system_logs_duration CHECK (duration_ms >= 0),
  trace_id    VARCHAR(64),
  user_id     BIGINT
              CONSTRAINT fk_system_logs_user REFERENCES users (id) ON DELETE SET NULL,
  details     JSONB,
  created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_users_email_lower ON users (lower(email));
CREATE INDEX ix_users_department_id ON users (department_id);
CREATE INDEX ix_projects_department_status ON projects (department_id, status);
CREATE INDEX ix_projects_owner_user_id ON projects (owner_user_id);
CREATE INDEX ix_tasks_project_status ON tasks (project_id, status);
CREATE INDEX ix_tasks_assignee_status ON tasks (assignee_user_id, status);
CREATE INDEX ix_tasks_due_date_open ON tasks (due_date) WHERE status <> 'done';
CREATE INDEX ix_system_logs_created_at ON system_logs (created_at DESC);
CREATE INDEX ix_system_logs_level_created_at ON system_logs (level, created_at DESC);
CREATE INDEX ix_system_logs_user_id ON system_logs (user_id);

CREATE TRIGGER trg_departments_updated_at BEFORE UPDATE ON departments
  FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_users_updated_at BEFORE UPDATE ON users
  FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_projects_updated_at BEFORE UPDATE ON projects
  FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_tasks_updated_at BEFORE UPDATE ON tasks
  FOR EACH ROW EXECUTE FUNCTION set_updated_at();

COMMENT ON FUNCTION set_updated_at() IS 'Trigger function that keeps updated_at current, independent of the API.';

COMMENT ON TABLE departments IS 'Organizational units. Reports group progress by department.';
COMMENT ON COLUMN departments.code IS 'Short unique code shown in reports, such as IT.';

COMMENT ON TABLE users IS 'User accounts, which also act as project owners and task assignees.';
COMMENT ON COLUMN users.email IS 'Login name. Unique regardless of letter case through ux_users_email_lower.';
COMMENT ON COLUMN users.password_hash IS 'BCrypt hash. NULL means the account cannot log in, as with seed data.';
COMMENT ON COLUMN users.role IS 'Access level: admin, manager or member.';
COMMENT ON COLUMN users.is_active IS 'False blocks login while keeping the history of the account.';

COMMENT ON TABLE projects IS 'Projects. department_id decides the department a project counts toward in reports.';
COMMENT ON COLUMN projects.owner_user_id IS 'Responsible user. May be empty during planning.';
COMMENT ON COLUMN projects.status IS 'planned, active, on_hold, completed or cancelled. Reports exclude cancelled projects.';
COMMENT ON COLUMN projects.budget IS 'NUMERIC keeps money exact, unlike floating point types.';

COMMENT ON TABLE tasks IS 'Work items of a project. Deleted together with their project.';
COMMENT ON COLUMN tasks.assignee_user_id IS 'User responsible for the task. May be unassigned.';
COMMENT ON COLUMN tasks.status IS 'todo, in_progress or done. Progress is done tasks divided by all tasks.';
COMMENT ON COLUMN tasks.due_date IS 'A task is overdue when it is not done and this date is before today in UTC.';
COMMENT ON COLUMN tasks.completed_at IS 'Set only while status is done, enforced by ck_tasks_completed_at.';
COMMENT ON INDEX ix_tasks_due_date_open IS 'Partial index for overdue checks, small because finished tasks are left out.';

COMMENT ON TABLE system_logs IS 'API events. Never holds request bodies, passwords or tokens. scripts/rotate_logs.sh deletes old rows.';
COMMENT ON COLUMN system_logs.event_type IS 'http_request, request_rejected, login_failed or exception.';
COMMENT ON COLUMN system_logs.path IS 'Request path without the query string, to avoid storing sensitive values.';
COMMENT ON COLUMN system_logs.trace_id IS 'Matches traceId in the error response of the same request.';
COMMENT ON COLUMN system_logs.user_id IS 'Set to NULL when the user is deleted, so the event is kept.';
COMMENT ON COLUMN system_logs.details IS 'Extra data that varies by event, such as the exception type.';

COMMIT;
