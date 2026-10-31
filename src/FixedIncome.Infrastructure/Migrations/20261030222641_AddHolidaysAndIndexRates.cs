using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FixedIncome.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHolidaysAndIndexRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "holiday",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_holiday", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "index_rate",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    index_type = table.Column<string>(type: "text", nullable: false),
                    reference_date = table.Column<DateOnly>(type: "date", nullable: false),
                    value = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_index_rate", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "holiday",
                columns: new[] { "id", "date", "name" },
                values: new object[,]
                {
                    { new Guid("20200101-0000-0000-0000-000000000000"), new DateOnly(2020, 1, 1), "Confraternização Universal" },
                    { new Guid("20200224-0000-0000-0000-000000000000"), new DateOnly(2020, 2, 24), "Carnaval" },
                    { new Guid("20200225-0000-0000-0000-000000000000"), new DateOnly(2020, 2, 25), "Carnaval" },
                    { new Guid("20200410-0000-0000-0000-000000000000"), new DateOnly(2020, 4, 10), "Paixão de Cristo" },
                    { new Guid("20200421-0000-0000-0000-000000000000"), new DateOnly(2020, 4, 21), "Tiradentes" },
                    { new Guid("20200501-0000-0000-0000-000000000000"), new DateOnly(2020, 5, 1), "Dia do Trabalho" },
                    { new Guid("20200611-0000-0000-0000-000000000000"), new DateOnly(2020, 6, 11), "Corpus Christi" },
                    { new Guid("20200907-0000-0000-0000-000000000000"), new DateOnly(2020, 9, 7), "Independência do Brasil" },
                    { new Guid("20201012-0000-0000-0000-000000000000"), new DateOnly(2020, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20201102-0000-0000-0000-000000000000"), new DateOnly(2020, 11, 2), "Finados" },
                    { new Guid("20201115-0000-0000-0000-000000000000"), new DateOnly(2020, 11, 15), "Proclamação da República" },
                    { new Guid("20201225-0000-0000-0000-000000000000"), new DateOnly(2020, 12, 25), "Natal" },
                    { new Guid("20210101-0000-0000-0000-000000000000"), new DateOnly(2021, 1, 1), "Confraternização Universal" },
                    { new Guid("20210215-0000-0000-0000-000000000000"), new DateOnly(2021, 2, 15), "Carnaval" },
                    { new Guid("20210216-0000-0000-0000-000000000000"), new DateOnly(2021, 2, 16), "Carnaval" },
                    { new Guid("20210402-0000-0000-0000-000000000000"), new DateOnly(2021, 4, 2), "Paixão de Cristo" },
                    { new Guid("20210421-0000-0000-0000-000000000000"), new DateOnly(2021, 4, 21), "Tiradentes" },
                    { new Guid("20210501-0000-0000-0000-000000000000"), new DateOnly(2021, 5, 1), "Dia do Trabalho" },
                    { new Guid("20210603-0000-0000-0000-000000000000"), new DateOnly(2021, 6, 3), "Corpus Christi" },
                    { new Guid("20210907-0000-0000-0000-000000000000"), new DateOnly(2021, 9, 7), "Independência do Brasil" },
                    { new Guid("20211012-0000-0000-0000-000000000000"), new DateOnly(2021, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20211102-0000-0000-0000-000000000000"), new DateOnly(2021, 11, 2), "Finados" },
                    { new Guid("20211115-0000-0000-0000-000000000000"), new DateOnly(2021, 11, 15), "Proclamação da República" },
                    { new Guid("20211225-0000-0000-0000-000000000000"), new DateOnly(2021, 12, 25), "Natal" },
                    { new Guid("20220101-0000-0000-0000-000000000000"), new DateOnly(2022, 1, 1), "Confraternização Universal" },
                    { new Guid("20220228-0000-0000-0000-000000000000"), new DateOnly(2022, 2, 28), "Carnaval" },
                    { new Guid("20220301-0000-0000-0000-000000000000"), new DateOnly(2022, 3, 1), "Carnaval" },
                    { new Guid("20220415-0000-0000-0000-000000000000"), new DateOnly(2022, 4, 15), "Paixão de Cristo" },
                    { new Guid("20220421-0000-0000-0000-000000000000"), new DateOnly(2022, 4, 21), "Tiradentes" },
                    { new Guid("20220501-0000-0000-0000-000000000000"), new DateOnly(2022, 5, 1), "Dia do Trabalho" },
                    { new Guid("20220616-0000-0000-0000-000000000000"), new DateOnly(2022, 6, 16), "Corpus Christi" },
                    { new Guid("20220907-0000-0000-0000-000000000000"), new DateOnly(2022, 9, 7), "Independência do Brasil" },
                    { new Guid("20221012-0000-0000-0000-000000000000"), new DateOnly(2022, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20221102-0000-0000-0000-000000000000"), new DateOnly(2022, 11, 2), "Finados" },
                    { new Guid("20221115-0000-0000-0000-000000000000"), new DateOnly(2022, 11, 15), "Proclamação da República" },
                    { new Guid("20221225-0000-0000-0000-000000000000"), new DateOnly(2022, 12, 25), "Natal" },
                    { new Guid("20230101-0000-0000-0000-000000000000"), new DateOnly(2023, 1, 1), "Confraternização Universal" },
                    { new Guid("20230220-0000-0000-0000-000000000000"), new DateOnly(2023, 2, 20), "Carnaval" },
                    { new Guid("20230221-0000-0000-0000-000000000000"), new DateOnly(2023, 2, 21), "Carnaval" },
                    { new Guid("20230407-0000-0000-0000-000000000000"), new DateOnly(2023, 4, 7), "Paixão de Cristo" },
                    { new Guid("20230421-0000-0000-0000-000000000000"), new DateOnly(2023, 4, 21), "Tiradentes" },
                    { new Guid("20230501-0000-0000-0000-000000000000"), new DateOnly(2023, 5, 1), "Dia do Trabalho" },
                    { new Guid("20230608-0000-0000-0000-000000000000"), new DateOnly(2023, 6, 8), "Corpus Christi" },
                    { new Guid("20230907-0000-0000-0000-000000000000"), new DateOnly(2023, 9, 7), "Independência do Brasil" },
                    { new Guid("20231012-0000-0000-0000-000000000000"), new DateOnly(2023, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20231102-0000-0000-0000-000000000000"), new DateOnly(2023, 11, 2), "Finados" },
                    { new Guid("20231115-0000-0000-0000-000000000000"), new DateOnly(2023, 11, 15), "Proclamação da República" },
                    { new Guid("20231225-0000-0000-0000-000000000000"), new DateOnly(2023, 12, 25), "Natal" },
                    { new Guid("20240101-0000-0000-0000-000000000000"), new DateOnly(2024, 1, 1), "Confraternização Universal" },
                    { new Guid("20240212-0000-0000-0000-000000000000"), new DateOnly(2024, 2, 12), "Carnaval" },
                    { new Guid("20240213-0000-0000-0000-000000000000"), new DateOnly(2024, 2, 13), "Carnaval" },
                    { new Guid("20240329-0000-0000-0000-000000000000"), new DateOnly(2024, 3, 29), "Paixão de Cristo" },
                    { new Guid("20240421-0000-0000-0000-000000000000"), new DateOnly(2024, 4, 21), "Tiradentes" },
                    { new Guid("20240501-0000-0000-0000-000000000000"), new DateOnly(2024, 5, 1), "Dia do Trabalho" },
                    { new Guid("20240530-0000-0000-0000-000000000000"), new DateOnly(2024, 5, 30), "Corpus Christi" },
                    { new Guid("20240907-0000-0000-0000-000000000000"), new DateOnly(2024, 9, 7), "Independência do Brasil" },
                    { new Guid("20241012-0000-0000-0000-000000000000"), new DateOnly(2024, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20241102-0000-0000-0000-000000000000"), new DateOnly(2024, 11, 2), "Finados" },
                    { new Guid("20241115-0000-0000-0000-000000000000"), new DateOnly(2024, 11, 15), "Proclamação da República" },
                    { new Guid("20241120-0000-0000-0000-000000000000"), new DateOnly(2024, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20241225-0000-0000-0000-000000000000"), new DateOnly(2024, 12, 25), "Natal" },
                    { new Guid("20250101-0000-0000-0000-000000000000"), new DateOnly(2025, 1, 1), "Confraternização Universal" },
                    { new Guid("20250303-0000-0000-0000-000000000000"), new DateOnly(2025, 3, 3), "Carnaval" },
                    { new Guid("20250304-0000-0000-0000-000000000000"), new DateOnly(2025, 3, 4), "Carnaval" },
                    { new Guid("20250418-0000-0000-0000-000000000000"), new DateOnly(2025, 4, 18), "Paixão de Cristo" },
                    { new Guid("20250421-0000-0000-0000-000000000000"), new DateOnly(2025, 4, 21), "Tiradentes" },
                    { new Guid("20250501-0000-0000-0000-000000000000"), new DateOnly(2025, 5, 1), "Dia do Trabalho" },
                    { new Guid("20250619-0000-0000-0000-000000000000"), new DateOnly(2025, 6, 19), "Corpus Christi" },
                    { new Guid("20250907-0000-0000-0000-000000000000"), new DateOnly(2025, 9, 7), "Independência do Brasil" },
                    { new Guid("20251012-0000-0000-0000-000000000000"), new DateOnly(2025, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20251102-0000-0000-0000-000000000000"), new DateOnly(2025, 11, 2), "Finados" },
                    { new Guid("20251115-0000-0000-0000-000000000000"), new DateOnly(2025, 11, 15), "Proclamação da República" },
                    { new Guid("20251120-0000-0000-0000-000000000000"), new DateOnly(2025, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20251225-0000-0000-0000-000000000000"), new DateOnly(2025, 12, 25), "Natal" },
                    { new Guid("20260101-0000-0000-0000-000000000000"), new DateOnly(2026, 1, 1), "Confraternização Universal" },
                    { new Guid("20260216-0000-0000-0000-000000000000"), new DateOnly(2026, 2, 16), "Carnaval" },
                    { new Guid("20260217-0000-0000-0000-000000000000"), new DateOnly(2026, 2, 17), "Carnaval" },
                    { new Guid("20260403-0000-0000-0000-000000000000"), new DateOnly(2026, 4, 3), "Paixão de Cristo" },
                    { new Guid("20260421-0000-0000-0000-000000000000"), new DateOnly(2026, 4, 21), "Tiradentes" },
                    { new Guid("20260501-0000-0000-0000-000000000000"), new DateOnly(2026, 5, 1), "Dia do Trabalho" },
                    { new Guid("20260604-0000-0000-0000-000000000000"), new DateOnly(2026, 6, 4), "Corpus Christi" },
                    { new Guid("20260907-0000-0000-0000-000000000000"), new DateOnly(2026, 9, 7), "Independência do Brasil" },
                    { new Guid("20261012-0000-0000-0000-000000000000"), new DateOnly(2026, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20261102-0000-0000-0000-000000000000"), new DateOnly(2026, 11, 2), "Finados" },
                    { new Guid("20261115-0000-0000-0000-000000000000"), new DateOnly(2026, 11, 15), "Proclamação da República" },
                    { new Guid("20261120-0000-0000-0000-000000000000"), new DateOnly(2026, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20261225-0000-0000-0000-000000000000"), new DateOnly(2026, 12, 25), "Natal" },
                    { new Guid("20270101-0000-0000-0000-000000000000"), new DateOnly(2027, 1, 1), "Confraternização Universal" },
                    { new Guid("20270208-0000-0000-0000-000000000000"), new DateOnly(2027, 2, 8), "Carnaval" },
                    { new Guid("20270209-0000-0000-0000-000000000000"), new DateOnly(2027, 2, 9), "Carnaval" },
                    { new Guid("20270326-0000-0000-0000-000000000000"), new DateOnly(2027, 3, 26), "Paixão de Cristo" },
                    { new Guid("20270421-0000-0000-0000-000000000000"), new DateOnly(2027, 4, 21), "Tiradentes" },
                    { new Guid("20270501-0000-0000-0000-000000000000"), new DateOnly(2027, 5, 1), "Dia do Trabalho" },
                    { new Guid("20270527-0000-0000-0000-000000000000"), new DateOnly(2027, 5, 27), "Corpus Christi" },
                    { new Guid("20270907-0000-0000-0000-000000000000"), new DateOnly(2027, 9, 7), "Independência do Brasil" },
                    { new Guid("20271012-0000-0000-0000-000000000000"), new DateOnly(2027, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20271102-0000-0000-0000-000000000000"), new DateOnly(2027, 11, 2), "Finados" },
                    { new Guid("20271115-0000-0000-0000-000000000000"), new DateOnly(2027, 11, 15), "Proclamação da República" },
                    { new Guid("20271120-0000-0000-0000-000000000000"), new DateOnly(2027, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20271225-0000-0000-0000-000000000000"), new DateOnly(2027, 12, 25), "Natal" },
                    { new Guid("20280101-0000-0000-0000-000000000000"), new DateOnly(2028, 1, 1), "Confraternização Universal" },
                    { new Guid("20280228-0000-0000-0000-000000000000"), new DateOnly(2028, 2, 28), "Carnaval" },
                    { new Guid("20280229-0000-0000-0000-000000000000"), new DateOnly(2028, 2, 29), "Carnaval" },
                    { new Guid("20280414-0000-0000-0000-000000000000"), new DateOnly(2028, 4, 14), "Paixão de Cristo" },
                    { new Guid("20280421-0000-0000-0000-000000000000"), new DateOnly(2028, 4, 21), "Tiradentes" },
                    { new Guid("20280501-0000-0000-0000-000000000000"), new DateOnly(2028, 5, 1), "Dia do Trabalho" },
                    { new Guid("20280615-0000-0000-0000-000000000000"), new DateOnly(2028, 6, 15), "Corpus Christi" },
                    { new Guid("20280907-0000-0000-0000-000000000000"), new DateOnly(2028, 9, 7), "Independência do Brasil" },
                    { new Guid("20281012-0000-0000-0000-000000000000"), new DateOnly(2028, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20281102-0000-0000-0000-000000000000"), new DateOnly(2028, 11, 2), "Finados" },
                    { new Guid("20281115-0000-0000-0000-000000000000"), new DateOnly(2028, 11, 15), "Proclamação da República" },
                    { new Guid("20281120-0000-0000-0000-000000000000"), new DateOnly(2028, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20281225-0000-0000-0000-000000000000"), new DateOnly(2028, 12, 25), "Natal" },
                    { new Guid("20290101-0000-0000-0000-000000000000"), new DateOnly(2029, 1, 1), "Confraternização Universal" },
                    { new Guid("20290212-0000-0000-0000-000000000000"), new DateOnly(2029, 2, 12), "Carnaval" },
                    { new Guid("20290213-0000-0000-0000-000000000000"), new DateOnly(2029, 2, 13), "Carnaval" },
                    { new Guid("20290330-0000-0000-0000-000000000000"), new DateOnly(2029, 3, 30), "Paixão de Cristo" },
                    { new Guid("20290421-0000-0000-0000-000000000000"), new DateOnly(2029, 4, 21), "Tiradentes" },
                    { new Guid("20290501-0000-0000-0000-000000000000"), new DateOnly(2029, 5, 1), "Dia do Trabalho" },
                    { new Guid("20290531-0000-0000-0000-000000000000"), new DateOnly(2029, 5, 31), "Corpus Christi" },
                    { new Guid("20290907-0000-0000-0000-000000000000"), new DateOnly(2029, 9, 7), "Independência do Brasil" },
                    { new Guid("20291012-0000-0000-0000-000000000000"), new DateOnly(2029, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20291102-0000-0000-0000-000000000000"), new DateOnly(2029, 11, 2), "Finados" },
                    { new Guid("20291115-0000-0000-0000-000000000000"), new DateOnly(2029, 11, 15), "Proclamação da República" },
                    { new Guid("20291120-0000-0000-0000-000000000000"), new DateOnly(2029, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20291225-0000-0000-0000-000000000000"), new DateOnly(2029, 12, 25), "Natal" },
                    { new Guid("20300101-0000-0000-0000-000000000000"), new DateOnly(2030, 1, 1), "Confraternização Universal" },
                    { new Guid("20300304-0000-0000-0000-000000000000"), new DateOnly(2030, 3, 4), "Carnaval" },
                    { new Guid("20300305-0000-0000-0000-000000000000"), new DateOnly(2030, 3, 5), "Carnaval" },
                    { new Guid("20300419-0000-0000-0000-000000000000"), new DateOnly(2030, 4, 19), "Paixão de Cristo" },
                    { new Guid("20300421-0000-0000-0000-000000000000"), new DateOnly(2030, 4, 21), "Tiradentes" },
                    { new Guid("20300501-0000-0000-0000-000000000000"), new DateOnly(2030, 5, 1), "Dia do Trabalho" },
                    { new Guid("20300620-0000-0000-0000-000000000000"), new DateOnly(2030, 6, 20), "Corpus Christi" },
                    { new Guid("20300907-0000-0000-0000-000000000000"), new DateOnly(2030, 9, 7), "Independência do Brasil" },
                    { new Guid("20301012-0000-0000-0000-000000000000"), new DateOnly(2030, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20301102-0000-0000-0000-000000000000"), new DateOnly(2030, 11, 2), "Finados" },
                    { new Guid("20301115-0000-0000-0000-000000000000"), new DateOnly(2030, 11, 15), "Proclamação da República" },
                    { new Guid("20301120-0000-0000-0000-000000000000"), new DateOnly(2030, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20301225-0000-0000-0000-000000000000"), new DateOnly(2030, 12, 25), "Natal" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_holiday_date",
                table: "holiday",
                column: "date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_index_rate_index_type_reference_date",
                table: "index_rate",
                columns: new[] { "index_type", "reference_date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "holiday");

            migrationBuilder.DropTable(
                name: "index_rate");
        }
    }
}
