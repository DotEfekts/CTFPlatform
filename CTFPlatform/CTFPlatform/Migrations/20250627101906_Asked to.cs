using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTFPlatform.Migrations
{
    /// <inheritdoc />
    public partial class Askedto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VpnCertificate_Users_UserId",
                table: "VpnCertificate");

            migrationBuilder.DropPrimaryKey(
                name: "PK_VpnCertificate",
                table: "VpnCertificate");

            migrationBuilder.RenameTable(
                name: "VpnCertificate",
                newName: "VpnCertificates");

            migrationBuilder.RenameIndex(
                name: "IX_VpnCertificate_UserId",
                table: "VpnCertificates",
                newName: "IX_VpnCertificates_UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_VpnCertificates",
                table: "VpnCertificates",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VpnCertificates_Users_UserId",
                table: "VpnCertificates",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VpnCertificates_Users_UserId",
                table: "VpnCertificates");

            migrationBuilder.DropPrimaryKey(
                name: "PK_VpnCertificates",
                table: "VpnCertificates");

            migrationBuilder.RenameTable(
                name: "VpnCertificates",
                newName: "VpnCertificate");

            migrationBuilder.RenameIndex(
                name: "IX_VpnCertificates_UserId",
                table: "VpnCertificate",
                newName: "IX_VpnCertificate_UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_VpnCertificate",
                table: "VpnCertificate",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VpnCertificate_Users_UserId",
                table: "VpnCertificate",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
