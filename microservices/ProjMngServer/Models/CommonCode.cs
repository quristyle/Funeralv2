
namespace ProjModel;

public class CommonCode {

  public string? Code { get; set; }
  public string? Name { get; set; }
  public string? Desc { get; set; }
  public Dictionary<string, string>? Others { get; set; }

  public override string ToString() {
    return Name??string.Empty;
  }

}