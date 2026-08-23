
namespace ProjModel;
public class ErdInfo {
    public List<EntityInfo> entities { get; set; } = new List<EntityInfo>();
    public List<RelationInfo> relations { get; set; } = new List<RelationInfo>();
  }

  public class EntityInfo {
    public string id { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public string desc { get; set; } = string.Empty;
    public List<string> fields { get; set; } = new List<string>();
    public int x { get; set; }
    public int y { get; set; }
    public int w { get; set; }
    public int h { get; set; }
  }

  public class RelationInfo {
    public string from { get; set; } = string.Empty;
    public string to { get; set; } = string.Empty;
    public string label { get; set; } = string.Empty;
  }


