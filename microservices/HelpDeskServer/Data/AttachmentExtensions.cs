using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;
using HelpDeskServer.Models;

using System.Reflection;
using System.Xml.Linq;



namespace HelpDeskServer.Data;

/// <summary>
/// 첨부파일 관련 확장 메서드
/// </summary>
public static class AttachmentExtensions
{
    
    /// <summary>
    /// 쿼리에 첨부파일 목록을 포함시킵니다.
    /// </summary>
    /// <typeparam name="TEntity">첨부파일을 포함할 엔티티 타입</typeparam>
    /// <param name="query">원본 IQueryable</param>
    /// <param name="db">데이터베이스 컨텍스트</param>
    /// <returns>첨부파일이 포함된 TEntityWithAttachments 래퍼 객체의 IQueryable</returns>
    public static IQueryable<TEntityWithAttachments<TEntity>> IncludeAttachments<TEntity>(
        this IQueryable<TEntity> query,
        DbContext db
    )
        where TEntity : class
    {
        var entityTypeName = typeof(TEntity).Name;

        return query.Select(entity => new TEntityWithAttachments<TEntity>
        {
            Entity = entity,
            Attachments = db.Set<Attachment>()
                .Where(a => a.EntityType == entityTypeName &&
                            a.EntityId == EF.Property<int>(entity, "Id"))
                .ToList()
        });
    }
}

/// <summary>
/// Attachments를 포함한 DTO Wrapper
/// </summary>
public class TEntityWithAttachments<TEntity>
{
    /// <summary>
    /// 원본 엔티티
    /// </summary>
    public TEntity Entity { get; set; } = default!;
    /// <summary>
    /// 첨부파일 목록
    /// </summary>
    public List<Attachment> Attachments { get; set; } = new();
}
