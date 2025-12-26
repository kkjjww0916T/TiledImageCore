# Geometry.Primitives 설계서

## 1. 개요

### 1.1 목표
솔루션 전반에 분산된 공간 관련 자료형을 **제네릭 기반 Value Object(VO)**로 통합하여 일관성과 재사용성을 확보한다.

### 1.2 현재 문제점 (모두 해결됨)

| 문제 | 영향 | 상태 |
|------|------|------|
| Point 타입 중복 | Point2F, ShapePoint, Point2L, Point2D 등 | ✅ `Point2<T>` 제네릭으로 통합 |
| Rect 타입 중복 | Rect2F, ShapeRect, Rect2L 등 | ✅ `Rect2<T>` 제네릭으로 통합 |
| 기능 구현 불일치 | 산술 연산 유무, readonly 여부 | ✅ 통합된 제네릭 타입 사용 |
| 별도 타입 파일 중복 | Point2D.cs, Point2F.cs 등 중복 정의 | ✅ global using 별칭으로 대체 |

### 1.3 설계 원칙

- **Value Object 패턴**: `readonly record struct` + `IEquatable<T>`
- **제네릭 수치 타입**: `INumber<T>` 제약 조건 (.NET 7+)
- **Immutable**: 모든 연산은 새 인스턴스 반환
- **차원 분리**: 2D/3D 명시적 구분

---

## 2. 타입 체계

### 2.1 차원 × 개념 매트릭스

| 개념 | 2D | 3D |
|------|-----|-----|
| **Point** (위치) | `Point2<T>` | `Point3<T>` |
| **Size** (크기) | `Size2<T>` | `Size3<T>` |
| **Rect/Box** (영역) | `Rect2<T>` | `Box3<T>` |

### 2.2 정밀도별 용도

| 타입 | 용도 | 예시 |
|------|------|------|
| `float` | GPU/SkiaSharp, 메모리 효율 | 변환 행렬, 렌더링 |
| `double` | UI/물리 좌표, 고정밀 | mm 단위, 도형 편집 |
| `long` | 대용량 이미지 픽셀 | 타일 좌표, 뷰포트 |
| `int` | 일반 픽셀 좌표 | 비트맵 인덱스 |

### 2.3 타입 별칭 (Type Aliases)

```
Point2<float>  → Point2F
Point2<double> → Point2D
Point2<long>   → Point2L
Point2<int>    → Point2I

Point3<float>  → Point3F
Point3<double> → Point3D
Point3<long>   → Point3L

Size2<long>    → Size2L
Size2<double>  → Size2D

Size3<long>    → Size3L
Size3<double>  → Size3D

Rect2<float>   → Rect2F
Rect2<double>  → Rect2D
Rect2<long>    → Rect2L

Box3<long>     → Box3L
Box3<double>   → Box3D
```

---

## 3. 핵심 타입 설계

### 3.1 Point2\<T\>

```
┌─────────────────────────────────────────────────────────────┐
│ Point2<T> : IEquatable<Point2<T>>                           │
│ where T : struct, INumber<T>                                │
├─────────────────────────────────────────────────────────────┤
│ Properties                                                  │
│   + X : T                                                   │
│   + Y : T                                                   │
├─────────────────────────────────────────────────────────────┤
│ Constructors                                                │
│   + Point2(T x, T y)                                        │
│   + static Zero : Point2<T>                                 │
├─────────────────────────────────────────────────────────────┤
│ Operators                                                   │
│   + operator +(Point2<T>, Point2<T>) → Point2<T>            │
│   + operator -(Point2<T>, Point2<T>) → Point2<T>            │
│   + operator *(Point2<T>, T) → Point2<T>                    │
│   + operator /(Point2<T>, T) → Point2<T>                    │
│   + operator -(Point2<T>) → Point2<T>  (negate)             │
├─────────────────────────────────────────────────────────────┤
│ Methods                                                     │
│   + LengthSquared() → T                                     │
│   + DistanceSquaredTo(Point2<T>) → T                        │
│   + Offset(T dx, T dy) → Point2<T>                          │
│   + WithX(T x) → Point2<T>                                  │
│   + WithY(T y) → Point2<T>                                  │
│   + Deconstruct(out T x, out T y)                           │
└─────────────────────────────────────────────────────────────┘
```

