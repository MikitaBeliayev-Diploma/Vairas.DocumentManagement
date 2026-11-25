using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vairas.DocumentManagement.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class SignedDownloadGrant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "signed_copy_id",
                table: "download_grant",
                type: "uuid",
                nullable: true);
            migrationBuilder.Sql("""
                ALTER TABLE document_job ADD CONSTRAINT dm_job_result_version FOREIGN KEY(school_id,result_version_id) REFERENCES document_version(school_id,document_version_id);
                ALTER TABLE download_grant ADD CONSTRAINT dm_download_version FOREIGN KEY(school_id,version_id) REFERENCES document_version(school_id,document_version_id);
                ALTER TABLE download_grant ADD CONSTRAINT dm_download_signed_copy FOREIGN KEY(school_id,signed_copy_id) REFERENCES contract_signed_copy(school_id,signed_copy_id);
                CREATE FUNCTION dm_artifact_file_guard() RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,public AS $$
                BEGIN
                  PERFORM dm_require_file(NEW.school_id,NEW.file_id,'Rendered');
                  IF NOT EXISTS(SELECT 1 FROM file_object WHERE school_id=NEW.school_id AND file_id=NEW.file_id AND
                    media_type=CASE WHEN NEW.format='pdf' THEN 'application/pdf' ELSE 'application/vnd.openxmlformats-officedocument.wordprocessingml.document' END)
                    THEN RAISE EXCEPTION 'Rendered artifact format mismatch'; END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER dm_artifact_file_guard BEFORE INSERT ON rendered_artifact FOR EACH ROW EXECUTE FUNCTION dm_artifact_file_guard();
                REVOKE ALL ON FUNCTION dm_artifact_file_guard() FROM PUBLIC;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Retained download provenance requires a reviewed retention migration.");
        }
    }
}
