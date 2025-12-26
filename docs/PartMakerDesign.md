# PartMaker 설계 문서

## 1. 개요

### 1.1 목적
PCB 검사 프로파일(.tv_lib)의 핀 기하정보를 TVRaw 이미지 위에 시각화하고, 검사 파이프라인(Segmentation, Feature Extraction, Judgment)을 편집하는 도구

### 1.2 주요 기능
- Library 폴더 관리 (DLL 로드, Part 탐색)
- .tv_lib 파일 로드/저장
- TVRaw 이미지 위에 핀 기하정보 렌더링
- 핀 선택 및 정보 표시
- 좌표계 설정 (LL/LR/UL/UR, CCW/CW)
- Segmentation 파이프라인 편집
- Feature Extraction 설정
- Judgment 규칙 편집

---

## 2. 아키텍처

### 2.1 설계 원칙
- **MVVM 패턴**: View와 비즈니스 로직 분리
- **Adapters 패턴**: 변경 가능성 높은 외부 환경 격리 (XML 구조, DLL/API)
- **DDD Aggregate**: Part를 Aggregate Root로 비즈니스 규칙 캡슐화
- **최소 인터페이스**: 변경 가능성 높은 부분만 인터페이스로 추상화

### 2.2 폴더 구조

```
┌─────────────────────────────────────────────────────────────────┐
│  ViewModels                                                      │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │ MainViewModel, PartBrowserViewModel,                     │    │
│  │ SegmentationViewModel, FeatureExtractionViewModel,       │    │
│  │ JudgmentViewModel                                        │    │
│  └────────────────────────┬────────────────────────────────┘    │
│                           │                                      │
├───────────────────────────┼──────────────────────────────────────┤
│  Services                 │                                      │
│  ┌────────────────────────┴────────────────────────────────┐    │
│  │ LibraryService      - Library 상태 관리                  │    │
│  │ PartService         - Part 로드/저장 조율                │    │
│  │ Mappers/            - ProfileData ↔ Config 변환         │    │
│  │                                                          │    │
│  │ Interfaces:                                              │    │
│  │ • IPartLoader       - XML 구조 변경 격리                 │    │
│  │ • IAlgorithmProvider- DLL → API 변경 격리                │    │
│  │ • IImageLoader, IDialogService, IAppSettingsRepository   │    │
│  └──────────────────────────────────────────────────────────┘    │
│           ▲                                                      │
├───────────┼──────────────────────────────────────────────────────┤
│  Models   │  (Domain)                                            │
│  ┌────────┴─────────────────────────────────────────────────┐   │
│  │ Part (Aggregate Root)                                     │   │
│  │ Entities: Pad, SegmentationConfig, FeatureExtractionConfig│   │
│  │ ValueObjects: SegmentationInfo, FeatureExtractionInfo,    │   │
│  │               ProfileData (저장용 DTO)                    │   │
│  │ Enums, Exceptions                                         │   │
│  └───────────────────────────────────────────────────────────┘   │
│                                                                   │
├───────────────────────────────────────────────────────────────────┤
│  Adapters  (외부 환경 격리)                                       │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐               │
│  │ Xml/        │  │ Dll/        │  │ Wpf/        │               │
│  │ XmlPartLoader│  │ DllAlgorithm│  │ WpfDialog   │               │
│  │ XmlModels   │  │ Provider    │  │ Service     │               │
│  └─────────────┘  └─────────────┘  └─────────────┘               │
│  ┌─────────────┐  ┌─────────────┐                                │
│  │ Image/      │  │ Settings/   │                                │
│  │ SkiaImage   │  │ JsonApp     │                                │
│  │ Loader      │  │ Settings    │                                │
│  └─────────────┘  └─────────────┘                                │
└───────────────────────────────────────────────────────────────────┘
           │
           ▼ 참조
┌───────────────────────────────────────────────────────────────────┐
│  기존 라이브러리                                                    │
│  • TiledImage.Core, TiledImage.Formats.TVRaw (TVRaw 로딩)          │
│  • Geometry.Shapes (IShape, PolygonShape)                          │
│  • TiledImage.Wpf.Drawing (DrawingImageControl)                    │
└───────────────────────────────────────────────────────────────────┘
```

### 2.3 의존성 방향

```
Views → ViewModels → Services → Models ← Adapters
```

### 2.4 인터페이스 분리 기준

변경 가능성이 높은 외부 환경만 인터페이스로 격리:

| 인터페이스 | 구현체 | 격리 대상 |
|-----------|--------|----------|
| IPartLoader | XmlPartLoader | XML 스키마 변경 |
| IAlgorithmProvider | DllAlgorithmProvider | DLL → API 변경 |
| IImageLoader | SkiaImageLoader | 이미지 포맷 변경 |
| IDialogService | WpfDialogService | UI 프레임워크 |
| IAppSettingsRepository | JsonAppSettingsRepository | 설정 저장 방식 |

---

## 3. 프로젝트 구조

