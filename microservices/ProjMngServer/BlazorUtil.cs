using ProjModel;
using System.Text.RegularExpressions;

namespace ProjMngServer;
public class BlazorUtil {

  private static string ExtractValue(string content, string key, string empStr) {
    string pattern = $@"{key}\s*:\s*(?<value>[^\n\r*]+)";
    Match match = Regex.Match(content, pattern);
    return match.Success ? match.Groups["value"].Value.Trim() : empStr;
  }








  // 경로를 못 구한 파일(디렉터리 없음)이나 아직 못 정한 기준 경로가 그대로 들어온다.
  public static string RemovePath(string? input, string? pathToRemove) {
    if (string.IsNullOrEmpty(input)) return string.Empty;
    if (string.IsNullOrEmpty(pathToRemove)) return input;

    // Normalize the path to remove by replacing backslashes with forward slashes and making it case-insensitive
    string normalizedPath = Regex.Escape(pathToRemove.Replace("\\", "/"));

    // Create a regex pattern to match the normalized path in a case-insensitive manner
    string pattern = "(?i)" + normalizedPath.Replace("/", "[/\\\\]");

    // Replace the matched path with an empty string
    return Regex.Replace(input, pattern, "", RegexOptions.IgnoreCase);
  }



  public static string RemoveNonAlphabeticLeadingChar(string input) {
    return Regex.Replace(input, "^[^a-zA-Z]+", "");
  }


  //blazor 파일을 읽어서 메뉴를 만들어 주는 함수
  public static List<Dictionary<string, string>> GetBlazorMenuList(string basePath, string projNamespace, string pageRoot, string pagePattern) {


    Console.WriteLine("GetBlazorMenuList start ------------------------------------------------- ");
    Console.WriteLine("basePath : " + basePath);
    Console.WriteLine("projNamespace : " + projNamespace);
    Console.WriteLine("pageRoot : " + pageRoot);
    Console.WriteLine("pagePattern : " + pagePattern);

    List<Dictionary<string, string>> aaa = new List<Dictionary<string, string>>();

   // string basePath = @"c:\projects\ProjMng";
   // string projNamespace = @"ProjMngWasm";
   // string pageRoot = @"Pages";
    string folderPath = basePath + @"/" + projNamespace + @"/";
    string searchPattern = "*.razor";
    //string pagePattern = "@page\\s+\"(?<url>[^\"]+)\"";
    //string namePattern = "@\\* description :\\s*(?<title>[^*]+)\\s*\\*@";

    Console.WriteLine("folderPath : " + folderPath);

    try {
      foreach (string file in Directory.GetFiles(folderPath, searchPattern, SearchOption.AllDirectories)) {


        Console.WriteLine("file : " + file);



        string content = File.ReadAllText(file);
        Match match = Regex.Match(content, pagePattern);

        if (match.Success) {
          string url = match.Groups["url"].Value;
          Dictionary<string, string> binfo = new Dictionary<string, string>();
          binfo.Add("name", Path.GetFileNameWithoutExtension(file));

          string? dir = Path.GetDirectoryName(file);
          string rurl = RemovePath(dir, basePath);
          rurl = RemovePath(rurl, projNamespace);
          rurl = RemovePath(rurl, pageRoot);
          rurl = RemovePath(rurl, "/");
          binfo.Add("dir", rurl);

          string filepath = Path.GetFullPath(file);
          rurl = RemovePath(filepath, basePath);
          rurl = RemovePath(rurl, ".razor");
          rurl = RemovePath(rurl, "..");

          binfo.Add("fullname", RemoveNonAlphabeticLeadingChar(rurl.Replace("\\", ".").Replace("/", ".")));



          binfo.Add("url", url);
          binfo.Add("description", ExtractValue(content, "description", Path.GetFileNameWithoutExtension(file) ));
          binfo.Add("title", ExtractValue(content, "title", ExtractValue(content, "화면명", url)));
          binfo.Add("sort", ExtractValue(content, "sort", "999") ) ;
          binfo.Add("credt", ExtractValue(content, "credt", ExtractValue(content, "작성일자", string.Empty)));
          binfo.Add("author", ExtractValue(content, "author", ExtractValue(content, "작성자명", string.Empty)));
          aaa.Add(binfo);





        }
      }
    }
    catch (Exception ex) {
      Console.WriteLine("오류 발생: " + ex.Message);
    }
    Console.WriteLine("GetBlazorMenuList end ------------------------------------------------- ");
    return aaa;
  }



