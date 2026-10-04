using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropLink.Infrastructure.Data.Migrations;

public partial class AddPropertyRecommendationAttributes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"Properties\" ADD COLUMN IF NOT EXISTS \"ParkingAvailable\" boolean;");
        migrationBuilder.Sql("ALTER TABLE \"Properties\" ADD COLUMN IF NOT EXISTS \"NearSchoolOrUniversity\" boolean;");
        migrationBuilder.Sql("ALTER TABLE \"Properties\" ADD COLUMN IF NOT EXISTS \"NearHospital\" boolean;");
        migrationBuilder.Sql("ALTER TABLE \"Properties\" ADD COLUMN IF NOT EXISTS \"NearPublicTransport\" boolean;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"Properties\" DROP COLUMN IF EXISTS \"NearPublicTransport\";");
        migrationBuilder.Sql("ALTER TABLE \"Properties\" DROP COLUMN IF EXISTS \"NearHospital\";");
        migrationBuilder.Sql("ALTER TABLE \"Properties\" DROP COLUMN IF EXISTS \"NearSchoolOrUniversity\";");
        migrationBuilder.Sql("ALTER TABLE \"Properties\" DROP COLUMN IF EXISTS \"ParkingAvailable\";");
    }
}