```
samples/PartMaker/
├── PartMaker.csproj
├── App.xaml / App.xaml.cs
│
├── Models/                              # 도메인 모델
│   ├── Part.cs                          # Aggregate Root
│   ├── PartId.cs                        # Part 식별자
│   ├── AppSettings.cs                   # 앱 설정
│   │
│   ├── Entities/                        # Entity 클래스
│   │   ├── GerberData.cs                # Gerber 데이터 (Footprint, Pins, PadDefinitions)
│   │   ├── Footprint.cs                 # Footprint 정의 (필수)
│   │   ├── Pin.cs                       # 핀 정보 (Footprint 상대 좌표)
│   │   ├── PadDefinition.cs             # 패드 템플릿 정의
│   │   ├── Pad.cs                       # 패드 정보 (레거시)
│   │   ├── CoordinateSettings.cs        # 좌표계 설정
│   │   ├── PartResource.cs              # 리소스 정보
│   │   ├── SegmentationConfig.cs        # Seg 런타임 설정
│   │   ├── FeatureExtractionConfig.cs   # FE 런타임 설정
│   │   ├── JudgmentConfig.cs            # Judgment 런타임 설정
│   │   └── FePinGroup.cs                # FE 핀 그룹
│   │
│   ├── ValueObjects/                    # Value Objects
│   │   ├── PadShape.cs                  # 형상 정의 (abstract)
│   │   ├── Point2D.cs                   # 2D 좌표
│   │   ├── Segmentation/
│   │   │   ├── SegmentationInfo.cs      # DLL 메타데이터
│   │   │   ├── SegmentationArgument.cs
│   │   │   └── SegmentationProfileData.cs   # 저장용 DTO
│   │   ├── FeatureExtraction/
│   │   │   ├── FeatureExtractionInfo.cs
│   │   │   ├── FeatureDefinition.cs
│   │   │   ├── FeatureExtractionProfileData.cs
│   │   │   └── FePinGroupData.cs
│   │   └── Judgment/
│   │       ├── JudgmentProfileData.cs
│   │       ├── FeatureReferenceData.cs
│   │       └── ClassRuleData.cs
│   │
│   ├── Enums/
│   │   ├── PadShapeType.cs
│   │   ├── CoordinateOrigin.cs
│   │   ├── RotationDirection.cs
│   │   ├── FePinGroupType.cs
│   │   └── JudgmentType.cs
│   │
│   └── Exceptions/
│       ├── LibraryException.cs
│       ├── LibraryStructureException.cs
│       ├── LibraryNotInitializedException.cs
│       ├── PinAssignmentException.cs
│       └── InvalidPinException.cs
│
├── Services/                            # 비즈니스 로직 + 인터페이스
│   ├── LibraryService.cs                # Library 상태 관리
│   ├── PartService.cs                   # Part 로드/저장 조율
│   │
│   ├── IPartLoader.cs                   # XML 구조 격리
│   ├── IAlgorithmProvider.cs            # DLL/API 격리
│   ├── IImageLoader.cs
│   ├── IDialogService.cs
│   ├── IAppSettingsRepository.cs
│   │
│   └── Mappers/                         # ProfileData ↔ Config 변환
│       ├── SegmentationProfileMapper.cs
│       ├── FeatureExtractionProfileMapper.cs
│       ├── JudgmentProfileMapper.cs
│       ├── CoordinateConverter.cs       # 좌표 변환 (Footprint 지원)
│       ├── PinShapeMapper.cs            # Pin → IShape 변환
│       └── ShapePinMapper.cs            # IShape → Pin 변환
│
├── Adapters/                            # 외부 환경 격리 구현체
│   ├── Xml/                             # XML 파싱/저장
│   │   ├── XmlPartLoader.cs             # IPartLoader 구현
│   │   ├── SegmentationXmlModels.cs     # DLL XML 모델
│   │   └── FeatureExtractionXmlModels.cs
│   │
│   ├── Dll/                             # 알고리즘 DLL 로딩
│   │   └── DllAlgorithmProvider.cs      # IAlgorithmProvider 구현
│   │
│   ├── Image/                           # 이미지 로딩
│   │   └── SkiaImageLoader.cs           # IImageLoader 구현
│   │
│   ├── Settings/                        # 앱 설정
│   │   └── JsonAppSettingsRepository.cs # IAppSettingsRepository 구현
│   │
│   └── Wpf/                             # WPF 전용
│       └── WpfDialogService.cs          # IDialogService 구현
│
├── ViewModels/                          # MVVM ViewModels
│   ├── MainViewModel.cs                 # Shape 관리, Footprint/Pin 생성
│   ├── PartBrowserViewModel.cs
│   ├── ImageGerberViewModel.cs          # Z축 탐색, Pin 목록, Drawing Target
│   ├── PinViewModel.cs                  # Pin 항목 표시/편집
│   ├── SegmentationViewModel.cs
│   ├── FeatureExtractionViewModel.cs
│   └── JudgmentViewModel.cs
│
├── Views/                               # WPF Views
│   ├── MainWindow.xaml
│   ├── PartBrowserPanel.xaml
│   ├── ImageGerberPanel.xaml            # Z축, Drawing Target, Pin 목록
│   ├── SegmentationPanel.xaml
│   ├── FeatureExtractionPanel.xaml
│   └── JudgmentPanel.xaml
│
└── Converters/                          # WPF 컨버터
    ├── NullToVisibilityConverter.cs
    ├── BoolToVisibilityConverter.cs
    └── InverseBoolToVisibilityConverter.cs
```

---

## 4. Models (Domain)

### 4.1 Part Aggregate 구조