**부동소수점 전용 확장 메서드** (T : IFloatingPoint):
- `Length() → T`
- `DistanceTo(Point2<T>) → T`
- `Normalize() → Point2<T>`
- `RotateAround(Point2<T> center, T radians) → Point2<T>`

### 3.2 Point3\<T\>

```
┌─────────────────────────────────────────────────────────────┐
│ Point3<T> : IEquatable<Point3<T>>                           │
│ where T : struct, INumber<T>                                │
├─────────────────────────────────────────────────────────────┤
│ Properties                                                  │
│   + X : T                                                   │
│   + Y : T                                                   │
│   + Z : T                                                   │
├─────────────────────────────────────────────────────────────┤
│ Constructors                                                │
│   + Point3(T x, T y, T z)                                   │
│   + Point3(Point2<T> xy, T z)                               │
│   + static Zero : Point3<T>                                 │
├─────────────────────────────────────────────────────────────┤
│ Operators                                                   │
│   + operator +(Point3<T>, Point3<T>) → Point3<T>            │
│   + operator -(Point3<T>, Point3<T>) → Point3<T>            │
│   + operator *(Point3<T>, T) → Point3<T>                    │
│   + operator /(Point3<T>, T) → Point3<T>                    │
│   + operator -(Point3<T>) → Point3<T>  (negate)             │
├─────────────────────────────────────────────────────────────┤
│ Methods                                                     │
│   + LengthSquared() → T                                     │
│   + DistanceSquaredTo(Point3<T>) → T                        │
│   + Offset(T dx, T dy, T dz) → Point3<T>                    │
│   + XY → Point2<T>  (projection)                            │
│   + XZ → Point2<T>                                          │
│   + YZ → Point2<T>                                          │
│   + WithX/Y/Z(T) → Point3<T>                                │
│   + Deconstruct(out T x, out T y, out T z)                  │
└─────────────────────────────────────────────────────────────┘
```

### 3.3 Size2\<T\>

```
┌─────────────────────────────────────────────────────────────┐
│ Size2<T> : IEquatable<Size2<T>>                             │
│ where T : struct, INumber<T>                                │
├─────────────────────────────────────────────────────────────┤
│ Properties                                                  │
│   + Width : T                                               │
│   + Height : T                                              │
│   + Area → T  (Width * Height)                              │
│   + IsEmpty → bool  (Width <= 0 || Height <= 0)             │
├─────────────────────────────────────────────────────────────┤
│ Constructors                                                │
│   + Size2(T width, T height)                                │
│   + static Empty : Size2<T>                                 │
├─────────────────────────────────────────────────────────────┤
│ Operators                                                   │
│   + operator *(Size2<T>, T) → Size2<T>                      │
│   + operator /(Size2<T>, T) → Size2<T>                      │
├─────────────────────────────────────────────────────────────┤
│ Methods                                                     │
│   + Scale(T factor) → Size2<T>                              │
│   + WithWidth(T) → Size2<T>                                 │
│   + WithHeight(T) → Size2<T>                                │
│   + ToPoint() → Point2<T>                                   │
│   + Deconstruct(out T width, out T height)                  │
└─────────────────────────────────────────────────────────────┘
```

### 3.4 Size3\<T\>

