CREATE TABLE "document_template" (
  "template_id" uuid PRIMARY KEY NOT NULL,
  "school_id" uuid NOT NULL,
  "kind" varchar NOT NULL,
  "name" varchar NOT NULL,
  "active_version_id" uuid,
  "revision" bigint NOT NULL,
  CHECK (kind IN ('TrainingContract','PracticalJournal','CompletionReport')),
  CHECK (revision > 0),
  CHECK (length(trim(name)) > 0)
);

CREATE TABLE "template_version" (
  "template_version_id" uuid PRIMARY KEY NOT NULL,
  "school_id" uuid NOT NULL,
  "template_id" uuid NOT NULL,
  "version_no" integer NOT NULL,
  "status" varchar NOT NULL,
  "file_id" uuid NOT NULL,
  "created_by" uuid NOT NULL,
  "created_at" timestamptz NOT NULL,
  CHECK (status IN ('Draft','Active','Archived')),
  CHECK (version_no > 0)
);

CREATE TABLE "file_object" (
  "file_id" uuid PRIMARY KEY NOT NULL,
  "school_id" uuid NOT NULL,
  "storage_key" varchar NOT NULL,
  "purpose" varchar NOT NULL,
  "sha256" varchar(64) NOT NULL,
  "byte_length" bigint NOT NULL,
  "media_type" varchar NOT NULL,
  "scan_status" varchar NOT NULL,
  "uploaded_by" uuid NOT NULL,
  "uploaded_at" timestamptz NOT NULL,
  CHECK (purpose IN ('Template','Rendered','SignedCopy','SourceBasis')),
  CHECK (scan_status IN ('Pending','Clean','Rejected')),
  CHECK (byte_length > 0),
  CHECK (sha256 ~ '^[0-9a-f]{64}$'),
  CHECK (length(trim(storage_key)) > 0 AND length(trim(media_type)) > 0)
);

CREATE TABLE "training_contract" (
  "contract_id" uuid PRIMARY KEY NOT NULL,
  "school_id" uuid NOT NULL,
  "enrollment_id" uuid NOT NULL,
  "student_id" uuid NOT NULL,
  "template_version_id" uuid NOT NULL,
  "generated_file_id" uuid NOT NULL,
  "basis_file_id" uuid NOT NULL,
  "basis_schema_version" varchar NOT NULL,
  "status" varchar NOT NULL,
  "replaces_contract_id" uuid,
  "generated_by" uuid NOT NULL,
  "generated_at" timestamptz NOT NULL,
  "revision" bigint NOT NULL,
  CHECK (status IN ('Generated','Signed','Cancelled')),
  CHECK (revision > 0),
  CHECK (replaces_contract_id IS NULL OR replaces_contract_id <> contract_id),
  CHECK (length(trim(basis_schema_version)) > 0)
);

CREATE TABLE "contract_signed_copy" (
  "signed_copy_id" uuid PRIMARY KEY NOT NULL,
  "school_id" uuid NOT NULL,
  "contract_id" uuid NOT NULL,
  "copy_no" integer NOT NULL,
  "file_id" uuid NOT NULL,
  "is_current" boolean NOT NULL,
  "declared_signed_at" timestamptz NOT NULL,
  "recorded_at" timestamptz NOT NULL,
  "recorded_by" uuid NOT NULL,
  "basis" text NOT NULL,
  CHECK (copy_no > 0),
  CHECK (declared_signed_at <= recorded_at),
  CHECK (length(trim(basis)) > 0)
);

CREATE TABLE "training_document" (
  "document_id" uuid PRIMARY KEY NOT NULL,
  "school_id" uuid NOT NULL,
  "enrollment_id" uuid NOT NULL,
  "kind" varchar NOT NULL,
  "revision" bigint NOT NULL,
  "current_version_id" uuid,
  CHECK (kind IN ('PracticalJournal','CompletionReport')),
  CHECK (revision > 0)
);

CREATE TABLE "document_version" (
  "document_version_id" uuid PRIMARY KEY NOT NULL,
  "school_id" uuid NOT NULL,
  "document_id" uuid NOT NULL,
  "enrollment_id" uuid NOT NULL,
  "version_no" integer NOT NULL,
  "template_version_id" uuid NOT NULL,
  "evidence_snapshot_id" uuid NOT NULL,
  "dependency_revision" bigint NOT NULL,
  "evidence_status" varchar NOT NULL,
  "rendered_file_id" uuid NOT NULL,
  "basis_file_id" uuid NOT NULL,
  "basis_schema_version" varchar NOT NULL,
  "renderer_version" varchar NOT NULL,
  "state" varchar NOT NULL,
  "revision" bigint NOT NULL,
  "generated_by" uuid NOT NULL,
  "generated_at" timestamptz NOT NULL,
  CHECK (version_no > 0),
  CHECK (dependency_revision >= 0),
  CHECK (revision > 0),
  CHECK (evidence_status IN ('Ready','Incomplete')),
  CHECK (state IN ('Prepared','Indeterminate','PublishedReview','PublishedReady','Stale','Withdrawn')),
  CHECK (state <> 'PublishedReady' OR evidence_status = 'Ready'),
  CHECK (length(trim(basis_schema_version)) > 0 AND length(trim(renderer_version)) > 0)
);