```
┌─────────────────────────────────────────────────────────────────────┐
│                      Part (Aggregate Root)                           │
├─────────────────────────────────────────────────────────────────────┤
│ Properties:                                                          │
│  + Name: string                                                      │
│  + ImageType: string                                                 │
│  + PartsType: string                                                 │
│  + Resource: PartResource                                            │
│  + Board: BoardInfo                      ← Slice 표면 정보           │
│  + Gerber: GerberData                    ← Footprint, Pins, PadDefs │
│  + Pads: IReadOnlyList<Pad>              ← 레거시 (하위호환용)        │
│  + SegmentationConfigs: IReadOnlyList<SegmentationConfig>            │
│  + FeatureExtractionConfigs: IReadOnlyList<FeatureExtractionConfig>  │
│  + JudgmentConfigs: IReadOnlyList<JudgmentConfig>                    │
├─────────────────────────────────────────────────────────────────────┤
│ Segmentation Methods:                                                │
│  + AddSegmentationConfig(info): SegmentationConfig                   │
│  + RemoveSegmentationConfig(configId): void                          │
│  + AssignPinsToSegmentation(configId, pins): void                    │
│  + ReorderSegmentationConfig(configId, newOrder): void               │
├─────────────────────────────────────────────────────────────────────┤
│ Feature Extraction Methods:                                          │
│  + AddFeatureExtractionConfig(info): FeatureExtractionConfig         │
│  + RemoveFeatureExtractionConfig(configIndex): void                  │
│  + AssignPinsToFeatureExtraction(configIndex, pins): void            │
│  + ClearFeatureExtractionPins(configIndex): void                     │
│  + GetAvailablePinsForFeatureExtraction(excludeIndex?): IReadOnlyList│
├─────────────────────────────────────────────────────────────────────┤
│ Judgment Methods:                                                    │
│  + AddJudgmentConfig(config): void                                   │
│  + RemoveJudgmentConfig(name): void                                  │
├─────────────────────────────────────────────────────────────────────┤
│ Persistence Methods:                                                 │
│  + GetSegmentationProfileData(): IReadOnlyList<ProfileData>          │
│  + GetFeatureExtractionProfileData(): IReadOnlyList<ProfileData>     │
│  + GetFePinGroupData(): IReadOnlyList<FePinGroupData>                │
│  + GetJudgmentProfileData(): IReadOnlyList<JudgmentProfileData>      │
└─────────────────────────────────────────────────────────────────────┘
```

### 4.2 비즈니스 규칙 캡슐화

Part Aggregate Root가 캡슐화하는 비즈니스 규칙:

| 규칙 | 설명 | 메서드 |
|------|------|--------|
| 핀 존재 검증 | 할당 전 핀이 Part에 존재하는지 확인 | ValidatePinsExist() |
| PIN 그룹 배타성 | Object Feature용 핀은 다른 FE Config와 중복 불가 | FindConflictingPins() |
| Segmentation 순서 관리 | Config 추가/제거 시 Order 자동 조정 | ReorderSegmentationConfig() |
| Config ID 자동 생성 | 새 Config 생성 시 고유 ID 할당 | AddXxxConfig() |

### 4.3 GerberData 구조

```
┌─────────────────────────────────────────────────────────────────────┐
│                         GerberData                                   │
├─────────────────────────────────────────────────────────────────────┤
│ Properties:                                                          │
│  + Footprint: Footprint           ← 필수 (Pin 생성 전제조건)          │
│  + Pins: IReadOnlyList<Pin>                                          │
│  + PadDefinitions: IReadOnlyList<PadDefinition>                      │
├─────────────────────────────────────────────────────────────────────┤
│ Methods:                                                             │
│  + SetFootprint(footprint): void                                     │
│  + AddPin(pin): void                                                 │
│  + RemovePin(pinName): void                                          │
│  + RenamePin(oldName, newName): void                                 │
│  + AddPadDefinition(padDef): void                                    │
│  + GetPadDefinition(padId): PadDefinition                            │
└─────────────────────────────────────────────────────────────────────┘
```

### 4.4 Footprint 구조

**Footprint는 필수 요소**로, Pin 배치 영역을 정의한다.

```
┌─────────────────────────────────────────────────────────────────────┐
│                          Footprint                                   │
├─────────────────────────────────────────────────────────────────────┤
│ Properties:                                                          │
│  + Number: int                    ← 풋프린트 번호                     │
│  + Name: string                   ← 풋프린트 이름                     │
│  + Width: double                  ← 풋프린트 너비 (mm)                │
│  + Height: double                 ← 풋프린트 높이 (mm)                │
│  + OffsetX: double                ← Part 중심 기준 X 오프셋 (mm)      │
│  + OffsetY: double                ← Part 중심 기준 Y 오프셋 (mm)      │
│  + IsValid: bool                  ← Width > 0 && Height > 0          │
└─────────────────────────────────────────────────────────────────────┘
```

**좌표 체계:**
```
Part 공간 (이미지 mm 좌표)
          │
          │ Footprint Offset (OffsetX, OffsetY)
          ▼
    ┌─────────────────────┐
    │    Footprint        │
    │  ┌───────────────┐  │
    │  │ Pin Position  │  │  ← Pin.Position은 Footprint 중심 기준
    │  │   (x, y)      │  │
    │  └───────────────┘  │
    │         ◉           │  ← Footprint Center
    └─────────────────────┘
```

### 4.5 Pin 구조

```
┌─────────────────────────────────────────────────────────────────────┐
│                            Pin                                       │
├─────────────────────────────────────────────────────────────────────┤
│ Properties:                                                          │
│  + Name: string                   ← 핀 이름 (고유)                    │
│  + PadId: string                  ← 참조할 PadDefinition ID          │
│  + Position: Point2D              ← Footprint 중심 기준 상대 좌표     │
│  + Rotation: double               ← 회전각 (도)                       │
└─────────────────────────────────────────────────────────────────────┘
```

> **중요**: Pin.Position은 **Footprint 중심 기준 상대 좌표**이므로, Footprint가 이동해도 Pin 데이터는 변경되지 않는다. UI에서만 Shape 위치가 자동 업데이트된다.

### 4.6 BoardInfo / SliceSurface 구조

**BoardInfo**는 Part의 3D 이미지에서 주요 Z 슬라이스 위치를 관리합니다.