```
┌─────────────────────────────────────────────────────────────┐
│ Size3<T> : IEquatable<Size3<T>>                             │
│ where T : struct, INumber<T>                                │
├─────────────────────────────────────────────────────────────┤
│ Properties                                                  │
│   + Width : T                                               │
│   + Height : T                                              │
│   + Depth : T                                               │
│   + Volume → T  (Width * Height * Depth)                    │
│   + IsEmpty → bool                                          │
├─────────────────────────────────────────────────────────────┤
│ Constructors                                                │
│   + Size3(T width, T height, T depth)                       │
│   + Size3(Size2<T> wh, T depth)                             │
│   + static Empty : Size3<T>                                 │
├─────────────────────────────────────────────────────────────┤
│ Methods                                                     │
│   + Scale(T factor) → Size3<T>                              │
│   + WH → Size2<T>  (projection)                             │
│   + WithWidth/Height/Depth(T) → Size3<T>                    │
│   + ToPoint() → Point3<T>                                   │
│   + Deconstruct(out T w, out T h, out T d)                  │
└─────────────────────────────────────────────────────────────┘
```

### 3.5 Rect2\<T\>

```
┌─────────────────────────────────────────────────────────────┐
│ Rect2<T> : IEquatable<Rect2<T>>                             │
│ where T : struct, INumber<T>                                │
├─────────────────────────────────────────────────────────────┤
│ Properties                                                  │
│   + X : T                                                   │
│   + Y : T                                                   │
│   + Width : T                                               │
│   + Height : T                                              │
│   + Left → T  (X)                                           │
│   + Top → T  (Y)                                            │
│   + Right → T  (X + Width)                                  │
│   + Bottom → T  (Y + Height)                                │
│   + Location → Point2<T>                                    │
│   + Size → Size2<T>                                         │
│   + Center → Point2<T>                                      │
│   + TopLeft, TopRight, BottomLeft, BottomRight → Point2<T>  │
│   + IsEmpty → bool                                          │
├─────────────────────────────────────────────────────────────┤
│ Constructors                                                │
│   + Rect2(T x, T y, T width, T height)                      │
│   + Rect2(Point2<T> location, Size2<T> size)                │
│   + static Empty : Rect2<T>                                 │
│   + static FromLTRB(T left, T top, T right, T bottom)       │
│   + static FromCenter(Point2<T> center, Size2<T> size)      │
│   + static FromPoints(Point2<T> p1, Point2<T> p2)           │
├─────────────────────────────────────────────────────────────┤
│ Methods                                                     │
│   + Contains(Point2<T>) → bool                              │
│   + Contains(Rect2<T>) → bool                               │
│   + IntersectsWith(Rect2<T>) → bool                         │
│   + Intersect(Rect2<T>) → Rect2<T>                          │
│   + Union(Rect2<T>) → Rect2<T>                              │
│   + Offset(T dx, T dy) → Rect2<T>                           │
│   + Inflate(T dx, T dy) → Rect2<T>                          │
│   + Scale(T factor) → Rect2<T>                              │
│   + WithLocation(Point2<T>) → Rect2<T>                      │
│   + WithSize(Size2<T>) → Rect2<T>                           │
│   + Deconstruct(out T x, out T y, out T w, out T h)         │
└─────────────────────────────────────────────────────────────┘
```

### 3.6 Box3\<T\>

```
┌─────────────────────────────────────────────────────────────┐
│ Box3<T> : IEquatable<Box3<T>>                               │
│ where T : struct, INumber<T>                                │
├─────────────────────────────────────────────────────────────┤
│ Properties                                                  │
│   + X, Y, Z : T                                             │
│   + Width, Height, Depth : T                                │
│   + Location → Point3<T>                                    │
│   + Size → Size3<T>                                         │
│   + Center → Point3<T>                                      │
│   + IsEmpty → bool                                          │
├─────────────────────────────────────────────────────────────┤
│ Constructors                                                │
│   + Box3(T x, T y, T z, T width, T height, T depth)         │
│   + Box3(Point3<T> location, Size3<T> size)                 │
│   + Box3(Rect2<T> xy, T z, T depth)                         │
│   + static Empty : Box3<T>                                  │
├─────────────────────────────────────────────────────────────┤
│ Methods                                                     │
│   + Contains(Point3<T>) → bool                              │
│   + Contains(Box3<T>) → bool                                │
│   + IntersectsWith(Box3<T>) → bool                          │
│   + XYRect → Rect2<T>  (projection)                         │
│   + XZRect → Rect2<T>                                       │
│   + YZRect → Rect2<T>                                       │
│   + Offset(T dx, T dy, T dz) → Box3<T>                      │
│   + SliceXY(T z) → Rect2<T>                                 │
│   + Deconstruct(...)                                        │
└─────────────────────────────────────────────────────────────┘
```