CREATE TABLE "state_event" (
  "event_id" uuid PRIMARY KEY NOT NULL,
  "school_id" uuid NOT NULL,
  "template_id" uuid,
  "template_version_id" uuid,
  "signed_copy_id" uuid,
  "action" varchar NOT NULL,
  "contract_id" uuid,
  "document_id" uuid,
  "document_version_id" uuid,
  "sequence_no" bigint NOT NULL,
  "from_state" varchar,
  "to_state" varchar NOT NULL,
  "actor_id" uuid NOT NULL,
  "occurred_at" timestamptz NOT NULL,
  "reason" text NOT NULL,
  "command_id" uuid NOT NULL,
  CHECK (num_nonnulls(template_id, contract_id, document_id, document_version_id) = 1),
  CHECK (template_version_id IS NULL OR template_id IS NOT NULL),
  CHECK (signed_copy_id IS NULL OR contract_id IS NOT NULL),
  CHECK (length(trim(action)) > 0),
  CHECK (sequence_no > 0),
  CHECK (length(trim(reason)) > 0)
);

CREATE TABLE "operation_receipt" (
  "command_id" uuid PRIMARY KEY NOT NULL,
  "school_id" uuid NOT NULL,
  "actor_id" uuid NOT NULL,
  "template_id" uuid,
  "template_version_id" uuid,
  "operation" varchar NOT NULL,
  "request_digest" varchar(64) NOT NULL,
  "status" varchar NOT NULL,
  "contract_id" uuid,
  "document_id" uuid,
  "document_version_id" uuid,
  "outcome_code" varchar,
  "created_at" timestamptz NOT NULL,
  "completed_at" timestamptz,
  CHECK (status IN ('Pending','Succeeded','Rejected','Indeterminate')),
  CHECK (request_digest ~ '^[0-9a-f]{64}$'),
  CHECK ((status IN ('Pending','Indeterminate') AND completed_at IS NULL) OR (status IN ('Succeeded','Rejected') AND completed_at IS NOT NULL)),
  CHECK (completed_at IS NULL OR completed_at >= created_at),
  CHECK (num_nonnulls(template_id, contract_id, document_id, document_version_id) <= 1),
  CHECK (template_version_id IS NULL OR template_id IS NOT NULL),
  CHECK (status NOT IN ('Succeeded','Rejected') OR outcome_code IS NOT NULL),
  CHECK (length(trim(operation)) > 0 AND (outcome_code IS NULL OR length(trim(outcome_code)) > 0))
);

CREATE TABLE "evidence_checkpoint" (
  "school_id" uuid NOT NULL,
  "enrollment_id" uuid NOT NULL,
  "dependency_revision" bigint NOT NULL,
  "last_change_id" uuid NOT NULL,
  "observed_at" timestamptz NOT NULL,
  CHECK (dependency_revision >= 0),
  PRIMARY KEY ("school_id", "enrollment_id")
);

CREATE UNIQUE INDEX ON "document_template" ("school_id", "template_id");

CREATE UNIQUE INDEX ON "template_version" ("school_id", "template_version_id");

CREATE UNIQUE INDEX ON "template_version" ("school_id", "template_id", "version_no");

CREATE UNIQUE INDEX ON "template_version" ("school_id", "template_id", "template_version_id");

CREATE UNIQUE INDEX ON "file_object" ("school_id", "file_id");

CREATE UNIQUE INDEX ON "file_object" ("school_id", "storage_key");

CREATE UNIQUE INDEX ON "training_contract" ("school_id", "contract_id");

CREATE UNIQUE INDEX ON "training_contract" ("school_id", "enrollment_id", "contract_id");

CREATE UNIQUE INDEX ON "contract_signed_copy" ("school_id", "signed_copy_id");

CREATE UNIQUE INDEX ON "contract_signed_copy" ("school_id", "contract_id", "signed_copy_id");

CREATE UNIQUE INDEX ON "contract_signed_copy" ("school_id", "contract_id", "copy_no");

CREATE UNIQUE INDEX ON "training_document" ("school_id", "document_id");

CREATE UNIQUE INDEX ON "training_document" ("school_id", "enrollment_id", "kind");