```
┌─────────────────────────────────────────────────────────────────────┐
│                          BoardInfo                                   │
├─────────────────────────────────────────────────────────────────────┤
│ Properties:                                                          │
│  + Surfaces: IReadOnlyList<SliceSurface>  ← Slice 표면 목록          │
├─────────────────────────────────────────────────────────────────────┤
│ Methods:                                                             │
│  + SetSurface(type, offsetMm): void       ← Slice 설정/갱신          │
│  + GetSurface(type): SliceSurface?        ← Slice 조회               │
│  + RemoveSurface(type): bool              ← Slice 제거               │
│  + GetMainSliceZIndex(spacingZ, depth): long?  ← Z 인덱스 계산       │
└─────────────────────────────────────────────────────────────────────┘
```

**SliceSurface** (Value Object)
```
┌─────────────────────────────────────────────────────────────────────┐
│                        SliceSurface                                  │
├─────────────────────────────────────────────────────────────────────┤
│ Properties:                                                          │
│  + Type: SliceType                        ← MainSlice/TopSlice/...   │
│  + OffsetMm: double                       ← 중심 기준 상대 오프셋     │
└─────────────────────────────────────────────────────────────────────┘
```

**SliceType** (Enum)
| 값 | 설명 |
|----|------|
| MainSlice | 주 검사면 (PCB 표면) |
| TopSlice | 상단 표면 (부품 상면) |
| BottomSlice | 하단 표면 (부품 하면) |

**Z 좌표 체계:**
- 이미지 Z 중심 (`ImageDepth / 2`)을 기준 0으로 사용
- **OffsetMm > 0**: 중심보다 위 (Z 인덱스 증가 방향)
- **OffsetMm < 0**: 중심보다 아래 (Z 인덱스 감소 방향)
- Z 인덱스 계산: `centerZ + (offsetMm / spacingZ)`

```
이미지 Z축 (슬라이스 인덱스)
     │
  99 ┤ ← Top (OffsetMm = +25mm)
     │
  50 ┤ ← 중심 (OffsetMm = 0mm) ← ImageDepth / 2
     │
  25 ┤ ← Main (OffsetMm = -12.5mm)
     │
   0 ┼────────────────────────────
```

### 4.7 Value Object vs Entity 구분

| 유형 | 클래스 | 식별자 | 불변성 |
|------|--------|--------|--------|
| Entity | Part, GerberData, BoardInfo, Pin, Pad, SegmentationConfig, FeatureExtractionConfig | 있음 | 가변 |
| Value Object | Point2D, PadShape, Footprint, SliceSurface, SegmentationInfo, FeatureExtractionInfo | 없음 | 불변 |
| DTO | SegmentationProfileData, FeatureExtractionProfileData, JudgmentProfileData | - | 가변 (저장용) |

---

## 5. Services

### 5.1 서비스 목록

| Service | 책임 |
|---------|------|
| LibraryService | Library 상태 관리 - 폴더 검증, 알고리즘 로드, Part 스캔 |
| PartService | Part 로드/저장 조율 - ProfileData → Config 변환 |

### 5.2 인터페이스 목록

| Interface | 책임 | 구현체 |
|-----------|------|--------|
| IPartLoader | Part XML 로드/저장 | XmlPartLoader |
| IAlgorithmProvider | 알고리즘 DLL 로드 | DllAlgorithmProvider |
| IImageLoader | 이미지 로드 | SkiaImageLoader |
| IDialogService | 다이얼로그 표시 | WpfDialogService |
| IAppSettingsRepository | 앱 설정 저장/로드 | JsonAppSettingsRepository |

### 5.3 Mapper 목록

| Mapper | 책임 | 주요 메서드 |
|--------|------|------------|
| CoordinateConverter | 픽셀 ↔ mm 좌표 변환 (Footprint 지원) | ToPixel(), ToMm(), ToPixelFromFootprint(), ToFootprintMm() |
| PinShapeMapper | Pin → IShape 변환 (렌더링용) | ToShape(pin, padDef) |
| ShapePinMapper | IShape → Pin 변환 (생성/수정용) | ToPin(), ToPadShape(), ToPinRelativeToFootprint() |

### 5.4 CoordinateConverter 좌표 변환

```
┌─────────────────────────────────────────────────────────────────────┐
│                      CoordinateConverter                             │
├─────────────────────────────────────────────────────────────────────┤
│ 생성자:                                                              │
│  CoordinateConverter(resource)                                       │
│  CoordinateConverter(resource, footprint)  ← Footprint 좌표 변환용  │
├─────────────────────────────────────────────────────────────────────┤
│ 기본 변환:                                                           │
│  + ToPixel(mmPoint): Point2D        ← mm → 픽셀 (Part 공간)         │
│  + ToMm(pixelPoint): Point2D        ← 픽셀 → mm (Part 공간)         │
├─────────────────────────────────────────────────────────────────────┤
│ Footprint 상대 좌표 변환:                                            │
│  + ToPixelFromFootprint(fpMm): Point2D  ← Footprint mm → 픽셀       │
│  + ToFootprintMm(pixel): Point2D        ← 픽셀 → Footprint mm       │
│  + GetFootprintPixelBounds(): (Center, Width, Height)               │
└─────────────────────────────────────────────────────────────────────┘
```

**변환 흐름:**
```
Footprint 상대 좌표 (Pin.Position)
         │
         │ + Footprint.Offset
         ▼
Part 절대 mm 좌표
         │
         │ × (1/Spacing) + Origin 변환
         ▼
픽셀 좌표 (Shape.CenterX/Y)
```

### 5.5 데이터 흐름 (Part 로드)

