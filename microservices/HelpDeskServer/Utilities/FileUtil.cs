using HelpDeskServer.Models;
using Microsoft.AspNetCore.Routing;
using System.Dynamic;
using Microsoft.EntityFrameworkCore;
using HelpDeskServer.Data;
using System.Linq.Dynamic.Core;
using HelpDeskServer.Dtos;
using HelpDeskServer.Helpers;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.ComponentModel;
using HelpDeskServer.Data;
using HelpDeskServer.Models;
using Microsoft.AspNetCore.Mvc;
using HelpDeskServer.Services;
using Microsoft.EntityFrameworkCore;
using HtmlAgilityPack;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace HelpDeskServer.Utilities;

/// <summary>
/// File 관련 유틸리티 클래스.
/// </summary>
public class FileUtil {



  /// <summary>
  /// img tag 에 ᅟbase64 이미지이면 이미지 파일을 보관하고 경로로 변경하여 제공.
  /// </summary>
  /// <param name="base64Str"></param>
  /// <param name="subDir"></param>
  /// <returns></returns>
  public static async Task<string> SaveImageToFile(string base64Str, string subDir
  ) {


    string chnage_Description = null;

    if (!string.IsNullOrWhiteSpace(base64Str)) {
      var doc = new HtmlDocument();
      doc.LoadHtml(base64Str);

      var imgNodes = doc.DocumentNode.SelectNodes("//img[@src]");
      if (imgNodes != null && imgNodes.Count > 0) {
        // 저장 기본 경로 (환경변수 또는 고정 경로)
        var storageBase = Environment.GetEnvironmentVariable("ImageStorage_BasePath") ?? "/home/lee/JinHelpContents/reqs";
        var reqFolder = Path.Combine(storageBase, subDir);
        Directory.CreateDirectory(reqFolder);

        foreach (var img in imgNodes) {
          var src = img.GetAttributeValue("src", null);
          if (string.IsNullOrEmpty(src)) continue;

          // data:[<mediatype>][;base64],<data>
          if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) {
            var parts = src.Split(',', 2);
            if (parts.Length != 2) continue;

            var meta = parts[0];      // e.g. data:image/png;base64
            var base64Data = parts[1];

            // only handle base64 data URIs
            if (!meta.EndsWith(";base64", StringComparison.OrdinalIgnoreCase)) continue;

            try {
              var mimeType = meta.Substring(5).Split(';')[0].ToLowerInvariant(); // strip "data:"
              string ext = mimeType switch {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/jpg" => ".jpg",
                "image/gif" => ".gif",
                "image/svg+xml" => ".svg",
                _ => ""
              };

              var fileName = $"{Guid.NewGuid()}{ext}";
              var filePath = Path.Combine(reqFolder, fileName);

              var bytes = Convert.FromBase64String(base64Data);
              await File.WriteAllBytesAsync(filePath, bytes);








              // 변경할 src 경로: /pubs/{requestId}/{fileName}
              //img.SetAttributeValue("src", $"/imgstoroge/{subDir}/{fileName}");






              // create thumbnail for non-svg images
              try {
                var extLower = ext?.ToLowerInvariant() ?? "";
                if (extLower != ".svg") {
                  var thumbName = Path.GetFileNameWithoutExtension(fileName) + "_thumb.jpg";
                  var thumbPath = Path.Combine(reqFolder, thumbName);

                  // generate small thumbnail from the saved file
                  using (var image = await Image.LoadAsync(filePath)) {
                    image.Mutate(x => x.Resize(new ResizeOptions {
                      Mode = ResizeMode.Max,
                      Size = new Size(200, 200)
                    }));
                    var encoder = new JpegEncoder { Quality = 60 }; // 조정 가능 (작게 할수록 용량 감소)
                    await image.SaveAsJpegAsync(thumbPath, encoder);
                  }

                  // 썸네일 경로를 data-thumb 속성으로 추가 (프론트에서 사용)
                  img.SetAttributeValue("data-thumb", $"/imgstoroge/{subDir}/{thumbName}");
                }
              }
              catch {
                // 썸네일 생성 실패 시에도 원본 이미지는 정상적으로 보이도록 처리합니다.
                // 로깅을 추가하여 실패 원인을 추적할 수 있습니다.
              }
              finally {
                // 썸네일 생성 성공 여부와 관계없이 원본 이미지의 src를 설정합니다.
                img.SetAttributeValue("src", $"/imgstoroge/{subDir}/{fileName}");
              }















            }
            catch {
              // 실패 시 원본 src 유지하도록 그냥 continue
              continue;
            }
          }
        }

        // 결과 HTML
        chnage_Description = doc.DocumentNode.OuterHtml;
      }
    }