CREATE UNIQUE INDEX ON "training_document" ("school_id", "document_id", "enrollment_id");

CREATE UNIQUE INDEX ON "document_version" ("school_id", "document_version_id");

CREATE UNIQUE INDEX ON "document_version" ("school_id", "document_id", "document_version_id");

CREATE UNIQUE INDEX ON "document_version" ("school_id", "document_id", "version_no");

CREATE UNIQUE INDEX ON "state_event" ("school_id", "event_id");

CREATE UNIQUE INDEX ON "state_event" ("school_id", "template_id", "sequence_no");

CREATE UNIQUE INDEX ON "state_event" ("school_id", "contract_id", "sequence_no");

CREATE UNIQUE INDEX ON "state_event" ("school_id", "document_id", "sequence_no");

CREATE UNIQUE INDEX ON "state_event" ("school_id", "document_version_id", "sequence_no");

CREATE UNIQUE INDEX ON "operation_receipt" ("school_id", "command_id");









ALTER TABLE "template_version" ADD FOREIGN KEY ("school_id", "template_id") REFERENCES "document_template" ("school_id", "template_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "document_template" ADD FOREIGN KEY ("school_id", "template_id", "active_version_id") REFERENCES "template_version" ("school_id", "template_id", "template_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "template_version" ADD FOREIGN KEY ("school_id", "file_id") REFERENCES "file_object" ("school_id", "file_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "training_contract" ADD FOREIGN KEY ("school_id", "generated_file_id") REFERENCES "file_object" ("school_id", "file_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "training_contract" ADD FOREIGN KEY ("school_id", "basis_file_id") REFERENCES "file_object" ("school_id", "file_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "training_contract" ADD FOREIGN KEY ("school_id", "template_version_id") REFERENCES "template_version" ("school_id", "template_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "training_contract" ADD FOREIGN KEY ("school_id", "enrollment_id", "replaces_contract_id") REFERENCES "training_contract" ("school_id", "enrollment_id", "contract_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "contract_signed_copy" ADD FOREIGN KEY ("school_id", "contract_id") REFERENCES "training_contract" ("school_id", "contract_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "contract_signed_copy" ADD FOREIGN KEY ("school_id", "file_id") REFERENCES "file_object" ("school_id", "file_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "document_version" ADD FOREIGN KEY ("school_id", "document_id", "enrollment_id") REFERENCES "training_document" ("school_id", "document_id", "enrollment_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "document_version" ADD FOREIGN KEY ("school_id", "template_version_id") REFERENCES "template_version" ("school_id", "template_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "document_version" ADD FOREIGN KEY ("school_id", "rendered_file_id") REFERENCES "file_object" ("school_id", "file_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "document_version" ADD FOREIGN KEY ("school_id", "basis_file_id") REFERENCES "file_object" ("school_id", "file_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "state_event" ADD FOREIGN KEY ("school_id", "contract_id") REFERENCES "training_contract" ("school_id", "contract_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "state_event" ADD FOREIGN KEY ("school_id", "document_version_id") REFERENCES "document_version" ("school_id", "document_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "operation_receipt" ADD FOREIGN KEY ("school_id", "contract_id") REFERENCES "training_contract" ("school_id", "contract_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "operation_receipt" ADD FOREIGN KEY ("school_id", "document_version_id") REFERENCES "document_version" ("school_id", "document_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "training_document" ADD FOREIGN KEY ("school_id", "document_id", "current_version_id") REFERENCES "document_version" ("school_id", "document_id", "document_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "state_event" ADD FOREIGN KEY ("school_id", "template_id") REFERENCES "document_template" ("school_id", "template_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "state_event" ADD FOREIGN KEY ("school_id", "template_id", "template_version_id") REFERENCES "template_version" ("school_id", "template_id", "template_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "state_event" ADD FOREIGN KEY ("school_id", "contract_id", "signed_copy_id") REFERENCES "contract_signed_copy" ("school_id", "contract_id", "signed_copy_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "state_event" ADD FOREIGN KEY ("school_id", "command_id") REFERENCES "operation_receipt" ("school_id", "command_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "operation_receipt" ADD FOREIGN KEY ("school_id", "template_id") REFERENCES "document_template" ("school_id", "template_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "operation_receipt" ADD FOREIGN KEY ("school_id", "template_id", "template_version_id") REFERENCES "template_version" ("school_id", "template_id", "template_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "state_event" ADD FOREIGN KEY ("school_id", "document_id") REFERENCES "training_document" ("school_id", "document_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "operation_receipt" ADD FOREIGN KEY ("school_id", "document_id") REFERENCES "training_document" ("school_id", "document_id") DEFERRABLE INITIALLY IMMEDIATE;