```
PartId
    │
    ▼
┌───────────────────────────────────────────┐
│ 1. LibraryService.GetXmlPath(partId)       │
│    └── Part 경로 조회                       │
└───────────────────────────────────────────┘
    │
    ▼
┌───────────────────────────────────────────┐
│ 2. IPartLoader.Load(xmlPath)               │
│    └── Part + ProfileData 반환             │
└───────────────────────────────────────────┘
    │
    ▼
┌───────────────────────────────────────────┐
│ 3. IImageLoader.LoadAsync(imagePath)       │
│    └── TiledImageSource 로드               │
└───────────────────────────────────────────┘
    │
    ▼
┌───────────────────────────────────────────┐
│ 4. ProfileMapper.ToConfigList()            │
│    └── ProfileData + DLL Info → Config     │
│    └── Part.AddXxxConfig()                 │
└───────────────────────────────────────────┘
    │
    ▼
PartService.LoadResult(Part, ImageSource, XmlPath)
```

---

## 6. Adapters

### 6.1 역할

외부 환경(XML 구조, DLL 형식, UI 프레임워크)의 변경을 격리합니다.

| Adapter | 격리 대상 | 변경 시나리오 |
|---------|----------|--------------|
| XmlPartLoader | XML 스키마 | .tv_lib 포맷 변경 |
| DllAlgorithmProvider | 알고리즘 로딩 방식 | DLL → REST API 변경 |
| SkiaImageLoader | 이미지 디코더 | 이미지 포맷 추가 |
| JsonAppSettingsRepository | 설정 저장 방식 | JSON → DB 변경 |
| WpfDialogService | UI 다이얼로그 | WPF → 다른 UI 프레임워크 |

### 6.2 Library 폴더 구조

```
Library Root/
├── SEGMENTATION/           # Segmentation DLL (*.TV_SEG)
├── FEATURE/                # Feature Extraction DLL (*.TV_FE)
├── DEPENDENCY/             # 의존성 DLL (선택적)
│   ├── *.dll               # 직접 의존성 파일
│   └── {SubFolder}/        # 하위 폴더 (재귀적으로 검색)
│       └── *.dll
└── PARTS/                  # Part 파일
    └── {PartName}.tv_lib   # Part XML
        └── {PartName}/
            └── default.image.raw
```

**DEPENDENCY 폴더:**
- Segmentation/Feature Extraction DLL이 의존하는 외부 DLL 포함
- DLL 로드 전 DEPENDENCY 폴더와 모든 하위 폴더가 PATH에 자동 등록
- 폴더가 없으면 무시 (선택적)

---

## 7. Two-Model 패턴 (Config vs ProfileData)

### 7.1 개요

런타임 모델(Config)과 저장 모델(ProfileData)을 분리합니다.

```
┌─────────────────────────────────────────────────────────────────┐
│                     Runtime Model (Config)                       │
│  - DLL 메타데이터 참조 (Info)                                     │
│  - Argument 값 저장                                              │
│  - 핀 그룹 관리                                                   │
│  - Part Aggregate 내에서만 존재                                   │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                  Mapper (변환)
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                Persistence Model (ProfileData)                   │
│  - DLL 이름/버전만 저장                                           │
│  - Argument 값 (List<ArgumentData>)                              │
│  - XML 직렬화 가능                                                │
│  - XmlPartLoader에서 로드/저장                                    │
└─────────────────────────────────────────────────────────────────┘
```

### 7.2 분리 이유

1. **Config 생성에 DLL Info 필수**: XML에는 Name/Version만 있고, Config 생성에는 SegmentationInfo 객체 필요
2. **XML 스키마 격리**: ProfileData가 XML 구조 변경을 흡수, Config는 영향 없음
3. **버전 호환성**: 저장된 ProfileData와 현재 DLL 버전 불일치 시 매칭/마이그레이션 가능

### 7.3 변환 흐름

**로드 시**: ProfileData → Config (DLL Info와 결합)
```
XmlPartLoader → ProfileData
                    │
                    ├─── IAlgorithmProvider (DLL Info)
                    ▼
ProfileMapper → Config
```

**저장 시**: Config → ProfileData
```
Part.GetXxxProfileData() → ProfileData
                              │
                              ▼
                    XmlPartLoader.Save()
```

---

## 8. ViewModels

### 8.1 구조

```
MainViewModel (조정자)
│
├── LibraryPath, IsLibraryLoaded
├── Part, ImageSource, Shapes, SelectedShapes
├── CoordinateSettings (Origin, Rotation)
├── _shapeToPin: Dictionary<IShape, Pin>   // Shape-Pin 매핑
├── _footprintShape: IShape?                // Footprint Shape
│
├── PartBrowser: PartBrowserViewModel
│   └── SelectedPart: PartId
│
├── ImageGerber: ImageGerberViewModel       // ← NEW
│   ├── CurrentZ, MaxZ                      // Z축 탐색
│   ├── DrawingTarget (Pin/Footprint)       // 그리기 대상
│   ├── CreationMode (Circle/Rect/...)      // 도형 생성 모드
│   ├── Pins: ObservableCollection<PinViewModel>
│   ├── SelectedPin, SelectedPins
│   ├── HasFootprint, FootprintInfo
│   └── CanDrawPin, IsShapeCreationEnabled  // Pin 생성 가능 여부
│
├── Segmentation: SegmentationViewModel
│   ├── AvailableSegmentations      // DLL 알고리즘 목록
│   ├── AppliedConfigs              // 적용된 Config 목록
│   └── SelectedPinNames ◀───────── MainViewModel에서 동기화
│
├── FeatureExtraction: FeatureExtractionViewModel
│   ├── AvailableFeatureExtractions
│   ├── AppliedConfigs
│   └── SelectedPinNames ◀───────── MainViewModel에서 동기화
│
└── Judgment: JudgmentViewModel
    ├── JudgmentTree                // COMP_JUDG + PIN_JUDG 트리
    └── SelectedJudgment
```

