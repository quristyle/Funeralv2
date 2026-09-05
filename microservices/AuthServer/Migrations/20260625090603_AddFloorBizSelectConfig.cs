using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthServer.Migrations
{
    /// <inheritdoc />
    public partial class AddFloorBizSelectConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 건물(building) BizSelect 메타정보 - 회사ID 파라미터로 해당 회사 소속 건물 목록 조회
            // 이미 DB에 존재할 수 있으므로 ON CONFLICT DO NOTHING 처리
            migrationBuilder.Sql(@"
                INSERT INTO scom.biz_select_configs
                    (id, biz_type, api_url, http_method, label_field, value_field, result_path, processor_type, remark, created_at, is_deleted)
                VALUES
                    (gen_random_uuid()::text, 'building', '/funeral/building/info/list', 'GET', 'name', 'id', 'result', NULL, '건물 목록 조회 (companyId 파라미터 종속)', NOW() AT TIME ZONE 'UTC', false)
                ON CONFLICT DO NOTHING;
            ");

            // 층(floor) BizSelect 메타정보 - 건물ID 파라미터로 해당 건물 소속 층 목록 조회
            migrationBuilder.Sql(@"
                INSERT INTO scom.biz_select_configs
                    (id, biz_type, api_url, http_method, label_field, value_field, result_path, processor_type, remark, created_at, is_deleted)
                VALUES
                    (gen_random_uuid()::text, 'floor', '/funeral/building/floor/list', 'GET', 'name', 'id', 'result', NULL, '층 목록 조회 (buildingId 파라미터 종속)', NOW() AT TIME ZONE 'UTC', false)
                ON CONFLICT DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 롤백 시 해당 biz_type 설정 삭제
            migrationBuilder.Sql("DELETE FROM scom.biz_select_configs WHERE biz_type IN ('building', 'floor');");
        }
    }
}
