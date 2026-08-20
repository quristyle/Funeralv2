

using System;
using HelpDeskServer.Services;
using System.Collections.Generic;

namespace HelpDeskServer.Models;


/// <summary>프로젝트 엔티티</summary>
public class Project : BaseEntity
{
    /// <summary>Project 명</summary>
    public string Name { get; set; } = string.Empty;


    /// <summary>
    /// 프로젝트 시작일
    /// </summary>
    public DateTime? ProjectStart { get; set; }

    /// <summary>
    /// 프로젝트 종료일
    /// </summary>
    public DateTime? ProjectEnd { get; set; }



    /// <summary>
    /// 담당 팀 ID (Foreign Key)
    /// </summary>
    public int? TeamId { get; set; }
    /// <summary>
    /// 담당 팀 (Navigation property)
    /// </summary>
    public Team? Team { get; set; }
}
