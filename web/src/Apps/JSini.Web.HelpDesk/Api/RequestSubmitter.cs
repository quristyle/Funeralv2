using System.Net.Http.Headers;
using System.Text.Json;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Http;
using Microsoft.JSInterop;

namespace JSini.Web.HelpDesk.Api;

/// <summary>요청 등록을 화면 밖에서 마친다. 「등록」을 누르면 화면은 곧바로 닫히고 그림 올리기 · 요청 만들기 · 첨부 올리기는 여기서 이어 간다.
/// 회로 수명(scoped)이라 등록 화면이 사라져도 살아 있고, 결과는 토스트로 말한다(토스트 판은 레이아웃에 있어 화면을 옮겨도 남는다).
/// 시작은 화면의 이벤트 처리기에서 하므로 await 뒤가 회로의 동기화 문맥으로 돌아온다 — ConfigureAwait(false) 를 붙이지 않는다.</summary>
public sealed class RequestSubmitter(HelpDeskApi api, ContentImages images, Toasts toasts, IJSRuntime js)
{
    /// <summary>보낼 것 한 벌. Files 는 화면의 고르개에서 떼어 와 이 작업이 지운다.</summary>
    /// <param name="Title">제목(이미 비지 않은 것으로 확인했다)</param>
    /// <param name="Content">편집기에서 읽은 본문 HTML. data URI 그림이 남아 있을 수 있다.</param>
    /// <param name="Files">첨부. 임시 파일은 이 작업의 것이다.</param>
    /// <param name="DraftKey">임시 보관 열쇠. 실패하면 이 열쇠로 글을 되돌려 둔다.</param>
    public sealed record Job(string Title, string Content, IReadOnlyList<PickedFile> Files, string DraftKey);

    private int _pending;

    /// <summary>요청이 들어갔다(새 요청 번호). 「요청 처리」가 듣고 목록을 다시 읽는다 — 화면이 먼저 열리므로 그때 읽은 목록에는 아직 없다.</summary>
    public event Action<int>? Created;

    /// <summary>아직 끝나지 않은 등록 수.</summary>
    public int Pending => Volatile.Read(ref _pending);

    /// <summary>뒤에서 등록을 시작하고 곧바로 돌아온다. 던지지 않는다 — 실패는 토스트로 말한다.</summary>
    public void Start(Job job)
    {
        Interlocked.Increment(ref _pending);
        _ = RunAsync(job);
    }

    private async Task RunAsync(Job job)
    {
        try
        {
            // 가로채기를 지나온 그림이 남아 있으면 파일로 바꾼다. base64 본문을 그대로 저장하지 않는다.
            var absorbed = await images.AbsorbAsync(job.Content);

            if (absorbed.Url is null)
            {
                await FailAsync(job, absorbed.Error ?? "본문에 든 그림을 올리지 못했습니다.");
                return;
            }

            int? id;
            try
            {
                id = await CreateAsync(job.Title, RequestContentHtml.ToStored(absorbed.Url));
            }
            catch (ApiException ex)
            {
                await FailAsync(job, $"요청을 등록하지 못했습니다 — {ex.Message}");
                return;
            }

            if (id is null)
            {
                await FailAsync(job, "요청을 등록했는데 번호를 받지 못했습니다. 요청 처리 목록을 확인해 주십시오.");
                return;
            }

            var failed = await UploadAsync(id.Value, job.Files);

            if (failed == 0)
            {
                toasts.Show("요청을 등록했습니다.");
            }
            else
            {
                // 요청은 이미 들어갔으니 다시 등록하면 안 된다 — 첨부는 상세에서 올린다.
                toasts.Show($"요청은 등록했지만 첨부 {failed}개를 올리지 못했습니다. "
                    + $"요청 {id} 번 상세에서 다시 올려 주십시오.", NoticeTone.Error);
            }

            Created?.Invoke(id.Value);
        }
        catch (Exception ex)
        {
            // 화면이 없는 자리라 새면 아무도 못 본다. 무엇이든 여기서 말하고 끝낸다.
            await FailAsync(job, $"요청을 등록하지 못했습니다 — {ex.Message}");
        }
        finally
        {
            DeleteFiles(job.Files);
            Interlocked.Decrement(ref _pending);
        }
    }