### 8.2 DrawingTarget (그리기 대상)

```csharp
public enum DrawingTarget
{
    Pin,        // Pin/Pad 그리기 (Footprint 필수)
    Footprint   // Footprint 영역 그리기
}
```

**모드별 동작:**

| DrawingTarget | 조건 | Shape 생성 | 결과 |
|---------------|------|-----------|------|
| Footprint | 항상 가능 | Rectangle | Footprint 설정, Pin Shape 위치 업데이트 |
| Pin | Footprint.IsValid | Circle/Rect/Oblong/Polygon | Pin + PadDefinition 생성 |
| Pin | !Footprint.IsValid | 비활성화 | 경고 메시지 표시 |

### 8.3 UI 레이아웃

```
┌─────────────────────────────────────────────────────────────────┐
│  Toolbar: [Select Library] [Load Part] [Save Part] | Origin|Rot │
├─────────────────────────────────────────┬───────────────────────┤
│                                         │  Part Browser (Tree)   │
│                                         │  └── Part 목록         │
│           Image Viewer                  ├───────────────────────┤
│        (DrawingImageControl)            │  Image/Gerber Panel    │
│           + Shape Overlay               │  ├── Z-Axis Navigation │
│           + Footprint Rectangle         │  ├── Drawing Target    │
│                                         │  │   ├── Pin/Pad       │
│                                         │  │   └── Footprint     │
│                                         │  ├── Shape Mode        │
│                                         │  └── Pin List          │
│                                         ├───────────────────────┤
│                                         │  Tab Control           │
│                                         │  ├── Segmentation      │
│                                         │  ├── Feature Extract   │
│                                         │  └── Judgment          │
├─────────────────────────────────────────┴───────────────────────┤
│  StatusBar: [상태 메시지]                                        │
└─────────────────────────────────────────────────────────────────┘
```

---

## 9. 의존성 주입 (DI)

### 9.1 등록

```csharp
// Adapters - 외부 환경 격리
services.AddSingleton<IPartLoader, XmlPartLoader>();
services.AddSingleton<IAlgorithmProvider, DllAlgorithmProvider>();
services.AddSingleton<IAppSettingsRepository, JsonAppSettingsRepository>();
services.AddSingleton<IDialogService, WpfDialogService>();
services.AddSingleton<IImageDecoder, TVRawImageDecoder>();
services.AddSingleton<IImageLoader, SkiaImageLoader>();

// Services
services.AddSingleton<LibraryService>();
services.AddSingleton<PartService>();

// ViewModels
services.AddTransient<MainViewModel>();
services.AddTransient<PartBrowserViewModel>();
services.AddTransient<SegmentationViewModel>();
services.AddTransient<FeatureExtractionViewModel>();
services.AddTransient<JudgmentViewModel>();
```

### 9.2 생명주기

| 타입 | 생명주기 | 이유 |
|------|---------|------|
| IPartLoader, IAlgorithmProvider | Singleton | 상태 없음 / DLL 핸들 유지 |
| LibraryService, PartService | Singleton | Library/Part 상태 관리 |
| ViewModel | Transient | 화면당 인스턴스 |

---

## 10. 예외 처리

### 10.1 도메인 예외

| 예외 | 발생 조건 | 포함 정보 |
|------|----------|----------|
| LibraryStructureException | Library 폴더 구조 오류 | LibraryPath, MissingFolders |
| LibraryNotInitializedException | Library 미초기화 상태 | - |
| InvalidPinException | 존재하지 않는 핀 참조 | InvalidPins |
| PinAssignmentException | 핀 중복 할당 시도 | ConflictingPins |

### 10.2 예외 흐름

```
Adapters (XmlPartLoader, DllAlgorithmProvider)
        │
        │ IOException, XmlException
        ▼
Services (LibraryService, PartService)
        │
        │ 그대로 전파
        ▼
Models (Part Aggregate Root)
        │
        │ InvalidPinException, PinAssignmentException
        ▼
ViewModels
        │
        │ try-catch로 처리, IDialogService로 표시
        ▼
    사용자에게 메시지 표시
```

---

## 11. 기존 라이브러리 활용

| 라이브러리 | 컴포넌트 | 용도 |
|-----------|----------|------|
| TiledImage.Core | TiledImageSource | 타일 기반 이미지 관리 |
| TiledImage.Formats.SkiaSharp | SkiaSharpImageLoader | 일반 이미지 로드 |
| TiledImage.Formats.TVRaw | TVRawImageLoader | TVRaw 이미지 로드 |
| Geometry.Shapes | IShape, PolygonShape 등 | 핀 기하 표현 |
| TiledImage.Wpf.Drawing | DrawingImageControl | 렌더링 및 상호작용 |

---

## 12. CLI (Command Line Interface)

### 12.1 개요

PartMaker는 GUI 외에 CLI 모드를 지원하여 외부 도구와 연동할 수 있습니다.

**설계 원칙:**
- CLI는 UI와 동일한 코드 경로 사용 (별도 로직 없음)
- `PartService.LoadAsync()`를 공유하여 프로필 복원 로직 통일
- XML 내용을 stdout으로 반환 (파일 경로가 아닌 내용 자체)

### 12.2 명령어 구조

```
PartMaker.exe <command> [options]
```

### 12.3 명령어 목록

| 명령어 | 설명 | 반환값 |
|--------|------|--------|
| create | 새 Part 생성 (버전 0.0.0.0) | XML 내용 (stdout) |
| edit | Part 편집 Dialog 표시 | XML 내용 (stdout) |
| register | Part를 Library에 등록 (0.0.0.0 → 1.0.0.0) | 등록된 경로 |
| update | Library Part 갱신 (변경 감지 후 버전 증가) | 갱신 결과 JSON |
| select | Library에서 Part 선택 Dialog | 선택된 Part 경로 |

