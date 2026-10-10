namespace JSini.Web.Admin.Components.Pages;

public class OrgNode
{
    public string Id { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsUser { get; set; }
    public string? LoginId { get; set; }
    public string? CompanyName { get; set; }
    public string? DeptName { get; set; }
    public List<OrgNode> Children { get; set; } = new();
}