  /// <summary> 경로에서 확장자에 맞는 파일 리스트를 가져온다. </summary>
  /// <returns></returns>
  // 기준 경로가 아직 안 정해졌으면 빈 목록이다.
  public static string[] GetFiles(string? basePath, string projNamespace, string extend) {
    if (string.IsNullOrEmpty(basePath)) return Array.Empty<string>();

    string folderPath = basePath + @"/" + projNamespace + @"/";
    string searchPattern = $"*.{extend}";
    string[] files = Directory.Exists(folderPath) ? Directory.GetFiles(folderPath, searchPattern, SearchOption.AllDirectories) : Array.Empty<string>();

    if (files == null || files.Length <= 0) {
      files = Directory.Exists(basePath) ? Directory.GetFiles(basePath, searchPattern, SearchOption.AllDirectories) : Array.Empty<string>();
    }
      return files;
  }

  public static List<Dictionary<string, string>> GetBlazorMenuList(SrcInfo si ) {


    // 아래에서 대체 경로로 다시 잡을 수 있다 — 그 사이 값이 없을 수 있다.
    string? basePath = si.Src_path; // srcInfoData[0]["src_path"].ToString();
    string projNamespace = si.Prj_namespace;  // = srcInfoData[0]["prj_namespace"].ToString();  // @"ProjMngWasm";
    string pageRoot = si.Src_ui_root; // = srcInfoData[0]["src_ui_root"].ToString();         // @"Pages";

    var urlDtl = si.SiDtlList
    .Where(dtl => string.Equals(dtl.Src_pattern_grp, "url", StringComparison.OrdinalIgnoreCase))
    .ToList().FirstOrDefault();

    var extends = si.SiDtlList
    .Where(dtl => string.Equals(dtl.Src_pattern_grp, "extend", StringComparison.OrdinalIgnoreCase))
    .ToList();

    List<Dictionary<string, string>> aaa = new List<Dictionary<string, string>>();

    foreach (var extend in extends) { 

      string folderPath = basePath + @"/" + projNamespace + @"/";
      string searchPattern = $"*.{extend.Url_pattern}";
      string pagePattern = urlDtl?.Url_pattern?? " "; // "@page\\s+\"(?<url>[^\"]+)\"";

      try {

        //string[] files = Directory.Exists(folderPath) ? Directory.GetFiles(folderPath, searchPattern, SearchOption.AllDirectories) : Array.Empty<string>();
        string[] files = GetFiles( basePath,  projNamespace, extend.Url_pattern);

        // GetFiles 는 언제나 배열을 돌려준다 — null 검사는 필요 없다.
        if (files.Length <= 0) {

          basePath = si.SiDtlList .Where(dtl => string.Equals(dtl.Src_pattern_grp, "src_path", StringComparison.OrdinalIgnoreCase)) .ToList().FirstOrDefault()?.Url_pattern;

          if (!string.IsNullOrEmpty(basePath)) {
            folderPath = basePath + @"/" + projNamespace + @"/";

            //files = Directory.Exists(folderPath) ? Directory.GetFiles(folderPath, searchPattern, SearchOption.AllDirectories) : Array.Empty<string>();

            files = GetFiles(basePath, projNamespace, extend.Url_pattern);
          }

        }

        foreach (string file in files) { 

          string content = File.ReadAllText(file);
          Match match = Regex.Match(content, pagePattern);

          if (string.IsNullOrEmpty(pagePattern) || match.Success) {

            string url = string.Empty;
            if (!string.IsNullOrEmpty(pagePattern)) {
              url = match.Groups["url"].Value;
            }
              Dictionary<string, string> binfo = new Dictionary<string, string>();
            binfo.Add("name", Path.GetFileNameWithoutExtension(file));

            string? dir = Path.GetDirectoryName(file);
            string rurl = RemovePath(dir, basePath);
            rurl = RemovePath(rurl, projNamespace);
            rurl = RemovePath(rurl, pageRoot);
            rurl = RemovePath(rurl, "/");
            binfo.Add("dir", rurl);

            string filepath = Path.GetFullPath(file);
            rurl = RemovePath(filepath, basePath);
            rurl = RemovePath(rurl, $".{extend.Url_pattern}");
            rurl = RemovePath(rurl, "..");

            binfo.Add("fullname", RemoveNonAlphabeticLeadingChar(rurl.Replace("\\", ".").Replace("/", "."))); 

            binfo.Add("url", url); 

            binfo.Add("description", ExtractValue(content, "description", Path.GetFileNameWithoutExtension(file))); 

            var descs = si.SiDtlList
            .Where(dtl => string.Equals(dtl.Src_extend, "desc", StringComparison.OrdinalIgnoreCase))
            .ToList();

            foreach(var desc in descs) {
              if( string.IsNullOrEmpty( desc.Url_pattern)) {
                binfo.Add(desc.Src_pattern_grp, ExtractValue(content, desc.Src_pattern_grp, string.Empty));
              }
              else {
                binfo.Add(desc.Src_pattern_grp, ExtractValue(content, desc.Url_pattern, string.Empty));
              }
            }


            aaa.Add(binfo); 

          }
        }
      }
      catch (Exception ex) {
        Console.WriteLine("오류 발생: " + ex.Message);
      } 
    } 
    return aaa;
  }








}