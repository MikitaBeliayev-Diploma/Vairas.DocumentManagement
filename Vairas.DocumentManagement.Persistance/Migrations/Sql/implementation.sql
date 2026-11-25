-- Additions to the frozen ten-table model. Business outcomes remain in canonical receipts/events.
CREATE TABLE document_job (
 id uuid PRIMARY KEY, school_id uuid NOT NULL, enrollment_id uuid NOT NULL,
 actor_id uuid NOT NULL, identity_id uuid NOT NULL, session_id uuid NOT NULL,
 template_version_id uuid NOT NULL, snapshot_id uuid, replaces_contract_id uuid,
 source_snapshot_version text NOT NULL, kind text NOT NULL CHECK(kind IN ('TrainingContract','PracticalJournal','CompletionReport')),
 status text NOT NULL CHECK(status IN ('Pending','Generating','Completed','Failed')),
 document_id uuid, error_code text, attempts integer NOT NULL CHECK(attempts>=0),
 next_attempt_at timestamptz NOT NULL, lease_until timestamptz, lease_id uuid, created_at timestamptz NOT NULL,
 UNIQUE(school_id,id), FOREIGN KEY(school_id,id) REFERENCES operation_receipt(school_id,command_id),
 FOREIGN KEY(school_id,template_version_id) REFERENCES template_version(school_id,template_version_id)
);
CREATE INDEX dm_due_jobs ON document_job(next_attempt_at) WHERE status IN ('Pending','Generating');
CREATE TABLE operation_outcome (
 id uuid NOT NULL, school_id uuid NOT NULL, response_json text NOT NULL, PRIMARY KEY(school_id,id),
 FOREIGN KEY(school_id,id) REFERENCES operation_receipt(school_id,command_id)
);
CREATE TRIGGER dm_outcome_immutable BEFORE UPDATE OR DELETE ON operation_outcome FOR EACH ROW EXECUTE FUNCTION dm_freeze_columns('');
CREATE TABLE rendered_artifact (
 id uuid PRIMARY KEY, school_id uuid NOT NULL, contract_id uuid, document_version_id uuid, file_id uuid NOT NULL,
 format text NOT NULL CHECK(format IN ('pdf','docx')), CHECK(num_nonnulls(contract_id,document_version_id)=1),
 FOREIGN KEY(school_id,contract_id) REFERENCES training_contract(school_id,contract_id),
 FOREIGN KEY(school_id,document_version_id) REFERENCES document_version(school_id,document_version_id),
 FOREIGN KEY(school_id,file_id) REFERENCES file_object(school_id,file_id)
);
CREATE UNIQUE INDEX dm_contract_format ON rendered_artifact(school_id,contract_id,format) WHERE contract_id IS NOT NULL;
CREATE UNIQUE INDEX dm_version_format ON rendered_artifact(school_id,document_version_id,format) WHERE document_version_id IS NOT NULL;
CREATE TRIGGER dm_artifact_immutable BEFORE UPDATE OR DELETE ON rendered_artifact FOR EACH ROW EXECUTE FUNCTION dm_freeze_columns('');
CREATE TABLE publication_decision (
 id uuid PRIMARY KEY, school_id uuid NOT NULL, document_version_id uuid NOT NULL,
 actor_id uuid NOT NULL, identity_id uuid NOT NULL, session_id uuid NOT NULL,
 expected_revision bigint NOT NULL, expected_version_revision bigint NOT NULL, artifact_digest text NOT NULL,
 status text NOT NULL CHECK(status IN ('Pending','Accepted','Rejected','Stale')), reason text NOT NULL,
 created_at timestamptz NOT NULL, decided_at timestamptz,
 FOREIGN KEY(school_id,id) REFERENCES operation_receipt(school_id,command_id),
 FOREIGN KEY(school_id,document_version_id) REFERENCES document_version(school_id,document_version_id)
);
CREATE TABLE download_grant (
 id uuid PRIMARY KEY, school_id uuid NOT NULL, actor_id uuid NOT NULL, document_id uuid NOT NULL,
 file_id uuid NOT NULL, version_id uuid, historical boolean NOT NULL, expires_at timestamptz NOT NULL,
 FOREIGN KEY(school_id,file_id) REFERENCES file_object(school_id,file_id)
);
CREATE TRIGGER dm_grant_immutable BEFORE UPDATE OR DELETE ON download_grant FOR EACH ROW EXECUTE FUNCTION dm_freeze_columns('');
CREATE TABLE inbox_message (id uuid PRIMARY KEY, school_id uuid NOT NULL, fingerprint text NOT NULL, processed_at timestamptz NOT NULL);
CREATE TRIGGER dm_inbox_immutable BEFORE UPDATE OR DELETE ON inbox_message FOR EACH ROW EXECUTE FUNCTION dm_freeze_columns('');
CREATE TABLE document_review (id uuid PRIMARY KEY, school_id uuid NOT NULL, document_version_id uuid NOT NULL, actor_id uuid NOT NULL, reviewed_at timestamptz NOT NULL, reason text NOT NULL,
 FOREIGN KEY(school_id,document_version_id) REFERENCES document_version(school_id,document_version_id));
CREATE TRIGGER dm_review_immutable BEFORE UPDATE OR DELETE ON document_review FOR EACH ROW EXECUTE FUNCTION dm_freeze_columns('');

-- Only metadata crosses the delivery-role boundary. Scope is re-established by the business worker.
CREATE FUNCTION dm_pending_jobs() RETURNS TABLE(school_id uuid,id uuid) LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT school_id,id FROM public.document_job WHERE status IN ('Pending','Generating') AND next_attempt_at<=now() AND (lease_until IS NULL OR lease_until<=now()) ORDER BY next_attempt_at,id LIMIT 100
$$;
CREATE FUNCTION dm_pending_publications() RETURNS TABLE(school_id uuid,id uuid) LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT school_id,id FROM public.publication_decision WHERE status='Pending' ORDER BY created_at,id LIMIT 100
$$;
CREATE FUNCTION dm_enrollment_catalogue() RETURNS TABLE(school_id uuid,enrollment_id uuid) LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT DISTINCT school_id,enrollment_id FROM public.training_document ORDER BY school_id,enrollment_id
$$;
CREATE FUNCTION dm_pending_files() RETURNS TABLE(school_id uuid,id uuid) LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT school_id,file_id FROM public.file_object WHERE scan_status='Pending' ORDER BY uploaded_at,file_id LIMIT 100
$$;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA public FROM PUBLIC;
