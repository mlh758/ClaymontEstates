using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class AddPets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Weight = table.Column<decimal>(type: "TEXT", nullable: true),
                    Species = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Breed = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pets_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pets_UserId",
                table: "Pets",
                column: "UserId");

            migrationBuilder.Sql("""
                CREATE VIRTUAL TABLE PetsFts USING fts5(
                    Name, Description, Species, Breed,
                    content='Pets', content_rowid='Id',
                    tokenize='porter unicode61'
                );

                CREATE TRIGGER Pets_ai AFTER INSERT ON Pets BEGIN
                    INSERT INTO PetsFts(rowid, Name, Description, Species, Breed)
                    VALUES (new.Id, new.Name, new.Description, new.Species, new.Breed);
                END;

                CREATE TRIGGER Pets_ad AFTER DELETE ON Pets BEGIN
                    INSERT INTO PetsFts(PetsFts, rowid, Name, Description, Species, Breed)
                    VALUES ('delete', old.Id, old.Name, old.Description, old.Species, old.Breed);
                END;

                CREATE TRIGGER Pets_au AFTER UPDATE ON Pets BEGIN
                    INSERT INTO PetsFts(PetsFts, rowid, Name, Description, Species, Breed)
                    VALUES ('delete', old.Id, old.Name, old.Description, old.Species, old.Breed);
                    INSERT INTO PetsFts(rowid, Name, Description, Species, Breed)
                    VALUES (new.Id, new.Name, new.Description, new.Species, new.Breed);
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Pets_au;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Pets_ad;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Pets_ai;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS PetsFts;");

            migrationBuilder.DropTable(
                name: "Pets");
        }
    }
}
