using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using FileServer.Entities;

namespace FileServer.Services;

/// <summary>
/// 파일 처리 비즈니스 로직을 위한 서비스 인터페이스
/// </summary>
public interface IFileService
{
    /// <summary>
    /// 파일 업로드 및 DB 메타데이터 등록
    /// </summary>
    Task<FileMetadata> UploadFileAsync(IFormFile file, string? userId);

    /// <summary>
    /// 파일 다운로드 정보 획득
    /// </summary>
    Task<(Stream FileStream, string ContentType, string OriginalName)> DownloadFileAsync(Guid id);

    /// <summary>
    /// 이미지 파일의 썸네일 획득 (150x150 WebP)
    /// </summary>
    Task<(Stream FileStream, string ContentType)> GetThumbnailAsync(Guid id);

    /// <summary>
    /// 이미지 파일의 중간 크기 이미지 획득 (600x600 WebP)
    /// </summary>
    Task<(Stream FileStream, string ContentType)> GetMediumImageAsync(Guid id);

    /// <summary>
    /// 이미지 파일의 큰 크기 이미지 획득 (1200x1200 WebP)
    /// </summary>
    Task<(Stream FileStream, string ContentType)> GetLargeImageAsync(Guid id);

    /// <summary>
    /// 이미지 파일의 특정 크기 리사이징 스트림 획득 (WebP)
    /// </summary>
    Task<(Stream FileStream, string ContentType)> GetResizedImageAsync(Guid id, int width, int height);

    /// <summary>
    /// 파일 삭제
    /// </summary>
    Task<bool> DeleteFileAsync(Guid id);

    /// <summary>
    /// 파일 메타데이터 조회
    /// </summary>
    Task<FileMetadata?> GetMetadataAsync(Guid id);

    /// <summary>
    /// 파일 그룹에 속하는 다중 파일 업로드
    /// </summary>
    Task<List<FileMetadata>> UploadGroupFilesAsync(List<IFormFile> files, Guid? groupId, string bizType, string? userId);

    /// <summary>
    /// 파일 그룹 내의 파일 목록 조회
    /// </summary>
    Task<List<FileMetadata>> GetGroupFilesAsync(Guid groupId);

    /// <summary>
    /// 파일 그룹 내의 대표 파일 지정
    /// </summary>
    Task<bool> SetRepresentativeFileAsync(Guid groupId, Guid fileId);

    /// <summary>
    /// 비디오 파일의 비동기 트랜스코딩 및 썸네일 추출 시작
    /// </summary>
    Task StartVideoTranscodingAsync(Guid fileId);
}
