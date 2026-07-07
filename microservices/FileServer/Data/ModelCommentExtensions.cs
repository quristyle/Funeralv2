using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;

namespace FileServer.Data
{
    public static class ModelCommentExtensions
    {
        public static void ApplyXmlComments(this ModelBuilder modelBuilder)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");

            if (!File.Exists(xmlPath)) return;

            XDocument xmlDoc;
            try
            {
                xmlDoc = XDocument.Load(xmlPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] XML 주석 파일 로드 실패: {ex.Message}");
                return;
            }

            var members = xmlDoc.Descendants("member").ToList();

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var type = entityType.ClrType;
                if (type == null) continue;

                // A. 테이블 주석 매핑 (클래스 주석)
                var classTypeName = $"T:{type.FullName}";
                var classMember = members.FirstOrDefault(m => m.Attribute("name")?.Value == classTypeName);
                var classSummary = classMember?.Element("summary")?.Value?.Trim();

                if (!string.IsNullOrEmpty(classSummary))
                {
                    entityType.SetComment(classSummary);
                }

                // B. 컬럼 주석 매핑 (프로퍼티 주석)
                foreach (var property in entityType.GetProperties())
                {
                    var memberInfo = property.PropertyInfo;
                    if (memberInfo == null) continue;

                    var propTypeName = $"P:{type.FullName}.{memberInfo.Name}";
                    var propMember = members.FirstOrDefault(m => m.Attribute("name")?.Value == propTypeName);
                    var propSummary = propMember?.Element("summary")?.Value?.Trim();

                    if (!string.IsNullOrEmpty(propSummary))
                    {
                        property.SetComment(propSummary);
                    }
                }
            }
        }
    }
}
