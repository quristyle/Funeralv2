
namespace ProjModel;


//glue xml 정보
public class ActivityInfo : BaseModel {
  public string ServiceName { get; set; }
  public string TransitionName { get; set; }
  public string TransitionValue { get; set; }
  public string Dao { get; set; }
  public string ProcedureName { get; set; }
  public string ResultKey { get; set; }
  public string Activity { get; set; }
  public string Activity_Type { get; set; }
  public string Active_context { get; set; }
}



public class SrcFileInfo : BaseModel {
  public string GubunDir { get; set; }
  public string FullPath { get; set; }
  public string FileName { get; set; }
  public string FileNameNExtend { get; set; }
  public string Extend { get; set; }
  public DateTime? CreateDate { get; set; }
  public DateTime? ModifyDate { get; set; }
  public DateTime? LastDate { get; set; }
}

