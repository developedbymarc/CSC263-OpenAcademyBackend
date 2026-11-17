using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenAcademy.Migrations
{
    /// <inheritdoc />
    public partial class ProfileAvatarAndCourseThumbnail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ThumbnailBlobPath",
                table: "Courses",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AvatarBlobPath",
                table: "AspNetUsers",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ThumbnailBlobPath",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "AvatarBlobPath",
                table: "AspNetUsers");
        }
    }
}
