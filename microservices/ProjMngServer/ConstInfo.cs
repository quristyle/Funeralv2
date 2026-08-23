namespace ProjMngServer;
public static class ConstInfo {

  public enum DbTypes {
    MSSQL,
    POSTGRESQL,
    MYSQL
  }


  public const string db_nick_key = "@db_nick";
  public const string dbConQuery = @"
select db_ip, db_port, db_database, db_id, db_pwd, db_cert, db_comm, db_nick, db_type, db_schema
  from projmng.devdbinfo d 
 where db_nick = @db_nick
";

  public const string dbVsqlResp = @"
select d.* 
  from projmng.devsqlresp d 
 where dsl_type = @dsl_type
   and dsl_cd = @dsl_cd 
";


  public const string ProcDbQuery = @"
 select a.* 
      , a.db_pvalue as Dsl_query
      , a.db_pkey as Dsl_cd
   from projmng.dev_db_prop a
  where db_pkey = @db_pkey
    and db_rid = @db_rid ::bigint
";


  public const string NullStr = null;


}