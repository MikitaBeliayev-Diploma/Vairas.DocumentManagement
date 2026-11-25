CREATE UNIQUE INDEX dm_one_active_template
  ON template_version (school_id, template_id) WHERE status = 'Active';
CREATE UNIQUE INDEX dm_one_current_signed_copy
  ON contract_signed_copy (school_id, contract_id) WHERE is_current;
CREATE INDEX dm_evidence_versions
  ON document_version (school_id, enrollment_id, dependency_revision);
CREATE INDEX dm_template_file ON template_version (school_id, file_id);
CREATE INDEX dm_version_template ON document_version (school_id, template_version_id);
CREATE INDEX dm_event_command ON state_event (school_id, command_id);

 
 
CREATE FUNCTION dm_freeze_columns() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE allowed text[] := string_to_array(TG_ARGV[0], ',');
BEGIN
  IF TG_OP = 'DELETE' THEN
    RAISE EXCEPTION 'Historical row deletion requires a separately approved retention migration';
  END IF;
  IF (to_jsonb(OLD) - allowed) IS DISTINCT FROM (to_jsonb(NEW) - allowed) THEN
    RAISE EXCEPTION 'Immutable payload changed on %', TG_TABLE_NAME;
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER dm_template_payload BEFORE UPDATE OR DELETE ON document_template
  FOR EACH ROW EXECUTE FUNCTION dm_freeze_columns('name,active_version_id,revision');
CREATE TRIGGER dm_template_version_payload BEFORE UPDATE OR DELETE ON template_version
  FOR EACH ROW EXECUTE FUNCTION dm_freeze_columns('status');
CREATE TRIGGER dm_contract_payload BEFORE UPDATE OR DELETE ON training_contract
  FOR EACH ROW EXECUTE FUNCTION dm_freeze_columns('status,revision');
CREATE TRIGGER dm_signed_payload BEFORE UPDATE OR DELETE ON contract_signed_copy
  FOR EACH ROW EXECUTE FUNCTION dm_freeze_columns('is_current');
CREATE TRIGGER dm_document_payload BEFORE UPDATE OR DELETE ON training_document
  FOR EACH ROW EXECUTE FUNCTION dm_freeze_columns('current_version_id,revision');
CREATE TRIGGER dm_version_payload BEFORE UPDATE OR DELETE ON document_version
  FOR EACH ROW EXECUTE FUNCTION dm_freeze_columns('state,revision');
CREATE TRIGGER dm_event_immutable BEFORE UPDATE OR DELETE ON state_event
  FOR EACH ROW EXECUTE FUNCTION dm_freeze_columns('');

CREATE FUNCTION dm_revision_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'INSERT' THEN
    IF NEW.revision <> 1 THEN RAISE EXCEPTION 'Initial revision must be 1'; END IF;
  ELSIF NEW.revision <> OLD.revision + 1 THEN
    RAISE EXCEPTION 'Revision must advance by exactly one';
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER dm_template_revision BEFORE INSERT OR UPDATE ON document_template
  FOR EACH ROW EXECUTE FUNCTION dm_revision_guard();
CREATE TRIGGER dm_contract_revision BEFORE INSERT OR UPDATE ON training_contract
  FOR EACH ROW EXECUTE FUNCTION dm_revision_guard();
CREATE TRIGGER dm_document_revision BEFORE INSERT OR UPDATE ON training_document
  FOR EACH ROW EXECUTE FUNCTION dm_revision_guard();
CREATE TRIGGER dm_version_revision BEFORE INSERT OR UPDATE ON document_version
  FOR EACH ROW EXECUTE FUNCTION dm_revision_guard();

CREATE FUNCTION dm_file_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'File deletion requires approved retention workflow'; END IF;
  IF TG_OP = 'INSERT' THEN
    IF NEW.scan_status <> 'Pending' THEN RAISE EXCEPTION 'New files start Pending'; END IF;
  ELSE
    IF OLD.scan_status <> 'Pending' OR NEW.scan_status NOT IN ('Clean','Rejected')
       OR (to_jsonb(OLD)-'scan_status') IS DISTINCT FROM (to_jsonb(NEW)-'scan_status') THEN
      RAISE EXCEPTION 'Only immutable Pending -> Clean/Rejected is allowed';
    END IF;
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER dm_file_guard BEFORE INSERT OR UPDATE OR DELETE ON file_object
  FOR EACH ROW EXECUTE FUNCTION dm_file_guard();

