# PartMaker.Client 라이브러리 설계서

## 개요

PartMaker CLI API를 타입 안전하게 래핑하는 .NET 8.0 라이브러리입니다.
PartMaker 프로젝트에 대한 직접 참조 없이, 외부 프로세스 호출(stdin/stdout)을 통해 통신합니다.

## 아키텍처

```
┌─────────────────────────────────────────────────────────────┐
│                    Consumer Application                      │
│                  (예: PartMaker.CliDemo)                     │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    PartMaker.Client                          │
│  ┌───────────────────────────────────────────────────────┐  │
│  │              IPartMakerClient                         │  │
│  │  - CreateAsync(CreateOptions)                         │  │
│  │  - EditAsync(EditOptions)                             │  │
│  │  - RegisterAsync(RegisterOptions)                     │  │
│  │  - UpdateAsync(UpdateOptions)                         │  │
│  │  - SelectAsync(SelectOptions)                         │  │
│  └───────────────────────────────────────────────────────┘  │
│                              │                               │
│                              ▼                               │
│  ┌───────────────────────────────────────────────────────┐  │
│  │              PartMakerClient                          │  │
│  │  - Process.Start() 호출                               │  │
│  │  - stdin/stdout 리다이렉트                            │  │
│  │  - 타임아웃 및 취소 지원                              │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼ (외부 프로세스 호출)
┌─────────────────────────────────────────────────────────────┐
│                     PartMaker.exe                            │
│                    (CLI 모드 실행)                           │
└─────────────────────────────────────────────────────────────┘
```

## 프로젝트 구조

```
src/PartMaker.Client/
├── PartMaker.Client.csproj
├── IPartMakerClient.cs              # 클라이언트 인터페이스
├── PartMakerClient.cs               # 클라이언트 구현
├── PartMakerClientOptions.cs        # 클라이언트 설정
├── Types/
│   ├── ExitCode.cs                  # 종료 코드 열거형
│   └── PartMakerResult.cs           # 결과 타입 (제네릭)
└── Options/
    ├── CreateOptions.cs             # create 명령 옵션
    ├── EditOptions.cs               # edit 명령 옵션
    ├── RegisterOptions.cs           # register 명령 옵션
    ├── UpdateOptions.cs             # update 명령 옵션
    └── SelectOptions.cs             # select 명령 옵션
```

## 네임스페이스 구조

| 폴더 | 네임스페이스 | 내용 |
|------|-------------|------|
| 루트 | `PartMaker.Client` | IPartMakerClient, PartMakerClient, PartMakerClientOptions |
| Types/ | `PartMaker.Client.Types` | ExitCode, PartMakerResult |
| Options/ | `PartMaker.Client.Options` | CreateOptions, EditOptions, RegisterOptions, UpdateOptions, SelectOptions |

## API 설계

### IPartMakerClient 인터페이스

| 메서드 | 설명 | 반환값 |
|--------|------|--------|
| `CreateAsync` | 새 Part 생성 (v0.0.0.0) | Part XML 내용 |
| `EditAsync` | Part 편집 | 수정된 XML 내용 |
| `RegisterAsync` | Library에 등록 (v1.0.0.0) | 등록된 파일 경로 |
| `UpdateAsync` | Library Part 갱신 | 변경 보고서 JSON |
| `SelectAsync` | Part 선택 다이얼로그 | 선택된 XML 경로 |

### CLI 명령 매핑

| 메서드 | CLI 명령 | stdin | stdout |
|--------|----------|-------|--------|
| CreateAsync | `create -l <lib> -i <img>` | - | Part XML |
| EditAsync | `edit -l <lib> [-i <img>]` | XML | 수정된 XML |
| RegisterAsync | `register -l <lib> -i <img> [-f]` | XML | 등록 경로 |
| UpdateAsync | `update -l <lib> [-d]` | XML | 변경 보고서 |
| SelectAsync | `select -l <lib>` | - | XML 경로 |

### ExitCode 열거형

| 값 | 이름 | 설명 |
|----|------|------|
| 0 | Success | 성공 |
| 1 | Cancelled | 사용자 취소 |
| 2 | InvalidArgs | 잘못된 인자 |
| 3 | FileNotFound | 파일 없음 |
| 4 | AlreadyExists | Part 이미 존재 |
| 5 | PartNotFound | Part 없음 |
| 6 | NoChanges | 변경 없음 |
| 7 | InvalidVersion | 잘못된 버전 |
| 10 | InternalError | 내부 오류 |

### PartMakerResult<T>

결과를 표현하는 readonly struct:

| 속성 | 타입 | 설명 |
|------|------|------|
| ExitCode | ExitCode | CLI 종료 코드 |
| Value | T? | 성공 시 결과 값 |
| Error | string? | 실패 시 에러 메시지 |
| IsSuccess | bool | 성공 여부 (ExitCode == 0) |
| IsCancelled | bool | 취소 여부 (ExitCode == 1) |

### Options 레코드

#### CreateOptions
```
LibraryPath: string  - Library 디렉토리 경로 (필수)
ImagePath: string    - 소스 이미지 파일 경로 (필수)
```

#### EditOptions
```
LibraryPath: string  - Library 디렉토리 경로 (필수)
XmlContent: string   - 편집할 Part XML 내용 (필수)
ImagePath: string?   - 소스 이미지 파일 경로 (선택)
```

#### RegisterOptions
```
LibraryPath: string  - Library 디렉토리 경로 (필수)
XmlContent: string   - 등록할 Part XML 내용 (필수)
ImagePath: string    - 소스 이미지 파일 경로 (필수)
Force: bool          - 덮어쓰기 여부 (기본값: false)
```

#### UpdateOptions
```
LibraryPath: string  - Library 디렉토리 경로 (필수)
XmlContent: string   - 수정된 Part XML 내용 (필수)
DryRun: bool         - 변경 사항만 확인 (기본값: false)
```

#### SelectOptions
```
LibraryPath: string  - Library 디렉토리 경로 (필수)
```

### PartMakerClientOptions

| 속성 | 타입 | 설명 | 기본값 |
|------|------|------|--------|
| ExecutablePath | string | PartMaker.exe 경로 | (필수) |
| WorkingDirectory | string? | 작업 디렉토리 | 현재 디렉토리 |
| DebugMode | bool | --debug 플래그 추가 | false |
| TimeoutMs | int | 타임아웃 (ms) | 60000 |

## 사용 예시

### 기본 사용법

```csharp
using PartMaker.Client;
using PartMaker.Client.Options;

// 클라이언트 생성
var options = new PartMakerClientOptions
{
    ExecutablePath = @"C:\Path\To\PartMaker.exe",
    WorkingDirectory = @"C:\WorkDir",
    TimeoutMs = 120000
};

using var client = new PartMakerClient(options);

// Create
var createResult = await client.CreateAsync(new CreateOptions(
    LibraryPath: @"C:\Library",
    ImagePath: @"C:\Images\sample.tvraw"
));

if (createResult.IsSuccess)
{
    var xmlContent = createResult.Value;
    Console.WriteLine($"Created: {xmlContent?.Length} chars");
}
```

### 전체 워크플로우

```csharp
// 1. Create - Part 생성 (v0.0.0.0)
var createResult = await client.CreateAsync(new CreateOptions(
    LibraryPath: libraryPath,
    ImagePath: imagePath
));

if (!createResult.IsSuccess)
{
    if (createResult.IsCancelled)
        Console.WriteLine("사용자 취소");
    else
        Console.WriteLine($"에러: {createResult.Error}");
    return;
}

var xmlContent = createResult.Value!;

// 2. Edit - 편집 (선택적)
var editResult = await client.EditAsync(new EditOptions(
    LibraryPath: libraryPath,
    XmlContent: xmlContent,
    ImagePath: imagePath
));

if (editResult.IsSuccess)
    xmlContent = editResult.Value!;

// 3. Register - Library에 등록 (v1.0.0.0)
var registerResult = await client.RegisterAsync(new RegisterOptions(
    LibraryPath: libraryPath,
    XmlContent: xmlContent,
    ImagePath: imagePath
));

if (registerResult.IsSuccess)
    Console.WriteLine($"등록됨: {registerResult.Value}");
```

### 취소 지원

```csharp
using var cts = new CancellationTokenSource();

// 30초 후 자동 취소
cts.CancelAfter(TimeSpan.FromSeconds(30));

try
{
    var result = await client.CreateAsync(
        new CreateOptions(libraryPath, imagePath),
        cts.Token
    );
}
catch (OperationCanceledException)
{
    Console.WriteLine("작업이 취소되었습니다.");
}
```

## 의존성

- 외부 종속성 없음
- System.Diagnostics.Process만 사용
- PartMaker 프로젝트에 대한 참조 없음

## 특징

1. **타입 안전성**: 모든 옵션과 결과가 강타입으로 정의됨
2. **비동기 API**: 모든 메서드가 async/await 지원
3. **취소 지원**: CancellationToken으로 작업 취소 가능
4. **타임아웃**: 설정 가능한 타임아웃으로 무한 대기 방지
5. **리소스 관리**: IDisposable 구현으로 프로세스 리소스 정리
6. **독립성**: PartMaker 프로젝트에 대한 직접 참조 없음