    /// <summary>실패를 알린다. 쓰던 글은 임시 보관으로 되돌려 두어 요청 등록을 다시 열면 되살아난다.</summary>
    private async Task FailAsync(Job job, string message)
    {
        var kept = await RestoreDraftAsync(job);

        toasts.Show(kept
            ? $"{message} 쓰던 글은 요청 등록을 다시 열면 되살아납니다."
            : message, NoticeTone.Error);
    }

    /// <summary>임시 보관에 글을 다시 적는다. 그 사이 새로 쓰기 시작한 글이 있으면 덮지 않는다(false).</summary>
    private async Task<bool> RestoreDraftAsync(Job job)
    {
        try
        {
            await using var draft = await js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.HelpDesk/js/request-draft.js");

            return await draft.InvokeAsync<bool>("save", job.DraftKey, job.Title, job.Content);
        }
        catch (JSException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            // 회로가 닫혔다.
            return false;
        }
    }

    /// <summary>요청을 만든다. 폼으로 보낸다 — 칸 이름(Title · Description)이 서버가 읽는 그대로여야 한다. 모르는 칸은 말없이 버려져 빈 값으로 저장된다.
    /// 고객 번호는 보내지 않는다 — 요청자는 글을 쓴 사람으로 고정이고 서버는 안 보내면 그렇게 한다(RequesterProvisioner.ResolveAsync).</summary>
    private async Task<int?> CreateAsync(string title, string description)
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(title.Trim()), "Title" },
            { new StringContent(description), "Description" },
        };

        var created = await api.PostMultipartAsync<ImprovementRequest>("requests", form);
        return created?.Id;
    }

    /// <summary>첨부를 한 번에 올린다. 돌려주는 값은 실패한 개수. 하나라도 실패하면 서버가 502 로 어느 것인지 담아 준다.</summary>
    private async Task<int> UploadAsync(int requestId, IReadOnlyList<PickedFile> files)
    {
        if (files.Count == 0)
        {
            return 0;
        }

        var streams = new List<Stream>(files.Count);
        try
        {
            using var form = new MultipartFormDataContent
            {
                { new StringContent("ImprovementRequest"), "entityType" },
                { new StringContent(requestId.ToString()), "entityId" },
            };

            foreach (var file in files)
            {
                var stream = file.OpenRead();
                streams.Add(stream);

                var part = new StreamContent(stream);
                part.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

                // 칸 이름이 `files` 여야 한다 — 서버가 그 이름으로 묶어 받는다.
                form.Add(part, "files", file.Name);
            }

            await api.PostMultipartAsync<JsonElement?>("files/upload", form);
            return 0;
        }
        catch (Exception ex) when (ex is ApiException or IOException or FormatException)
        {
            return files.Count;
        }
        finally
        {
            foreach (var stream in streams)
            {
                await stream.DisposeAsync();
            }
        }
    }

    /// <summary>고르개에서 떼어 온 임시 파일을 이 작업의 자리로 옮긴다. 고르개는 화면이 닫힐 때 제 폴더를 지우므로 그대로 두면 올리기 전에 사라진다.
    /// 같은 디스크 안의 옮기기라 바이트를 다시 쓰지 않는다.</summary>
    public static IReadOnlyList<PickedFile> TakeOver(IReadOnlyList<PickedFile> files)
    {
        if (files.Count == 0)
        {
            return [];
        }

        var dir = Path.Combine(Path.GetTempPath(), "jsini-hd-submit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var moved = new List<PickedFile>(files.Count);

        foreach (var file in files)
        {
            var target = Path.Combine(dir, Guid.NewGuid().ToString("N"));

            try
            {
                File.Move(file.TempPath, target);
                moved.Add(file with { TempPath = target });
            }
            catch (IOException)
            {
                // 옮기지 못했으면 원래 자리를 쓴다 — 화면이 닫히기 전에 읽히면 그대로 올라간다.
                moved.Add(file);
            }
        }

        return moved;
    }

    private static void DeleteFiles(IReadOnlyList<PickedFile> files)
    {
        foreach (var dir in files.Select(f => Path.GetDirectoryName(f.TempPath)).Distinct())
        {
            if (dir is null || !Path.GetFileName(dir).StartsWith("jsini-hd-submit-", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
                // 임시 파일이다. 못 지워도 업무에 영향이 없다.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
