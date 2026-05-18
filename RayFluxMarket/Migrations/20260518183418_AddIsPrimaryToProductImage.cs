using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RayFluxMarket.Migrations
{
    /// <inheritdoc />
    public partial class AddIsPrimaryToProductImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsMain",
                table: "ProductImages",
                newName: "IsPrimary");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsPrimary",
                table: "ProductImages",
                newName: "IsMain");
        }
    }
}
