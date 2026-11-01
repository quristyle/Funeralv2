using System.Data;
using System.Text;
using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>팀 공유 문서 — <c>projmng.wbs_docs</c> 와 붙인 문서 <c>projmng.wbs_doc_files</c>. 프로젝트마다 쪽이 여럿이다.</summary>
/// 지우기는 휴지통으로 보내는 것(deleted_at)이고 영영 지우기는 따로다. 붙인 HTML 은 디스크가 아니라 표에 담는다 — 컨테이너에 데이터 볼륨이 없다.
public sealed class WbsDocsService(IConfiguration configuration)
{
    /// <summary>한 장짜리 문서다. 이보다 크면 뭔가 잘못 골랐다.</summary>
    public const int MaxHtmlBytes = 8 * 1024 * 1024;

    /// <summary>한 쪽에 붙일 수 있는 장수. 탭으로 보이므로 그 이상은 사람이 못 고른다.</summary>
    public const int MaxFilesPerDoc = 30;

    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    private const string DocCols = """
        select id         as Id
             , title      as Title
             , content    as Content
             , sort_order as SortOrder
             , updated_by as UpdatedBy
             , to_char(updated_at at time zone 'Asia/Seoul', 'YYYY-MM-DD HH24:MI') as UpdatedAt
             , created_by as CreatedBy
             , to_char(created_at at time zone 'Asia/Seoul', 'YYYY-MM-DD HH24:MI') as CreatedAt
             , deleted_by as DeletedBy
             , to_char(deleted_at at time zone 'Asia/Seoul', 'YYYY-MM-DD HH24:MI') as DeletedAt
          from projmng.wbs_docs
        """;

    public async Task<List<WbsBoardDoc>> ListAsync(int prjRid)
    {
        using var db = Open();

        var rows = await db.QueryAsync<WbsBoardDoc>(DocCols + """
             where prj_rid = @prjRid and deleted_at is null
             order by sort_order, id
            """, new { prjRid });

        return [.. rows];
    }

    /// <summary>휴지통. 본문은 싣지 않는다 — 목록에 필요 없다.</summary>
    public async Task<List<WbsBoardDoc>> TrashAsync(int prjRid)
    {
        using var db = Open();

        var rows = await db.QueryAsync<WbsBoardDoc>(DocCols.Replace("content    as Content", "''         as Content") + """
             where prj_rid = @prjRid and deleted_at is not null
             order by deleted_at desc, id
            """, new { prjRid });

        return [.. rows];
    }

    public async Task<int> CreateAsync(int prjRid, WbsBoardDoc doc, string? userId)
    {
        using var db = Open();

        return await db.ExecuteScalarAsync<int>("""
            insert into projmng.wbs_docs (prj_rid, title, content, sort_order, updated_by, created_by, created_at)
            values (@prjRid, @Title, @content, @SortOrder, @userId, @userId, now())
            returning id
            """, new
        {
            prjRid,
            doc.Title,
            content = doc.Content ?? "",

            // 차례를 안 주면 맨 뒤로 보낸다. 0 으로 두면 새 쪽이 맨 앞에 끼어들어 목차가 뒤집힌다.
            SortOrder = doc.SortOrder == 0 ? 100 : doc.SortOrder,
            userId,
        });
    }

    /// <summary>담겨 온 것만 고친다. 안 담긴 칸은 그대로 둔다 — 목록에서 옮길 때 본문이 빈 글자로 덮이면 안 된다.</summary>
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
             where prj_rid = @prjRid and id = @id and deleted_at is null
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

    /// <summary>목차 차례만 다시 매긴다. updated_at 을 건드리지 않는다 — 올리면 옮긴 쪽 전부가 모두에게 「변경됨」으로 뜬다. 한 묶음으로 넣는다(중간에 끊기면 차례가 반쯤 섞인다).</summary>
    public async Task<int> ReorderAsync(int prjRid, IReadOnlyList<int> ids)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var n = 0;

        for (var i = 0; i < ids.Count; i++)
        {
            n += await conn.ExecuteAsync(
                "update projmng.wbs_docs set sort_order = @order where prj_rid = @prjRid and id = @id",
                new { order = (i + 1) * 10, prjRid, id = ids[i] }, tx);
        }

