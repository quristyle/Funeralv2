
namespace ProjModel;

public class ProgramInfo {
  public ProgramInfo(string _componentType, string projname) {
    ComponentType = Type.GetType(AppendAssemblyName(_componentType));
    ProjName = projname;
  }
  public string? ProjName { get; set; }
  public string? ProjId { get; set; }
  public string? Url { get; set; }
  public string? ProjPath { get; set; }
  public Type? ComponentType { get; set; }
  string AppendAssemblyName(string fullTypeName) {
    int dotIndex = fullTypeName.IndexOf('.');
    string assemblyName = dotIndex >= 0 ? fullTypeName.Substring(0, dotIndex) : fullTypeName;
    return $"{fullTypeName}, {assemblyName}";
  }

}