
namespace ProjModel;

public class ResultInfo<T> {
  public int? Code { get; set; } = 0;
  public string? Message { get; set; } = "success";
  public IDictionary<string, object>? Res { get; set; }
  public IDictionary<string, string>? Cols { get; set; }
  public List<T>? Data { get; set; }
  //public IEnumerable<dynamic>? Data2 { get; set; }
}