        await tx.CommitAsync();
        return n;
    }

    /// <summary>휴지통으로 보낸다. 붙인 문서는 그대로 둔다 — 되살렸을 때 빈 탭이 남으면 안 된다.</summary>
    public async Task<bool> TrashAsync(int prjRid, int id, string? userId)
    {
        using var db = Open();

        return await db.ExecuteAsync("""
            update projmng.wbs_docs set deleted_at = now(), deleted_by = @userId
             where prj_rid = @prjRid and id = @id and deleted_at is null
            """, new { prjRid, id, userId }) > 0;
    }

    /// <summary>되살린다. 차례를 그대로 두었으므로 지운 자리로 돌아간다.</summary>
    public async Task<bool> RestoreAsync(int prjRid, int id)
    {
        using var db = Open();

        return await db.ExecuteAsync("""
            update projmng.wbs_docs set deleted_at = null, deleted_by = null
             where prj_rid = @prjRid and id = @id and deleted_at is not null
            """, new { prjRid, id }) > 0;
    }

    /// <summary>영영 지운다. 휴지통에 있는 쪽만 — 살아 있는 쪽을 곧장 지우는 길은 두지 않는다. 붙인 문서는 FK 가 함께 지운다.</summary>
    public async Task<bool> PurgeAsync(int prjRid, int id)
    {
        using var db = Open();

        return await db.ExecuteAsync(
            "delete from projmng.wbs_docs where prj_rid = @prjRid and id = @id and deleted_at is not null",
            new { prjRid, id }) > 0;
    }

    // ── 붙인 문서 ──────────────────────────────────────────────

    private const string FileCols = """
        select f.id          as Id
             , f.doc_id      as DocId
             , f.name        as Name
             , f.url         as Url
             , f.bytes       as Bytes
             , f.sort_order  as SortOrder
             , f.uploaded_by as UploadedBy
             , to_char(f.uploaded_at at time zone 'Asia/Seoul', 'YYYY-MM-DD HH24:MI') as UploadedAt
          from projmng.wbs_doc_files f
          join projmng.wbs_docs d on d.id = f.doc_id
        """;

    /// <summary>프로젝트의 붙인 문서 전부. 쪽마다 물으면 스무 번을 묻게 된다 — 화면이 한 번 받아 쪽별로 가른다.</summary>
    public async Task<List<WbsBoardDocFile>> FilesAsync(int prjRid)
    {
        using var db = Open();

        var rows = await db.QueryAsync<WbsBoardDocFile>(FileCols + """
             where d.prj_rid = @prjRid and d.deleted_at is null
             order by f.doc_id, f.sort_order, f.id
            """, new { prjRid });

        return [.. rows];
    }

    /// <summary>올린 HTML 한 장. 주소로 건 줄이면 url 만 채워 돌려준다.</summary>
    public async Task<DocFileBody?> ContentAsync(int prjRid, int docId, int fileId)
    {
        using var db = Open();

        var row = await db.QuerySingleOrDefaultAsync<DocFileBody>("""
            select f.html as Html, f.url as Url
              from projmng.wbs_doc_files f
              join projmng.wbs_docs d on d.id = f.doc_id
             where d.prj_rid = @prjRid and f.doc_id = @docId and f.id = @fileId
            """, new { prjRid, docId, fileId });

        return row;
    }

    /// <summary>한 장을 붙인다. 있던 것 뒤에 선다. 쪽이 없거나 장수를 넘으면 null 과 까닭을 준다.</summary>
    public async Task<(WbsBoardDocFile? File, string? Error)> AddFileAsync(
        int prjRid, int docId, string name, string? url, string? html, string? userId)
    {
        using var db = Open();

        var have = await db.QuerySingleOrDefaultAsync<DocFileCount>("""
            select count(f.id) as Count, coalesce(max(f.sort_order), 0) as MaxOrder
              from projmng.wbs_docs d
              left join projmng.wbs_doc_files f on f.doc_id = d.id
             where d.prj_rid = @prjRid and d.id = @docId and d.deleted_at is null
             group by d.id
            """, new { prjRid, docId });

        if (have is not { } h) return (null, "그 쪽을 찾지 못했습니다.");
        if (h.Count >= MaxFilesPerDoc) return (null, $"한 쪽에 {MaxFilesPerDoc}장까지입니다.");

        var id = await db.ExecuteScalarAsync<int>("""
            insert into projmng.wbs_doc_files (doc_id, name, url, html, bytes, sort_order, uploaded_by)
            values (@docId, @name, @url, @html, @bytes, @order, @userId)
            returning id
            """, new
        {
            docId,
            name,
            url,
            html,
            bytes = html is null ? 0L : Encoding.UTF8.GetByteCount(html),
            order = h.MaxOrder + 10,
            userId,
        });

        var file = await db.QuerySingleAsync<WbsBoardDocFile>(
            FileCols + " where f.id = @id", new { id });

        return (file, null);
    }

    public async Task<bool> RemoveFileAsync(int prjRid, int docId, int fileId)
    {
        using var db = Open();

        return await db.ExecuteAsync("""
            delete from projmng.wbs_doc_files f
             using projmng.wbs_docs d
             where d.id = f.doc_id and d.prj_rid = @prjRid and f.doc_id = @docId and f.id = @fileId
            """, new { prjRid, docId, fileId }) > 0;
    }

    /// <summary>받은 차례대로 탭 차례를 다시 매긴다.</summary>
    public async Task<int> ReorderFilesAsync(int prjRid, int docId, IReadOnlyList<int> ids)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var n = 0;

        for (var i = 0; i < ids.Count; i++)
        {
            n += await conn.ExecuteAsync("""
                update projmng.wbs_doc_files f set sort_order = @order
                  from projmng.wbs_docs d
                 where d.id = f.doc_id and d.prj_rid = @prjRid and f.doc_id = @docId and f.id = @id
                """, new { order = (i + 1) * 10, prjRid, docId, id = ids[i] }, tx);
        }

        await tx.CommitAsync();
        return n;
    }

    public sealed class DocFileBody
    {
        public string? Html { get; set; }
        public string? Url { get; set; }
    }

    private sealed class DocFileCount
    {
        public long Count { get; set; }
        public int MaxOrder { get; set; }
    }
}
