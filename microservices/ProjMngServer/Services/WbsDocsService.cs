using System.Data;
using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// 팀 공유 문서 — <c>projmng.wbs_docs</c>. 프로젝트마다 쪽이 여럿이다.
/// </summary>
/// <remarks>
/// 본문은 서식 편집기가 만든 HTML 이라 <b>그대로 담고 그대로 돌려준다.</b>
/// 붙여넣은 그림은 첨부 서버로 가고 본문에는 주소만 남는다.
/// </remarks>
public sealed class WbsDocsService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    public async Task<List<WbsBoardDoc>> ListAsync(int prjRid)
    {
        using var db = Open();

        var rows = await db.QueryAsync<WbsBoardDoc>("""
            select id         as Id
                 , title      as Title
                 , content    as Content
                 , sort_order as SortOrder
                 , updated_by as UpdatedBy
                 , to_char(updated_at, 'YYYY-MM-DD HH24:MI') as UpdatedAt
              from projmng.wbs_docs
             where prj_rid = @prjRid
             order by sort_order, id
            """, new { prjRid });

        return [.. rows];
    }

    public async Task<int> CreateAsync(int prjRid, WbsBoardDoc doc, string? userId)
    {
        using var db = Open();

        return await db.ExecuteScalarAsync<int>("""
            insert into projmng.wbs_docs (prj_rid, title, content, sort_order, updated_by)
            values (@prjRid, @Title, @content, @SortOrder, @userId)
            returning id
            """, new
        {
            prjRid,
            doc.Title,
            content = doc.Content ?? "",

            // 차례를 안 주면 맨 뒤로 보낸다. 0 으로 두면 새 쪽이 맨 앞에 끼어들어
            // 목차가 뒤집힌다.
            SortOrder = doc.SortOrder == 0 ? 100 : doc.SortOrder,
            userId,
        });
    }

    /// <summary>
    /// 담겨 온 것만 고친다 — <b>안 담긴 칸은 그대로 둔다.</b> 목차에서 차례만
    /// 옮길 때 본문이 빈 글자로 덮이면 안 된다.
    /// </summary>
    public async Task<int> UpdateAsync(int prjRid, int id, WbsBoardDoc doc, string? userId)
    {
        using var db = Open();

        return await db.ExecuteAsync("""
            update projmng.wbs_docs
               set title      = coalesce(@Title, title)
                 , content    = coalesce(@Content, content)
                 , sort_order = coalesce(@sortOrder, sort_order)
                 , updated_by = coalesce(@userId, updated_by)
                 , updated_at = now()
             where prj_rid = @prjRid and id = @id
            """, new
        {
            prjRid,
            id,
            doc.Title,
            doc.Content,
            sortOrder = doc.SortOrder == 0 ? (int?)null : doc.SortOrder,
            userId,
        });
    }

    public async Task<bool> DeleteAsync(int prjRid, int id)
    {
        using var db = Open();

        var affected = await db.ExecuteAsync(
            "delete from projmng.wbs_docs where prj_rid = @prjRid and id = @id",
            new { prjRid, id });

        return affected > 0;
    }
}