### 12.4 create 명령

새 Part를 생성합니다. 생성된 Part는 버전 0.0.0.0으로 Library에 미등록 상태입니다.

```bash
PartMaker.exe create --library <path> --image <path>
```

| 옵션 | 단축 | 필수 | 설명 |
|------|------|------|------|
| --library | -l | ✓ | Library 경로 (Part Type 목록 표시용) |
| --image | -i | ✓ | TVRaw 이미지 경로 |

**흐름:**
```
1. Library 로드 (기존 Part Type 목록 가져오기)
2. NewPartDialog 표시 (Part Type 선택/입력)
3. Part 생성 (PartCreationService)
4. PartEditorDialog 표시
5. XML 내용을 stdout으로 반환
```

**예시:**
```bash
PartMaker.exe create -l "C:\Library" -i "C:\image.tvraw" > part.tv_lib
```

### 12.5 edit 명령

기존 Part를 편집합니다. XML은 stdin에서 받으며, Library 경로가 필수입니다 (알고리즘 DLL 로드용).

```bash
cat part.tv_lib | PartMaker.exe edit --library <path> [--image <path>]
```

| 옵션 | 단축 | 필수 | 설명 |
|------|------|------|------|
| (stdin) | | ✓ | Part XML 내용 (pipe로 전달) |
| --library | -l | ✓ | Library 경로 (알고리즘 로드용) |
| --image | -i | | 이미지 경로 (표시용) |

**흐름:**
```
1. stdin에서 XML 내용 읽기
2. LibraryService 초기화 (알고리즘 DLL 로드)
3. PartService.LoadFromStringAsync() 호출 (UI와 동일한 로직)
   - XML 파싱
   - ProfileData → Config 변환 (알고리즘 매칭)
4. PartEditorDialog 표시
5. 수정된 XML 내용을 stdout으로 반환
```

**예시:**
```bash
cat part.tv_lib | PartMaker.exe edit -l "C:\Library" -i "C:\image.tvraw" > modified.tv_lib
```

### 12.6 register 명령

버전 0.0.0.0인 Part를 Library에 등록합니다. XML은 stdin에서 받으며, 등록 시 버전이 1.0.0.0으로 변경됩니다.

```bash
cat part.tv_lib | PartMaker.exe register --library <path> --image <path> [--force]
```

| 옵션 | 단축 | 필수 | 설명 |
|------|------|------|------|
| (stdin) | | ✓ | Part XML 내용 (pipe로 전달, 버전 0.0.0.0) |
| --library | -l | ✓ | Library 경로 |
| --image | -i | ✓ | 원본 이미지 경로 (Library로 복사) |
| --force | -f | | 동일 Part 존재 시 덮어쓰기 |

**흐름:**
```
1. stdin에서 XML 내용 읽기
2. 이미지 파일 존재 확인
3. XML 파싱 (버전 0.0.0.0 확인)
4. Library에 중복 확인
5. 버전 1.0.0.0 부여
6. 이미지 복사
   └── {Library}/PARTS/{PartsType}/{ImageType}/{Name}/default.image.raw
7. Library에 XML 저장
8. 등록된 경로 반환
```

**예시:**
```bash
cat part.tv_lib | PartMaker.exe register -l "C:\Library" -i "C:\image.tvraw"
cat part.tv_lib | PartMaker.exe register -l "C:\Library" -i "C:\image.tvraw" --force
```

### 12.7 update 명령

Library에 등록된 Part를 갱신합니다. XML은 stdin에서 받으며, 변경 사항을 비교하여 버전을 자동 증가시킵니다.

```bash
cat modified.tv_lib | PartMaker.exe update --library <path> [--dry-run]
```

| 옵션 | 단축 | 필수 | 설명 |
|------|------|------|------|
| (stdin) | | ✓ | 수정된 Part XML 내용 (pipe로 전달) |
| --library | -l | ✓ | Library 경로 |
| --dry-run | -d | | 변경 사항만 확인 (저장 안함) |

**흐름:**
```
1. stdin에서 XML 내용 읽기
2. 수정된 Part 로드 (PartService.LoadFromStringAsync - 프로필 복원)
3. Library에서 기존 Part 찾기
4. 기존 Part 로드 (PartService.LoadAsync - 프로필 복원)
5. PartSnapshot으로 변경 사항 비교
6. 버전 증가 규칙 적용
7. 새 버전으로 저장 (dry-run 제외)
8. 변경 보고서 JSON 반환
```

**버전 증가 규칙:**

| 변경 레벨 | 버전 증가 | 예시 |
|----------|----------|------|
| Minor | Minor+1 | Footprint, Pin, Segmentation, Feature Extraction 변경 |
| Patch | Patch+1 | Judgment 규칙 변경 (Min/Max, IsUse, ClassRule) |
| Revision | Revision+1 | 메타데이터 변경 (TAG, PACKAGE, **Slice**) |

**Slice 변경 감지 (Revision 레벨):**
- Main Slice / Top Slice / Bottom Slice 추가/삭제/변경
- OffsetMm 값 변경 시 Revision 증가

**예시:**
```bash
# 변경 사항 미리보기
cat modified.tv_lib | PartMaker.exe update -l "C:\Library" -d

# 갱신 실행
cat modified.tv_lib | PartMaker.exe update -l "C:\Library"
```

**반환 JSON 예시:**
```json
{
  "currentVersion": "1.0.0.0",
  "newVersion": "1.1.0.0",
  "changeLevel": "Minor",
  "hasChanges": true,
  "changes": [
    "Added pin: PIN_NEW",
    "Modified segmentation parameter: Threshold"
  ]
}
```

