using System.Data.Common;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace ProjModel;

public class DbInfo {

  public string? Db_ip { get; set; }
  public string? Db_port { get; set; }
  public string? Db_database { get; set; }
  public string? Db_id { get; set; }
  public string? Db_pwd { get; set; }
  public string? Db_cert { get; set; }
  public string? Db_comm { get; set; }
  public string? Db_nick { get; set; }
  public string? Db_type { get; set; }
  public string? Db_schema { get; set; }
  public string? Proj { get; set; }
  public string? Proj_name { get; set; }

  /// <summary>접속 문자열. db_cert 는 덧붙일 옵션 조각이라 먼저 읽고, 접속 대상 칸은 그 위에 덮어 조각이 호스트·계정을 바꾸지 못하게 한다.</summary>
  public string ToConnectionString() {
    switch (Db_type) {
      case "MSSQL": {
        var b = new SqlConnectionStringBuilder(Db_cert ?? string.Empty) {
          DataSource = string.IsNullOrWhiteSpace(Db_port) ? Db_ip ?? string.Empty : $"{Db_ip},{Db_port}",
          InitialCatalog = Db_database ?? string.Empty,
          UserID = Db_id ?? string.Empty,
          Password = Db_pwd ?? string.Empty,
        };
        return b.ConnectionString;
      }
      case "POSTGRESQL":
      case "EDB": {
        var b = new NpgsqlConnectionStringBuilder(Db_cert ?? string.Empty) {
          Host = Db_ip,
          Database = Db_database,
          Username = Db_id,
          Password = Db_pwd,
        };
        if (!string.IsNullOrWhiteSpace(Db_port)) b.Port = int.Parse(Db_port);
        if (!string.IsNullOrWhiteSpace(Db_schema)) b.SearchPath = Db_schema;
        return b.ConnectionString;
      }
      case "MYSQL": {
        var b = new DbConnectionStringBuilder {
          ["Server"] = Db_ip ?? string.Empty,
          ["Database"] = Db_database ?? string.Empty,
          ["User Id"] = Db_id ?? string.Empty,
          ["Password"] = Db_pwd ?? string.Empty,
        };
        if (!string.IsNullOrWhiteSpace(Db_port)) b["Port"] = Db_port;
        return b.ConnectionString;
      }
    }
    return string.Empty;
  }

  public override string ToString() {
    return ToConnectionString();

  }



}