CREATE FUNCTION dm_require_file(s uuid, f uuid, expected_purpose text)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM file_object WHERE school_id=s AND file_id=f
      AND scan_status='Clean' AND purpose=expected_purpose) THEN
    RAISE EXCEPTION 'Artifact requires Clean file of expected purpose in same school';
  END IF;
END $$;

 
CREATE FUNCTION dm_template_version_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  PERFORM 1 FROM document_template WHERE school_id=NEW.school_id
    AND template_id=NEW.template_id FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Template parent unavailable'; END IF;
  PERFORM dm_require_file(NEW.school_id, NEW.file_id, 'Template');
  IF TG_OP='INSERT' THEN
    IF NEW.status <> 'Draft' THEN RAISE EXCEPTION 'New template version starts Draft'; END IF;
  ELSIF NOT ((OLD.status='Draft' AND NEW.status IN ('Active','Archived'))
         OR (OLD.status='Active' AND NEW.status='Archived')) THEN
    RAISE EXCEPTION 'Invalid template version transition';
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER dm_template_version_guard BEFORE INSERT OR UPDATE ON template_version
  FOR EACH ROW EXECUTE FUNCTION dm_template_version_guard();

 
CREATE FUNCTION dm_check_template() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE s uuid := NEW.school_id; t uuid := NEW.template_id; ptr uuid; active_id uuid;
BEGIN
  SELECT active_version_id INTO ptr FROM document_template
    WHERE school_id=s AND template_id=t FOR UPDATE;
  SELECT template_version_id INTO active_id FROM template_version
    WHERE school_id=s AND template_id=t AND status='Active';
  IF ptr IS DISTINCT FROM active_id THEN
    RAISE EXCEPTION 'Active pointer must equal the sole Active template version';
  END IF;
  RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER dm_template_final AFTER INSERT OR UPDATE ON document_template
  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION dm_check_template();
CREATE CONSTRAINT TRIGGER dm_template_child_final AFTER INSERT OR UPDATE ON template_version
  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION dm_check_template();

 
 
CREATE FUNCTION dm_activate_template(s uuid, t uuid, v uuid, expected_revision bigint)
RETURNS bigint LANGUAGE plpgsql AS $$
DECLARE old_id uuid; r bigint;
BEGIN
  SELECT active_version_id,revision INTO old_id,r FROM document_template
    WHERE school_id=s AND template_id=t FOR UPDATE;
  IF NOT FOUND OR r<>expected_revision THEN RAISE EXCEPTION 'Template CAS conflict'; END IF;
  IF NOT EXISTS(SELECT 1 FROM template_version WHERE school_id=s AND template_id=t
      AND template_version_id=v AND status='Draft') THEN
    RAISE EXCEPTION 'Activation requires own Draft version';
  END IF;
  IF old_id IS NOT NULL THEN
    UPDATE template_version SET status='Archived'
      WHERE school_id=s AND template_id=t AND template_version_id=old_id;
  END IF;
  UPDATE template_version SET status='Active'
    WHERE school_id=s AND template_id=t AND template_version_id=v;
  UPDATE document_template SET active_version_id=v,revision=revision+1
    WHERE school_id=s AND template_id=t;
  RETURN r+1;
END $$;

