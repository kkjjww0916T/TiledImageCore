# TVRaw 이미지 변환 설계서

## 1. 개요

### 1.1 목표
TVRaw 이미지에 변환(회전, 크롭, 스케일, 플립)을 적용하고, 변환된 이미지의 헤더 정보를 재계산하여 **물리 좌표 동치**를 유지한다.

### 1.2 물리 좌표 동치
```
P_픽셀 = (P_물리 - OriginOffset) / VolumeSpacing
```

### 1.3 좌표계 해석 (상대 좌표계)
- 이미지 좌표계는 **이미지와 함께 회전**한다
- 0°에서 촬영한 이미지와 20°에서 촬영한 이미지 모두 `OriginOffset = (0,0,0)`
- 따라서 0° 이미지를 20° 회전하면 `OriginOffset`은 그대로 `(0,0,0)` 유지
- 동일한 도형 정의가 원본/회전 이미지 모두에서 올바르게 렌더링됨

---

## 2. 아키텍처

### 2.1 클래스 다이어그램

```
┌─────────────────────────────────────────────────────────────────┐
│                    TiledImage.Formats.TVRaw                      │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────────┐     ┌──────────────────────────────┐   │
│  │ TVRawTransformOptions│     │ TVRawTransformService        │   │
│  ├─────────────────────┤     ├──────────────────────────────┤   │
│  │ RotationDegrees     │────▶│ TransformAsync()             │   │
│  │ CropRect            │     │ GetTransformedHeaderInfo()   │   │
│  │ ScaleFactor         │     │ CreateCompositeHeader()      │   │
│  │ FlipHorizontal      │     └──────────────┬───────────────┘   │
│  │ FlipVertical        │                    │                   │
│  │ Interpolation       │                    ▼                   │
│  └─────────────────────┘     ┌──────────────────────────────┐   │
│                              │ TVRawHeaderTransformer       │   │
│                              ├──────────────────────────────┤   │
│                              │ ComputeTransformedHeader()   │   │
│                              │ PhysicalToPixel()            │   │
│                              │ PixelToPhysical()            │   │
│                              └──────────────────────────────┘   │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ TransformedTVRawTileProvider : ITileDataProvider         │   │
│  ├──────────────────────────────────────────────────────────┤   │
│  │ Header (변환된 헤더)                                      │   │
│  │ ReadTileData() / ReadPixels()                            │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                    TiledImage.Transforms                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌────────────────────────────────────────────────────────┐     │
│  │ PerspectiveTransform                                    │     │
│  ├────────────────────────────────────────────────────────┤     │
│  │ ComputeHomography()                                     │     │
│  └────────────────────────────────────────────────────────┘     │
│                                                                  │
│  ┌────────────────────────────────────────────────────────┐     │
│  │ Sampling/                                               │     │
│  ├────────────────────────────────────────────────────────┤     │
│  │ ┌───────────────┐  ┌─────────────────────────────────┐ │     │
│  │ │ IPixelSampler │  │ PixelResampler                  │ │     │
│  │ ├───────────────┤  ├─────────────────────────────────┤ │     │
│  │ │ GetGray8()    │◀─│ ResampleRegion()                │ │     │
│  │ │ GetGray16()   │  │ BilinearInterpolate()           │ │     │
│  │ │ GetRgb24()    │  │ BicubicInterpolate() (Catmull)  │ │     │
│  │ │ GetPixelBytes()│ └─────────────────────────────────┘ │     │
│  │ └───────┬───────┘                                      │     │
│  │         │                                              │     │
│  │         ▼                                              │     │
│  │ ┌─────────────────────────────────────────────────┐    │     │
│  │ │ TilePixelSampler : IPixelSampler               │    │     │
│  │ ├─────────────────────────────────────────────────┤    │     │
│  │ │ 64x64 tile caching                              │    │     │
│  │ │ Wraps ITileDataProvider                         │    │     │
│  │ └─────────────────────────────────────────────────┘    │     │
│  └────────────────────────────────────────────────────────┘     │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 클래스 책임

**TiledImage.Formats.TVRaw:**
| 클래스 | 책임 |
|--------|------|
| `TVRawTransformOptions` | 변환 옵션 정의 및 유효성 검사 |
| `TVRawTransformService` | 변환 파이프라인 조정, 스플릿 이미지 처리 |
| `TVRawHeaderTransformer` | 헤더 재계산 로직 (물리 좌표 동치 보장) |
| `TransformedTVRawTileProvider` | 변환된 데이터에 대한 ITileDataProvider |
| `TVRawImageExporter` | TVRaw 파일 저장 (헤더 + 픽셀 데이터) |

**TiledImage.Transforms:**
| 클래스 | 책임 |
|--------|------|
| `PerspectiveTransform` | 호모그래피 기반 투시 변환 (DLT 알고리즘) |
| `InterpolationMode` | 보간 모드 열거형 (Nearest/Bilinear/Bicubic) |
| `IPixelSampler` | 픽셀 접근 추상화 인터페이스 |
| `PixelResampler` | 픽셀 보간 알고리즘 (IPixelSampler 사용) |
| `TilePixelSampler` | ITileDataProvider를 IPixelSampler로 래핑 (64x64 캐시) |

---

## 3. 변환 파이프라인

### 3.1 데이터 흐름

```
┌─────────────────────────────────────────────────────────────────┐
│ 입력: TiledImageSource (TVRawTileProvider)                       │
│   - 단일 이미지 또는 스플릿 이미지                                 │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│ 헤더 결정                                                        │
│   if (단일 이미지)  → Splits[0].Header 사용                       │
│   if (스플릿 이미지) → CreateCompositeHeader() 호출               │
│     - PixelSize = (합성 너비, 합성 높이, Depth)                   │
│     - OriginOffset = min(각 스플릿의 OriginOffset)               │
│     - VolumeSpacing = 첫 번째 스플릿과 동일                       │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│ TVRawHeaderTransformer.ComputeTransformedHeader()               │
│   - 변환 순서: Crop → Scale → Flip → Rotation                    │
│   - 결과: TransformedHeaderInfo (Header, PixelTransform, etc.)  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│ PixelResampler (Z 슬라이스별, 타일별 병렬 처리)                    │
│   - TilePixelSampler: ITileDataProvider → IPixelSampler 변환     │
│   - InverseTransform으로 출력→입력 좌표 매핑 (backward mapping)   │
│   - IPixelSampler.GetGray8/16()로 소스 픽셀 읽기                 │
│   - 보간 적용 (Nearest/Bilinear/Bicubic)                        │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│ 출력: TiledImageSource (TransformedTVRawTileProvider)            │
│   - MemoryMappedFile에 변환된 픽셀 데이터 저장                    │
│   - Header는 재계산된 값 포함                                    │
└─────────────────────────────────────────────────────────────────┘
```

---

## 4. 헤더 재계산 알고리즘

### 4.1 변환 적용 순서
```
1. Crop → 2. Scale → 3. Flip → 4. Rotation
```

### 4.2 각 변환별 공식

#### Crop (크롭)
| 속성 | 변환 |
|------|------|
| PixelSize | `(CropRect.Width, CropRect.Height, Depth)` |
| OriginOffset | `Origin + CropStart * Spacing` |
| VolumeSpacing | 유지 |
| PixelTransform | `Translate(-CropX, -CropY)` |

#### Scale (스케일, s배)
| 속성 | 변환 |
|------|------|
| PixelSize | `(W * s, H * s, Depth)` |
| OriginOffset | 유지 |
| VolumeSpacing | `(Sx / s, Sy / s, Sz)` |
| PixelTransform | `Scale(s, s)` |

#### Flip (플립)
| 속성 | FlipX | FlipY |
|------|-------|-------|
| PixelSize | 유지 | 유지 |
| OriginOffset.X | `+= (W-1) * SpacingX` | 유지 |
| OriginOffset.Y | 유지 | `+= (H-1) * SpacingY` |
| VolumeSpacing | `SpacingX = -SpacingX` | `SpacingY = -SpacingY` |
| PixelTransform | `FlipH(Width)` | `FlipV(Height)` |

#### Rotation (회전, θ도) - 상대 좌표계
| 속성 | 변환 |
|------|------|
| PixelSize | 바운딩 박스 확장 (아래 공식) |
| OriginOffset | **유지** (좌표계가 이미지와 함께 회전) |
| VolumeSpacing | **유지** |
| PixelTransform | `Translate(offset) * RotateAt(θ, center)` |

**바운딩 박스 계산:**
```
NewWidth  = ceil(W * |cos(θ)| + H * |sin(θ)|)
NewHeight = ceil(W * |sin(θ)| + H * |cos(θ)|)
```

**픽셀 변환 행렬:**
```
preCenterX  = (Width - 1) / 2
preCenterY  = (Height - 1) / 2
postCenterX = (NewWidth - 1) / 2
postCenterY = (NewHeight - 1) / 2

