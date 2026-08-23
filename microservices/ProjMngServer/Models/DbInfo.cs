

namespace ProjModel;

public class DbInfo {

  public const string mssqlConstrFormat = @"Server={0},{1};Database={2};User Id={3};Password={4};{5}";
  public const string postgresqlConstrFormat = @"Host={0};Port={1};Database={2};Username={3};Password={4};SearchPath={5};{6}";
  public const string mysqlConstrFormat = @"Server={0},{1};Database={2};User Id={3};Password={4};{5}";

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

  public string ToConnectionString() {
    string result = string.Empty;
    switch (Db_type) {
      case "MSSQL":
        result = string.Format(mssqlConstrFormat, Db_ip, Db_port, Db_database, Db_id, Db_pwd, Db_cert);
        break;
      case "POSTGRESQL":
      case "EDB":
        result = string.Format(postgresqlConstrFormat, Db_ip, Db_port, Db_database, Db_id, Db_pwd, Db_schema, Db_cert);
        break;
      case "MYSQL":
        result = string.Format(mysqlConstrFormat, Db_ip, Db_port, Db_database, Db_id, Db_pwd);
        break;
    }
    return result;

  }

  public override string ToString() {
    return ToConnectionString();

  }



}