CREATE FUNCTION dm_contract_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE k text;
BEGIN
  IF TG_OP='INSERT' THEN
     
     
     
    IF NEW.replaces_contract_id IS NOT NULL THEN
      PERFORM 1 FROM training_contract WHERE school_id=NEW.school_id
        AND enrollment_id=NEW.enrollment_id AND contract_id=NEW.replaces_contract_id
        FOR KEY SHARE;
      IF NOT FOUND THEN RAISE EXCEPTION 'Replacement requires an already existing same-enrollment predecessor'; END IF;
    END IF;
    PERFORM 1 FROM document_template t JOIN template_version v
      ON (t.school_id,t.template_id)=(v.school_id,v.template_id)
      WHERE v.school_id=NEW.school_id AND v.template_version_id=NEW.template_version_id
      FOR SHARE OF t;
    SELECT t.kind INTO k FROM template_version v JOIN document_template t
      ON (t.school_id,t.template_id)=(v.school_id,v.template_id)
      WHERE v.school_id=NEW.school_id AND v.template_version_id=NEW.template_version_id
        AND v.status='Active';
    IF k IS DISTINCT FROM 'TrainingContract' THEN
      RAISE EXCEPTION 'Contract requires Active TrainingContract template';
    END IF;
    IF NEW.status<>'Generated' THEN RAISE EXCEPTION 'New contract starts Generated'; END IF;
    PERFORM dm_require_file(NEW.school_id,NEW.generated_file_id,'Rendered');
    PERFORM dm_require_file(NEW.school_id,NEW.basis_file_id,'SourceBasis');
  ELSIF NOT ((OLD.status='Generated' AND NEW.status IN ('Signed','Cancelled'))
          OR (OLD.status='Signed' AND NEW.status IN ('Signed','Cancelled'))) THEN
    RAISE EXCEPTION 'Invalid contract transition';
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER dm_contract_guard BEFORE INSERT OR UPDATE ON training_contract
  FOR EACH ROW EXECUTE FUNCTION dm_contract_guard();

CREATE FUNCTION dm_signed_copy_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE st text;
BEGIN
  SELECT status INTO st FROM training_contract WHERE school_id=NEW.school_id
    AND contract_id=NEW.contract_id FOR UPDATE;
  IF st IS NULL OR st='Cancelled' THEN RAISE EXCEPTION 'Contract cannot accept signed-copy changes'; END IF;
  PERFORM dm_require_file(NEW.school_id,NEW.file_id,'SignedCopy');
  IF TG_OP='INSERT' THEN
    IF NOT NEW.is_current THEN RAISE EXCEPTION 'New signed copy must be selected'; END IF;
  ELSIF NOT (OLD.is_current AND NOT NEW.is_current) THEN
    RAISE EXCEPTION 'Signed-copy history cannot be reselected or edited';
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER dm_signed_copy_guard BEFORE INSERT OR UPDATE ON contract_signed_copy
  FOR EACH ROW EXECUTE FUNCTION dm_signed_copy_guard();

CREATE FUNCTION dm_check_contract() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE s uuid:=NEW.school_id; c uuid:=NEW.contract_id; st text; n integer;
BEGIN
  SELECT status INTO st FROM training_contract WHERE school_id=s AND contract_id=c FOR UPDATE;
  SELECT count(*) INTO n FROM contract_signed_copy WHERE school_id=s AND contract_id=c AND is_current;
  IF (st='Signed' AND n<>1) OR (st='Generated' AND n<>0) THEN
    RAISE EXCEPTION 'Signed requires exactly one current copy; Generated requires none';
  END IF;
   
  RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER dm_contract_final AFTER INSERT OR UPDATE ON training_contract
  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION dm_check_contract();
CREATE CONSTRAINT TRIGGER dm_signed_final AFTER INSERT OR UPDATE ON contract_signed_copy
  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION dm_check_contract();

