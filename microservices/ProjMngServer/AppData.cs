using ProjModel;

namespace ProjMngServer;
public class AppData {
  //시스템이 연결 가능한 db정보
  public static List<DbInfo> DB_Infos = new List<DbInfo>();
  // 시스템이 사용할 db 타입에 따른 시스템 쿼리 정보들.
  public static List<Devsqlresp> DsrInfos = new List<Devsqlresp>();

}

