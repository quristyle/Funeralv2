using Microsoft.EntityFrameworkCore;
using FileServer.Entities;

namespace FileServer.Data;

/// <summary>
/// 파일 서비스 데이터베이스 컨텍스트 클래스
/// </summary>
public class FileDbContext : DbContext
{
    public FileDbContext(DbContextOptions<FileDbContext> options) : base(options)
    {
    }

    public DbSet<FileMetadata> FileMetadatas => Set<FileMetadata>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 모든 엔티티에 대해 공통 규칙 적용
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // 테이블 이름을 소문자로 강제 설정
            var tableName = entity.GetTableName()?.ToLower();
            entity.SetTableName(tableName);
            
            // 모든 테이블을 'scom' 스키마에 배치
            entity.SetSchema("scom");

            // 모든 컬럼 이름을 소문자로 강제 설정
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(property.GetColumnName().ToLower());
            }
        }
    }
}