CREATE FUNCTION dm_version_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE dk text; tk text; watermark bigint;
BEGIN
   
   
  SELECT dependency_revision INTO watermark FROM evidence_checkpoint
    WHERE school_id=NEW.school_id AND enrollment_id=NEW.enrollment_id FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Initialize authoritative evidence checkpoint first'; END IF;
  SELECT kind INTO dk FROM training_document WHERE school_id=NEW.school_id
    AND document_id=NEW.document_id AND enrollment_id=NEW.enrollment_id FOR UPDATE;
  IF dk IS NULL THEN RAISE EXCEPTION 'Document owner mismatch'; END IF;
  IF TG_OP='INSERT' THEN
    PERFORM 1 FROM document_template t JOIN template_version v
      ON (t.school_id,t.template_id)=(v.school_id,v.template_id)
      WHERE v.school_id=NEW.school_id AND v.template_version_id=NEW.template_version_id
      FOR SHARE OF t;
    SELECT t.kind INTO tk FROM template_version v JOIN document_template t
      ON (t.school_id,t.template_id)=(v.school_id,v.template_id)
      WHERE v.school_id=NEW.school_id AND v.template_version_id=NEW.template_version_id
        AND v.status='Active';
    IF tk IS DISTINCT FROM dk THEN RAISE EXCEPTION 'Document/template kind mismatch or inactive template'; END IF;
    IF NEW.state<>'Prepared' THEN RAISE EXCEPTION 'New document version starts Prepared'; END IF;
    PERFORM dm_require_file(NEW.school_id,NEW.rendered_file_id,'Rendered');
    PERFORM dm_require_file(NEW.school_id,NEW.basis_file_id,'SourceBasis');
  ELSIF NOT (
    (OLD.state='Prepared' AND NEW.state IN ('Indeterminate','PublishedReview','PublishedReady','Stale','Withdrawn')) OR
    (OLD.state='Indeterminate' AND NEW.state IN ('PublishedReview','PublishedReady','Stale','Withdrawn')) OR
    (OLD.state IN ('PublishedReview','PublishedReady') AND NEW.state IN (OLD.state,'Stale','Withdrawn')) OR
    (OLD.state='Stale' AND NEW.state='Withdrawn')) THEN
    RAISE EXCEPTION 'Invalid document version transition';
  END IF;
  IF NEW.state IN ('PublishedReview','PublishedReady') AND NEW.dependency_revision < watermark THEN
    RAISE EXCEPTION 'Known newer evidence prevents publication';
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER dm_version_guard BEFORE INSERT OR UPDATE ON document_version
  FOR EACH ROW EXECUTE FUNCTION dm_version_guard();

CREATE FUNCTION dm_check_selection() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE s uuid:=NEW.school_id; d uuid:=NEW.document_id; ptr uuid; e uuid;
BEGIN
  SELECT enrollment_id INTO e FROM training_document WHERE school_id=s AND document_id=d;
  PERFORM 1 FROM evidence_checkpoint WHERE school_id=s AND enrollment_id=e FOR UPDATE;
   
   
  SELECT current_version_id INTO ptr FROM training_document WHERE school_id=s AND document_id=d FOR UPDATE;
  IF ptr IS NOT NULL AND NOT EXISTS(SELECT 1 FROM document_version v
    JOIN evidence_checkpoint c ON (c.school_id,c.enrollment_id)=(v.school_id,v.enrollment_id)
    WHERE v.school_id=s AND v.document_id=d AND v.document_version_id=ptr
      AND v.state IN ('PublishedReview','PublishedReady')
      AND v.dependency_revision>=c.dependency_revision) THEN
    RAISE EXCEPTION 'Current pointer requires own published, nonstale version';
  END IF;
  RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER dm_selection_final AFTER INSERT OR UPDATE ON training_document
  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION dm_check_selection();
CREATE CONSTRAINT TRIGGER dm_selection_child_final AFTER INSERT OR UPDATE ON document_version
  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION dm_check_selection();

CREATE FUNCTION dm_check_checkpoint_selection() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF EXISTS (SELECT 1 FROM training_document d JOIN document_version v
    ON (v.school_id,v.document_id,v.document_version_id)=
       (d.school_id,d.document_id,d.current_version_id)
    WHERE d.school_id=NEW.school_id AND d.enrollment_id=NEW.enrollment_id
      AND v.dependency_revision<NEW.dependency_revision) THEN
    RAISE EXCEPTION 'New evidence watermark requires clearing older current selections';
  END IF;
  RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER dm_checkpoint_selection AFTER INSERT OR UPDATE ON evidence_checkpoint
  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION dm_check_checkpoint_selection();

