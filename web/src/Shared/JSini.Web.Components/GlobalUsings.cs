// ASP.NET Core 웹 프로젝트(Sdk.Web)는 이런 것들을 암묵 using 으로 넣어 주지만,
// Razor 클래스 라이브러리(Sdk.Razor)는 넣어 주지 않는다. 파일마다 적는 대신
// 여기 한 번 모은다 — Shell 에서 옮겨 온 코드가 그대로 컴파일되게 하려는 것이기도 하다.
global using Microsoft.AspNetCore.Http;
global using Microsoft.Extensions.Logging;
