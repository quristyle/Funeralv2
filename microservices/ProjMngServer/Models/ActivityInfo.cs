
namespace ProjModel;


//glue xml 정보
public class ActivityInfo : BaseModel {
  public string ServiceName { get; set; } = string.Empty;
  public string TransitionName { get; set; } = string.Empty;
  public string TransitionValue { get; set; } = string.Empty;
  public string Dao { get; set; } = string.Empty;
  public string ProcedureName { get; set; } = string.Empty;
  public string ResultKey { get; set; } = string.Empty;
  public string Activity { get; set; } = string.Empty;
  public string Activity_Type { get; set; } = string.Empty;
  public string Active_context { get; set; } = string.Empty;
}



public class SrcFileInfo : BaseModel {
  public string GubunDir { get; set; } = string.Empty;
  public string FullPath { get; set; } = string.Empty;
  public string FileName { get; set; } = string.Empty;
  public string FileNameNExtend { get; set; } = string.Empty;
  public string Extend { get; set; } = string.Empty;
  public DateTime? CreateDate { get; set; }
  public DateTime? ModifyDate { get; set; }
  public DateTime? LastDate { get; set; }
}