CREATE FUNCTION dm_receipt_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Receipt deletion requires approved policy'; END IF;
  IF TG_OP='INSERT' THEN
    IF NEW.status<>'Pending' THEN RAISE EXCEPTION 'Reserve Pending command first'; END IF;
  ELSE
    IF OLD.status IN ('Succeeded','Rejected') OR
      (to_jsonb(OLD)-ARRAY['status','template_id','template_version_id','contract_id','document_id','document_version_id','outcome_code','completed_at'])
      IS DISTINCT FROM
      (to_jsonb(NEW)-ARRAY['status','template_id','template_version_id','contract_id','document_id','document_version_id','outcome_code','completed_at']) THEN
      RAISE EXCEPTION 'Receipt request identity and terminal outcomes are immutable';
    END IF;
    IF (OLD.template_id IS NOT NULL AND OLD.template_id IS DISTINCT FROM NEW.template_id)
       OR (OLD.template_version_id IS NOT NULL AND OLD.template_version_id IS DISTINCT FROM NEW.template_version_id)
       OR (OLD.contract_id IS NOT NULL AND OLD.contract_id IS DISTINCT FROM NEW.contract_id)
       OR (OLD.document_id IS NOT NULL AND OLD.document_id IS DISTINCT FROM NEW.document_id)
       OR (OLD.document_version_id IS NOT NULL AND OLD.document_version_id IS DISTINCT FROM NEW.document_version_id) THEN
      RAISE EXCEPTION 'Known operation result cannot be rebound';
    END IF;
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER dm_receipt_guard BEFORE INSERT OR UPDATE OR DELETE ON operation_receipt
  FOR EACH ROW EXECUTE FUNCTION dm_receipt_guard();

CREATE FUNCTION dm_checkpoint_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Checkpoint deletion requires approved migration'; END IF;
  IF (NEW.school_id,NEW.enrollment_id) IS DISTINCT FROM (OLD.school_id,OLD.enrollment_id)
     OR NEW.dependency_revision<=OLD.dependency_revision THEN
    RAISE EXCEPTION 'Checkpoint identity immutable; only strictly higher revisions apply';
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER dm_checkpoint_guard BEFORE UPDATE OR DELETE ON evidence_checkpoint
  FOR EACH ROW EXECUTE FUNCTION dm_checkpoint_guard();

CREATE FUNCTION dm_event_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE r operation_receipt%ROWTYPE; d training_document%ROWTYPE; event_document uuid;
BEGIN
  SELECT * INTO r FROM operation_receipt WHERE school_id=NEW.school_id
    AND command_id=NEW.command_id FOR UPDATE;
  IF NOT FOUND OR r.actor_id<>NEW.actor_id OR r.status NOT IN ('Pending','Indeterminate') THEN
    RAISE EXCEPTION 'Event requires same-school actor and open operation receipt';
  END IF;
  IF (r.template_id IS NOT NULL AND r.template_id IS DISTINCT FROM NEW.template_id)
    OR (r.contract_id IS NOT NULL AND r.contract_id IS DISTINCT FROM NEW.contract_id)
    OR (r.document_version_id IS NOT NULL AND NEW.document_id IS NULL
        AND r.document_version_id IS DISTINCT FROM NEW.document_version_id) THEN
    RAISE EXCEPTION 'Event/operation target mismatch';
  END IF;
  event_document := NEW.document_id;
  IF NEW.document_version_id IS NOT NULL THEN
    SELECT document_id INTO event_document FROM document_version
      WHERE school_id=NEW.school_id AND document_version_id=NEW.document_version_id;
  END IF;
  IF r.document_id IS NOT NULL AND r.document_id IS DISTINCT FROM event_document THEN
    RAISE EXCEPTION 'Event/operation document mismatch';
  END IF;
  IF NEW.contract_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM training_contract
    WHERE school_id=NEW.school_id AND contract_id=NEW.contract_id
      AND revision=NEW.sequence_no AND status=NEW.to_state) THEN
    RAISE EXCEPTION 'Contract event must describe its current revision and state';
  END IF;
  IF NEW.document_version_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM document_version
    WHERE school_id=NEW.school_id AND document_version_id=NEW.document_version_id
      AND revision=NEW.sequence_no AND state=NEW.to_state) THEN
    RAISE EXCEPTION 'Version event must describe its current revision and state';
  END IF;
  IF NEW.document_id IS NOT NULL THEN
    SELECT * INTO d FROM training_document WHERE school_id=NEW.school_id AND document_id=NEW.document_id;
    IF NOT FOUND OR NEW.sequence_no<>d.revision
      OR NEW.to_state<>coalesce(d.current_version_id::text,'Unselected')
      OR NEW.action NOT IN ('DocumentCreated','DocumentChanged','CurrentVersionSelected','CurrentVersionCleared') THEN
      RAISE EXCEPTION 'TrainingDocument event must describe its current root revision and selection';
    END IF;
    IF r.document_version_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM document_version
      WHERE school_id=NEW.school_id AND document_version_id=r.document_version_id
        AND document_id=NEW.document_id) THEN
      RAISE EXCEPTION 'Root event does not belong to operation result version';
    END IF;
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER dm_event_guard BEFORE INSERT ON state_event
  FOR EACH ROW EXECUTE FUNCTION dm_event_guard();

 
 
 
 
