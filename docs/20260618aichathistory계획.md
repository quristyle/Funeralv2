# AI 채팅 내역(최근 1일) 저장 및 복원 구현 계획서

**작성일**: 2026-06-18

이 문서는 사용자와 AI(AIAgentServer) 간의 채팅 기록을 데이터베이스에 저장하고, 사용자가 다음 접속 시 최근 24시간 이내의 대화 내역만 화면에 복원하여 보여주는 기능을 구현하기 위한 마스터 플랜입니다.

---

## 1. 아키텍처 개요 (Architecture Strategy)

*   **저장소 위치**: `AIAgentServer`가 전용 데이터베이스 구조(예: PostgreSQL 스키마 `scom_ai`)를 통해 직접 관리합니다.
*   **저장 방식**: 클라이언트가 별도의 저장 API를 호출하지 않고, 기존 `POST /ai/chat` API 호출 시 내부적으로 트랜잭션을 묶어 사용자의 질문과 AI의 답변을 함께 기록합니다.
*   **데이터 노출 규칙 (1일 치 필터링)**: 조회 API(`GET /ai/chat/history`)에서 DB 쿼리 시 `CreatedAt >= DateTime.UtcNow.AddDays(-1)` 조건을 부여하여 24시간 이내의 데이터만 반환합니다. 

---

## 2. 백엔드 구현 계획 (`AIAgentServer`)

### 2.1. 엔티티 설계 (`Entities/ChatHistory.cs`)
```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace AIAgentServer.Entities;

[Table("chat_histories", Schema = "scom_ai")]
public class ChatHistory : BaseEntity<string>
{
    public ChatHistory() { Id = Guid.NewGuid().ToString(); }

    [Required]
    public string UserId { get; set; } = string.Empty; // JWT에서 추출
    
    [Required]
    public string Role { get; set; } = string.Empty; // "user" 또는 "assistant"
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    // (선택) 여러 채팅방(세션)을 구분할 필요가 있을 때 사용
    public string? SessionId { get; set; } 
}
```

### 2.2. 데이터베이스 연동 및 마이그레이션
1.  `AIAgentServer.csproj`에 EF Core 패키지 (Npgsql 등) 추가.
2.  `Data/AppDbContext.cs` 생성 및 `ChatHistories` DbSet 구성.
3.  `Program.cs`에 DbContext 및 인증(JWT) 미들웨어 구성. (UserId를 식별하기 위함)
4.  `dotnet ef migrations add`로 마이그레이션 파일 생성 및 적용.

### 2.3. 서비스 및 엔드포인트 수정
1.  **POST `/chat` (기존 API 확장)**:
    *   JWT 토큰에서 `UserId` 추출.
    *   입력받은 사용자 메시지(User)를 DB에 Insert.
    *   LLM 추론 결과 획득.
    *   결과 메시지(Assistant)를 DB에 Insert.
2.  **GET `/chat/history` (신규 API)**:
    *   JWT 토큰에서 `UserId` 추출.
    *   `CreatedAt >= DateTime.UtcNow.AddDays(-1)` 조건으로 데이터 조회 후 정렬하여 반환.

### 2.4. (옵션) 백그라운드 데이터 정리
데이터 누적 방지를 위해 `IHostedService` 또는 `BackgroundService`를 구현하여 매일 새벽, 24시간이 지난 `ChatHistory` 데이터를 물리적으로 `DELETE`하는 스케줄러 추가 고려.

---

## 3. 프론트엔드 구현 계획 (`fronts/apps/funeralv2`)

### 3.1. API 클라이언트 추가 (`api/ai/chat.ts`)
```typescript
/**
 * 최근 24시간 이내의 채팅 내역 불러오기
 */
export function getChatHistory() {
  return requestClient.get<ChatMessage[]>('/ai/chat/history');
}
```

### 3.2. 화면 연동 (`views/ai/chat/index.vue`)
1.  **화면 진입 시 (`onMounted`)**:
    *   `getChatHistory()`를 호출하여 서버에서 이전 데이터를 가져옵니다.
2.  **데이터 분기 처리**:
    *   **내역이 없는 경우 (빈 배열)**: 기존처럼 "안녕하세요! 저는..."과 같은 초기 안내 메시지를 `messages` 배열에 푸시합니다.
    *   **내역이 있는 경우**: 반환받은 과거 대화 배열을 `messages` 상태에 매핑하여 이전 대화창을 완벽히 복원합니다.
3.  **메시지 전송 시 무결성 유지**:
    *   사용자가 메시지를 보낼 때마다 화면 상의 `messages` 배열을 갱신하되, 백엔드에서 이미 DB 저장을 처리하므로 프론트엔드에서는 렌더링에만 집중합니다.
