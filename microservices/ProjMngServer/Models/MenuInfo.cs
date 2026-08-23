


using System.Reflection;

namespace ProjModel;

public class MenuInfo : BaseModel {

  public MenuInfo() {
    Children = new List<MenuInfo>();
  }
  public MenuInfo OwnerMenu { get; set; }


  public string owner_id { get; set; }


  public string mnu_cd { get; set; }

  public string mnu_nm { get; set; }
  public string mnu_url { get; set; }

  public decimal? disp_seq { get; set; }
  public string parent_mnu_cd { get; set; }
  public string mnu_grp_yn { get; set; }
  public string pgm_id { get; set; }

  public string mnu_id { get; set; }
  public string use_yn { get; set; }
  public string pgm_ty { get; set; }



  public string mnu_desc { get; set; }
  



  public bool Expanded { get; set; }
  public List<MenuInfo> Children { get; set; } //= new List<MenuInfo>();





  public static List<MenuInfo> BuildMenuTree(List<MenuInfo> source) {

    bool isFirstExpanded = false;

    var menuMap = new Dictionary<string, MenuInfo>();
    var rootMenus = new List<MenuInfo>();

    // 1. 모든 메뉴를 사전에 저장
    foreach (var menu in source) {
      menuMap[menu.mnu_id] = menu;
    }

    // 2. 부모 메뉴와 자식 메뉴 연결
    foreach (var menu in source) {
      if (menu.owner_id == "ROOT") {
        rootMenus.Add(menu); // 최상위 메뉴
      }
      else if (menuMap.TryGetValue(menu.owner_id, out var parent)) {
        menu.OwnerMenu = parent;       // 부모 참조 설정

        if (parent.Children == null) {
          parent.Children = new List<MenuInfo>();
        }
        if (!isFirstExpanded) {
          parent.Expanded = true;
          isFirstExpanded = true;
        }
        parent.Children.Add(menu);     // 자식 리스트에 추가
      }
    }

    foreach (var menu in source) {
      menu.isChanged = false; // 초기화
    }

    return rootMenus;
  }





}

public class BaseModel {
  public string cre_id { get; set; }
  public DateTime? cre_dt { get; set; }
  public string mod_id { get; set; }
  public DateTime? mod_dt { get; set; }

  public string cre_user { get; set; }
  public string mod_user { get; set; }
  public bool isChanged { get; set; }



  public void OnPropertyChanged(string propertyName) {
    if (propertyName != nameof(isChanged)) {
      isChanged = true;
    }
  }







}
