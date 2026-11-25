-- Run as cluster administrator. Supply LOGIN credentials outside this file.
DO $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='v_document_management_migration') THEN
  CREATE ROLE v_document_management_migration LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
 END IF;
 IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='v_document_management_runtime') THEN
  CREATE ROLE v_document_management_runtime LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
 END IF;
 IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='v_document_management_delivery') THEN
  CREATE ROLE v_document_management_delivery LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
 END IF;
END $$;
