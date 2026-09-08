
namespace ProjModel;


public class WbsInfo : BaseModel {

  public string prj_rid { get; set; } = string.Empty;
  public string wbs_id { get; set; } = string.Empty;
  public string proc_id { get; set; } = string.Empty;
  public string gb1 { get; set; } = string.Empty;
  public string gb2 { get; set; } = string.Empty;
  public string proc_nm { get; set; } = string.Empty;
  public string proc_full_nm { get { if (string.IsNullOrEmpty(proc_id)) { return proc_nm; } else {  return $"{proc_id} : {proc_nm}"; } } }
  public string proc_tp { get; set; } = string.Empty;
  public string proc_lvl { get; set; } = string.Empty;
  public string build_user { get; set; } = string.Empty;
  public string build_status { get; set; } = string.Empty;
  public string dev_user { get; set; } = string.Empty;
  public DateTime? plan_sdt { get; set; }
  public DateTime? plan_edt { get; set; }
  public DateTime? dev_sdt { get; set; }
  public DateTime? dev_edt { get; set; }
  public DateTime? display_edt { 
    get { 
      if( dev_edt.HasValue) {
        return dev_edt.Value.AddDays(1);
      }
      else {
        // 계획 종료일도 비어 있으면 보여 줄 날짜가 없다. 전에는 여기서 예외가 났다.
        return plan_edt?.AddDays(1);
      }
    } 
  }
  public string dev_chk { get; set; } = string.Empty;
  public string build_chk { get; set; } = string.Empty;
  public string build_chk_dt { get; set; } = string.Empty;
  public string qc_user { get; set; } = string.Empty;
  public string qc_chk { get; set; } = string.Empty;
  public string qc_chk_dt { get; set; } = string.Empty;
  public string comm { get; set; } = string.Empty;
  public string schedule_type { get; set; } = string.Empty;

  


  public string wbs_state { get; set; } = string.Empty;
  public bool isComplatge { get; set; }

}