CREATE FUNCTION dm_require_revision_event() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE ok boolean;
BEGIN
  IF TG_TABLE_NAME='document_template' THEN
    SELECT EXISTS(SELECT 1 FROM state_event WHERE school_id=NEW.school_id
      AND template_id=NEW.template_id AND sequence_no=NEW.revision
      AND to_state=CASE WHEN NEW.active_version_id IS NULL THEN 'NoActiveVersion' ELSE 'Active' END) INTO ok;
  ELSIF TG_TABLE_NAME='training_contract' THEN
    SELECT EXISTS(SELECT 1 FROM state_event WHERE school_id=NEW.school_id
      AND contract_id=NEW.contract_id AND sequence_no=NEW.revision
      AND to_state=NEW.status AND from_state IS NOT DISTINCT FROM
        CASE WHEN TG_OP='INSERT' THEN NULL ELSE OLD.status END) INTO ok;
  ELSIF TG_TABLE_NAME='training_document' THEN
    SELECT EXISTS(SELECT 1 FROM state_event WHERE school_id=NEW.school_id
      AND document_id=NEW.document_id AND sequence_no=NEW.revision
      AND from_state IS NOT DISTINCT FROM CASE WHEN TG_OP='INSERT' THEN NULL
        ELSE coalesce(OLD.current_version_id::text,'Unselected') END
      AND to_state=coalesce(NEW.current_version_id::text,'Unselected')
      AND action=CASE
        WHEN TG_OP='INSERT' THEN 'DocumentCreated'
        WHEN OLD.current_version_id IS NOT DISTINCT FROM NEW.current_version_id THEN 'DocumentChanged'
        WHEN NEW.current_version_id IS NULL THEN 'CurrentVersionCleared'
        ELSE 'CurrentVersionSelected' END) INTO ok;
  ELSE
    SELECT EXISTS(SELECT 1 FROM state_event WHERE school_id=NEW.school_id
      AND document_version_id=NEW.document_version_id AND sequence_no=NEW.revision
      AND to_state=NEW.state AND from_state IS NOT DISTINCT FROM
        CASE WHEN TG_OP='INSERT' THEN NULL ELSE OLD.state END) INTO ok;
  END IF;
  IF NOT ok THEN RAISE EXCEPTION 'Missing matching lifecycle revision audit event'; END IF;
  RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER dm_template_audit AFTER INSERT OR UPDATE ON document_template
  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION dm_require_revision_event();
CREATE CONSTRAINT TRIGGER dm_contract_audit AFTER INSERT OR UPDATE ON training_contract
  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION dm_require_revision_event();
CREATE CONSTRAINT TRIGGER dm_document_audit AFTER INSERT OR UPDATE ON training_document
  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION dm_require_revision_event();
CREATE CONSTRAINT TRIGGER dm_version_audit AFTER INSERT OR UPDATE ON document_version
  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION dm_require_revision_event();

CREATE FUNCTION dm_require_copy_event() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF NOT EXISTS(SELECT 1 FROM state_event e WHERE e.school_id=NEW.school_id
    AND e.contract_id=NEW.contract_id AND e.signed_copy_id=NEW.signed_copy_id
    AND e.action IN ('SignedCopyAttached','SignedCopyReplaced')) THEN
    RAISE EXCEPTION 'New signed copy requires structured attachment/replacement audit';
  END IF;
  RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER dm_signed_audit AFTER INSERT ON contract_signed_copy
  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION dm_require_copy_event();

 
 
 
 