---

## 4. 프로젝트 구조

```
src/Geometry.Primitives/
├── Geometry.Primitives.csproj
├── GlobalUsings.cs          # 모든 타입 별칭 정의 (프로젝트 내부용)
├── Matrix3x3.cs             # 변환 행렬 (Point2<float> 사용)
│
├── Core/
│   ├── Point2.cs            # Point2<T> readonly record struct
│   ├── Point3.cs            # Point3<T> readonly record struct
│   ├── Size2.cs             # Size2<T> readonly record struct
│   ├── Size3.cs             # Size3<T> readonly record struct
│   ├── Rect2.cs             # Rect2<T> readonly record struct
│   └── Box3.cs              # Box3<T> readonly record struct
│
└── Extensions/
    ├── FloatingPointExtensions.cs  # IFloatingPointIeee754 전용 (Length, Normalize, Rotate)
    └── ConversionExtensions.cs     # 타입 간 변환 (Cast, Round, Floor, Ceiling)
```

### 4.1 의존 프로젝트별 GlobalUsings.cs

각 프로젝트는 필요한 타입 별칭만 정의:

```
src/Geometry.Shapes/GlobalUsings.cs         → Point2D, Rect2D
src/TiledImage.Transforms/GlobalUsings.cs   → Point2F
src/TiledImage.Formats.TVRaw/GlobalUsings.cs → Point3D, Size3L, Rect2L
src/TiledImage.Wpf.Viewer/GlobalUsings.cs   → Point2L, Rect2L
src/TiledImage.Wpf.Mapping/GlobalUsings.cs  → Point2F, Rect2L
src/TiledImage.Wpf.Drawing/GlobalUsings.cs  → Point2D, Rect2D
samples/TiledImage.Demo/GlobalUsings.cs     → Point2D, Rect2L
```

---

## 5. 네임스페이스 구조

| 폴더 | 네임스페이스 | 내용 |
|------|-------------|------|
| Core/ | `Geometry.Primitives` | Point2, Point3, Size2, Size3, Rect2, Box3 |
| Extensions/ | `Geometry.Primitives` | 확장 메서드 (같은 네임스페이스) |
| Transforms/ | `Geometry.Primitives` | Matrix3x3 |

**사용:**
```csharp
using Geometry.Primitives;

var p = new Point2<double>(1.5, 2.5);
var rect = Rect2<long>.FromLTRB(0, 0, 100, 100);
```

---

## 6. 타입 변환 전략

### 6.1 동일 차원, 다른 정밀도

```
Point2Extensions.cs:
  + Point2<T>.Cast<TTarget>() → Point2<TTarget>
  + Point2<T>.Round() → Point2<long>   (IFloatingPoint only)
  + Point2<T>.Floor() → Point2<long>
  + Point2<T>.Ceiling() → Point2<long>
```

### 6.2 2D ↔ 3D 변환

```
Point3<T>:
  + XY, XZ, YZ → Point2<T>  (projection)
  + static FromXY(Point2<T>, T z) → Point3<T>

Size3<T>:
  + WH → Size2<T>
  + static FromWH(Size2<T>, T depth) → Size3<T>

Box3<T>:
  + XYRect, XZRect, YZRect → Rect2<T>
  + SliceXY(T z) → Rect2<T>
```

