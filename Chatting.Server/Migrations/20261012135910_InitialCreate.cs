using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chatting.Server.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Comments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserEmail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HomePageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TextFileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TextFileContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CommentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comments", x => x.Id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_Comments_Comments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "Comments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_CommentId",
                table: "Comments",
                column: "CommentId");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IX_Comments_TopLevel_ByDate ON dbo.Comments;");
            migrationBuilder.Sql("DROP INDEX IX_Comments_TopLevel_ByUserName ON dbo.Comments;");
            migrationBuilder.Sql("DROP INDEX IX_Comments_TopLevel_ByUserEmail ON dbo.Comments;");
            migrationBuilder.Sql("DROP INDEX IX_Comments_Replies_ByDate ON dbo.Comments;");

            migrationBuilder.DropTable(
                name: "Comments");
        }
    }
}
