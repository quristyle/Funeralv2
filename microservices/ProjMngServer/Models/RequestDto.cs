
namespace ProjModel;


public class RequestDto {
  public string ProcName { get; set; }
  public string ProcType { get; set; } = "srch";
  public string SSUserId { get; set; } = "";//로그인 사용자.
  public bool IsFast { get; set; }//fast api 사용
  public bool IsProjDb { get; set; } = false; //프로젝트의 정의된 프로젝트 DB를 사용.
  public DateTime Start { get; set; }
  public IDictionary<string, string> MainParam { get; set; }
  public List<Dictionary<string, object>> MultyData { get; set; } = new ();



}