### 6.3 외부 타입 변환 (확장 메서드)

| 변환 | 위치 | 메서드 |
|------|------|--------|
| Point2F ↔ SKPoint | TiledImage.Wpf.Mapping | ToSKPoint(), ToPoint2F() |
| Rect2F ↔ SKRect | TiledImage.Wpf.Mapping | ToSKRect(), ToRect2F() |
| Point2D ↔ System.Windows.Point | TiledImage.Wpf.* | ToWpfPoint(), ToPoint2D() |

---

## 7. 기존 타입 마이그레이션

### 7.1 매핑 테이블

| 기존 타입 | 위치 | 신규 타입 | 처리 |
|-----------|------|-----------|------|
| `Point2F` (struct) | Geometry.Primitives/Aliases | `Point2<float>` | ✅ 삭제 (global using 별칭) |
| `Point2D` (struct) | Geometry.Primitives/Aliases | `Point2<double>` | ✅ 삭제 (global using 별칭) |
| `Point2L` (record) | Geometry.Primitives/Aliases | `Point2<long>` | ✅ 삭제 (global using 별칭) |
| `Rect2F` (struct) | Geometry.Primitives/Aliases | `Rect2<float>` | ✅ 삭제 (global using 별칭) |
| `Rect2D` (record) | Geometry.Primitives/Aliases | `Rect2<double>` | ✅ 삭제 (global using 별칭) |
| `Rect2L` (record) | Geometry.Primitives/Aliases | `Rect2<long>` | ✅ 삭제 (global using 별칭) |
| `Point3D` (struct) | Geometry.Primitives/Aliases | `Point3<double>` | ✅ 삭제 (global using 별칭) |
| `Size3L` (struct) | Geometry.Primitives/Aliases | `Size3<long>` | ✅ 삭제 (global using 별칭) |
| `ShapePoint` | Geometry.Primitives | `Point2<double>` | ✅ 삭제 (Point2D로 대체) |
| `ShapeRect` | Geometry.Primitives | `Rect2<double>` | ✅ 삭제 (Rect2D로 대체) |
| `LongPoint` | TiledImage.Core | `Point2<long>` | ✅ 삭제 (Point2L로 대체) |
| `LongRect` | TiledImage.Core | `Rect2<long>` | ✅ 삭제 (Rect2L로 대체) |
| `Matrix3x3` | Geometry.Primitives | 유지 | Point2<float> 사용 |
| `Volume3DPosition` | CrossSection | 유지 | 도메인 특화 로직 |
| `PinLocation` | Labeling | 유지 | 도메인 모델 |
| `ImageInfo` | Labeling | 유지 | 도메인 모델 |

### 7.2 마이그레이션 단계

```
Phase 1: Geometry.Primitives 제네릭 타입 구현 ✅ 완료
  └── Point2<T>, Point3<T>, Size2<T>, Size3<T>, Rect2<T>, Box3<T>

Phase 2: 타입 별칭 정의 ✅ 완료
  └── global using으로 각 프로젝트별 GlobalUsings.cs에 정의

Phase 3: 의존 프로젝트 순차 마이그레이션 ✅ 완료
  ├── Geometry.Shapes → Point2D, Rect2D
  ├── TiledImage.Core → (direct generic usage)
  ├── TiledImage.Transforms → Point2F
  ├── TiledImage.Formats.TVRaw → Point3D, Size3L, Rect2L
  ├── TiledImage.Wpf.Viewer → Point2L, Rect2L
  ├── TiledImage.Wpf.Mapping → Point2F, Rect2L
  ├── TiledImage.Wpf.Drawing → Point2D, Rect2D
  └── TiledImage.Demo → Point2D, Rect2L

Phase 4: 기존 타입 파일 제거 ✅ 완료
  ├── Aliases/ 폴더 전체 삭제 (Point2D.cs, Point2F.cs, Point2L.cs, Rect2D.cs 등)
  ├── ShapeAliases.cs (ShapePoint, ShapeRect) 삭제
  └── LongPoint.cs, LongRect.cs 삭제
```

