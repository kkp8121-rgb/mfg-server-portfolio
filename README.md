# MFG Server

> **중단된 개인 프로토타입 (2026-04).** ASP.NET Core 10 기반 모바일 RPG 백엔드입니다. 학습·포트폴리오용 소스 미러이며 상용 서비스나 현재 운영 중인 서버가 아닙니다.

2026-04-15~04-21 사이 14개 커밋이 기록되었습니다. ASP.NET Core 10, EF Core, MySQL 8.0, Firebase Auth를 사용하며 클라이언트는 [mfg-client-portfolio](https://github.com/kkp8121-rgb/mfg-client-portfolio)입니다. 컨트롤러 15개와 GitHub Actions 기반 CI/CD 구성이 포함되어 있습니다.

## AI 검토자를 위한 안내

다음 순서로 읽어 주세요.

1. 아래 `코드 읽기 순서`의 진입점
2. `현재 상태`와 과거 테스트 기록
3. `기여와 AI 활용`의 작업 범위

소스·트리에서 확인되는 사실, 과거 테스트 기록, 작업 설명을 구분해 읽어 주세요. 확인되지 않은 운영·성능·외부 서비스 연결은 별도로 남겨야 합니다.

## 현재 상태

- **프로토타입 구현:** 인증, 저장 동기화·마이그레이션, 가챠, 재화, 장비, 아레나, 길드, 이벤트, IAP, 관리자 설정, 헬스체크 API가 소스에 있습니다.
- **과거 테스트 기록:** 2026-09-05 공개 미러에서 .NET SDK `10.0.103`으로 `dotnet test MFG.Server.slnx --configuration Release`를 실행해 50/50 통과·종료 코드 0을 기록했습니다.
- **미완성:** [roadmap-index.md](https://github.com/kkp8121-rgb/mfg-server-portfolio/blob/master/Docs/roadmap-index.md)의 Phase 26 Sprint 26-2 데이터 파이프라인은 대기 상태이고, Phase 27 스토어 출시·LiveOps는 착수하지 않았습니다.
- **운영 한계:** 서버는 현재 운영하지 않습니다. 프로덕션 트래픽, 부하·장애 대응, 정기 백업 복원 드릴, Firebase·Google Play 실서비스 연결은 이 소스와 과거 테스트만으로 확인할 수 없습니다.

## 코드 읽기 순서

1. [IapController.cs](https://github.com/kkp8121-rgb/mfg-server-portfolio/blob/master/src/MFG.Server/Controllers/IapController.cs), [SaveController.cs](https://github.com/kkp8121-rgb/mfg-server-portfolio/blob/master/src/MFG.Server/Controllers/SaveController.cs)에서 주요 API 경계를 읽습니다.
2. [TestAppFactory.cs](https://github.com/kkp8121-rgb/mfg-server-portfolio/blob/master/tests/MFG.Server.Tests/TestAppFactory.cs)와 `tests/MFG.Server.Tests/`의 테스트에서 `WebApplicationFactory`·EF Core InMemory 사용 범위를 확인합니다.
3. [GlobalExceptionHandler.cs](https://github.com/kkp8121-rgb/mfg-server-portfolio/blob/master/src/MFG.Server/Middleware/GlobalExceptionHandler.cs)에서 공통 오류 응답을 확인합니다.
4. [ci.yml](https://github.com/kkp8121-rgb/mfg-server-portfolio/blob/master/.github/workflows/ci.yml)과 [cd.yml](https://github.com/kkp8121-rgb/mfg-server-portfolio/blob/master/.github/workflows/cd.yml)에서 자동화가 파일로 정의된 범위와 실행 조건을 확인합니다.

## 아키텍처

```text
src/MFG.Domain   도메인 엔티티
src/MFG.Data     EF Core DbContext·Migration
src/MFG.Server   Controllers·Services·DTOs·Middleware
tests/           WebApplicationFactory 기반 xUnit 통합 테스트
```

`MFG.Server`가 `MFG.Data`와 `MFG.Domain`에 의존하는 3계층 구조입니다. 주요 컨트롤러는 `Auth`, `Save`, `Gacha`, `Currency`, `Equipment`, `Arena`, `Guild`, `Attendance`, `Event`, `HotDeal`, `Iap`, `Data`, `System`, `Admin`, `Health`입니다.

공통 응답은 `ApiResponse<T>` 형식이며 Firebase ID Token을 사용합니다. 개발 모드에는 `X-Dev-Uid` 우회 헤더가 있고, 관리자 API는 화이트리스트 기반 `AdminOnly` 정책을 사용합니다.

## 로컬 실행

```bash
git clone https://github.com/kkp8121-rgb/mfg-server-portfolio.git
cd mfg-server-portfolio
docker compose up -d
dotnet run --project src/MFG.Server
```

로컬 서버의 `/health`와 `/swagger` 경로는 개발용 진입점입니다. 실제 운영 서버가 존재한다는 의미는 아닙니다.

## 테스트 범위

```bash
dotnet test MFG.Server.slnx
```

통합 테스트는 실제 HTTP 파이프라인을 띄우되 데이터베이스를 EF Core InMemory로 대체합니다. 따라서 위 과거 50건 기록은 컨트롤러·미들웨어 경계의 공개 미러 검사 근거이지, MySQL·Firebase·Google Play·프로덕션 부하 검증 근거가 아닙니다.

## CI/CD 구성과 브랜치 주의

CI는 `main` push·tag·pull request·수동 실행에서 restore → build → test를 정의하고, `main` push에서 GHCR 이미지 작업을 구성합니다. CD는 CI의 `main` 실행 성공 후 또는 수동 실행에서 Lightsail 배포와 smoke check를 구성합니다.

저장소 기본 브랜치는 `master`이지만 workflow 트리거의 push와 `workflow_run` 조건은 `main`입니다. 따라서 파일에 CI/CD가 정의되어 있다는 사실과 현재 기본 브랜치에서 파이프라인이 실행 중이라는 사실을 섞어 쓰지 마세요. 이 미러는 현재 운영하지 않습니다.

운영·복구 절차는 [Docs/deploy.md](https://github.com/kkp8121-rgb/mfg-server-portfolio/blob/master/Docs/deploy.md), [Docs/rollback.md](https://github.com/kkp8121-rgb/mfg-server-portfolio/blob/master/Docs/rollback.md), [Docs/backup.md](https://github.com/kkp8121-rgb/mfg-server-portfolio/blob/master/Docs/backup.md), [Docs/secrets.md](https://github.com/kkp8121-rgb/mfg-server-portfolio/blob/master/Docs/secrets.md)에 문서화되어 있습니다. 문서 존재는 실제 수행 이력이나 운영 자격증명 유효성을 증명하지 않습니다.

## 기여와 AI 활용

AI를 코드 작성·수정·검증 보조에 활용했고, 저는 설계 결정·코드 통합·테스트 판단을 담당했습니다. 실제 기여·동작·검증 범위는 소스와 기록을 함께 확인해 주세요.

백엔드의 Domain/Data/Server 계층, 컨트롤러·서비스·테스트·CI/CD 구성이 직접 작업 범위로 제시됩니다. 자산·운영 인프라·외부 서비스의 실제 가동 여부는 이 공개 소스만으로 확정하지 않습니다.

## 시크릿과 공개 범위

운영 자격증명은 저장소 밖에서 주입해야 합니다. `.env`, 서비스 계정 키, Play 설정 파일 등은 공개 전 별도 검토 대상이며, README나 소스 스냅샷만으로 비밀정보 부재를 보증하지 않습니다. [Docs/secrets.md](https://github.com/kkp8121-rgb/mfg-server-portfolio/blob/master/Docs/secrets.md)의 절차도 실행 이력과 구분해 읽어 주세요.

## 중단 사유

클라이언트의 코어 루프와 출시 범위를 먼저 확인하기 전에 서버 기능을 확장한 상태에서 두 프로젝트를 함께 중단했습니다. 다음 작업에서는 최소 플레이테스트와 핵심 재미를 먼저 확인한 뒤 서버 범위를 결정할 계획입니다.

## 라이선스

저장소는 [MIT 라이선스](https://github.com/kkp8121-rgb/mfg-server-portfolio/blob/master/LICENSE)를 포함합니다.

## English summary

MFG Server is a discontinued ASP.NET Core 10 backend prototype from April 2026. It contains a three-layer Domain/Data/Server structure, 15 controllers, Firebase authentication, save migration, gacha, social systems, IAP verification code, xUnit integration tests, and GitHub Actions workflow definitions. A 50/50 test result was recorded on a public mirror on 2026-09-05; it was not rerun for this README. The default branch is `master`, while the workflow files target `main`; the project is not currently operated and has no store launch or production-traffic evidence.