### 12.8 select 명령

Library에서 Part를 선택하는 Dialog를 표시합니다.

```bash
PartMaker.exe select --library <path>
```

| 옵션 | 단축 | 필수 | 설명 |
|------|------|------|------|
| --library | -l | ✓ | Library 경로 |

**예시:**
```bash
PartMaker.exe select -l "C:\Library"
```

### 12.9 종료 코드

| 코드 | 상수 | 설명 |
|------|------|------|
| 0 | Success | 성공 |
| 1 | Cancelled | 사용자 취소 |
| 2 | InvalidArgs | 잘못된 인수 |
| 3 | FileNotFound | 파일/디렉토리 없음 |
| 10 | InternalError | 내부 오류 |

### 12.10 전역 옵션

| 옵션 | 설명 |
|------|------|
| --debug | 실행 전 디버거 연결 대기 |

### 12.11 CLI 아키텍처

```
┌─────────────────────────────────────────────────────────────┐
│  App.xaml.cs                                                 │
│  └── CLI 모드 감지 → CliRunner 실행                          │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  CliRunner                                                   │
│  └── 명령 파싱 → Command 생성 → 실행                         │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  Commands (CreateCommand, EditCommand, ...)                  │
│  └── Services 호출 (UI와 동일한 코드 경로)                   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  Services (PartService, PartRegistrationService, ...)        │
│  └── UI/CLI 공용 비즈니스 로직                               │
└─────────────────────────────────────────────────────────────┘
```

---

## 13. 변경 이력

| 버전 | 날짜 | 변경 내용 |
|------|------|-----------|
| 1.0 | 2025-01 | 초안 작성 |
| 2.0 | 2025-01 | Segmentation 구현 반영 |
| 3.0 | 2025-01 | Feature Extraction 구현, UseCase 패턴 적용 |
| 4.0 | 2025-12 | DDD Aggregate 패턴으로 재구조화 |
| 5.0 | 2025-12 | **Clean Architecture → MVVM + Adapters 단순화** |
|     |        | - Domain/Application/Infrastructure 레이어 제거 |
|     |        | - Models/Services/Adapters/ViewModels/Views 구조로 변경 |
|     |        | - UseCase 제거, Service 직접 사용 |
|     |        | - 변경 가능성 높은 부분만 인터페이스 격리 (IPartLoader, IAlgorithmProvider) |
| 6.0 | 2025-12 | **GerberData/Footprint 구조 추가** |
|     |        | - GerberData 엔티티 추가 (Footprint, Pins, PadDefinitions) |
|     |        | - Footprint 필수 요소화 (Pin 생성 전제조건) |
|     |        | - Pin.Position: Footprint 중심 기준 상대 좌표 |
|     |        | - ImageGerberViewModel, DrawingTarget 추가 |
|     |        | - CoordinateConverter Footprint 좌표 변환 지원 |
|     |        | - PinShapeMapper/ShapePinMapper 추가 |
|     |        | - UI 레이아웃 업데이트 (Image/Gerber Panel) |
| 7.0 | 2025-12 | **CLI 문서 추가** |
|     |        | - CLI 명령어 (create, edit, register, update, select) 문서화 |
|     |        | - CLI가 UI와 동일한 코드 경로 사용하도록 리팩토링 |
|     |        | - PartService.LoadAsync() 오버로드로 통합 |
| 7.1 | 2025-12 | **CLI stdin 입력 방식 변경** |
|     |        | - edit/register/update 명령: 파일 경로 대신 stdin에서 XML 수신 |
|     |        | - PartService.LoadFromStringAsync() 추가 |
|     |        | - IPartLoader.LoadFromString() 추가 |
| 7.2 | 2025-12 | **register 명령 이미지 복사 기능 추가** |
|     |        | - --image, -i 옵션 필수화 (원본 이미지 경로) |
|     |        | - Library 구조에 맞게 이미지 복사: {Library}/PARTS/{PartsType}/{ImageType}/{Name}/default.image.raw |
| 7.3 | 2025-12 | **CLI 불필요 옵션 정리** |
|     |        | - create: --no-ui 옵션 제거 (빈 Part 생성 무의미) |
|     |        | - register: --name, --type, --image-type 옵션 제거 (XML에서 읽음) |
|     |        | - update: --force 옵션 제거 (변경 없으면 갱신 불필요) |
|     |        | - 전역 옵션: help 명령 제거 |
| 7.4 | 2025-12 | **CLI 일관성 개선** |
|     |        | - create: --library (-l) 옵션 필수화 (Part Type 목록 표시용) |
|     |        | - 파라미터 순서 통일: LibraryPath → XmlContent → ImagePath → 플래그 |
|     |        | - 파싱/에러메시지 순서 통일: --library (-l) 먼저 |
|     |        | - update: --dry-run에 -d 단축키 추가 |
| 7.5 | 2025-12 | **BoardInfo/Slice 구조 추가** |
|     |        | - BoardInfo 엔티티 추가 (Part.Board) |
|     |        | - SliceSurface Value Object (Type, OffsetMm) |
|     |        | - SliceType Enum (MainSlice, TopSlice, BottomSlice) |
|     |        | - Z 좌표 체계: 이미지 중심 기준 상대값 (+: 위, -: 아래) |
|     |        | - 버전 관리에 Slice 변경 감지 추가 (Revision 레벨) |
| 7.6 | 2025-12 | **DEPENDENCY 폴더 지원 추가** |
|     |        | - Library 폴더 구조에 DEPENDENCY 폴더 추가 |
|     |        | - DllAlgorithmProvider: DLL 로드 전 의존성 경로 PATH 등록 |
|     |        | - 하위 폴더 재귀 검색 지원 |