    // fallback: 원본이나 빈 문자열
    if (string.IsNullOrEmpty(chnage_Description)) {
      chnage_Description = base64Str ?? string.Empty;
    }
    return chnage_Description;

  }



  /// <summary>
  /// html에서 처음 발견한 img 테그의 url 을 추출 하여 리턴.
  /// </summary>
  /// <param name="targerString"></param>
  /// <returns></returns>
  public static async Task<string> GetFirstImageUrl(string targerString) {


    string result = string.Empty;
    Console.WriteLine($"11111111111111111111111111111111111111111111111111{targerString}");

    if (!string.IsNullOrWhiteSpace(targerString)) {
      var doc = new HtmlDocument();
      doc.LoadHtml(targerString);

      var imgNodes = doc.DocumentNode.SelectNodes("//img[@src]");
      if (imgNodes != null && imgNodes.Count > 0) {

        Console.WriteLine($"333333333333333333333333333333333333333333333333333333333333333333333333333333 :           {imgNodes.Count}");
        foreach (var img in imgNodes) {

          Console.WriteLine($"444444444444444444444444444444444444444444444444444444 : {img}");

          var src = img.GetAttributeValue("src", null);

          Console.WriteLine($"5555555555555555555555555555555555555555555555555555555555 : {src}");

          if (string.IsNullOrEmpty(src)) continue;
          else {
            result = src;

            Console.WriteLine($"22222222222222222222222222222222222222222222222222222222222222222222222222{src}");
            break;
          }

        }
      }
    }
    return result;

  }














  /// <summary>
  /// base64 문자열을 파일로 저장하고 저장된 파일 경로를 반환한다. 
  /// </summary>
  /// <param name="subDir"></param>
  /// <param name="base64Str"></param>
  /// <returns></returns>
  public static async Task<string> SaveImageFromBase64(string subDir, string base64Str, string conurl = "pubstor") {





    string result_path = base64Str;

    if (!string.IsNullOrWhiteSpace(base64Str)) {

      // 저장 기본 경로 (환경변수 또는 고정 경로)
      var storageBase = Environment.GetEnvironmentVariable("ImageStorage_BasePath") ?? "/home/lee/JinHelpContents";
      var reqFolder = Path.Combine(storageBase, subDir);
      Directory.CreateDirectory(reqFolder);




      if (base64Str.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) {
        var parts = base64Str.Split(',', 2);
        if (parts.Length != 2) return result_path;

        var meta = parts[0];      // e.g. data:image/png;base64
        var base64Data = parts[1];

        // only handle base64 data URIs
        if (!meta.EndsWith(";base64", StringComparison.OrdinalIgnoreCase)) return result_path; ;

        try {
          var mimeType = meta.Substring(5).Split(';')[0].ToLowerInvariant(); // strip "data:"
          string ext = mimeType switch {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/jpg" => ".jpg",
            "image/gif" => ".gif",
            "image/svg+xml" => ".svg",
            _ => ""
          };

          var fileName = $"{Guid.NewGuid()}{ext}";
          var filePath = Path.Combine(reqFolder, fileName);

          var bytes = Convert.FromBase64String(base64Data);
          await File.WriteAllBytesAsync(filePath, bytes);

          // create thumbnail for non-svg images
          try {
            var extLower = ext?.ToLowerInvariant() ?? "";
            if (extLower != ".svg") {
              var thumbName = Path.GetFileNameWithoutExtension(fileName) + "_thumb.jpg";
              var thumbPath = Path.Combine(reqFolder, thumbName);

              // generate small thumbnail from the saved file
              using (var image = await Image.LoadAsync(filePath)) {
                image.Mutate(x => x.Resize(new ResizeOptions {
                  Mode = ResizeMode.Max,
                  Size = new Size(200, 200)
                }));
                var encoder = new JpegEncoder { Quality = 60 }; // 조정 가능 (작게 할수록 용량 감소)
                await image.SaveAsJpegAsync(thumbPath, encoder);
              }

              // 썸네일 경로를 data-thumb 속성으로 추가 (프론트에서 사용)
              //img.SetAttributeValue("data-thumb", $"/imgstoroge/{subDir}/{thumbName}");
            }
          }
          catch {
            // 썸네일 생성 실패 시에도 원본 이미지는 정상적으로 보이도록 처리합니다.
            // 로깅을 추가하여 실패 원인을 추적할 수 있습니다.
          }
          finally {
            // 썸네일 생성 성공 여부와 관계없이 원본 이미지의 src를 설정합니다.
            //img.SetAttributeValue("src", $"/imgstoroge/{subDir}/{fileName}");
            result_path = $"/{conurl}/{subDir}/{fileName}";
          }















        }
        catch {
          // 실패 시 원본 src 유지하도록 그냥 continue
          return result_path;
        }
      }
      else {

      }




    }

    return result_path;


















  }


  /// <summary>
  /// 지정된 디렉토리를 삭제합니다.
  /// </summary>
  /// <param name="basePath"></param>
  /// <param name="subDir"></param>
  /// <returns></returns>
  public static async Task DeleteImageFileDir(string basePath, string subDir) {


    // 안전장치: 인수가 비어있으면 삭제하지 않음.
    if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(subDir)) return;

    // 안전장치: basePath에 JinHelpContents가 포함되어 있지 않으면 삭제하지 않음.
    if (!(basePath.IndexOf("JinHelpContents") > 0)) return;

    var storageBase = Environment.GetEnvironmentVariable("ImageStorage_BasePath") ?? "/home/lee/JinHelpContents/reqs";
    var reqFolder = Path.Combine(storageBase, subDir);
    // Directory.Delete(reqFolder); // 게시물의 폴더와 파일도 지운다.
    // 안전하게 폴더(및 내부 파일)를 삭제. 폴더가 없으면 무시.
    // Directory.Delete(path, true) 는 비어있지 않은 폴더도 삭제하지만
    // 읽기전용 파일 등으로 실패할 수 있으므로 예외 시 파일 속성 정리 후 재시도.
    if (Directory.Exists(reqFolder)) {
      try {
        Directory.Delete(reqFolder, recursive: true);
      }
      catch (Exception) {
        try {
          // 내부 파일들의 속성을 정상으로 만들어 삭제 재시도
          foreach (var file in Directory.EnumerateFiles(reqFolder, "*", SearchOption.AllDirectories)) {
            try { File.SetAttributes(file, FileAttributes.Normal); } catch { /* 무시 */ }
          }

          Directory.Delete(reqFolder, recursive: true);
        }
        catch (Exception) {
          // 삭제 실패 시에도 흐름을 막지 않도록 무시 또는 로깅(원하면 로거 사용)
        }
      }
    }

  }


}