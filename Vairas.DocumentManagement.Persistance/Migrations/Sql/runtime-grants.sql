REVOKE ALL ON SCHEMA public FROM PUBLIC;
REVOKE ALL ON ALL TABLES IN SCHEMA public FROM PUBLIC;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO v_document_management_runtime,v_document_management_delivery;
DO $$ BEGIN
 EXECUTE format('REVOKE CONNECT,CREATE,TEMPORARY ON DATABASE %I FROM PUBLIC',current_database());
 EXECUTE format('GRANT CONNECT ON DATABASE %I TO v_document_management_runtime,v_document_management_delivery,v_document_management_migration',current_database());
END $$;
GRANT SELECT,INSERT ON document_template,template_version,file_object,training_contract,contract_signed_copy,training_document,document_version,state_event,operation_receipt,evidence_checkpoint,
 document_job,operation_outcome,rendered_artifact,publication_decision,download_grant,inbox_message,document_review TO v_document_management_runtime;
GRANT UPDATE(name,active_version_id,revision) ON document_template TO v_document_management_runtime;
GRANT UPDATE(status) ON template_version TO v_document_management_runtime;
GRANT UPDATE(scan_status) ON file_object TO v_document_management_runtime;
GRANT UPDATE(status,revision) ON training_contract TO v_document_management_runtime;
GRANT UPDATE(is_current) ON contract_signed_copy TO v_document_management_runtime;
GRANT UPDATE(revision,current_version_id) ON training_document TO v_document_management_runtime;
GRANT UPDATE(state,revision) ON document_version TO v_document_management_runtime;
GRANT UPDATE(status,template_id,template_version_id,contract_id,document_id,document_version_id,outcome_code,completed_at) ON operation_receipt TO v_document_management_runtime;
GRANT UPDATE(dependency_revision,last_change_id,observed_at) ON evidence_checkpoint TO v_document_management_runtime;
GRANT UPDATE(status,document_id,result_version_id,error_code,attempts,next_attempt_at,lease_until,lease_id) ON document_job TO v_document_management_runtime;
GRANT UPDATE(status,decided_at) ON publication_decision TO v_document_management_runtime;
GRANT EXECUTE ON FUNCTION dm_require_file(uuid,uuid,text) TO v_document_management_runtime;
GRANT EXECUTE ON FUNCTION dm_pending_jobs(),dm_pending_publications(),dm_enrollment_catalogue(),dm_pending_files() TO v_document_management_delivery;
DO $security$ DECLARE t text; f record; BEGIN
 FOREACH t IN ARRAY ARRAY['document_template','template_version','file_object','training_contract','contract_signed_copy','training_document','document_version','state_event','operation_receipt','evidence_checkpoint','document_job','operation_outcome','rendered_artifact','publication_decision','download_grant','inbox_message','document_review'] LOOP
  EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY',t);
  IF NOT EXISTS(SELECT 1 FROM pg_policy WHERE polrelid=to_regclass('public.'||t) AND polname='tenant_scope') THEN
   EXECUTE format('CREATE POLICY tenant_scope ON %I TO v_document_management_runtime USING(school_id=nullif(current_setting(''vairas.school_id'',true),'''')::uuid) WITH CHECK(school_id=nullif(current_setting(''vairas.school_id'',true),'''')::uuid)',t);
  END IF;
 END LOOP;
 FOR f IN SELECT p.oid::regprocedure AS signature FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='public' AND p.proname LIKE 'dm_%' LOOP
  EXECUTE format('ALTER FUNCTION %s SET search_path=pg_catalog,public,pg_temp',f.signature);
 END LOOP;
END $security$;
