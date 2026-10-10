using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.Admin.Components.Pages;

public partial class NewsBreaking
{
    public class NewsItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string Category { get; set; } = "";
        public string Urgency { get; set; } = "";
        public string Source { get; set; } = "";
        public DateTime PublishedAt { get; set; }
    }

    private List<NewsItem>? Data { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        // Dummy data for now, since API isn't fully implemented
        Data = new List<NewsItem>
        {
            new NewsItem { Id = 1, Title = "울산 앞바다 지진 발생", Category = "재난", Urgency = "🔴 속보", Source = "NAVER", PublishedAt = DateTime.UtcNow, Url = "https://news.naver.com" },
            new NewsItem { Id = 2, Title = "태풍 북상 중, 전국 비", Category = "날씨", Urgency = "🟠 주요뉴스", Source = "BIG KINDS", PublishedAt = DateTime.UtcNow.AddMinutes(-30), Url = "https://bigkinds.or.kr" }
        };
        await Task.CompletedTask;
    }
}
