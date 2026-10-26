using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chatting.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentOneToOneRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IX_Comments_TopLevel_ByDate ON dbo.Comments;");
            migrationBuilder.Sql("DROP INDEX IX_Comments_TopLevel_ByUserName ON dbo.Comments;");
            migrationBuilder.Sql("DROP INDEX IX_Comments_TopLevel_ByUserEmail ON dbo.Comments;");
            migrationBuilder.Sql("DROP INDEX IX_Comments_Replies_ByDate ON dbo.Comments;");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "TextFileContent",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "TextFileName",
                table: "Comments");

            migrationBuilder.CreateTable(
                name: "CommentFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TextFileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TextFileContent = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommentFiles_Comments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "Comments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommentImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommentImages_Comments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "Comments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommentFiles_CommentId",
                table: "CommentFiles",
                column: "CommentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommentImages_CommentId",
                table: "CommentImages",
                column: "CommentId",
                unique: true);

            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX IX_Comments_TopLevel_ByDate ON dbo.Comments (Date)
                INCLUDE (Id, UserName, UserEmail, HomePageUrl, Text, ParentId)
                WHERE ParentId IS NULL;");

            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX IX_Comments_TopLevel_ByUserName ON dbo.Comments (UserName)
                INCLUDE (Id, UserEmail, HomePageUrl, Date, Text, ParentId)
                WHERE ParentId IS NULL;");

            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX IX_Comments_TopLevel_ByUserEmail ON dbo.Comments (UserEmail)
                INCLUDE (Id, UserName, HomePageUrl, Date, Text, ParentId)
                WHERE ParentId IS NULL;");

            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX IX_Comments_Replies_ByDate ON dbo.Comments (Date DESC)
                INCLUDE (Id, UserName, UserEmail, HomePageUrl, Text, ParentId)
                WHERE ParentId IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IX_Comments_TopLevel_ByDate ON dbo.Comments;");
            migrationBuilder.Sql("DROP INDEX IX_Comments_TopLevel_ByUserName ON dbo.Comments;");
            migrationBuilder.Sql("DROP INDEX IX_Comments_TopLevel_ByUserEmail ON dbo.Comments;");
            migrationBuilder.Sql("DROP INDEX IX_Comments_Replies_ByDate ON dbo.Comments;");

            migrationBuilder.DropTable(
                name: "CommentFiles");

            migrationBuilder.DropTable(
                name: "CommentImages");

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Comments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextFileContent",
                table: "Comments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextFileName",
                table: "Comments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX IX_Comments_TopLevel_ByDate ON dbo.Comments (Date)
                INCLUDE (Id, UserName, UserEmail, ImageUrl, HomePageUrl, Text, TextFileName, TextFileContent, ParentId)
                WHERE ParentId IS NULL;");

            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX IX_Comments_TopLevel_ByUserName ON dbo.Comments (UserName)
                INCLUDE (Id, UserEmail, ImageUrl, HomePageUrl, Date, Text, TextFileName, TextFileContent, ParentId)
                WHERE ParentId IS NULL;");

            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX IX_Comments_TopLevel_ByUserEmail ON dbo.Comments (UserEmail)
                INCLUDE (Id, UserName, ImageUrl, HomePageUrl, Date, Text, TextFileName, TextFileContent, ParentId)
                WHERE ParentId IS NULL;");

            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX IX_Comments_Replies_ByDate ON dbo.Comments (Date DESC)
                INCLUDE (Id, UserName, UserEmail, ImageUrl, HomePageUrl, Text, TextFileName, TextFileContent, ParentId)
                WHERE ParentId IS NOT NULL;");
        }
    }
}
