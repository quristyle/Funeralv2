using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// EAI 인터페이스 카탈로그 — <c>projmng.if_*</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>조회는 뷰, 편집은 원본 표</b>다. 뷰가 코드값을 이름으로 풀고 단계·메모를
/// 세어 주므로 목록 화면이 조인을 다시 짜지 않는다.
/// </para>
///
/// <para>
/// <c>updated_at</c> 은 <b>보내지 않는다</b> — DB 트리거가 찍는다. 어느 길로
/// 고쳐도 빠지지 않고, 화면이 보낸 시각과 서버 시각이 어긋날 일도 없다.
/// </para>
///
/// <para>
/// [프로젝트는 뿌리에서만 본다]
/// </para>
///
/// <para>
/// <c>prj_rid</c> 는 <c>if_master</c>·<c>if_system</c>·<c>if_attr_def</c> 셋에만
/// 있다. 단계·메모·항목값은 <b>인터페이스를 타고</b> 걸러진다 — 그래서 이 서비스의
/// 단계·메모 조회는 <c>ifId</c> 를 받되 <b>그 인터페이스가 이 프로젝트 것인지
/// 먼저 확인한다</b>(<see cref="OwnsAsync"/>). 확인을 빠뜨리면 번호만 알면
/// 남의 프로젝트 단계를 읽고 고칠 수 있다.
/// </para>
/// </remarks>
public sealed class InterfaceService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private NpgsqlConnection Open() => new(_connectionString);

    // ──────────────────────────────────────────── 고칠 수 있는 칸

    private static readonly string[] MasterCols =
    [
        "if_cd", "if_nm", "if_desc", "direction_cd", "src_system_id", "tgt_system_id",
        "domain_cd", "trigger_cd", "cycle_cd", "status_cd", "owner_nm", "owner_bp_id",
        "plan_sdt", "plan_edt", "open_dt", "remark", "ext", "sort_order", "use_yn",
    ];

    private static readonly string[] StepCols =
    [
        "step_no", "step_nm", "step_type_cd", "system_id", "object_owner", "object_nm",
        "object_type", "step_desc", "params", "ext", "use_yn",
    ];

    private static readonly string[] NoteCols =
    [
        "step_id", "note_type_cd", "title", "content", "writer_nm", "note_dt",
        "done_yn", "sort_order", "ext",
    ];

    /// <summary>
    /// 칸마다 붙일 형변환. <b>전부 <c>::text</c> 로 넘기면 안 된다</b> —
    /// 숫자·날짜·<c>jsonb</c> 칸이 섞여 있다.
    /// </summary>
    private static string Cast(string col) => col switch
    {
        "src_system_id" or "tgt_system_id" or "system_id" or "step_id"
            or "step_no" or "sort_order" => "::int",
        "plan_sdt" or "plan_edt" or "open_dt" or "note_dt" => "::date",
        "params" or "ext" => "::jsonb",
        _ => "::text",
    };

    /// <summary>본문에서 허용한 칸만 뽑는다.</summary>
    private static List<(string Col, object? Val)> Pick(
        IDictionary<string, object?> body, string[] allow)
    {
        var set = new HashSet<string>(allow, StringComparer.OrdinalIgnoreCase);
        var list = new List<(string, object?)>();

        foreach (var (key, raw) in body)
        {
            if (!set.Contains(key)) continue;

            // 불리언 칸이 DB 에서는 'Y'/'N' 글자다. 화면이 참·거짓으로 보내도 받는다.
            var val = raw switch
            {
                bool b => b ? "Y" : "N",
                string s when string.IsNullOrWhiteSpace(s) => null,
                _ => raw,
            };

            var col = key.ToLowerInvariant();

            // `ext`·`params` 는 NOT NULL 이고 기본값이 `{}` 다. 화면이 「비움」을
            // 보내는 것은 「없앤다」는 뜻이지 「줄을 못 넣게 한다」가 아니라,
            // 빈 값이면 기본값과 같은 것으로 바꿔 넣는다.
            if (val is null && col is "ext" or "params") val = "{}";

            list.Add((col, val));
        }

        return list;
    }

    private static DynamicParameters Args(List<(string Col, object? Val)> vals)
    {
        var args = new DynamicParameters();
        for (var i = 0; i < vals.Count; i++) args.Add($"p{i}", vals[i].Val);
        return args;
    }

    // ──────────────────────────────────────────── 코드 · 시스템

    public async Task<List<IfCodeRow>> CodesAsync(string? grp)
    {
        using var db = Open();

        var rows = await db.QueryAsync<IfCodeRow>("""
            select code_grp   as CodeGrp
                 , code       as Code
                 , code_nm    as CodeNm
                 , code_desc  as CodeDesc
                 , sort_order as SortOrder
              from projmng.if_code
             where use_yn = 'Y'
               and (@grp::text is null or code_grp = @grp)
             order by code_grp, sort_order, code
            """, new { grp = WbsBoardSql.Nz(grp) });

        return [.. rows];
    }

    public async Task<List<IfSystemRow>> SystemsAsync(int prjRid)
    {
        using var db = Open();

        var rows = await db.QueryAsync<IfSystemRow>("""
            select system_id   as SystemId
                 , system_cd   as SystemCd
                 , system_nm   as SystemNm
                 , system_kind as SystemKind
                 , host        as Host
                 , port        as Port
                 , db_nm       as DbNm
                 , schema_nm   as SchemaNm
                 , system_desc as SystemDesc
                 , sort_order  as SortOrder
              from projmng.if_system
             where prj_rid = @prjRid and use_yn = 'Y'
             order by sort_order, system_cd
            """, new { prjRid });

        return [.. rows];
    }

    // ──────────────────────────────────────────── 목록 · 상세

    public async Task<List<IfMasterRow>> ListAsync(
        int prjRid, string? status, string? domain, string? q, string? useYn)
    {
        using var db = Open();

        var rows = await db.QueryAsync<IfMasterRow>("""
            select m.if_id           as IfId
                 , m.if_cd           as IfCd
                 , m.if_nm           as IfNm
                 , m.domain_cd       as DomainCd
                 , m.direction_cd    as DirectionCd
                 , m.direction_nm    as DirectionNm
                 , m.src_system_nm   as SrcSystemNm
                 , m.tgt_system_nm   as TgtSystemNm
                 , m.status_cd       as StatusCd
                 , m.status_nm       as StatusNm
                 , m.cycle_cd        as CycleCd
                 , m.cycle_nm        as CycleNm
                 , m.owner_nm        as OwnerNm
                 , m.plan_sdt::text  as PlanSdt
                 , m.plan_edt::text  as PlanEdt
                 , m.open_dt::text   as OpenDt
                 , m.step_cnt::int   as StepCnt
                 , m.note_cnt::int   as NoteCnt
                 , m.open_issue_cnt::int as OpenIssueCnt
                 , m.remark          as Remark
                 , m.use_yn          as UseYn
                 , m.sort_order      as SortOrder
                 , to_char(m.updated_at, 'YYYY-MM-DD HH24:MI') as UpdatedAt
                 , m.updated_by      as UpdatedBy
                 , f.flow            as Flow
              from projmng.v_if_master m
              left join projmng.v_if_flow f on f.if_id = m.if_id
             where m.prj_rid = @prjRid
               and (@status::text is null or m.status_cd = @status)
               and (@domain::text is null or m.domain_cd = @domain)
               and (@q::text is null
                    or m.if_cd ilike '%' || @q || '%'
                    or m.if_nm ilike '%' || @q || '%')
               and (@useYn::text is null or m.use_yn = @useYn)
             order by m.sort_order, m.if_cd
            """, new
        {
            prjRid,
            status = WbsBoardSql.Nz(status),
            domain = WbsBoardSql.Nz(domain),
            q = WbsBoardSql.Nz(q),
            useYn = WbsBoardSql.Nz(useYn),
        });

        return [.. rows];
    }

    /// <summary>그 인터페이스가 이 프로젝트 것인가. <b>딸린 자료를 만지기 전에 늘 묻는다.</b></summary>
    public async Task<bool> OwnsAsync(int prjRid, int ifId)
    {
        using var db = Open();

        return await db.ExecuteScalarAsync<bool>("""
            select exists (select 1 from projmng.if_master
                            where prj_rid = @prjRid and if_id = @ifId)
            """, new { prjRid, ifId });
    }

    public async Task<IfDetail?> DetailAsync(int prjRid, int ifId)
    {
        using var db = Open();

        var master = await db.QuerySingleOrDefaultAsync<IfMasterDetail>("""
            select if_id          as IfId
                 , if_cd          as IfCd
                 , if_nm          as IfNm
                 , if_desc        as IfDesc
                 , direction_cd   as DirectionCd
                 , src_system_id  as SrcSystemId
                 , tgt_system_id  as TgtSystemId
                 , domain_cd      as DomainCd
                 , trigger_cd     as TriggerCd
                 , cycle_cd       as CycleCd
                 , status_cd      as StatusCd
                 , owner_nm       as OwnerNm
                 , owner_bp_id    as OwnerBpId
                 , plan_sdt::text as PlanSdt
                 , plan_edt::text as PlanEdt
                 , open_dt::text  as OpenDt
                 , remark         as Remark
                 , ext::text      as Ext
                 , sort_order     as SortOrder
                 , use_yn         as UseYn
                 , to_char(updated_at, 'YYYY-MM-DD HH24:MI') as UpdatedAt
                 , updated_by     as UpdatedBy
              from projmng.if_master
             where prj_rid = @prjRid and if_id = @ifId
            """, new { prjRid, ifId });

        if (master is null) return null;

        return new IfDetail
        {
            Master = master,
            Steps = await StepsAsync(ifId),
            Attrs = await AttrsAsync(ifId),
            Notes = await NotesAsync(ifId),
            Flow = await db.ExecuteScalarAsync<string?>(
                "select flow from projmng.v_if_flow where if_id = @ifId", new { ifId }),
        };
    }

    // ──────────────────────────────────────────── 기본정보 CRUD

    /// <returns>새 번호. 코드가 겹치면 <c>-1</c>.</returns>
    public async Task<int> CreateAsync(int prjRid, IDictionary<string, object?> body, string? who)
    {
        var vals = Pick(body, MasterCols);
        vals.Add(("created_by", who));
        vals.Add(("updated_by", who));

        var cols = string.Join(", ", new[] { "prj_rid" }.Concat(vals.Select(v => v.Col)));
        var phs = string.Join(", ",
            new[] { "@prjRid" }.Concat(vals.Select((v, i) => $"@p{i}{Cast(v.Col)}")));

        var args = Args(vals);
        args.Add("prjRid", prjRid);

        using var db = Open();

        try
        {
            return await db.ExecuteScalarAsync<int>(
                $"insert into projmng.if_master ({cols}) values ({phs}) returning if_id", args);
        }
        catch (PostgresException e) when (e.SqlState == "23505")
        {
            // 인터페이스 코드는 프로젝트 안에서 하나다(uk_if_master).
            return -1;
        }
    }

    /// <returns>고친 줄 수. 받을 칸이 없으면 <c>-1</c>.</returns>
    public async Task<int> UpdateAsync(
        int prjRid, int ifId, IDictionary<string, object?> body, string? who)
    {
        var vals = Pick(body, MasterCols);
        if (vals.Count == 0) return -1;

        vals.Add(("updated_by", who));

        var sets = string.Join(", ", vals.Select((v, i) => $"{v.Col} = @p{i}{Cast(v.Col)}"));
        var args = Args(vals);
        args.Add("prjRid", prjRid);
        args.Add("ifId", ifId);

        using var db = Open();

        return await db.ExecuteAsync($"""
            update projmng.if_master set {sets}
             where prj_rid = @prjRid and if_id = @ifId
            """, args);
    }

    /// <summary>지운다. 단계·메모·항목값은 <b>외래키가 함께 지운다</b>.</summary>
    public async Task<bool> DeleteAsync(int prjRid, int ifId)
    {
        using var db = Open();

        var affected = await db.ExecuteAsync(
            "delete from projmng.if_master where prj_rid = @prjRid and if_id = @ifId",
            new { prjRid, ifId });

        return affected > 0;
    }

    // ──────────────────────────────────────────── 처리 단계

    public async Task<List<IfStepRow>> StepsAsync(int ifId)
    {
        using var db = Open();

        var rows = await db.QueryAsync<IfStepRow>("""
            select step_id        as StepId
                 , if_id          as IfId
                 , step_no        as StepNo
                 , step_nm        as StepNm
                 , step_type_cd   as StepTypeCd
                 , step_type_nm   as StepTypeNm
                 , system_nm      as SystemNm
                 , object_owner   as ObjectOwner
                 , object_nm      as ObjectNm
                 , object_full_nm as ObjectFullNm
                 , object_type    as ObjectType
                 , step_desc      as StepDesc
                 , params::text   as Params
                 , ext::text      as Ext
                 , use_yn         as UseYn
                 , to_char(updated_at, 'YYYY-MM-DD HH24:MI') as UpdatedAt
              from projmng.v_if_step
             where if_id = @ifId
             order by step_no
            """, new { ifId });

        return [.. rows];
    }

    public async Task<int> CreateStepAsync(int ifId, IDictionary<string, object?> body, string? who)
    {
        var vals = Pick(body, StepCols);

        using var db = Open();

        // 차례를 안 주면 맨 뒤에 붙인다.
        if (!vals.Any(v => v.Col == "step_no"))
        {
            var next = await db.ExecuteScalarAsync<int>(
                "select coalesce(max(step_no), 0) + 1 from projmng.if_step where if_id = @ifId",
                new { ifId });
            vals.Add(("step_no", next));
        }

        vals.Add(("created_by", who));
        vals.Add(("updated_by", who));

        var cols = string.Join(", ", new[] { "if_id" }.Concat(vals.Select(v => v.Col)));
        var phs = string.Join(", ",
            new[] { "@ifId" }.Concat(vals.Select((v, i) => $"@p{i}{Cast(v.Col)}")));

        var args = Args(vals);
        args.Add("ifId", ifId);

        return await db.ExecuteScalarAsync<int>(
            $"insert into projmng.if_step ({cols}) values ({phs}) returning step_id", args);
    }

    public async Task<int> UpdateStepAsync(int stepId, IDictionary<string, object?> body, string? who)
    {
        var vals = Pick(body, StepCols);
        if (vals.Count == 0) return -1;

        vals.Add(("updated_by", who));

        var sets = string.Join(", ", vals.Select((v, i) => $"{v.Col} = @p{i}{Cast(v.Col)}"));
        var args = Args(vals);
        args.Add("stepId", stepId);

        using var db = Open();

        return await db.ExecuteAsync(
            $"update projmng.if_step set {sets} where step_id = @stepId", args);
    }

    public async Task<bool> DeleteStepAsync(int stepId)
    {
        using var db = Open();

        return await db.ExecuteAsync(
            "delete from projmng.if_step where step_id = @stepId", new { stepId }) > 0;
    }

    /// <summary>
    /// 차례를 통째로 다시 매긴다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>한 번에 못 바꾼다.</b> <c>(if_id, step_no)</c> 가 유일이라, 2번을 1번으로
    /// 옮기는 순간 아직 자리를 안 비운 1번과 부딪힌다. 그래서 세 걸음으로 간다 —
    /// ① 전부 음수로 대피 ② 받은 차례대로 1..N ③ 목록에 없던 단계는 그 뒤로.
    /// </para>
    ///
    /// <para>
    /// 세 걸음이 한 트랜잭션이다. 가운데서 끊기면 <b>차례가 전부 음수인 상태</b>로
    /// 남는데, 그 꼴은 화면에서 되돌릴 방법이 없다.
    /// </para>
    /// </remarks>
    public async Task<int> ReorderStepsAsync(int ifId, IReadOnlyList<int> stepIds)
    {
        using var db = Open();
        await db.OpenAsync();

        using var tx = await db.BeginTransactionAsync();

        await db.ExecuteAsync(
            "update projmng.if_step set step_no = -step_no where if_id = @ifId",
            new { ifId }, tx);

        for (var i = 0; i < stepIds.Count; i++)
        {
            await db.ExecuteAsync("""
                update projmng.if_step set step_no = @no
                 where step_id = @stepId and if_id = @ifId
                """, new { no = i + 1, stepId = stepIds[i], ifId }, tx);
        }

        await db.ExecuteAsync("""
            update projmng.if_step s
               set step_no = t.rn
              from (select step_id
                         , @count + row_number() over (order by -step_no) as rn
                      from projmng.if_step
                     where if_id = @ifId and step_no < 0) t
             where s.step_id = t.step_id
            """, new { ifId, count = stepIds.Count }, tx);

        await tx.CommitAsync();

        return stepIds.Count;
    }

    // ──────────────────────────────────────────── 메모 · 이슈

    public async Task<List<IfNoteRow>> NotesAsync(int ifId)
    {
        using var db = Open();

        var rows = await db.QueryAsync<IfNoteRow>("""
            select n.note_id      as NoteId
                 , n.if_id        as IfId
                 , n.step_id      as StepId
                 , s.step_nm      as StepNm
                 , n.note_type_cd as NoteTypeCd
                 , c.code_nm      as NoteTypeNm
                 , n.title        as Title
                 , n.content      as Content
                 , n.writer_nm    as WriterNm
                 , n.note_dt::text as NoteDt
                 , n.done_yn      as DoneYn
                 , n.sort_order   as SortOrder
                 , to_char(n.updated_at, 'YYYY-MM-DD HH24:MI') as UpdatedAt
              from projmng.if_note n
              left join projmng.if_step s on s.step_id = n.step_id
              left join projmng.if_code c on c.code_grp = 'NOTE_TYPE' and c.code = n.note_type_cd
             where n.if_id = @ifId
             -- 안 끝난 것이 위로. 그다음은 사람이 정한 차례, 마지막은 최신순.
             order by coalesce(n.done_yn, 'N'), n.sort_order, n.note_id desc
            """, new { ifId });

        return [.. rows];
    }

    public async Task<int> CreateNoteAsync(int ifId, IDictionary<string, object?> body, string? who)
    {
        var vals = Pick(body, NoteCols);
        vals.Add(("created_by", who));
        vals.Add(("updated_by", who));

        var cols = string.Join(", ", new[] { "if_id" }.Concat(vals.Select(v => v.Col)));
        var phs = string.Join(", ",
            new[] { "@ifId" }.Concat(vals.Select((v, i) => $"@p{i}{Cast(v.Col)}")));

        var args = Args(vals);
        args.Add("ifId", ifId);

        using var db = Open();

        return await db.ExecuteScalarAsync<int>(
            $"insert into projmng.if_note ({cols}) values ({phs}) returning note_id", args);
    }

    public async Task<int> UpdateNoteAsync(int noteId, IDictionary<string, object?> body, string? who)
    {
        var vals = Pick(body, NoteCols);
        if (vals.Count == 0) return -1;

        vals.Add(("updated_by", who));

        var sets = string.Join(", ", vals.Select((v, i) => $"{v.Col} = @p{i}{Cast(v.Col)}"));
        var args = Args(vals);
        args.Add("noteId", noteId);

        using var db = Open();

        return await db.ExecuteAsync(
            $"update projmng.if_note set {sets} where note_id = @noteId", args);
    }

    public async Task<bool> DeleteNoteAsync(int noteId)
    {
        using var db = Open();

        return await db.ExecuteAsync(
            "delete from projmng.if_note where note_id = @noteId", new { noteId }) > 0;
    }

    // ──────────────────────────────────────────── 추가 관리항목

    public async Task<List<IfAttrRow>> AttrsAsync(int ifId)
    {
        using var db = Open();

        var rows = await db.QueryAsync<IfAttrRow>("""
            select attr_def_id as AttrDefId
                 , attr_cd     as AttrCd
                 , attr_nm     as AttrNm
                 , attr_type   as AttrType
                 , code_grp    as CodeGrp
                 , required_yn as RequiredYn
                 , attr_val    as AttrVal
                 , sort_order  as SortOrder
                 , to_char(updated_at, 'YYYY-MM-DD HH24:MI') as UpdatedAt
                 , updated_by  as UpdatedBy
              from projmng.v_if_attr
             where if_id = @ifId
             order by sort_order, attr_cd
            """, new { ifId });

        return [.. rows];
    }

    /// <summary>항목값을 한꺼번에 담는다. 없던 것은 만들고 있던 것은 덮는다.</summary>
    public async Task<int> SaveAttrsAsync(int ifId, IDictionary<int, string?> values, string? who)
    {
        using var db = Open();

        var saved = 0;

        foreach (var (defId, value) in values)
        {
            saved += await db.ExecuteAsync("""
                insert into projmng.if_attr (if_id, attr_def_id, attr_val, created_by, updated_by)
                     values (@ifId, @defId, @value, @who, @who)
                on conflict (if_id, attr_def_id)
                  do update set attr_val = excluded.attr_val, updated_by = excluded.updated_by
                """, new { ifId, defId, value, who });
        }

        return saved;
    }
}
