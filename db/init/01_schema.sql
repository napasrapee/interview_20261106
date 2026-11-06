-- Schema for the Project Monitoring system. Runs once, on an empty data volume.
-- Column rationale: see the Data Dictionary in the SA document.
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
  code       VARCHAR(20)  NOT NULL UNIQUE,
  name       VARCHAR(150) NOT NULL UNIQUE,
  created_at TIMESTAMPTZ  NOT NULL DEFAULT now(),
  updated_at TIMESTAMPTZ  NOT NULL DEFAULT now()
);

CREATE TABLE users (
  id            BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  department_id BIGINT       NOT NULL REFERENCES departments (id) ON DELETE RESTRICT,
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
  department_id BIGINT       NOT NULL REFERENCES departments (id) ON DELETE RESTRICT,
  owner_user_id BIGINT       REFERENCES users (id) ON DELETE RESTRICT,
  code          VARCHAR(30)  NOT NULL UNIQUE,
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
  project_id       BIGINT       NOT NULL REFERENCES projects (id) ON DELETE CASCADE,
  assignee_user_id BIGINT       REFERENCES users (id) ON DELETE RESTRICT,
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
  user_id     BIGINT      REFERENCES users (id) ON DELETE SET NULL,
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

COMMIT;
