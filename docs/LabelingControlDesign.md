# 라벨링 컨트롤 설계 문서

## 1. 개요

### 1.1 목적
검사 이미지에 라벨 마스크 영역을 오버레이하여 시각화하는 WPF 컨트롤. 원본 검사 이미지 위에 색상화된 오버레이를 표시한다.

### 1.2 설계 원칙
- **컨트롤 책임 분리**: 컨트롤은 순수하게 "이미지 표시 + 오버레이 렌더링"만 담당
- **도메인 독립성**: 비트마스크 해석, XML 파싱 등 도메인 로직은 유틸리티로 분리
- **유연성**: 다양한 비트마스크 스키마에 재사용 가능

### 1.3 주요 기능
- 원본 이미지(8bit/16bit) 표시
- 오버레이 이미지 렌더링 (외부에서 색상 변환 함수 제공)
- 오버레이 투명도 조절
- 영역 하이라이트 (Rect 기반)

---

## 2. 프로젝트 구조

### 2.1 아키텍처 분리

```
┌─────────────────────────────────────────────────────────────────┐
│                        사용자 코드 (ViewModel)                   │
│                                                                 │
│  1. 유틸리티로 라벨 이미지 → 컬러 변환 함수 생성                   │
│  2. 컨트롤에 변환 함수 바인딩                                     │
└─────────────────────────────────────────────────────────────────┘
                              │
          ┌───────────────────┴───────────────────┐
          ▼                                       ▼
┌──────────────────────┐              ┌──────────────────────┐
│   LabelingImageControl│              │   Utilities (선택)   │
│   (TiledImage.Wpf.    │              │   (TiledImage.Wpf.   │
│    Labeling)          │              │    Labeling)         │
│                       │              │                      │
│  • 순수 오버레이 표시  │              │  • LabelBit 열거형   │
│  • 색상 변환 위임      │              │  • 색상 매핑 헬퍼    │
│  • 투명도 조절        │              │  • XML 파서 (선택)   │
│  • 영역 하이라이트    │              │                      │
└──────────────────────┘              └──────────────────────┘
         │
         │ 상속
         ▼
┌──────────────────────┐
│   ImageViewControl    │
│   (TiledImage.Wpf.    │
│    Viewer)            │
└──────────────────────┘
```

### 2.2 프로젝트 폴더

```
TiledImage.Wpf.Labeling/           # .NET 8.0-windows (WPF)
├── Controls/
│   └── LabelingImageControl.cs    # 메인 컨트롤 (도메인 독립적)
├── Types/
│   ├── InspectionResultType.cs    # OK/NG/None 열거형
│   ├── ImageInfo.cs               # 이미지 크기, Spacing 정보
│   ├── PinLocation.cs             # PIN 위치 (mm + pixel 좌표)
│   ├── PinResult.cs               # PIN 결과 데이터
│   └── InspectionResult.cs        # 전체 검사 결과
├── Parsing/
│   └── InspectionResultParser.cs  # XML 파서
├── Utilities/
│   ├── LabelBit.cs                # 비트마스크 열거형 (편의 제공)
│   └── LabelColorMapper.cs        # 비트 → 색상 매핑 헬퍼
└── Themes/
    └── Generic.xaml               # 기본 스타일
```

### 2.3 솔루션 폴더 배치

```
Library.sln
├── TiledImage.Wpf/
│   ├── TiledImage.Wpf.Viewer
│   ├── TiledImage.Wpf.Drawing
│   ├── TiledImage.Wpf.Mapping
│   └── TiledImage.Wpf.Labeling    ← 신규
```

### 2.4 네임스페이스

| 폴더 | 네임스페이스 | 설명 |
|------|-------------|------|
| Controls/ | `TiledImage.Wpf.Labeling` | LabelingImageControl, OverlayPixelHoverEventArgs |
| Types/ | `TiledImage.Wpf.Labeling.Types` | InspectionResultType, ImageInfo, PinLocation, PinResult, InspectionResult |
| Parsing/ | `TiledImage.Wpf.Labeling.Parsing` | InspectionResultParser |
| Utilities/ | `TiledImage.Wpf.Labeling.Utilities` | LabelBit, LabelColorMapper |

### 2.5 의존성

```
TiledImage.Wpf.Labeling
├── TiledImage.Wpf.Viewer      # ImageViewControl 상속
├── TiledImage.Core            # TiledImageSource, PixelFormat
└── System.Xml.Linq            # XML 파싱 (InspectionResultParser)
```

---

## 3. 데이터 구조

### 3.1 라벨 비트마스크 (LabelBit)

8bit grayscale 이미지의 각 비트가 나타내는 의미:

```
┌─────────────────────────────────────────────────────────────┐
│                    8-bit Label Mask                         │
├─────┬─────┬─────┬─────┬─────┬─────┬─────┬─────┬────────────┤
│ Bit7│ Bit6│ Bit5│ Bit4│ Bit3│ Bit2│ Bit1│ Bit0│            │
│ 0x80│ 0x40│ 0x20│ 0x10│ 0x08│ 0x04│ 0x02│ 0x01│            │
├─────┴─────┴─────┴─────┴─────┴─────┴─────┴─────┼────────────┤
│Main │Board│Gerber│Gerber│ Rsv │ Rsv │ Void│Object│ 의미      │
│Layer│Surf │Foot  │Pad   │     │     │     │      │            │
└─────────────────────────────────────────────────────────────┘
```

| 비트 | 값 | 이름 | 설명 |
|------|-----|------|------|
| Bit 0 | 0x01 | Object | 16bit 이미지에서 인식된 객체 |
| Bit 1 | 0x02 | Void | 객체 내 빈 공간 (Void) |
| Bit 2 | 0x04 | Reserved1 | 미정 |
| Bit 3 | 0x08 | Reserved2 | 미정 |
| Bit 4 | 0x10 | GerberPad | Gerber Main Layer의 Pad 영역 |
| Bit 5 | 0x20 | GerberFootprint | Gerber Main Layer의 Footprint 영역 |
| Bit 6 | 0x40 | BoardSurface | PCB 보드 표면 (윗면/아랫면) |
| Bit 7 | 0x80 | MainLayer | 메인 레이어 표시 |

### 3.2 라벨 색상 매핑 (LabelColorMap)

```
LabelColorMap
├── Colors: Dictionary<LabelBit, Color>     # 비트별 기본 색상
├── Opacity: double                          # 전체 투명도 (0.0 ~ 1.0)
├── VisibleBits: LabelBit                    # 표시할 비트 플래그
└── BlendMode: LabelBlendMode                # 색상 혼합 모드
```

#### 기본 색상 팔레트

| 비트 | 기본 색상 | ARGB |
|------|----------|------|
| Object | Red | 0x80FF0000 |
| Void | Blue | 0x800000FF |
| GerberPad | Green | 0x8000FF00 |
| GerberFootprint | Yellow | 0x80FFFF00 |
| BoardSurface | Cyan | 0x8000FFFF |
| MainLayer | Gray | 0x40808080 |

#### 색상 혼합 모드 (LabelBlendMode)

| 모드 | 설명 |
|------|------|
| Priority | 높은 비트 우선 (단일 색상) |
| Blend | 활성화된 모든 비트 색상 혼합 |
| Additive | 색상 가산 혼합 |

### 3.3 검사 결과 모델 (InspectionResult)

