## dotnet ef 

가장 쉬운 방법은로.

```
0. migration 폴더 삭제

1.

    DROP TABLE public."__EFMigrationsHistory";  


2.
dotnet ef migrations add InitialCreate  
```
```

3. 자동으로 만들어진 코드를 아래코드로 대체.


using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HelpDeskServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            
        }
    }
}

```

```

4. dotnet ef database update


5. dotnet ef migrations add InitTable


6. dotnet ef database update

이것 이후에 모델에 변화를 주고 
5,6 다시 진행.



```

잘 될껍니다.

문제는 항상 저렇게 해야함 ^^;

아래는 이제 무시...

## 설명서

마이그레이션 도구 dotnet-ef 설치

```
dotnet tool install --global dotnet-ef

```

마이그레이션 실행

```
dotnet ef migrations add InitTable
dotnet ef database update

```

- 이게 잘 안되시는 분이 계실텐데.. 아무도 문의가 없긴 하군요.. ^^

- 이것은 마이그레이션시 초기 테이블 생성 코드가 만들어 지기 때문입니다.
- 이래서 프로젝트때 한사람이 도 맡아서 지시 하는데..
- 우리는 누구나 처리 가능한 상태(능력)를 가지는게 목표 입니다.

- 마이그레이션의 설정을 아래와 같이 준비 합니다.

## Step by step

1. Migrations 폴더 삭제

2. 마이그레이션 초기 명령으로 최초 파일들을 생성

```
dotnet ef migrations add InitialCreate
```

3. Migrations 폴더 안에 날짜시간...\_InitialCreate.cs 파일의 UP method 의 내용물 모두 삭제.. up 껍데기만 남겨 둔다.
   아래와 같은 모습으로.

```

/// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

        }
```

3.1 up 내용은 지우시는게 좋겠어요 . 아래 쿼리를 실행후 up 그대로 두고 아래 절차를 따른다.

```
 DELETE FROM jsini."__EFMigrationsHistory";
```

4. 껍데기 마이그레이션을 한다.

```
dotnet ef database update
```

5. 실제 마이그레이션을 한다.

```
dotnet ef migrations add 영문과숫자를이용한제목정도
```

6. db 에 반영.

```
dotnet ef database update
```

7. 이후 부터는 HelpDeskServer 폴더에서 아래 명령을 이용하여 사용한다.

```
dotnet ef migrations add 영문과숫자를이용한제목정도
dotnet ef database update

```

## step by step 2

```

TRUNCATE table "__EFMigrationsHistory"

```

1. 모든 마이그레이션과 AppDbContextModelSnapshot.cs 파일을 삭제합니다.
2. 새 Initial 마이그레이션을 생성합니다.

```
dotnet ef migrations add InitialCreate
```

3.  dotnet ef database update를 실행합니다(실패 예상).

```
dotnet ef database update
```

4.  Initial 마이그레이션 파일의 Up 메서드 내용을 수동으로 삭제합니다.

```

/// <inheritdoc />
     protected override void Up(MigrationBuilder migrationBuilder)
     {

     }
```

5.  dotnet ef database update를 다시 실행하여 마이그레이션을 기록합니다.

```
dotnet ef database update
```

6.  Up 메서드 내용을 복원합니다.

```

/// <inheritdoc />
     protected override void Up(MigrationBuilder migrationBuilder)
     {
기존꺼 복구
     }
```

7.  모델에서 FileSize 속성을 제거하고 RemoveFileSize 마이그레이션을 생성합니다.
8.  FileSize 속성을 다시 추가하고 AddFileSize 마이그레이션을 생성합니다.
9.  dotnet ef database update를 실행합니다.

```














TRUNCATE table "__EFMigrationsHistory"


rm -rf Migrations

dotnet ef migrations add InitialCreate -- --ignore-changes












using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HelpDeskServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        { }
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        { }
    }
}




dotnet ef database update


dotnet ef migrations add renew1234


dotnet ef database update


```
