
namespace ProjModel;
  public class TableInfo :BaseModel {
    public string TableName { get; set; } = string.Empty;
  public string Description { get; set; } = string.Empty;
  public string Schema { get; set; } = string.Empty;
  public string DataBase { get; set; } = string.Empty;
  public List<ColumnInfo> ColumnInfos { get; set; } = new List<ColumnInfo>();

}