offsetX = postCenterX - preCenterX
offsetY = postCenterY - preCenterY

Transform = Translate(offsetX, offsetY) * RotateAt(θ, preCenterX, preCenterY) * PrevTransform
```

---

## 5. 스플릿 이미지 처리

### 5.1 스플릿 이미지 구조
```
┌─────────────────────────────────────────────────────────────────┐
│ TVRaw Container File                                            │
├─────────────────────────────────────────────────────────────────┤
│ [Split 0 Data] [Split 1 Data] ... [Split N-1 Data]              │
│ [Split 0 Header] [Split 1 Header] ... [Container Header]        │
└─────────────────────────────────────────────────────────────────┘

각 스플릿:
  - 자체 PixelSize, OriginOffset, VolumeSpacing
  - VirtualX, VirtualY로 합성 위치 계산
```

### 5.2 합성 헤더 생성 (CreateCompositeHeader)

| 속성 | 값 |
|------|-----|
| PixelSize | `(ImageWidth, ImageHeight, ImageDepth)` - 합성 크기 |
| OriginOffset | `min(각 스플릿의 OriginOffset)` - 합성 원점 |
| VolumeSpacing | 첫 번째 스플릿과 동일 |
| 기타 메타데이터 | 첫 번째 스플릿에서 복사 |

### 5.3 픽셀 읽기
- `TVRawTileProvider.ReadPixels()`가 자동으로 스플릿 합성 처리
- `PixelResampler`는 합성된 픽셀을 투명하게 읽음

---

## 6. Matrix3x3 변환 행렬

### 6.1 행렬 레이아웃
```
| M11  M12  M13 |   | ScaleX  SkewX   TransX |
| M21  M22  M23 | = | SkewY   ScaleY  TransY |
| M31  M32  M33 |   | Persp0  Persp1  Persp2 |
```

### 6.2 주요 변환 행렬

**Translation:**
```
| 1  0  tx |
| 0  1  ty |
| 0  0  1  |
```

**Scale:**
```
| sx 0  0 |
| 0  sy 0 |
| 0  0  1 |
```

**Rotation (θ):**
```
| cos(θ)  -sin(θ)  0 |
| sin(θ)   cos(θ)  0 |
| 0        0       1 |
```

**FlipHorizontal(width):**
```
| -1  0  width |
| 0   1  0     |
| 0   0  1     |
```

**FlipVertical(height):**
```
| 1   0  0      |
| 0  -1  height |
| 0   0  1      |
```

### 6.3 중심 기준 회전 (RotateAt)
```
RotateAt(θ, cx, cy) = T(cx, cy) * R(θ) * T(-cx, -cy)
```

행렬 곱셈은 **오른쪽에서 왼쪽**으로 적용됨:
1. 점을 (-cx, -cy)로 이동
2. 원점 기준 회전
3. (cx, cy)로 다시 이동

---

## 7. 의존성

```
Geometry.Primitives
├── Matrix3x3
├── Point2F
└── Rect2F

TiledImage.Core
└── Tiling/ITileDataProvider

TiledImage.Transforms (no SkiaSharp)
├── PerspectiveTransform  ──────────────▶ Geometry.Primitives
├── InterpolationMode
└── Sampling/
    ├── IPixelSampler
    ├── PixelResampler
    └── TilePixelSampler ───────────────▶ TiledImage.Core (ITileDataProvider)

TiledImage.Formats.TVRaw
├── Transform/
│   ├── TVRawTransformService  ─────────▶ TiledImage.Transforms
│   └── TVRawHeaderTransformer ─────────▶ Geometry.Primitives
├── Tiling/
│   └── TransformedTVRawTileProvider
├── Types/
│   └── TVRawTransformOptions ──────────▶ TiledImage.Transforms (InterpolationMode)
└── IO/
    └── TVRawImageExporter
```
