using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.RegularExpressions;
using HelpDeskServer.Utilities;
using RabbitMQ.Client;
using Org.BouncyCastle.Asn1.X509;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using HelpDeskServer.Data;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskServer.Endpoints;

public static class UtilEndpoints {


  public static List<MC_Models> MC_MODEL_LIST = null;

// 기기의 장비를 찾아 돌려준다. 모델명으로 찾는다. 모델명은 KEPCO 이런식으로 되어있다. MC_NAME이 KEPCO인 모델을 찾아준다.
public static MC_Models GetModelByHeader(string mc_name) {
    if (MC_MODEL_LIST == null) return null;
    return MC_MODEL_LIST.FirstOrDefault(m => string.Equals(m.MC_NAME, mc_name, StringComparison.OrdinalIgnoreCase));
  }

    public static void MapUtilEndpoints(this IEndpointRouteBuilder routes) {

        var group = routes.MapGroup("/api/utils").WithTags("Utilities");

        group.MapPost("/parse-ascii", ([FromBody] ParseRequest request) => {
            if (string.IsNullOrEmpty(request.Content)) return Results.BadRequest("Content is empty");
            var lines = request.Content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var results = new List<string>();
            var heads = request.Heads ?? new List<string>();

            foreach (var line in lines) {
                if (string.IsNullOrWhiteSpace(line)) { results.Add(""); continue; }
                string targetContent = line;
                if (heads.Any()) {
                    int earliestIndex = -1;
                    string? matchedHead = null;
                    foreach (var head in heads) {
                        int index = line.IndexOf(head, StringComparison.OrdinalIgnoreCase);
                        if (index != -1 && (earliestIndex == -1 || index < earliestIndex)) {
                            earliestIndex = index;
                            matchedHead = head;
                        }
                    }
                    if (matchedHead != null) targetContent = line.Substring(earliestIndex + matchedHead.Length).Trim();
                }
                results.Add(InterpretLine(targetContent, request.InterpretationType));
            }
            return Results.Ok(new { originalCount = lines.Length, parsedLines = results });
        }).WithName("ParseAscii");

        group.MapPost("/parse-binary", async ([FromBody] ParseRequest request, AppDbContext db) => {


          
          // db에서 읽고 반영.
          MC_MODEL_LIST = await db.MC_Models
              .Include(m => m.ParseItems)
                  .ThenInclude(p => p.TagItems)
              .ToListAsync();

          foreach (var mc in MC_MODEL_LIST) {
            mc.CodeBook = CreateKepcoTagCodeBook();
          }


          if (string.IsNullOrEmpty(request.Content)) return Results.BadRequest("Content is empty");
          //var lines = request.Content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
          var line = request.Content;
          var heads = request.Heads ?? new List<string>();// RX, TX,count,CRC 등 헤더 리스트


          // 계산용 객체 생성.. 이걸로 가지고 논다.
          BinaryCalcInfo bci = new BinaryCalcInfo();

          var results = bci.Results; // new List<object>();

      //      foreach (var line in lines) {
                //if (string.IsNullOrWhiteSpace(line)) { results.Add(""); continue; }

                // 헤더(RX/TX) 추출 및 필터링
                bci.TargetContent = line;
                bci.TargetContentNotCRC = line;
                bci.OriginalContent = line;
                bci.TargetContentNotHead = line;

                string? matchedHead = null;
                if (heads.Any()) {
                    int earliestIndex = -1;
                    foreach (var head in heads) {
                        int index = line.IndexOf(head, StringComparison.OrdinalIgnoreCase);
                        if (index != -1 && (earliestIndex == -1 || index < earliestIndex)) {
                            earliestIndex = index;
                            matchedHead = head;
                        }
                    }
                    if (matchedHead != null) {
                      bci.TargetContent = line.Substring(earliestIndex + matchedHead.Length).Trim();
                      bci.TargetContentNotCRC = line.Substring(earliestIndex + matchedHead.Length).Trim();
                    }
                }

                // 첫째토큰 는 길이
                var tokens = bci.TargetContent.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                bci.TargetContent = string.Join(" ", tokens.Skip(1));

                // 길이, CRC 제거
                bci.TargetContentNotCRC = tokens.Length > 3
                    ? string.Join(" ", tokens.Skip(1).Take(tokens.Length - 3))
                    : "";

                // 2. HEX 추출
                var matches = Regex.Matches(bci.TargetContent, @"(?<![0-9A-Fa-f])[0-9A-Fa-f]{2}(?![0-9A-Fa-f])");

                var hexTokens = matches.Select(m => m.Value).ToList();

                if (hexTokens.Count == 0)        {
                    results.Add(new            {                TargetContent = bci.TargetContent,                error = "NO HEX FOUND"            });
                }

                byte[] lineBytes = hexTokens.Select(x => Convert.ToByte(x, 16)).ToArray();

                // CRC 계산용 데이터
                byte[] crcTarget = lineBytes.Length >= 2
                    ? lineBytes.Take(lineBytes.Length - 2).ToArray()
                    : Array.Empty<byte>();


                // 2. 헥사 데이터 추출 (공백 등 제거)
                var hexMatches = Regex.Matches(bci.TargetContent, @"[0-9A-Fa-f]{2}");
                if (hexMatches.Count == 0) {
                    results.Add(bci.TargetContent);
                }

                var model = GetModelByHeader(request.Model);

                bci.TargetContentNotHead = bci.TargetContent.StartsWith(model.StartKey)
                    ? bci.TargetContent[model.StartKey.Length..].Trim()
                    : bci.TargetContent;


                // 3. KEPCO 헤더 감지 시 구조화 분석 실행
                if ( model.MC_NAME == request.Model ){
                  var parse_item =    model.FindMatchingItem( matchedHead, lineBytes); // 0x52, 0x00, 0x01

                  Console.WriteLine($"Detected model: {model.MC_NAME}, parse_item...{parse_item}");

                  if( parse_item == null) {
                    Console.WriteLine($"Detected model: {model.MC_NAME}, parse_item...NOT FOUND");

                    results.Add(AnalyzeKepcoProtocolStructured(model, null, lineBytes, bci));
                  }
                  else{
                    results.Add(AnalyzeKepcoProtocolStructured(model, parse_item, lineBytes, bci));
                  }

                } else {
                    // 일반 응답: 헥사 문자열 반환
                    results.Add(string.Join(" ", lineBytes.Select(b => b.ToString("X2"))));
                }

results.Add(new {
                OriginalContent = bci.OriginalContent,
                TargetContent = bci.TargetContent,
                TargetContentCRC = bci.TargetContentNotCRC,
                TargetContentNotHead = bci.TargetContentNotHead,
                matchedHead = matchedHead,
                ModelUsed = request.Model
            });

      //      }



            return Results.Ok(new { originalCount = 1, parsedLines = results });
        }).WithName("ParseBinary");

        group.MapGet("/mc-models", (AppDbContext db) => HelpDeskServer.Models.ApiResponseBuilder.CreateAsync(
            () => db.MC_Models
                .Select(m => new { m.Id, mc_name = m.MC_NAME, m.StartKey })
                .ToListAsync()
        )).WithName("GetMcModels");

        group.MapGet("/mc-models-full", (AppDbContext db) => HelpDeskServer.Models.ApiResponseBuilder.CreateAsync(
            () => db.MC_Models
                .Select(m => new {
                    m.Id,
                    mcName = m.MC_NAME,
                    m.StartKey,
                    ParseItems = m.ParseItems.Select(p => new {
                        p.Id,
                        p.Desc,
                        p.PTYPE,
                        p.KeyIdx,
                        p.Keys,
                        p.BlocParseType,
                        p.BlocParseLength,
                        TagItems = p.TagItems.OrderBy(t => t.SortNo).ToList()
                    }).ToList(),
                    AckFinds = m.AckFinds.ToList(),
                    Samples = m.Samples.Select(s => new {
                        s.Id,
                        s.MC_ModelsId,
                        s.Title,
                        s.CreatedAt
                    }).OrderByDescending(s => s.CreatedAt).ToList()
                })
                .ToListAsync()
        )).WithName("GetMcModelsFull");

        group.MapGet("/samples/{id}", (AppDbContext db, int id) => HelpDeskServer.Models.ApiResponseBuilder.CreateAsync(
            () => db.BinarySamples.FindAsync(id).AsTask()
        )).WithName("GetBinarySample");

        group.MapPost("/mc-models", (AppDbContext db, [FromBody] MCModelCreateDto dto) => HelpDeskServer.Models.ApiResponseBuilder.CreateAsync(async () => {
            var model = new MC_Models {
                MC_NAME = dto.McName,
                StartKey = dto.StartKey
            };
            db.MC_Models.Add(model);
            await db.SaveChangesAsync();
            return model;
        })).WithName("CreateMcModel");

        group.MapPost("/mc-models/{mcModelId}/parse-items", (AppDbContext db, int mcModelId, [FromBody] ParseItemCreateDto dto) => HelpDeskServer.Models.ApiResponseBuilder.CreateAsync(async () => {
            var model = await db.MC_Models.FindAsync(mcModelId);
            if (model is null) return null;

            var byteList = new List<byte>();
            if (!string.IsNullOrWhiteSpace(dto.Keys)) {
                var tokens = dto.Keys.Split(new[] { ' ', '-', ':' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var token in tokens) {
                    if (byte.TryParse(token, System.Globalization.NumberStyles.HexNumber, null, out var b)) {
                        byteList.Add(b);
                    }
                }
            }

            var item = new ParseItem {
                MC_ModelsId = mcModelId,
                Desc = dto.Desc,
                PTYPE = dto.Ptype,
                KeyIdx = dto.KeyIdx,
                Keys = byteList,
                BlocParseType = dto.BlocParseType,
                BlocParseLength = dto.BlocParseLength
            };
            db.ParseItems.Add(item);
            await db.SaveChangesAsync();
            return item;
        })).WithName("CreateParseItem");

        group.MapPost("/parse-items/{parseItemId}/tag-items", (AppDbContext db, int parseItemId, [FromBody] TagItemCreateDto dto) => HelpDeskServer.Models.ApiResponseBuilder.CreateAsync(async () => {
            var parseItem = await db.ParseItems.FindAsync(parseItemId);
            if (parseItem is null) return null;

            if (!Enum.TryParse<DataTypeEnum>(dto.DataType, true, out var dt)) {
                dt = DataTypeEnum.NUMBER;
            }

            var tag = new TagItem {
                ParseItemId = parseItemId,
                Desc = dto.Desc,
                TagIdx = dto.TagIdx,
                TagLength = dto.TagLength,
                DataType = dt,
                SortNo = dto.SortNo
            };
            db.TagItems.Add(tag);
            await db.SaveChangesAsync();
            return tag;
        })).WithName("CreateTagItem");

        group.MapPut("/tag-items/{id}", (AppDbContext db, int id, [FromBody] TagItemUpdateDto dto) => HelpDeskServer.Models.ApiResponseBuilder.CreateAsync(async () => {
            var tag = await db.TagItems.FindAsync(id);
            if (tag is null) return null;

            tag.Desc = dto.Desc;
            tag.TagIdx = dto.TagIdx;
            tag.TagLength = dto.TagLength;
            tag.SortNo = dto.SortNo;
            if (Enum.TryParse<DataTypeEnum>(dto.DataType, true, out var dt)) {
                tag.DataType = dt;
            }

            await db.SaveChangesAsync();
            return tag;
        })).WithName("UpdateTagItem");

        group.MapDelete("/tag-items/{id}", (AppDbContext db, int id) => HelpDeskServer.Models.ApiResponseBuilder.CreateAsync(async () => {
            var tag = await db.TagItems.FindAsync(id);
            if (tag is null) return null;

            db.TagItems.Remove(tag);
            await db.SaveChangesAsync();
            return tag;
        })).WithName("DeleteTagItem");

        group.MapPut("/tag-items/reorder", (AppDbContext db, [FromBody] List<TagItemOrderDto> items) => HelpDeskServer.Models.ApiResponseBuilder.CreateAsync(async () => {
            foreach (var item in items) {
                var tag = await db.TagItems.FindAsync(item.Id);
                if (tag != null) {
                    tag.SortNo = item.SortNo;
                }
            }
            await db.SaveChangesAsync();
            return new { success = true };
        })).WithName("ReorderTagItems");

        group.MapGet("/mc-models/{mcModelId}/samples", (AppDbContext db, int mcModelId) => HelpDeskServer.Models.ApiResponseBuilder.CreateAsync(
            () => db.BinarySamples
                .Where(s => s.MC_ModelsId == mcModelId)
                .Select(s => new { s.Id, s.MC_ModelsId, s.Title, s.CreatedAt })
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync()
        )).WithName("GetBinarySamples");

        group.MapPost("/mc-models/{mcModelId}/samples", (AppDbContext db, int mcModelId, [FromBody] BinarySampleSaveDto dto) => HelpDeskServer.Models.ApiResponseBuilder.CreateAsync(async () => {
            var sample = new HelpDeskServer.Models.BinarySample {
                MC_ModelsId = mcModelId,
                Title = dto.Title,
                Content = dto.Content
            };
            db.BinarySamples.Add(sample);
            await db.SaveChangesAsync();
            return sample;
        })).WithName("CreateBinarySample");

        group.MapPut("/samples/{id}", (AppDbContext db, int id, [FromBody] BinarySampleSaveDto dto) => HelpDeskServer.Models.ApiResponseBuilder.CreateAsync(async () => {
            var sample = await db.BinarySamples.FindAsync(id);
            if (sample is null) return null;

            sample.Title = dto.Title;
            sample.Content = dto.Content;
            await db.SaveChangesAsync();
            return sample;
        })).WithName("UpdateBinarySample");

        group.MapPut("/mc-models/{id}", (AppDbContext db, int id, [FromBody] MCModelUpdateDto dto) => HelpDeskServer.Models.ApiResponseBuilder.CreateAsync(async () => {
            var model = await db.MC_Models.FindAsync(id);
            if (model is null) return null;

            model.MC_NAME = dto.McName;
            model.StartKey = dto.StartKey;
            await db.SaveChangesAsync();
            return model;
        })).WithName("UpdateMcModel");

        group.MapDelete("/mc-models/{id}", (AppDbContext db, int id) => HelpDeskServer.Models.ApiResponseBuilder.CreateAsync(async () => {
            var model = await db.MC_Models.FindAsync(id);
            if (model is null) return null;

            db.MC_Models.Remove(model);
            await db.SaveChangesAsync();
            return model;
        })).WithName("DeleteMcModel");

        group.MapPut("/parse-items/{id}", (AppDbContext db, int id, [FromBody] ParseItemUpdateDto dto) => HelpDeskServer.Models.ApiResponseBuilder.CreateAsync(async () => {
            var item = await db.ParseItems.FindAsync(id);
            if (item is null) return null;

            var byteList = new List<byte>();
            if (!string.IsNullOrWhiteSpace(dto.Keys)) {
                var tokens = dto.Keys.Split(new[] { ' ', '-', ':' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var token in tokens) {
                    if (byte.TryParse(token, System.Globalization.NumberStyles.HexNumber, null, out var b)) {
                        byteList.Add(b);
                    }
                }
            }

            item.Desc = dto.Desc;
            item.PTYPE = dto.Ptype;
            item.KeyIdx = dto.KeyIdx;
            item.Keys = byteList;
            item.BlocParseType = dto.BlocParseType;
            item.BlocParseLength = dto.BlocParseLength;

            await db.SaveChangesAsync();
            return item;
        })).WithName("UpdateParseItem");

        group.MapDelete("/parse-items/{id}", (AppDbContext db, int id) => HelpDeskServer.Models.ApiResponseBuilder.CreateAsync(async () => {
            var item = await db.ParseItems.FindAsync(id);
            if (item is null) return null;

            db.ParseItems.Remove(item);
            await db.SaveChangesAsync();
            return item;
        })).WithName("DeleteParseItem");

        group.MapPost("/mc-models/{mcModelId}/ack-finds", (AppDbContext db, int mcModelId, [FromBody] AckFindSaveDto dto) => HelpDeskServer.Models.ApiResponseBuilder.CreateAsync(async () => {
            var model = await db.MC_Models.FindAsync(mcModelId);
            if (model is null) return null;

            var ack = new MC_ACK_FIND {
                MC_ModelsId = mcModelId,
                startCalcArrow = dto.StartCalcArrow,
                startCalcTarget = dto.StartCalcTarget,
                startCalcIdx = dto.StartCalcIdx,
                startCalcValue = dto.StartCalcValue,
                startCalcEquals = dto.StartCalcEquals,
                endCalcArrow = dto.EndCalcArrow,
                endCalcTarget = dto.EndCalcTarget,
                endCalcIdx = dto.EndCalcIdx,
                endCalcValue = dto.EndCalcValue,
                endCalcEquals = dto.EndCalcEquals
            };
            db.MC_AckFinds.Add(ack);
            await db.SaveChangesAsync();
            return ack;
        })).WithName("CreateAckFind");

        group.MapPut("/ack-finds/{id}", (AppDbContext db, int id, [FromBody] AckFindSaveDto dto) => HelpDeskServer.Models.ApiResponseBuilder.CreateAsync(async () => {
            var ack = await db.MC_AckFinds.FindAsync(id);
            if (ack is null) return null;

            ack.startCalcArrow = dto.StartCalcArrow;
            ack.startCalcTarget = dto.StartCalcTarget;
            ack.startCalcIdx = dto.StartCalcIdx;
            ack.startCalcValue = dto.StartCalcValue;
            ack.startCalcEquals = dto.StartCalcEquals;
            ack.endCalcArrow = dto.EndCalcArrow;
            ack.endCalcTarget = dto.EndCalcTarget;
            ack.endCalcIdx = dto.EndCalcIdx;
            ack.endCalcValue = dto.EndCalcValue;
            ack.endCalcEquals = dto.EndCalcEquals;

            await db.SaveChangesAsync();
            return ack;
        })).WithName("UpdateAckFind");

        group.MapDelete("/ack-finds/{id}", (AppDbContext db, int id) => HelpDeskServer.Models.ApiResponseBuilder.CreateAsync(async () => {
            var ack = await db.MC_AckFinds.FindAsync(id);
            if (ack is null) return null;

            db.MC_AckFinds.Remove(ack);
            await db.SaveChangesAsync();
            return ack;
        })).WithName("DeleteAckFind");

        group.MapPost("/check-crc", async ([FromBody] ParseRequest request) => {

            if (string.IsNullOrEmpty(request.Content)) return Results.BadRequest("Content is empty");
            var lines = request.Content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var results = new List<object>();
            var heads = request.Heads ?? new List<string>();// RX, TX 등 헤더 리스트

            foreach (var line in lines) {
                if (string.IsNullOrWhiteSpace(line)) { results.Add(""); continue; }

                // 1. 헤더(RX/TX) 추출 및 필터링
                string targetContent = line;
                string? matchedHead = null;
                if (heads.Any()) {
                    int earliestIndex = -1;
                    foreach (var head in heads) {
                        int index = line.IndexOf(head, StringComparison.OrdinalIgnoreCase);
                        if (index != -1 && (earliestIndex == -1 || index < earliestIndex)) {
                            earliestIndex = index;
                            matchedHead = head;
                        }
                    }
                    if (matchedHead == null) { results.Add(""); continue; }
                    targetContent = line.Substring(earliestIndex + matchedHead.Length).Trim();
                }



        // 2. HEX 추출 (디버깅 포함)
        var matches = Regex.Matches(targetContent, @"(?<![0-9A-Fa-f])[0-9A-Fa-f]{2}(?![0-9A-Fa-f])");

        var hexTokens = matches.Select(m => m.Value).ToList();

        if (hexTokens.Count == 0)
        {
            results.Add(new
            {
                targetContent,
                error = "NO HEX FOUND"
            });
            continue;
        }

        byte[] lineBytes = hexTokens.Select(x => Convert.ToByte(x, 16)).ToArray();

        // CRC 계산용 데이터
        byte[] crcTarget = lineBytes.Length >= 2
            ? lineBytes.Take(lineBytes.Length - 2).ToArray()
            : Array.Empty<byte>();


        var rst = new List<object>();

CrcResultAdd("KepcoModubus",  rst,  targetContent );
CrcResultAdd("KepcoCrc",  rst,  targetContent);
CrcResultAdd("ComputeHdlcCrc",  rst,  targetContent );
CrcResultAdd("Crc16Maxim(Custom)",  rst,  targetContent );


        results.Add( new { Target = targetContent, rst= rst });


            }

            return Results.Ok(results);
        }).WithName("CheckCrc");
   
    }

// 나중에 DB에서 읽어서 반영
private static ITagCodeBook CreateKepcoTagCodeBook() {
  var map = new Dictionary<(DataTypeEnum DataType, uint Code), string> {
    [(DataTypeEnum.CONTROL, 0x00)] = "ACK", [(DataTypeEnum.CONTROL, 0x10)] = "ACK", [(DataTypeEnum.CONTROL, 0x80)] = "ACK", [(DataTypeEnum.CONTROL, 0x90)] = "ACK",
    [(DataTypeEnum.CONTROL, 0x01)] = "NACK", [(DataTypeEnum.CONTROL, 0x11)] = "NACK", [(DataTypeEnum.CONTROL, 0x81)] = "NACK", [(DataTypeEnum.CONTROL, 0x91)] = "NACK",
    [(DataTypeEnum.CONTROL, 0x02)] = "AUT_A", [(DataTypeEnum.CONTROL, 0x82)] = "AUT_A",
    [(DataTypeEnum.CONTROL, 0x03)] = "AUT_R", [(DataTypeEnum.CONTROL, 0x83)] = "AUT_R",
    [(DataTypeEnum.CONTROL, 0x0B)] = "LINK_STATUS_REPLY", [(DataTypeEnum.CONTROL, 0x1B)] = "LINK_STATUS_REPLY", [(DataTypeEnum.CONTROL, 0x8B)] = "LINK_STATUS_REPLY", [(DataTypeEnum.CONTROL, 0x9B)] = "LINK_STATUS_REPLY",
    [(DataTypeEnum.CONTROL, 0x40)] = "RESET_LINK", [(DataTypeEnum.CONTROL, 0x60)] = "RESET_LINK", [(DataTypeEnum.CONTROL, 0xC0)] = "RESET_LINK", [(DataTypeEnum.CONTROL, 0xE0)] = "RESET_LINK",
    [(DataTypeEnum.CONTROL, 0x44)] = "UNCONFIRMED_USER_DATA", [(DataTypeEnum.CONTROL, 0x64)] = "UNCONFIRMED_USER_DATA", [(DataTypeEnum.CONTROL, 0xC4)] = "UNCONFIRMED_USER_DATA", [(DataTypeEnum.CONTROL, 0xE4)] = "UNCONFIRMED_USER_DATA",
    [(DataTypeEnum.CONTROL, 0x49)] = "LINK_STATUS_REQUEST", [(DataTypeEnum.CONTROL, 0x69)] = "LINK_STATUS_REQUEST", [(DataTypeEnum.CONTROL, 0xC9)] = "LINK_STATUS_REQUEST", [(DataTypeEnum.CONTROL, 0xE9)] = "LINK_STATUS_REQUEST",
    [(DataTypeEnum.CONTROL, 0x51)] = "AUTHENTICATION", [(DataTypeEnum.CONTROL, 0x71)] = "AUTHENTICATION", [(DataTypeEnum.CONTROL, 0xD1)] = "AUTHENTICATION", [(DataTypeEnum.CONTROL, 0xF1)] = "AUTHENTICATION",
    [(DataTypeEnum.CONTROL, 0x52)] = "TEST_LINK", [(DataTypeEnum.CONTROL, 0x72)] = "TEST_LINK", [(DataTypeEnum.CONTROL, 0xD2)] = "TEST_LINK", [(DataTypeEnum.CONTROL, 0xF2)] = "TEST_LINK",
    [(DataTypeEnum.CONTROL, 0x53)] = "CONFIRMED_USER_DATA", [(DataTypeEnum.CONTROL, 0x73)] = "CONFIRMED_USER_DATA", [(DataTypeEnum.CONTROL, 0xD3)] = "CONFIRMED_USER_DATA", [(DataTypeEnum.CONTROL, 0xF3)] = "CONFIRMED_USER_DATA",
    [(DataTypeEnum.REQUEST_CODE, 0x00)] = "CONFIRM",
    [(DataTypeEnum.REQUEST_CODE, 0x01)] = "READ",
    [(DataTypeEnum.REQUEST_CODE, 0x02)] = "SELECT",
    [(DataTypeEnum.REQUEST_CODE, 0x03)] = "WRITE",
    [(DataTypeEnum.REQUEST_CODE, 0x04)] = "DIRECT WRITE",
    [(DataTypeEnum.REQUEST_CODE, 0x05)] = "COLD RESTART",
    [(DataTypeEnum.REQUEST_CODE, 0x06)] = "WARM RESTART",
    [(DataTypeEnum.REQUEST_CODE, 0x07)] = " INITIALIZE DATA TO DEFAULT",
    [(DataTypeEnum.REQUEST_CODE, 0x0B)] = "ENABLE UNSOLICITED MESSAGE",
    [(DataTypeEnum.REQUEST_CODE, 0x0C)] = "DISABLE UNSOLICITED MESSAGE",
    [(DataTypeEnum.REQUEST_CODE, 0x0D)] = "PROGRAM DOWNLOAD",
    [(DataTypeEnum.REQUEST_CODE, 0x0E)] = "DELAY MEASUREMENT",
    [(DataTypeEnum.REQUEST_CODE, 0x0F)] = "TIME ADJUSTMENT",
    [(DataTypeEnum.REQUEST_CODE, 0x10)] = "통신 종료",
    [(DataTypeEnum.REQUEST_CODE, 0x11)] = "통신 대기",
    [(DataTypeEnum.REQUEST_CODE, 0x12)] = "광포트 통신속도변경",
    [(DataTypeEnum.RESPONSE_CODE, 0x00)] = "CONFIRM",
    [(DataTypeEnum.RESPONSE_CODE, 0x81)] = "RESPONSE",
    [(DataTypeEnum.RESPONSE_CODE, 0x82)] = "UNSOLICITED MESSAGE",
    [(DataTypeEnum.RESPONSE_CODE, 0x01)] = "BAD FUNCTION",
    [(DataTypeEnum.RESPONSE_CODE, 0x02)] = "OBJECT UNKNOWN",
    [(DataTypeEnum.RESPONSE_CODE, 0x03)] = "FORMAT ERROR",
    [(DataTypeEnum.RESPONSE_CODE, 0x04)] = "AUTHENTICATION REJECT",
    [(DataTypeEnum.RESPONSE_CODE, 0x05)] = "DEVICE TROUBLE",
    [(DataTypeEnum.ENERGY_LIMIT, 0x01)] = "1상한",
    [(DataTypeEnum.ENERGY_LIMIT, 0x02)] = "2상한",
    [(DataTypeEnum.ENERGY_LIMIT, 0x03)] = "3상한",
    [(DataTypeEnum.ENERGY_LIMIT, 0x04)] = "4상한",
    [(DataTypeEnum.APP_CODE, 0x1F05)] = "에너지",
    [(DataTypeEnum.APP_CODE, 0x2F05)] = "수요 전력",
    [(DataTypeEnum.APP_CODE, 0x7F05)] = "수요전력 발생역률(지상)",
    [(DataTypeEnum.APP_CODE, 0x9F05)] = "평균 역률(지상)",
    [(DataTypeEnum.APP_CODE, 0x7F04)] = "일반 정보 Log",
    [(DataTypeEnum.APP_CODE, 0x2F04)] = "검침",
    [(DataTypeEnum.APP_CODE, 0x1F04)] = "기본"
  };

  return new InMemoryTagCodeBook(map);
}

private static void CrcResultAdd(string crcName, List<object> results, string targetContent ) {


//Console.WriteLine($"Calculating {crcName} start ... : **{targetContent}**");


// Console.WriteLine($"Calculating {crcName} for content: **{targetContent}**");
  string c1 = CrcResultChangeCont(crcName, targetContent);


// 첫째토큰 는 길이의 의미
                var tokens = targetContent.Split(' ', StringSplitOptions.RemoveEmptyEntries);
var t2 = string.Join(" ", tokens.Skip(1));

// Console.WriteLine($"Calculating {crcName} for t2 content: **{t2}**");
  string c2 = CrcResultChangeCont(crcName, t2);

var t3 = string.Join(" ", tokens.Take(tokens.Length - 2));
// Console.WriteLine($"Calculating {crcName} for t3 content: **{t3}**");
  string c3 = CrcResultChangeCont(crcName, t3);

var t4 = tokens.Length > 3
    ? string.Join(" ", tokens.Skip(1).Take(tokens.Length - 3))
    : "";
// Console.WriteLine($"Calculating {crcName} for t4 content: **{t4}**");
  string c4 = CrcResultChangeCont(crcName, t4);


        results.Add( new {
          crcName = crcName,
          computed1 = c1,
          computed2 = c2,
          computed3 = c3,
          computed4 = c4
        });

  }


private static string CrcResultChangeCont(string crcName, string targetContent) {



  var matches = Regex.Matches(targetContent, @"(?<![0-9A-Fa-f])[0-9A-Fa-f]{2}(?![0-9A-Fa-f])");

        var hexTokens = matches.Select(m => m.Value).ToList();

        if (hexTokens.Count == 0)
        {
        }

        byte[] lineBytes = hexTokens.Select(x => Convert.ToByte(x, 16)).ToArray();

        // CRC 계산용 데이터
        byte[] crcTarget = lineBytes.Length >= 2
            ? lineBytes.ToArray()
            : Array.Empty<byte>();

        ushort computed = 0;
        if( crcName == "KepcoCrc") {
          computed = KepcoCrc.Compute(crcTarget);
        }
        else if( crcName == "ComputeHdlcCrc") {
          computed = KepcoCrc.ComputeHdlcCrc(crcTarget);
        }
        else if( crcName == "KepcoModubus") {
          computed = Crc16Modbus.Compute(crcTarget);
        }
        else if( crcName == "Crc16Maxim(Custom)") {
          computed = Crc16Maxim.Compute(crcTarget);
        return $"{(byte)(computed >> 8):X2} {(byte)(computed & 0xFF):X2}";
        }
       
        return $"{(byte)(computed & 0xFF):X2} {(byte)(computed >> 8):X2}";

  }

    private static string Blockstring(ParseItem pitm, byte[] bytes, int sIdx) {
    if( pitm?.BlocParseType == "date" ) {
        ushort year = BitConverter.ToUInt16(bytes, sIdx);
        byte month = bytes[sIdx + 2];
        byte day = bytes[sIdx + 3];
        return $"{year}-{month:D2}-{day:D2}";
    } else {
        return BitConverter.ToUInt64(bytes, sIdx).ToString("N0");
    }
  }

    private static object AnalyzeKepcoProtocolStructured( MC_Models model,ParseItem pitm, byte[] bytes, BinaryCalcInfo bci) {




        // 2. HEX 추출 (디버깅 포함)
        var matches = Regex.Matches(bci.TargetContent, @"(?<![0-9A-Fa-f])[0-9A-Fa-f]{2}(?![0-9A-Fa-f])");

        var hexTokens = matches.Select(m => m.Value).ToList();

        if (hexTokens.Count == 0)
        {
        }

        byte[] lineBytes = hexTokens.Select(x => Convert.ToByte(x, 16)).ToArray();

        // CRC 계산용 데이터
        byte[] crcTarget = lineBytes.Length >= 2
            ? lineBytes.Take(lineBytes.Length - 2).ToArray()
            : Array.Empty<byte>();


        // Data Blocks 분석 (8바이트 단위)
        var dataBlocks = new List<object>();
        int blockCount = 1;

        //tag 로 분석한 데이터가 만들어지는곳.. start quri ,....start
        if( pitm != null) {
            
            foreach( var tag in pitm.TagItems.OrderBy(t => t.SortNo)) {
                // ack를 보내서 추가로 데이터를 요청하는 경우,
                // ack를 안보내면 정의된것보다 실제로 더 짧은 데이터가 올 수 있다.
                if (tag.TagIdx+tag.TagLength > bytes.Length) {
                    continue;
                }
                byte[] value = tag.getValue(bytes);
                string convertedValue = TagValueConverter.Convert(value, tag.DataType, model.CodeBook);
                dataBlocks.Add(new {
                    Index = blockCount++,
                    Date = tag.Desc,
                    Value = convertedValue,
                    Raw = BitConverter.ToString(bytes, tag.TagIdx, tag.TagLength).Replace("-", " "),
                    TagItemMeta = new {
                        Id = tag.Id,
                        Desc = tag.Desc,
                        TagIdx = tag.TagIdx,
                        TagLength = tag.TagLength,
                        DataType = tag.DataType.ToString(),
                        SortNo = tag.SortNo
                    }
                });

            }


        }
        else
        {
            int dataStart = model.StartKey != null && bci.TargetContent.StartsWith(model.StartKey)
                ? model.StartKey.Length / 2 // "XX " 형태로 3글자씩 차지한다고 가정
                : 0;
            int dataEnd = bytes.Length - dataStart;

            for (int i = dataStart; i + 8 <= dataEnd; i += 8) {
                uint value = BitConverter.ToUInt32(bytes, i + 4);
                
                dataBlocks.Add(new {
                    Index = blockCount++,
                    Date = Blockstring(pitm, bytes, i),
                    Value = value.ToString("N0"),
                    Raw = BitConverter.ToString(bytes, i, 8).Replace("-", " ")
                });
            }
        }


//tag 로 분석한 데이터가 만들어지는곳..  quri ...end


        Console.WriteLine($"Data blocks extracted: {dataBlocks.Count}, targetContent: {bci.TargetContent}");

        string computed = CrcResultChangeCont("Crc16Maxim(Custom)", bci.TargetContentNotCRC);

        return new {
            ProtocolType = pitm?.Desc?? "NOT MATCHED",
            ProcessingInfo = new {
                Separator = pitm?.Separator,
                TotalLength = bytes.Length,
                ModelName = model.MC_NAME
                , startKey = model.StartKey
                , type = pitm?.PTYPE
            },
            Header = new {
                Id = pitm?.Desc??"UNKNOWN",
                StatusLength = pitm?.KeyIdx+""??"UNKNOWN",
                Separator = pitm?.Separator
            },
            DataBlocks = dataBlocks,
            Crc = new {
                Received = $"{lineBytes[^2]:X2} {lineBytes[^1]:X2}",
                computed = computed
            },
            ModelMeta = new {
                Id = model.Id,
                McName = model.MC_NAME,
                StartKey = model.StartKey
            },
            ParseItemMeta = pitm == null ? null : new {
                Id = pitm.Id,
                Desc = pitm.Desc,
                Ptype = pitm.PTYPE,
                KeyIdx = pitm.KeyIdx,
                Keys = string.Join(" ", pitm.Keys.Select(k => k.ToString("X2"))),
                BlocParseType = pitm.BlocParseType,
                BlocParseLength = pitm.BlocParseLength
            }
        };
    }

    private static string InterpretLine(string content, string? type) {
        if (string.IsNullOrWhiteSpace(content)) return content;
        if (type == "Decimal") {
            try {
                var pts = Regex.Split(content.Trim(), @"[ ,\-]+").Where(p => !string.IsNullOrEmpty(p)).ToList();
                byte[] bs = new byte[pts.Count];
                for (int i = 0; i < pts.Count; i++) if (byte.TryParse(pts[i], out byte b)) bs[i] = b; else return content;
                return Encoding.UTF8.GetString(bs);
            } catch { return content; }
        }
        string cleaned = content.Replace("0x", "", StringComparison.OrdinalIgnoreCase);
        cleaned = Regex.Replace(cleaned, @"[^0-9A-Fa-f]", "");
        if (!string.IsNullOrEmpty(cleaned) && cleaned.Length % 2 == 0) {
            try {
                byte[] bs = new byte[cleaned.Length / 2];
                for (int i = 0; i < bs.Length; i++) bs[i] = Convert.ToByte(cleaned.Substring(i * 2, 2), 16);
                return Encoding.UTF8.GetString(bs);
            } catch { return content; }
        }
        return content;
    }

    public record ParseRequest(string Content, List<string>? Heads, string? InterpretationType, bool? IsRxLengthFirst, bool? IsLittleEndian, int? ByteGroup, bool? IsProtocolMode, string? Model);
    public record TagItemUpdateDto(string Desc, int TagIdx, int TagLength, string DataType, int SortNo);
    public record BinarySampleSaveDto(string Title, string Content);
    public record MCModelCreateDto(string McName, string StartKey);
    public record MCModelUpdateDto(string McName, string StartKey);
    public record ParseItemCreateDto(string Desc, string Ptype, int KeyIdx, string Keys, string BlocParseType, string BlocParseLength);
    public record ParseItemUpdateDto(string Desc, string Ptype, int KeyIdx, string Keys, string BlocParseType, string BlocParseLength);
    public record TagItemCreateDto(string Desc, int TagIdx, int TagLength, string DataType, int SortNo);
    public record TagItemOrderDto(int Id, int SortNo);
    public record AckFindSaveDto(
        string StartCalcArrow, string StartCalcTarget, string StartCalcIdx, string StartCalcValue, string StartCalcEquals,
        string EndCalcArrow, string EndCalcTarget, string EndCalcIdx, string EndCalcValue, string EndCalcEquals
    );
}
