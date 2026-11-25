using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vairas.DocumentManagement.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class ExactJobOutcome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "missing_items_json",
                table: "document_job",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<Guid>(
                name: "result_version_id",
                table: "document_job",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Retained job outcomes require a reviewed retention migration.");
        }
    }
}
