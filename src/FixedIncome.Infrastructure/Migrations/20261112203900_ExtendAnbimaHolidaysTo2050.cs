using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FixedIncome.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExtendAnbimaHolidaysTo2050 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "holiday",
                columns: new[] { "id", "date", "name" },
                values: new object[,]
                {
                    { new Guid("20310101-0000-0000-0000-000000000000"), new DateOnly(2031, 1, 1), "Confraternização Universal" },
                    { new Guid("20310224-0000-0000-0000-000000000000"), new DateOnly(2031, 2, 24), "Carnaval" },
                    { new Guid("20310225-0000-0000-0000-000000000000"), new DateOnly(2031, 2, 25), "Carnaval" },
                    { new Guid("20310411-0000-0000-0000-000000000000"), new DateOnly(2031, 4, 11), "Paixão de Cristo" },
                    { new Guid("20310421-0000-0000-0000-000000000000"), new DateOnly(2031, 4, 21), "Tiradentes" },
                    { new Guid("20310501-0000-0000-0000-000000000000"), new DateOnly(2031, 5, 1), "Dia do Trabalho" },
                    { new Guid("20310612-0000-0000-0000-000000000000"), new DateOnly(2031, 6, 12), "Corpus Christi" },
                    { new Guid("20310907-0000-0000-0000-000000000000"), new DateOnly(2031, 9, 7), "Independência do Brasil" },
                    { new Guid("20311012-0000-0000-0000-000000000000"), new DateOnly(2031, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20311102-0000-0000-0000-000000000000"), new DateOnly(2031, 11, 2), "Finados" },
                    { new Guid("20311115-0000-0000-0000-000000000000"), new DateOnly(2031, 11, 15), "Proclamação da República" },
                    { new Guid("20311120-0000-0000-0000-000000000000"), new DateOnly(2031, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20311225-0000-0000-0000-000000000000"), new DateOnly(2031, 12, 25), "Natal" },
                    { new Guid("20320101-0000-0000-0000-000000000000"), new DateOnly(2032, 1, 1), "Confraternização Universal" },
                    { new Guid("20320209-0000-0000-0000-000000000000"), new DateOnly(2032, 2, 9), "Carnaval" },
                    { new Guid("20320210-0000-0000-0000-000000000000"), new DateOnly(2032, 2, 10), "Carnaval" },
                    { new Guid("20320326-0000-0000-0000-000000000000"), new DateOnly(2032, 3, 26), "Paixão de Cristo" },
                    { new Guid("20320421-0000-0000-0000-000000000000"), new DateOnly(2032, 4, 21), "Tiradentes" },
                    { new Guid("20320501-0000-0000-0000-000000000000"), new DateOnly(2032, 5, 1), "Dia do Trabalho" },
                    { new Guid("20320527-0000-0000-0000-000000000000"), new DateOnly(2032, 5, 27), "Corpus Christi" },
                    { new Guid("20320907-0000-0000-0000-000000000000"), new DateOnly(2032, 9, 7), "Independência do Brasil" },
                    { new Guid("20321012-0000-0000-0000-000000000000"), new DateOnly(2032, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20321102-0000-0000-0000-000000000000"), new DateOnly(2032, 11, 2), "Finados" },
                    { new Guid("20321115-0000-0000-0000-000000000000"), new DateOnly(2032, 11, 15), "Proclamação da República" },
                    { new Guid("20321120-0000-0000-0000-000000000000"), new DateOnly(2032, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20321225-0000-0000-0000-000000000000"), new DateOnly(2032, 12, 25), "Natal" },
                    { new Guid("20330101-0000-0000-0000-000000000000"), new DateOnly(2033, 1, 1), "Confraternização Universal" },
                    { new Guid("20330228-0000-0000-0000-000000000000"), new DateOnly(2033, 2, 28), "Carnaval" },
                    { new Guid("20330301-0000-0000-0000-000000000000"), new DateOnly(2033, 3, 1), "Carnaval" },
                    { new Guid("20330415-0000-0000-0000-000000000000"), new DateOnly(2033, 4, 15), "Paixão de Cristo" },
                    { new Guid("20330421-0000-0000-0000-000000000000"), new DateOnly(2033, 4, 21), "Tiradentes" },
                    { new Guid("20330501-0000-0000-0000-000000000000"), new DateOnly(2033, 5, 1), "Dia do Trabalho" },
                    { new Guid("20330616-0000-0000-0000-000000000000"), new DateOnly(2033, 6, 16), "Corpus Christi" },
                    { new Guid("20330907-0000-0000-0000-000000000000"), new DateOnly(2033, 9, 7), "Independência do Brasil" },
                    { new Guid("20331012-0000-0000-0000-000000000000"), new DateOnly(2033, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20331102-0000-0000-0000-000000000000"), new DateOnly(2033, 11, 2), "Finados" },
                    { new Guid("20331115-0000-0000-0000-000000000000"), new DateOnly(2033, 11, 15), "Proclamação da República" },
                    { new Guid("20331120-0000-0000-0000-000000000000"), new DateOnly(2033, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20331225-0000-0000-0000-000000000000"), new DateOnly(2033, 12, 25), "Natal" },
                    { new Guid("20340101-0000-0000-0000-000000000000"), new DateOnly(2034, 1, 1), "Confraternização Universal" },
                    { new Guid("20340220-0000-0000-0000-000000000000"), new DateOnly(2034, 2, 20), "Carnaval" },
                    { new Guid("20340221-0000-0000-0000-000000000000"), new DateOnly(2034, 2, 21), "Carnaval" },
                    { new Guid("20340407-0000-0000-0000-000000000000"), new DateOnly(2034, 4, 7), "Paixão de Cristo" },
                    { new Guid("20340421-0000-0000-0000-000000000000"), new DateOnly(2034, 4, 21), "Tiradentes" },
                    { new Guid("20340501-0000-0000-0000-000000000000"), new DateOnly(2034, 5, 1), "Dia do Trabalho" },
                    { new Guid("20340608-0000-0000-0000-000000000000"), new DateOnly(2034, 6, 8), "Corpus Christi" },
                    { new Guid("20340907-0000-0000-0000-000000000000"), new DateOnly(2034, 9, 7), "Independência do Brasil" },
                    { new Guid("20341012-0000-0000-0000-000000000000"), new DateOnly(2034, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20341102-0000-0000-0000-000000000000"), new DateOnly(2034, 11, 2), "Finados" },
                    { new Guid("20341115-0000-0000-0000-000000000000"), new DateOnly(2034, 11, 15), "Proclamação da República" },
                    { new Guid("20341120-0000-0000-0000-000000000000"), new DateOnly(2034, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20341225-0000-0000-0000-000000000000"), new DateOnly(2034, 12, 25), "Natal" },
                    { new Guid("20350101-0000-0000-0000-000000000000"), new DateOnly(2035, 1, 1), "Confraternização Universal" },
                    { new Guid("20350205-0000-0000-0000-000000000000"), new DateOnly(2035, 2, 5), "Carnaval" },
                    { new Guid("20350206-0000-0000-0000-000000000000"), new DateOnly(2035, 2, 6), "Carnaval" },
                    { new Guid("20350323-0000-0000-0000-000000000000"), new DateOnly(2035, 3, 23), "Paixão de Cristo" },
                    { new Guid("20350421-0000-0000-0000-000000000000"), new DateOnly(2035, 4, 21), "Tiradentes" },
                    { new Guid("20350501-0000-0000-0000-000000000000"), new DateOnly(2035, 5, 1), "Dia do Trabalho" },
                    { new Guid("20350524-0000-0000-0000-000000000000"), new DateOnly(2035, 5, 24), "Corpus Christi" },
                    { new Guid("20350907-0000-0000-0000-000000000000"), new DateOnly(2035, 9, 7), "Independência do Brasil" },
                    { new Guid("20351012-0000-0000-0000-000000000000"), new DateOnly(2035, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20351102-0000-0000-0000-000000000000"), new DateOnly(2035, 11, 2), "Finados" },
                    { new Guid("20351115-0000-0000-0000-000000000000"), new DateOnly(2035, 11, 15), "Proclamação da República" },
                    { new Guid("20351120-0000-0000-0000-000000000000"), new DateOnly(2035, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20351225-0000-0000-0000-000000000000"), new DateOnly(2035, 12, 25), "Natal" },
                    { new Guid("20360101-0000-0000-0000-000000000000"), new DateOnly(2036, 1, 1), "Confraternização Universal" },
                    { new Guid("20360225-0000-0000-0000-000000000000"), new DateOnly(2036, 2, 25), "Carnaval" },
                    { new Guid("20360226-0000-0000-0000-000000000000"), new DateOnly(2036, 2, 26), "Carnaval" },
                    { new Guid("20360411-0000-0000-0000-000000000000"), new DateOnly(2036, 4, 11), "Paixão de Cristo" },
                    { new Guid("20360421-0000-0000-0000-000000000000"), new DateOnly(2036, 4, 21), "Tiradentes" },
                    { new Guid("20360501-0000-0000-0000-000000000000"), new DateOnly(2036, 5, 1), "Dia do Trabalho" },
                    { new Guid("20360612-0000-0000-0000-000000000000"), new DateOnly(2036, 6, 12), "Corpus Christi" },
                    { new Guid("20360907-0000-0000-0000-000000000000"), new DateOnly(2036, 9, 7), "Independência do Brasil" },
                    { new Guid("20361012-0000-0000-0000-000000000000"), new DateOnly(2036, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20361102-0000-0000-0000-000000000000"), new DateOnly(2036, 11, 2), "Finados" },
                    { new Guid("20361115-0000-0000-0000-000000000000"), new DateOnly(2036, 11, 15), "Proclamação da República" },
                    { new Guid("20361120-0000-0000-0000-000000000000"), new DateOnly(2036, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20361225-0000-0000-0000-000000000000"), new DateOnly(2036, 12, 25), "Natal" },
                    { new Guid("20370101-0000-0000-0000-000000000000"), new DateOnly(2037, 1, 1), "Confraternização Universal" },
                    { new Guid("20370216-0000-0000-0000-000000000000"), new DateOnly(2037, 2, 16), "Carnaval" },
                    { new Guid("20370217-0000-0000-0000-000000000000"), new DateOnly(2037, 2, 17), "Carnaval" },
                    { new Guid("20370403-0000-0000-0000-000000000000"), new DateOnly(2037, 4, 3), "Paixão de Cristo" },
                    { new Guid("20370421-0000-0000-0000-000000000000"), new DateOnly(2037, 4, 21), "Tiradentes" },
                    { new Guid("20370501-0000-0000-0000-000000000000"), new DateOnly(2037, 5, 1), "Dia do Trabalho" },
                    { new Guid("20370604-0000-0000-0000-000000000000"), new DateOnly(2037, 6, 4), "Corpus Christi" },
                    { new Guid("20370907-0000-0000-0000-000000000000"), new DateOnly(2037, 9, 7), "Independência do Brasil" },
                    { new Guid("20371012-0000-0000-0000-000000000000"), new DateOnly(2037, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20371102-0000-0000-0000-000000000000"), new DateOnly(2037, 11, 2), "Finados" },
                    { new Guid("20371115-0000-0000-0000-000000000000"), new DateOnly(2037, 11, 15), "Proclamação da República" },
                    { new Guid("20371120-0000-0000-0000-000000000000"), new DateOnly(2037, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20371225-0000-0000-0000-000000000000"), new DateOnly(2037, 12, 25), "Natal" },
                    { new Guid("20380101-0000-0000-0000-000000000000"), new DateOnly(2038, 1, 1), "Confraternização Universal" },
                    { new Guid("20380308-0000-0000-0000-000000000000"), new DateOnly(2038, 3, 8), "Carnaval" },
                    { new Guid("20380309-0000-0000-0000-000000000000"), new DateOnly(2038, 3, 9), "Carnaval" },
                    { new Guid("20380421-0000-0000-0000-000000000000"), new DateOnly(2038, 4, 21), "Tiradentes" },
                    { new Guid("20380423-0000-0000-0000-000000000000"), new DateOnly(2038, 4, 23), "Paixão de Cristo" },
                    { new Guid("20380501-0000-0000-0000-000000000000"), new DateOnly(2038, 5, 1), "Dia do Trabalho" },
                    { new Guid("20380624-0000-0000-0000-000000000000"), new DateOnly(2038, 6, 24), "Corpus Christi" },
                    { new Guid("20380907-0000-0000-0000-000000000000"), new DateOnly(2038, 9, 7), "Independência do Brasil" },
                    { new Guid("20381012-0000-0000-0000-000000000000"), new DateOnly(2038, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20381102-0000-0000-0000-000000000000"), new DateOnly(2038, 11, 2), "Finados" },
                    { new Guid("20381115-0000-0000-0000-000000000000"), new DateOnly(2038, 11, 15), "Proclamação da República" },
                    { new Guid("20381120-0000-0000-0000-000000000000"), new DateOnly(2038, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20381225-0000-0000-0000-000000000000"), new DateOnly(2038, 12, 25), "Natal" },
                    { new Guid("20390101-0000-0000-0000-000000000000"), new DateOnly(2039, 1, 1), "Confraternização Universal" },
                    { new Guid("20390221-0000-0000-0000-000000000000"), new DateOnly(2039, 2, 21), "Carnaval" },
                    { new Guid("20390222-0000-0000-0000-000000000000"), new DateOnly(2039, 2, 22), "Carnaval" },
                    { new Guid("20390408-0000-0000-0000-000000000000"), new DateOnly(2039, 4, 8), "Paixão de Cristo" },
                    { new Guid("20390421-0000-0000-0000-000000000000"), new DateOnly(2039, 4, 21), "Tiradentes" },
                    { new Guid("20390501-0000-0000-0000-000000000000"), new DateOnly(2039, 5, 1), "Dia do Trabalho" },
                    { new Guid("20390609-0000-0000-0000-000000000000"), new DateOnly(2039, 6, 9), "Corpus Christi" },
                    { new Guid("20390907-0000-0000-0000-000000000000"), new DateOnly(2039, 9, 7), "Independência do Brasil" },
                    { new Guid("20391012-0000-0000-0000-000000000000"), new DateOnly(2039, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20391102-0000-0000-0000-000000000000"), new DateOnly(2039, 11, 2), "Finados" },
                    { new Guid("20391115-0000-0000-0000-000000000000"), new DateOnly(2039, 11, 15), "Proclamação da República" },
                    { new Guid("20391120-0000-0000-0000-000000000000"), new DateOnly(2039, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20391225-0000-0000-0000-000000000000"), new DateOnly(2039, 12, 25), "Natal" },
                    { new Guid("20400101-0000-0000-0000-000000000000"), new DateOnly(2040, 1, 1), "Confraternização Universal" },
                    { new Guid("20400213-0000-0000-0000-000000000000"), new DateOnly(2040, 2, 13), "Carnaval" },
                    { new Guid("20400214-0000-0000-0000-000000000000"), new DateOnly(2040, 2, 14), "Carnaval" },
                    { new Guid("20400330-0000-0000-0000-000000000000"), new DateOnly(2040, 3, 30), "Paixão de Cristo" },
                    { new Guid("20400421-0000-0000-0000-000000000000"), new DateOnly(2040, 4, 21), "Tiradentes" },
                    { new Guid("20400501-0000-0000-0000-000000000000"), new DateOnly(2040, 5, 1), "Dia do Trabalho" },
                    { new Guid("20400531-0000-0000-0000-000000000000"), new DateOnly(2040, 5, 31), "Corpus Christi" },
                    { new Guid("20400907-0000-0000-0000-000000000000"), new DateOnly(2040, 9, 7), "Independência do Brasil" },
                    { new Guid("20401012-0000-0000-0000-000000000000"), new DateOnly(2040, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20401102-0000-0000-0000-000000000000"), new DateOnly(2040, 11, 2), "Finados" },
                    { new Guid("20401115-0000-0000-0000-000000000000"), new DateOnly(2040, 11, 15), "Proclamação da República" },
                    { new Guid("20401120-0000-0000-0000-000000000000"), new DateOnly(2040, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20401225-0000-0000-0000-000000000000"), new DateOnly(2040, 12, 25), "Natal" },
                    { new Guid("20410101-0000-0000-0000-000000000000"), new DateOnly(2041, 1, 1), "Confraternização Universal" },
                    { new Guid("20410304-0000-0000-0000-000000000000"), new DateOnly(2041, 3, 4), "Carnaval" },
                    { new Guid("20410305-0000-0000-0000-000000000000"), new DateOnly(2041, 3, 5), "Carnaval" },
                    { new Guid("20410419-0000-0000-0000-000000000000"), new DateOnly(2041, 4, 19), "Paixão de Cristo" },
                    { new Guid("20410421-0000-0000-0000-000000000000"), new DateOnly(2041, 4, 21), "Tiradentes" },
                    { new Guid("20410501-0000-0000-0000-000000000000"), new DateOnly(2041, 5, 1), "Dia do Trabalho" },
                    { new Guid("20410620-0000-0000-0000-000000000000"), new DateOnly(2041, 6, 20), "Corpus Christi" },
                    { new Guid("20410907-0000-0000-0000-000000000000"), new DateOnly(2041, 9, 7), "Independência do Brasil" },
                    { new Guid("20411012-0000-0000-0000-000000000000"), new DateOnly(2041, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20411102-0000-0000-0000-000000000000"), new DateOnly(2041, 11, 2), "Finados" },
                    { new Guid("20411115-0000-0000-0000-000000000000"), new DateOnly(2041, 11, 15), "Proclamação da República" },
                    { new Guid("20411120-0000-0000-0000-000000000000"), new DateOnly(2041, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20411225-0000-0000-0000-000000000000"), new DateOnly(2041, 12, 25), "Natal" },
                    { new Guid("20420101-0000-0000-0000-000000000000"), new DateOnly(2042, 1, 1), "Confraternização Universal" },
                    { new Guid("20420217-0000-0000-0000-000000000000"), new DateOnly(2042, 2, 17), "Carnaval" },
                    { new Guid("20420218-0000-0000-0000-000000000000"), new DateOnly(2042, 2, 18), "Carnaval" },
                    { new Guid("20420404-0000-0000-0000-000000000000"), new DateOnly(2042, 4, 4), "Paixão de Cristo" },
                    { new Guid("20420421-0000-0000-0000-000000000000"), new DateOnly(2042, 4, 21), "Tiradentes" },
                    { new Guid("20420501-0000-0000-0000-000000000000"), new DateOnly(2042, 5, 1), "Dia do Trabalho" },
                    { new Guid("20420605-0000-0000-0000-000000000000"), new DateOnly(2042, 6, 5), "Corpus Christi" },
                    { new Guid("20420907-0000-0000-0000-000000000000"), new DateOnly(2042, 9, 7), "Independência do Brasil" },
                    { new Guid("20421012-0000-0000-0000-000000000000"), new DateOnly(2042, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20421102-0000-0000-0000-000000000000"), new DateOnly(2042, 11, 2), "Finados" },
                    { new Guid("20421115-0000-0000-0000-000000000000"), new DateOnly(2042, 11, 15), "Proclamação da República" },
                    { new Guid("20421120-0000-0000-0000-000000000000"), new DateOnly(2042, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20421225-0000-0000-0000-000000000000"), new DateOnly(2042, 12, 25), "Natal" },
                    { new Guid("20430101-0000-0000-0000-000000000000"), new DateOnly(2043, 1, 1), "Confraternização Universal" },
                    { new Guid("20430209-0000-0000-0000-000000000000"), new DateOnly(2043, 2, 9), "Carnaval" },
                    { new Guid("20430210-0000-0000-0000-000000000000"), new DateOnly(2043, 2, 10), "Carnaval" },
                    { new Guid("20430327-0000-0000-0000-000000000000"), new DateOnly(2043, 3, 27), "Paixão de Cristo" },
                    { new Guid("20430421-0000-0000-0000-000000000000"), new DateOnly(2043, 4, 21), "Tiradentes" },
                    { new Guid("20430501-0000-0000-0000-000000000000"), new DateOnly(2043, 5, 1), "Dia do Trabalho" },
                    { new Guid("20430528-0000-0000-0000-000000000000"), new DateOnly(2043, 5, 28), "Corpus Christi" },
                    { new Guid("20430907-0000-0000-0000-000000000000"), new DateOnly(2043, 9, 7), "Independência do Brasil" },
                    { new Guid("20431012-0000-0000-0000-000000000000"), new DateOnly(2043, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20431102-0000-0000-0000-000000000000"), new DateOnly(2043, 11, 2), "Finados" },
                    { new Guid("20431115-0000-0000-0000-000000000000"), new DateOnly(2043, 11, 15), "Proclamação da República" },
                    { new Guid("20431120-0000-0000-0000-000000000000"), new DateOnly(2043, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20431225-0000-0000-0000-000000000000"), new DateOnly(2043, 12, 25), "Natal" },
                    { new Guid("20440101-0000-0000-0000-000000000000"), new DateOnly(2044, 1, 1), "Confraternização Universal" },
                    { new Guid("20440229-0000-0000-0000-000000000000"), new DateOnly(2044, 2, 29), "Carnaval" },
                    { new Guid("20440301-0000-0000-0000-000000000000"), new DateOnly(2044, 3, 1), "Carnaval" },
                    { new Guid("20440415-0000-0000-0000-000000000000"), new DateOnly(2044, 4, 15), "Paixão de Cristo" },
                    { new Guid("20440421-0000-0000-0000-000000000000"), new DateOnly(2044, 4, 21), "Tiradentes" },
                    { new Guid("20440501-0000-0000-0000-000000000000"), new DateOnly(2044, 5, 1), "Dia do Trabalho" },
                    { new Guid("20440616-0000-0000-0000-000000000000"), new DateOnly(2044, 6, 16), "Corpus Christi" },
                    { new Guid("20440907-0000-0000-0000-000000000000"), new DateOnly(2044, 9, 7), "Independência do Brasil" },
                    { new Guid("20441012-0000-0000-0000-000000000000"), new DateOnly(2044, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20441102-0000-0000-0000-000000000000"), new DateOnly(2044, 11, 2), "Finados" },
                    { new Guid("20441115-0000-0000-0000-000000000000"), new DateOnly(2044, 11, 15), "Proclamação da República" },
                    { new Guid("20441120-0000-0000-0000-000000000000"), new DateOnly(2044, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20441225-0000-0000-0000-000000000000"), new DateOnly(2044, 12, 25), "Natal" },
                    { new Guid("20450101-0000-0000-0000-000000000000"), new DateOnly(2045, 1, 1), "Confraternização Universal" },
                    { new Guid("20450220-0000-0000-0000-000000000000"), new DateOnly(2045, 2, 20), "Carnaval" },
                    { new Guid("20450221-0000-0000-0000-000000000000"), new DateOnly(2045, 2, 21), "Carnaval" },
                    { new Guid("20450407-0000-0000-0000-000000000000"), new DateOnly(2045, 4, 7), "Paixão de Cristo" },
                    { new Guid("20450421-0000-0000-0000-000000000000"), new DateOnly(2045, 4, 21), "Tiradentes" },
                    { new Guid("20450501-0000-0000-0000-000000000000"), new DateOnly(2045, 5, 1), "Dia do Trabalho" },
                    { new Guid("20450608-0000-0000-0000-000000000000"), new DateOnly(2045, 6, 8), "Corpus Christi" },
                    { new Guid("20450907-0000-0000-0000-000000000000"), new DateOnly(2045, 9, 7), "Independência do Brasil" },
                    { new Guid("20451012-0000-0000-0000-000000000000"), new DateOnly(2045, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20451102-0000-0000-0000-000000000000"), new DateOnly(2045, 11, 2), "Finados" },
                    { new Guid("20451115-0000-0000-0000-000000000000"), new DateOnly(2045, 11, 15), "Proclamação da República" },
                    { new Guid("20451120-0000-0000-0000-000000000000"), new DateOnly(2045, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20451225-0000-0000-0000-000000000000"), new DateOnly(2045, 12, 25), "Natal" },
                    { new Guid("20460101-0000-0000-0000-000000000000"), new DateOnly(2046, 1, 1), "Confraternização Universal" },
                    { new Guid("20460205-0000-0000-0000-000000000000"), new DateOnly(2046, 2, 5), "Carnaval" },
                    { new Guid("20460206-0000-0000-0000-000000000000"), new DateOnly(2046, 2, 6), "Carnaval" },
                    { new Guid("20460323-0000-0000-0000-000000000000"), new DateOnly(2046, 3, 23), "Paixão de Cristo" },
                    { new Guid("20460421-0000-0000-0000-000000000000"), new DateOnly(2046, 4, 21), "Tiradentes" },
                    { new Guid("20460501-0000-0000-0000-000000000000"), new DateOnly(2046, 5, 1), "Dia do Trabalho" },
                    { new Guid("20460524-0000-0000-0000-000000000000"), new DateOnly(2046, 5, 24), "Corpus Christi" },
                    { new Guid("20460907-0000-0000-0000-000000000000"), new DateOnly(2046, 9, 7), "Independência do Brasil" },
                    { new Guid("20461012-0000-0000-0000-000000000000"), new DateOnly(2046, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20461102-0000-0000-0000-000000000000"), new DateOnly(2046, 11, 2), "Finados" },
                    { new Guid("20461115-0000-0000-0000-000000000000"), new DateOnly(2046, 11, 15), "Proclamação da República" },
                    { new Guid("20461120-0000-0000-0000-000000000000"), new DateOnly(2046, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20461225-0000-0000-0000-000000000000"), new DateOnly(2046, 12, 25), "Natal" },
                    { new Guid("20470101-0000-0000-0000-000000000000"), new DateOnly(2047, 1, 1), "Confraternização Universal" },
                    { new Guid("20470225-0000-0000-0000-000000000000"), new DateOnly(2047, 2, 25), "Carnaval" },
                    { new Guid("20470226-0000-0000-0000-000000000000"), new DateOnly(2047, 2, 26), "Carnaval" },
                    { new Guid("20470412-0000-0000-0000-000000000000"), new DateOnly(2047, 4, 12), "Paixão de Cristo" },
                    { new Guid("20470421-0000-0000-0000-000000000000"), new DateOnly(2047, 4, 21), "Tiradentes" },
                    { new Guid("20470501-0000-0000-0000-000000000000"), new DateOnly(2047, 5, 1), "Dia do Trabalho" },
                    { new Guid("20470613-0000-0000-0000-000000000000"), new DateOnly(2047, 6, 13), "Corpus Christi" },
                    { new Guid("20470907-0000-0000-0000-000000000000"), new DateOnly(2047, 9, 7), "Independência do Brasil" },
                    { new Guid("20471012-0000-0000-0000-000000000000"), new DateOnly(2047, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20471102-0000-0000-0000-000000000000"), new DateOnly(2047, 11, 2), "Finados" },
                    { new Guid("20471115-0000-0000-0000-000000000000"), new DateOnly(2047, 11, 15), "Proclamação da República" },
                    { new Guid("20471120-0000-0000-0000-000000000000"), new DateOnly(2047, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20471225-0000-0000-0000-000000000000"), new DateOnly(2047, 12, 25), "Natal" },
                    { new Guid("20480101-0000-0000-0000-000000000000"), new DateOnly(2048, 1, 1), "Confraternização Universal" },
                    { new Guid("20480217-0000-0000-0000-000000000000"), new DateOnly(2048, 2, 17), "Carnaval" },
                    { new Guid("20480218-0000-0000-0000-000000000000"), new DateOnly(2048, 2, 18), "Carnaval" },
                    { new Guid("20480403-0000-0000-0000-000000000000"), new DateOnly(2048, 4, 3), "Paixão de Cristo" },
                    { new Guid("20480421-0000-0000-0000-000000000000"), new DateOnly(2048, 4, 21), "Tiradentes" },
                    { new Guid("20480501-0000-0000-0000-000000000000"), new DateOnly(2048, 5, 1), "Dia do Trabalho" },
                    { new Guid("20480604-0000-0000-0000-000000000000"), new DateOnly(2048, 6, 4), "Corpus Christi" },
                    { new Guid("20480907-0000-0000-0000-000000000000"), new DateOnly(2048, 9, 7), "Independência do Brasil" },
                    { new Guid("20481012-0000-0000-0000-000000000000"), new DateOnly(2048, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20481102-0000-0000-0000-000000000000"), new DateOnly(2048, 11, 2), "Finados" },
                    { new Guid("20481115-0000-0000-0000-000000000000"), new DateOnly(2048, 11, 15), "Proclamação da República" },
                    { new Guid("20481120-0000-0000-0000-000000000000"), new DateOnly(2048, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20481225-0000-0000-0000-000000000000"), new DateOnly(2048, 12, 25), "Natal" },
                    { new Guid("20490101-0000-0000-0000-000000000000"), new DateOnly(2049, 1, 1), "Confraternização Universal" },
                    { new Guid("20490301-0000-0000-0000-000000000000"), new DateOnly(2049, 3, 1), "Carnaval" },
                    { new Guid("20490302-0000-0000-0000-000000000000"), new DateOnly(2049, 3, 2), "Carnaval" },
                    { new Guid("20490416-0000-0000-0000-000000000000"), new DateOnly(2049, 4, 16), "Paixão de Cristo" },
                    { new Guid("20490421-0000-0000-0000-000000000000"), new DateOnly(2049, 4, 21), "Tiradentes" },
                    { new Guid("20490501-0000-0000-0000-000000000000"), new DateOnly(2049, 5, 1), "Dia do Trabalho" },
                    { new Guid("20490617-0000-0000-0000-000000000000"), new DateOnly(2049, 6, 17), "Corpus Christi" },
                    { new Guid("20490907-0000-0000-0000-000000000000"), new DateOnly(2049, 9, 7), "Independência do Brasil" },
                    { new Guid("20491012-0000-0000-0000-000000000000"), new DateOnly(2049, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20491102-0000-0000-0000-000000000000"), new DateOnly(2049, 11, 2), "Finados" },
                    { new Guid("20491115-0000-0000-0000-000000000000"), new DateOnly(2049, 11, 15), "Proclamação da República" },
                    { new Guid("20491120-0000-0000-0000-000000000000"), new DateOnly(2049, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20491225-0000-0000-0000-000000000000"), new DateOnly(2049, 12, 25), "Natal" },
                    { new Guid("20500101-0000-0000-0000-000000000000"), new DateOnly(2050, 1, 1), "Confraternização Universal" },
                    { new Guid("20500221-0000-0000-0000-000000000000"), new DateOnly(2050, 2, 21), "Carnaval" },
                    { new Guid("20500222-0000-0000-0000-000000000000"), new DateOnly(2050, 2, 22), "Carnaval" },
                    { new Guid("20500408-0000-0000-0000-000000000000"), new DateOnly(2050, 4, 8), "Paixão de Cristo" },
                    { new Guid("20500421-0000-0000-0000-000000000000"), new DateOnly(2050, 4, 21), "Tiradentes" },
                    { new Guid("20500501-0000-0000-0000-000000000000"), new DateOnly(2050, 5, 1), "Dia do Trabalho" },
                    { new Guid("20500609-0000-0000-0000-000000000000"), new DateOnly(2050, 6, 9), "Corpus Christi" },
                    { new Guid("20500907-0000-0000-0000-000000000000"), new DateOnly(2050, 9, 7), "Independência do Brasil" },
                    { new Guid("20501012-0000-0000-0000-000000000000"), new DateOnly(2050, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil" },
                    { new Guid("20501102-0000-0000-0000-000000000000"), new DateOnly(2050, 11, 2), "Finados" },
                    { new Guid("20501115-0000-0000-0000-000000000000"), new DateOnly(2050, 11, 15), "Proclamação da República" },
                    { new Guid("20501120-0000-0000-0000-000000000000"), new DateOnly(2050, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra" },
                    { new Guid("20501225-0000-0000-0000-000000000000"), new DateOnly(2050, 12, 25), "Natal" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20310101-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20310224-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20310225-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20310411-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20310421-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20310501-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20310612-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20310907-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20311012-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20311102-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20311115-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20311120-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20311225-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20320101-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20320209-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20320210-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20320326-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20320421-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20320501-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20320527-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20320907-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20321012-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20321102-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20321115-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20321120-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20321225-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20330101-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20330228-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20330301-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20330415-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20330421-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20330501-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20330616-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20330907-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20331012-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20331102-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20331115-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20331120-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20331225-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20340101-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20340220-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20340221-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20340407-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20340421-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20340501-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20340608-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20340907-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20341012-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20341102-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20341115-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20341120-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20341225-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20350101-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20350205-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20350206-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20350323-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20350421-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20350501-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20350524-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20350907-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20351012-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20351102-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20351115-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20351120-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20351225-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20360101-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20360225-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20360226-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20360411-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20360421-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20360501-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20360612-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20360907-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20361012-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20361102-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20361115-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20361120-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20361225-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20370101-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20370216-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20370217-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20370403-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20370421-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20370501-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20370604-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20370907-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20371012-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20371102-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20371115-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20371120-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20371225-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20380101-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20380308-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20380309-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20380421-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20380423-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20380501-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20380624-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20380907-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20381012-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20381102-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20381115-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20381120-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20381225-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20390101-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20390221-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20390222-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20390408-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20390421-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20390501-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20390609-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20390907-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20391012-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20391102-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20391115-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20391120-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20391225-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20400101-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20400213-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20400214-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20400330-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20400421-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20400501-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20400531-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20400907-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20401012-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20401102-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20401115-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20401120-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20401225-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20410101-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20410304-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20410305-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20410419-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20410421-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20410501-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20410620-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20410907-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20411012-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20411102-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20411115-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20411120-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20411225-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20420101-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20420217-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20420218-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20420404-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20420421-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20420501-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20420605-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20420907-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20421012-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20421102-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20421115-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20421120-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20421225-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20430101-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20430209-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20430210-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20430327-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20430421-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20430501-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20430528-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20430907-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20431012-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20431102-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20431115-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20431120-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20431225-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20440101-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20440229-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20440301-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20440415-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20440421-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20440501-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20440616-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20440907-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20441012-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20441102-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20441115-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20441120-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20441225-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20450101-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20450220-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20450221-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20450407-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20450421-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20450501-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20450608-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20450907-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20451012-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20451102-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20451115-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20451120-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20451225-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20460101-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20460205-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20460206-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20460323-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20460421-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20460501-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20460524-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20460907-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20461012-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20461102-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20461115-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20461120-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20461225-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20470101-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20470225-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20470226-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20470412-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20470421-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20470501-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20470613-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20470907-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20471012-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20471102-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20471115-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20471120-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20471225-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20480101-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20480217-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20480218-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20480403-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20480421-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20480501-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20480604-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20480907-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20481012-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20481102-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20481115-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20481120-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20481225-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20490101-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20490301-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20490302-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20490416-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20490421-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20490501-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20490617-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20490907-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20491012-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20491102-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20491115-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20491120-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20491225-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20500101-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20500221-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20500222-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20500408-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20500421-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20500501-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20500609-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20500907-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20501012-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20501102-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20501115-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20501120-0000-0000-0000-000000000000"));

            migrationBuilder.DeleteData(
                table: "holiday",
                keyColumn: "id",
                keyValue: new Guid("20501225-0000-0000-0000-000000000000"));
        }
    }
}