XML 검사 결과를 파싱한 데이터 모델 (C# record 타입):

```
InspectionResultType (enum)
├── OK
├── NG
└── None

ImageInfo (record)
├── Width: int              # 이미지 너비 (픽셀)
├── Height: int             # 이미지 높이 (픽셀)
├── Depth: int              # Z축 깊이 (슬라이스 수)
├── SpacingX: double        # X축 간격 (mm/pixel)
├── SpacingY: double        # Y축 간격 (mm/pixel)
└── SpacingZ: double        # Z축 간격 (mm/pixel)

PinLocation (record)
├── X, Y, Z: double         # 중심 좌표 (mm, 이미지 중심 원점)
├── W, H, D: double         # 크기 (mm)
├── XPixel, YPixel: int?    # Top-left 픽셀 좌표 (선택적)
├── ZPixel: int?            # Z 슬라이스 인덱스 (선택적)
├── WPixel, HPixel, DPixel: int?   # 픽셀 크기 (선택적)
└── ToPixelRect(ImageInfo): Rect   # mm → 픽셀 Rect 변환 메서드

PinResult (record)
├── Name: string            # PIN 이름 (예: "A1", "B2")
├── Result: InspectionResultType
├── NgMessage: string?      # NG 사유 메시지 (선택적)
└── Location: PinLocation

InspectionResult (record)
├── ComponentName: string   # 컴포넌트 이름 (예: "U66")
├── ComponentResult: InspectionResultType
├── ImageInfo: ImageInfo
└── Pins: IReadOnlyList<PinResult>
```

### 3.4 좌표 변환

#### 좌표계 개요

```
이미지 좌표계 (Pixel)              XML 좌표계 (mm, 중심 원점)
     (0,0)
       ┌────────────► X                 ▲ Y-
       │                                │
       │                           ─────┼─────► X+
       │                                │
       ▼ Y                              ▼ Y+
   (Width, Height)                  이미지 중심 (0, 0)
```

#### XML 좌표 속성 구분

| 속성 | 좌표 기준 | 설명 |
|------|----------|------|
| X, Y (mm) | Center | 이미지 중심 원점 기준 PIN 중심 좌표 |
| W, H (mm) | - | PIN 영역 크기 |
| X_PXL, Y_PXL | **Top-left** | PIN 영역의 좌상단 픽셀 좌표 |
| W_PXL, H_PXL | - | PIN 영역 픽셀 크기 |

**주의**: mm 좌표(X, Y)는 center 기준이지만, 픽셀 좌표(X_PXL, Y_PXL)는 **top-left 기준**입니다.

#### 변환 규칙

| 변환 | 수식 |
|------|------|
| mm → pixel (center) X | mm_x / spacing.X + imageWidth / 2 |
| mm → pixel (center) Y | mm_y / spacing.Y + imageHeight / 2 |
| center → top-left X | centerX - width / 2 |
| center → top-left Y | centerY - height / 2 |

#### ToPixelRect 변환 우선순위

| 조건 | 동작 |
|------|------|
| X_PXL, Y_PXL 존재 | 픽셀 좌표 직접 사용 (이미 top-left 기준) |
| 픽셀 좌표 없음 | mm 좌표에서 center 계산 후 top-left로 변환 |

---

## 4. 클래스 다이어그램

### 4.1 컨트롤 (도메인 독립적)

```
ImageViewControl (TiledImage.Wpf.Viewer)
├── ImageSource: TiledImageSource
├── ImageDepth: long (읽기 전용)
├── CurrentZ: long
└── OnCurrentZChangedCore(oldZ, newZ)  # virtual
        │
        │ 상속
        ▼
LabelingImageControl
├── Dependency Properties
│   ├── OverlaySource: TiledImageSource        # 오버레이 이미지 (8bit grayscale)
│   ├── OverlayOpacity: double                 # 오버레이 투명도 (0.0 ~ 1.0)
│   ├── OverlayVisible: bool                   # 오버레이 표시 여부
│   ├── PixelColorConverter: Func<byte,Color>  # 픽셀값 → 색상 변환 함수
│   ├── HighlightRegions: IEnumerable<Rect>    # 하이라이트 영역 목록
│   ├── HighlightStroke: Brush                 # 하이라이트 테두리 색상
│   └── HighlightStrokeThickness: double       # 하이라이트 테두리 두께
│
├── Methods
│   └── GetOverlayPixelValue(x, y): byte?      # 오버레이 픽셀값 조회
│
├── Events
│   ├── OverlayLoaded                          # 오버레이 로드 완료
│   └── OverlayPixelHover(x, y, value)         # 마우스 호버 시 픽셀값
│
└── Z-Slice 동기화
    └── OnCurrentZChangedCore 오버라이드       # 오버레이 CurrentZ 동기화
```

### 4.2 유틸리티 (도메인 특화, 선택적 사용)

```
LabelBit (Flags Enum)
├── None        = 0x00
├── Object      = 0x01
├── Void        = 0x02
├── Reserved1   = 0x04
├── Reserved2   = 0x08
├── GerberPad   = 0x10
├── GerberFootprint = 0x20
├── BoardSurface = 0x40
└── MainLayer   = 0x80

LabelColorMapper (Static Helper)
├── DefaultColors: Dictionary<LabelBit, Color>
├── CreateConverter(colorMap, visibleBits): Func<byte, Color>
└── CreateConverter(colorMap, visibleBits, blendMode): Func<byte, Color>
```

### 4.3 파싱 (InspectionResultParser)

```
InspectionResultParser (static class)
├── Parse(xmlPath): InspectionResult           # 파일에서 파싱
├── ParseFromString(xmlContent): InspectionResult  # 문자열에서 파싱
│
└── 내부 메서드
    ├── ParseDocument(XDocument): InspectionResult
    ├── ParseImageInfo(XElement?): ImageInfo
    ├── ParsePin(XElement): PinResult
    ├── ParseLocation(XElement?): PinLocation
    ├── ParseResultType(string?): InspectionResultType
    └── ParseDouble(string): double            # 과학적 표기법 지원
```

### 4.4 렌더링 구조

```
LabelingImageControl
        │
        │ OnRenderOverlay(dc)
        ▼
├── RenderLabelOverlay(dc)
│   ├── 라벨 타일 로드
│   ├── ConvertTileToColorBitmap()
│   │   └── PixelColorConverter 호출
│   └── 오버레이 렌더링 (DrawImage)
│
└── RenderHighlightRegions(dc)
    └── HighlightRegions의 각 Rect를
        HighlightStroke로 테두리 그리기
```

---

## 5. 렌더링 파이프라인

### 5.1 오버레이 렌더링 흐름

```
┌──────────────────┐
│  원본 이미지     │
│  (ImageSource)   │
└────────┬─────────┘
         │
         ▼
┌──────────────────┐    ┌──────────────────┐
│  타일 렌더링     │    │  라벨 이미지     │
│  (Base Control)  │    │  (LabelSource)   │
└────────┬─────────┘    └────────┬─────────┘
         │                       │
         │                       ▼
         │              ┌──────────────────┐
         │              │  비트마스크 해석  │
         │              │  (LabelColorMap) │
         │              └────────┬─────────┘
         │                       │
         │                       ▼
         │              ┌──────────────────┐
         │              │  BGRA 변환       │
         │              │  (Color + Alpha) │
         │              └────────┬─────────┘
         │                       │
         ▼                       ▼
┌─────────────────────────────────────────┐
│           오버레이 합성                  │
│  (OnRenderOverlay → DrawImage)          │
└─────────────────────────────────────────┘
         │
         ▼
┌──────────────────┐
│  PIN 경계선      │
│  (선택적)        │
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│  최종 출력       │
└──────────────────┘
```

### 5.2 비트마스크 → 색상 변환

```
입력: byte labelValue (예: 0b10110001 = 177)

처리:
  1. 각 비트 검사 (VisibleLabels 마스크 적용)
  2. BlendMode에 따른 색상 결정

Priority 모드:
  - 높은 우선순위 비트의 색상만 사용
  - 우선순위: Object > Void > Pad > Footprint > ...

Blend 모드:
  - 활성화된 모든 비트의 색상 평균

출력: BGRA 픽셀 (Color + LabelOpacity)
```

---

## 6. 데이터 흐름

### 6.1 초기화 흐름

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│  원본 이미지  │     │  라벨 이미지  │     │  결과 XML    │
│  (.raw)      │     │  (.raw)      │     │  (.xml)      │
└──────┬───────┘     └──────┬───────┘     └──────┬───────┘
       │                    │                    │
       ▼                    ▼                    ▼
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│ TVRaw/Skia   │     │ TVRaw        │     │ XML Parser   │
│ Decoder      │     │ Decoder      │     │              │
└──────┬───────┘     └──────┬───────┘     └──────┬───────┘
       │                    │                    │
       ▼                    ▼                    ▼
┌──────────────────────────────────────────────────────────┐
│                  LabelingImageControl                     │
│                                                          │
│  ImageSource ◄────────────────────────────────────────── │
│  LabelSource ◄────────────────────────────────────────── │
│  InspectionResult ◄──────────────────────────────────── │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │               렌더링 파이프라인                      │ │
│  │  원본 타일 + 라벨 오버레이 + PIN 경계               │ │
│  └────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────┘
```

### 6.2 사용자 상호작용

```
사용자 입력                     컨트롤 응답
───────────                    ──────────
마우스 이동          →         현재 픽셀의 라벨 비트 조회
                              PixelLabelQueried 이벤트 발생

PIN 클릭             →         SelectedPin 업데이트
                              PinSelected 이벤트 발생
                              PIN 영역 하이라이트

라벨 토글            →         VisibleLabels 업데이트
(체크박스 등)                  오버레이 다시 렌더링

투명도 조절          →         LabelOpacity 업데이트
(슬라이더 등)                  오버레이 다시 렌더링
```

---

## 7. 확장 고려사항

### 7.1 성능 최적화

| 항목 | 전략 |
|------|------|
| 라벨 타일 캐싱 | 변환된 BGRA 타일을 별도 캐시에 저장 |
| 지연 로딩 | 뷰포트 내 타일만 변환/렌더링 |
| MipMap | 줌 아웃 시 축소된 라벨 이미지 사용 |

### 7.2 향후 확장

- 라벨 편집 모드 (브러시로 라벨 수정)
- 라벨 통계 표시 (비트별 픽셀 수)
- 다중 라벨 레이어 지원
- 라벨 내보내기 (PNG with alpha)

---

## 8. 변경 이력

| 버전 | 날짜 | 작성자 | 변경 내용 |
|------|------|--------|-----------|
| 1.0 | 2025-12-18 | - | 초안 작성 |
| 1.1 | 2025-12-19 | - | 구현 내용 반영: Types/, Parsing/ 폴더 구조, InspectionResult record 모델, Z-Slice 동기화, HighlightStroke/Thickness 속성 추가 |
| 1.2 | 2025-12-26 | - | PinLocation 수정: XPixel/YPixel이 center가 아닌 top-left 좌표임을 명확화 |
