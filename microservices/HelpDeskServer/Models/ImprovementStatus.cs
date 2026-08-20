
using System;
using HelpDeskServer.Services;
using System.Collections.Generic;

namespace HelpDeskServer.Models;

/// <summary>개선 요청 상태</summary>
public enum ImprovementStatus {
  /// <summary>
  /// 접수대기
  /// </summary>
  [System.ComponentModel.DataAnnotations.Display(Name = "대기")]
  Pending,

  /// <summary>
  /// 진행중
  /// </summary>
  [System.ComponentModel.DataAnnotations.Display(Name = "진행")]
  InProgress,

  /// <summary>
  /// 반려
  /// </summary>
  [System.ComponentModel.DataAnnotations.Display(Name = "반려")]
  Rejected,

  /// <summary>
  /// 처리완료
  /// </summary>
  [System.ComponentModel.DataAnnotations.Display(Name = "완료")]
  Completed,


  /// <summary>
  /// 삭제
  /// </summary>
  [System.ComponentModel.DataAnnotations.Display(Name = "삭제")]
  Delete

,
  /// <summary>
  /// 협의
  /// </summary>
  [System.ComponentModel.DataAnnotations.Display(Name = "협의")]
  Consultation,
  /// <summary>
  /// 논의
  /// </summary>
  [System.ComponentModel.DataAnnotations.Display(Name = "논의")]
  Negotiation,

  /// <summary>
  /// 종료
  /// </summary>
  [System.ComponentModel.DataAnnotations.Display(Name = "종료")]
  UserCompleted





}



/// <summary>개선 요청 타입구분</summary>
public enum ImprovementType {
  /// <summary>
  /// 질문
  /// </summary>
  [System.ComponentModel.DataAnnotations.Display(Name = "질문")]
  Question,

  /// <summary>
  /// 개선
  /// </summary>
  [System.ComponentModel.DataAnnotations.Display(Name = "개선")]
  Improvement,

  /// <summary>
  /// 추가
  /// </summary>
  [System.ComponentModel.DataAnnotations.Display(Name = "추가")]
  Addition,

  /// <summary>
  /// 기타
  /// </summary>
  [System.ComponentModel.DataAnnotations.Display(Name = "기타")]
  Etc,

  /// <summary>
  /// 오류
  /// </summary>
  [System.ComponentModel.DataAnnotations.Display(Name = "오류")]
  Error,

  /// <summary>
  /// 버그
  /// </summary>
  [System.ComponentModel.DataAnnotations.Display(Name = "버그")]
  Bug,

  /// <summary>
  /// 긴급/장애
  /// </summary>
  [System.ComponentModel.DataAnnotations.Display(Name = "긴급/장애")]
  Emergency
}