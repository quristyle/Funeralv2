
namespace ProjModel;
  public class TableInfo :BaseModel {
    public string TableName { get; set; }
  public string Description { get; set; }
  public string Schema { get; set; }
  public string DataBase { get; set; }
  public List<ColumnInfo> ColumnInfos { get; set; } = new List<ColumnInfo>();

}