---

## 8. 의존성 다이어그램

```
┌─────────────────────────────────────────────────────────────────┐
│                     Geometry.Primitives                          │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │ Core Types (record struct, INumber<T>)                  │    │
│  │   Point2<T>, Point3<T>, Size2<T>, Size3<T>              │    │
│  │   Rect2<T>, Box3<T>                                     │    │
│  └─────────────────────────────────────────────────────────┘    │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │ Matrix3x3 (uses Point2<float>)                          │    │
│  └─────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│ Geometry.Shapes │  │ TiledImage.Core │  │TiledImage.Transf│
│                 │  │                 │  │orms             │
│ Point2D (✅)    │  │ Point2L (✅)    │  │ Point2<float>   │
│ Rect2D (✅)     │  │ Rect2L (✅)     │  │ Matrix3x3       │
└─────────────────┘  └─────────────────┘  └─────────────────┘
          │                   │                   │
          ▼                   ▼                   ▼
┌─────────────────────────────────────────────────────────────────┐
│                    TiledImage.Wpf.*                              │
│   Point2D (UI), Point2L (Viewport)                               │
│   ↔ SKPoint, System.Windows.Point 변환                           │
└─────────────────────────────────────────────────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────────────────────┐
│                  TiledImage.Formats.TVRaw                        │
│   Point3D (물리 좌표), Size3L (픽셀 크기)                          │
└─────────────────────────────────────────────────────────────────┘
```

---

## 9. 타입 별칭 정의 (GlobalUsings.cs)

각 프로젝트는 자체 `GlobalUsings.cs`에 필요한 타입 별칭만 정의한다.

**Geometry.Primitives/GlobalUsings.cs** (전체 별칭 정의):
```
global using Point2D = Geometry.Primitives.Point2<double>;
global using Point2F = Geometry.Primitives.Point2<float>;
global using Point2L = Geometry.Primitives.Point2<long>;
global using Point2I = Geometry.Primitives.Point2<int>;

global using Rect2D = Geometry.Primitives.Rect2<double>;
global using Rect2F = Geometry.Primitives.Rect2<float>;
global using Rect2L = Geometry.Primitives.Rect2<long>;
global using Rect2I = Geometry.Primitives.Rect2<int>;

global using Size2D = Geometry.Primitives.Size2<double>;
global using Size2L = Geometry.Primitives.Size2<long>;

global using Point3D = Geometry.Primitives.Point3<double>;
global using Size3L = Geometry.Primitives.Size3<long>;
```

**의존 프로젝트 (필요한 타입만 정의)**:
```
// Geometry.Shapes/GlobalUsings.cs
global using Point2D = Geometry.Primitives.Point2<double>;
global using Rect2D = Geometry.Primitives.Rect2<double>;

// TiledImage.Transforms/GlobalUsings.cs
global using Point2F = Geometry.Primitives.Point2<float>;
```

**장점:**
- 각 프로젝트가 필요한 타입만 import (의존성 명확화)
- 중앙 집중식 타입 정의 불필요 (코드 중복 제거)
- 제네릭 기반이므로 별도 타입 파일 없음

---

## 10. 구현 우선순위

| 순서 | 타입 | 이유 |
|------|------|------|
| 1 | `Point2<T>` | 가장 많이 사용, 기반 타입 |
| 2 | `Size2<T>` | Rect2 의존 |
| 3 | `Rect2<T>` | 뷰포트, 영역 계산 핵심 |
| 4 | `Point3<T>` | 3D 좌표 |
| 5 | `Size3<T>` | 3D 크기 |
| 6 | `Box3<T>` | 3D 영역 |
| 7 | Extensions | Float 전용 메서드 |
| 8 | Matrix3x3 수정 | Point2<float> 사용 |
