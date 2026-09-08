using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjModel;
  public class SrcInfo() {
    public string Src_rid { get; set; } = string.Empty;
    public string Src_os { get; set; } = string.Empty;
    public string Src_path { get; set; } = string.Empty;
    public string Src_nick { get; set; } = string.Empty;
    public string Src_type { get; set; } = string.Empty;
    public string Src_lang { get; set; } = string.Empty;
    public string Src_comm { get; set; } = string.Empty;
    public string Prj_rid { get; set; } = string.Empty;
    public string Src_ui_root { get; set; } = string.Empty;
  public string Prj_namespace { get; set; } = string.Empty;
  //public string Url_pattern { get; set; }

  

    public List<SrcInfoDtl> SiDtlList { get; set; } = new();

  }
