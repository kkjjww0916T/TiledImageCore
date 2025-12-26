# TiledImage

A modular, high-performance large image viewing control system for WPF built with .NET 8.0.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue)](https://www.microsoft.com/windows)

## Features

- **Tile-based Rendering**: Efficiently display images of any size with configurable tile sizes (default 512x512)
- **MipMap LOD Support**: Smooth zoom experience with level-of-detail rendering
- **Memory Efficient**: Memory-mapped file and unmanaged memory support for 2GB+ images
- **Multiple Pixel Formats**: Bgra32, Rgba32, Bgr24, Rgb24, Gray8, Gray16
- **Transform Pipeline**: Crop, scale, rotate, flip, affine, and perspective transformations
- **Shape Overlay**: Draw and edit shapes (ellipse, rectangle, oblong, polygon) on images
- **Perspective Editing**: Interactive perspective transformation with corner dragging
- **MVVM Ready**: Full data binding support for WPF applications

---

## Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
- [Architecture](#architecture)
- [Project Structure](#project-structure)
- [Module Details](#module-details)
- [API Reference](#api-reference)
- [Requirements](#requirements)
- [License](#license)

---

## Installation

### NuGet (Coming Soon)

```bash
dotnet add package TiledImage.Wpf.Viewer
dotnet add package TiledImage.Wpf.Drawing
dotnet add package TiledImage.Wpf.Mapping
```

### Build from Source

```bash
git clone https://github.com/kkjjww0916T/TiledImageCore.git
cd TiledImageCore
dotnet build Library.sln
```

---

## Quick Start

### Basic Image Viewing

```xml
<Window xmlns:viewer="clr-namespace:TiledImage.Wpf.Viewer;assembly=TiledImage.Wpf.Viewer">
    <viewer:ImageViewControl ImageSource="{Binding ImageSource}" />
</Window>
```

```csharp
using TiledImage.Core.Memory;
using TiledImage.Formats.SkiaSharp.IO;

var loader = new SkiaSharpImageLoader();
loader.BufferType = BufferType.Unmanaged;
var source = await loader.LoadAsync("large-image.jpg", tileSize: 512);
```

### Drawing Shapes

```xml
<drawing:DrawingImageControl
    ImageSource="{Binding ImageSource}"
    Shapes="{Binding Shapes}"
    SelectedShape="{Binding SelectedShape}"
    IsShapeEditEnabled="True" />
```

```csharp
Shapes.Add(EllipseShape.CreateCircle(100, 100, 50));
Shapes.Add(new RectangleShape(200, 200, 100, 60));
```

### Image Transformations

```csharp
var pipeline = new TransformPipeline()
    .Crop(100, 100, 800, 600)
    .Scale(0.5, InterpolationMode.Bilinear)
    .Rotate90();

using var result = pipeline.Execute(source.Provider, z: 0, BufferType.Unmanaged);
```

---

## Architecture

### Layer Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                            WPF Controls                                  │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐          │
│  │ ImageViewControl│  │DrawingImage     │  │ImageMapping     │          │
│  │     (Base)      │◄─┤    Control      │  │    Control      │          │
│  └────────┬────────┘  └─────────────────┘  └─────────────────┘          │
│           │                    │                    │                    │
│  TiledImage.Wpf.Viewer   Wpf.Drawing          Wpf.Mapping               │
└───────────┼────────────────────┼────────────────────┼───────────────────┘
            │                    │                    │
            ▼                    ▼                    ▼
┌───────────────────────────────────────────────────────────────────────┐
│                         Domain Libraries                               │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐        │
│  │ Geometry.Shapes │  │   TiledImage    │  │   TiledImage    │        │
│  │                 │  │   Transforms    │  │ Formats.SkiaSharp│        │
│  └────────┬────────┘  └────────┬────────┘  └────────┬────────┘        │
│           │                    │                    │                  │
└───────────┼────────────────────┼────────────────────┼──────────────────┘
            │                    │                    │
            ▼                    ▼                    ▼
┌───────────────────────────────────────────────────────────────────────┐
│                          Core Libraries                                │
│  ┌─────────────────────────────┐  ┌─────────────────────────────┐     │
│  │    Geometry.Primitives      │  │      TiledImage.Core        │     │
│  │  Point2<T>, Rect2<T>, etc.  │  │  ITileDataProvider, etc.    │     │
│  └─────────────────────────────┘  └─────────────────────────────┘     │
└───────────────────────────────────────────────────────────────────────┘
```

### Dependency Graph

```
Geometry.Primitives (no dependencies)
       │
       ├──► Geometry.Shapes
       │          │
       │          ▼
       │    TiledImage.Wpf.Drawing ──► TiledImage.Wpf.Viewer
       │                                       │
       └──► TiledImage.Transforms              │
                   │                           │
                   ▼                           │
             TiledImage.Core ◄─────────────────┘
                   │
                   ▼
         TiledImage.Formats.SkiaSharp
```

### TiledImageSource Architecture

The image source system uses **composition over inheritance** for flexibility:

```
┌─────────────────────────────────────────────────────────────────┐
│                      TiledImageSource                            │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │  - ImageWidth, ImageHeight, ImageDepth                    │  │
│  │  - TileSize, TileCountX, TileCountY                       │  │
│  │  - GetTile(x, y, z) → ImageTile                           │  │
│  │  - GetTileData(x, y, z) → byte[]                          │  │
│  └───────────────────────────────────────────────────────────┘  │
│                            │ delegates to                        │
│                            ▼                                     │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │              ITileDataProvider (interface)                 │  │
│  │  - ImageWidth, ImageHeight, ImageDepth                    │  │
│  │  - PixelFormat, BytesPerPixel                             │  │
│  │  - ReadTileData(x, y, z, w, h, buffer)                    │  │
│  │  - ReadPixels(x, y, z, w, h, buffer)                      │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                            │
            ┌───────────────┴───────────────┐
            ▼                               ▼
   ┌─────────────────┐            ┌─────────────────┐
   │  RawTileProvider │            │  Custom Provider │
   │                  │            │  (Extensible)    │
   └────────┬─────────┘            └──────────────────┘
            │
            ▼
   ┌─────────────────────────────────────────────────┐
   │              IPixelBuffer (interface)            │
   │  - Width, Height, Stride, BytesPerPixel         │
   │  - GetPixelSpan(), GetRowSpan(y)                │
   └─────────────────────────────────────────────────┘
            │
            ├────────────────────────┐
            ▼                        ▼
   ┌─────────────────┐      ┌─────────────────┐
   │MemoryMapped     │      │  Unmanaged      │
   │  PixelBuffer    │      │  PixelBuffer    │
   │                 │      │  (2GB+ support) │
   └─────────────────┘      └─────────────────┘
```

### Transform Pipeline Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     TransformPipeline                            │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │  Fluent API: .Crop().Scale().Rotate().Flip()              │  │
│  │  Execute(provider, z, bufferType) → IPixelBuffer          │  │
│  └───────────────────────────────────────────────────────────┘  │
│                            │                                     │
│                            ▼                                     │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │           List<ITransformStep> _steps                      │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                            │
        ┌───────────────────┼───────────────────┐
        ▼                   ▼                   ▼
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│ CropTransform│    │ScaleTransform│    │RotateTransform│
│              │    │              │    │              │
│ Fast memory  │    │ Integer fast │    │ 90/180/270   │
│ copy         │    │ path (1/2,   │    │ fast path +  │
│              │    │ 1/4, 1/8)    │    │ arbitrary    │
└──────────────┘    └──────────────┘    └──────────────┘
        │                   │                   │
        └───────────────────┼───────────────────┘
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                    ITransformStep (interface)                    │
│  - CalculateOutputSize(inputWidth, inputHeight) → Size2L       │
│  - Execute(provider, z, bufferType, progress) → IPixelBuffer   │
└─────────────────────────────────────────────────────────────────┘

Transform Operations:
┌──────────────────┬───────────────────────────────────────────────┐
│ CropTransform    │ Fast crop using direct memory copy            │
├──────────────────┼───────────────────────────────────────────────┤
│ FlipTransform    │ Horizontal/Vertical using index reversal      │
├──────────────────┼───────────────────────────────────────────────┤
│ RotateTransform  │ 90/180/270 fast path + arbitrary with interp  │
├──────────────────┼───────────────────────────────────────────────┤
│ ScaleTransform   │ Integer downscale fast path + interpolation   │
├──────────────────┼───────────────────────────────────────────────┤
│ AffineTransform  │ Matrix-based affine transformation            │
├──────────────────┼───────────────────────────────────────────────┤
│ Perspective      │ DLT homography-based quad-to-quad mapping     │
│ Transform        │                                               │
└──────────────────┴───────────────────────────────────────────────┘
```

### WPF Control Hierarchy

```
┌─────────────────────────────────────────────────────────────────┐
│                    ImageViewControl (Base)                       │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │  Properties:                                               │  │
│  │  - ImageSource (TiledImageSource)                         │  │
│  │  - Zoom, MinZoom, MaxZoom                                 │  │
│  │  - OffsetX, OffsetY                                       │  │
│  │                                                           │  │
│  │  Features:                                                │  │
│  │  - Tile-based rendering with TileCache (LRU)              │  │
│  │  - MipMap LOD support via MipMapManager                   │  │
│  │  - Mouse wheel zoom, drag pan                             │  │
│  │  - Z-slice navigation for 3D volumes                      │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                            │
            ┌───────────────┴───────────────┐
            ▼                               ▼
┌─────────────────────────┐      ┌─────────────────────────┐
│  DrawingImageControl    │      │  ImageMappingControl    │
│  ───────────────────    │      │  ───────────────────    │
│  + Shapes (IShape[])    │      │  + OverlayImagePath     │
│  + SelectedShape        │      │  + OverlayOpacity       │
│  + IsShapeEditEnabled   │      │  + IsPerspectiveMode    │
│                         │      │  + FrameWidth/Height    │
│  Features:              │      │                         │
│  - Shape rendering      │      │  Features:              │
│  - Shape selection      │      │  - Overlay blending     │
│  - Drag-to-move         │      │  - Perspective editing  │
│  - Hit testing          │      │  - Corner dragging      │
│                         │      │  - Export with transform│
└─────────────────────────┘      └─────────────────────────┘
```

### Shape System

```
┌─────────────────────────────────────────────────────────────────┐
│                      IShape (interface)                          │
│  - Id: Guid                                                     │
│  - CenterX, CenterY: double                                     │
│  - Rotation: double                                             │
│  - Style: ShapeStyle                                            │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                   ShapeBase (abstract)                           │
│  - INotifyPropertyChanged implementation                        │
│  - Common property change handling                              │
└─────────────────────────────────────────────────────────────────┘
                            │
        ┌───────────────────┼───────────────────┐
        ▼                   ▼                   ▼
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│ EllipseShape │    │RectangleShape│    │PolygonShape  │
│              │    │              │    │              │
│ + RadiusX    │    │ + Width      │    │ + Relative   │
│ + RadiusY    │    │ + Height     │    │   Points     │
│              │    │              │    │              │
│ CreateCircle │    │              │    │CreateRegular │
│ (factory)    │    │              │    │ (factory)    │
└──────────────┘    └──────────────┘    └──────────────┘
                            │
                            ▼
                    ┌──────────────┐
                    │ OblongShape  │
                    │ (Capsule)    │
                    │              │
                    │ + Width      │
                    │ + Height     │
                    │ (semicircular│
                    │  ends)       │
                    └──────────────┘

ShapeStyle:
┌─────────────────────────────────────────────────────────────────┐
│  - StrokeColor: uint (ARGB)                                     │
│  - FillColor: uint (ARGB)                                       │
│  - StrokeWidth: double                                          │
└─────────────────────────────────────────────────────────────────┘
```

### Geometry Primitives

```
┌─────────────────────────────────────────────────────────────────┐
│              Generic Types with INumber<T> Constraint            │
│                        (.NET 7+ feature)                         │
└─────────────────────────────────────────────────────────────────┘

Point2<T>                    Size2<T>                   Rect2<T>
┌──────────────┐            ┌──────────────┐           ┌──────────────┐
│ + X: T       │            │ + Width: T   │           │ + X, Y: T    │
│ + Y: T       │            │ + Height: T  │           │ + Width: T   │
│              │            │              │           │ + Height: T  │
│ Operators:   │            │ + Area       │           │              │
│ +, -, *, /   │            │              │           │ + Contains() │
└──────────────┘            └──────────────┘           │ + Intersect()│
                                                       │ + Union()    │
                                                       └──────────────┘

Type Aliases (via global using):
┌─────────────────────────────────────────────────────────────────┐
│  Point2D = Point2<double>    Rect2D = Rect2<double>             │
│  Point2F = Point2<float>     Rect2F = Rect2<float>              │
│  Point2L = Point2<long>      Rect2L = Rect2<long>               │
│  Size2D = Size2<double>      Size2L = Size2<long>               │
└─────────────────────────────────────────────────────────────────┘

Matrix3x3:
┌─────────────────────────────────────────────────────────────────┐
│  Factory Methods:                                                │
│  - CreateIdentity(), CreateTranslation(), CreateScale()         │
│  - CreateRotation(), CreateRotationAt()                         │
│  - CreateFlipHorizontal(), CreateFlipVertical()                 │
│                                                                 │
│  Operations:                                                    │
│  - MapPoint(Point2F) → Point2F                                  │
│  - TryInvert() → Matrix3x3?                                     │
│  - operator * (matrix multiplication)                           │
└─────────────────────────────────────────────────────────────────┘
```

---

## Project Structure

```
Library.sln
├── src/
│   ├── TiledImage.Core/              # Core (cross-platform, no SkiaSharp)
│   │   ├── IO/                       # IImageLoader, IImageExporter
│   │   ├── Memory/                   # IPixelBuffer implementations
│   │   ├── Tiling/                   # TiledImageSource, ITileDataProvider
│   │   └── Types/                    # PixelFormat, ImageFormat
│   │
│   ├── TiledImage.Transforms/        # Transform pipeline (no SkiaSharp)
│   │   ├── Operations/               # Crop, Flip, Rotate, Scale, etc.
│   │   ├── Sampling/                 # IPixelSampler, PixelResampler
│   │   └── Types/                    # InterpolationMode, etc.
│   │
│   ├── TiledImage.Formats.SkiaSharp/ # SkiaSharp loader/exporter
│   │   └── IO/                       # SkiaSharpImageLoader, Exporter
│   │
│   ├── Geometry.Primitives/          # Generic spatial types
│   │   ├── Core/                     # Point2<T>, Size2<T>, Rect2<T>
│   │   └── Extensions/               # FloatingPoint extensions
│   │
│   ├── Geometry.Shapes/              # UI-independent shapes
│   │   ├── Shapes/                   # IShape, ShapeBase, implementations
│   │   ├── Types/                    # ShapeStyle
│   │   └── Geometry/                 # ShapeHitTester
│   │
│   ├── TiledImage.Wpf.Viewer/        # Base WPF control
│   │   ├── Controls/                 # ImageViewControl
│   │   ├── Caching/                  # TileCache, MipMapManager
│   │   └── Viewport/                 # ViewPortManager
│   │
│   ├── TiledImage.Wpf.Drawing/       # Shape overlay control
│   │   ├── Controls/                 # DrawingImageControl
│   │   └── Drawing/                  # ShapeRenderer
│   │
│   └── TiledImage.Wpf.Mapping/       # Perspective control
│       ├── Controls/                 # ImageMappingControl
│       └── Helpers/                  # PerspectiveEditor
│
└── samples/
    └── TiledImage.Demo/              # Demo application
```

### Solution Folder Structure

```
Library.sln
├── TiledImage/
│   ├── TiledImage.Core
│   ├── TiledImage.Transforms
│   └── TiledImage.Formats.SkiaSharp
├── TiledImage.Wpf/
│   ├── TiledImage.Wpf.Viewer
│   ├── TiledImage.Wpf.Drawing
│   └── TiledImage.Wpf.Mapping
├── Geometry/
│   ├── Geometry.Primitives
│   └── Geometry.Shapes
└── Samples/
    └── TiledImage.Demo
```

---

## Module Details

### TiledImage.Core

| Component | Description |
|-----------|-------------|
| `IImageLoader` | Interface for loading images (`LoadAsync`, `BufferType`) |
| `IImageExporter` | Interface for exporting images (`ExportAsync`) |
| `BufferType` | Enum: `MemoryMapped`, `Unmanaged` |
| `IPixelBuffer` | Memory access abstraction |
| `MemoryMappedPixelBuffer` | Memory-mapped file access |
| `UnmanagedPixelBuffer` | Direct pointer with 2GB+ support |
| `ITileDataProvider` | Interface for tile data access |
| `RawTileProvider` | Standard image data provider |
| `TiledImageSource` | Tile-based image access |
| `PixelFormat` | Bgra32, Rgba32, Bgr24, Rgb24, Gray8, Gray16 |

### TiledImage.Transforms

| Component | Description |
|-----------|-------------|
| `TransformPipeline` | Fluent API for chaining transforms |
| `ITransformStep` | Interface for pipeline-compatible operations |
| `CropTransform` | Fast crop using memory copy |
| `FlipTransform` | Horizontal/Vertical flip |
| `RotateTransform` | 90/180/270 fast path + arbitrary rotation |
| `ScaleTransform` | Integer downscale fast path + interpolation |
| `AffineTransform` | Matrix-based affine transformation |
| `PerspectiveTransform` | DLT homography-based perspective |
| `IPixelSampler` | Pixel access abstraction |
| `PixelResampler` | NearestNeighbor, Bilinear, Bicubic |
| `InterpolationMode` | Interpolation algorithm selection |

### TiledImage.Wpf.Viewer

| Component | Description |
|-----------|-------------|
| `ImageViewControl` | Base control for large image viewing |
| `TileCache` | LRU tile cache for memory efficiency |
| `MipMapManager` | MipMap pyramid for LOD support |
| `ViewPortManager` | Viewport coordinate management |

### TiledImage.Wpf.Drawing

| Component | Description |
|-----------|-------------|
| `DrawingImageControl` | Shape overlay with MVVM support |
| `ShapeRenderer` | Renders shapes to WPF canvas |
| `ShapeStyleConverter` | Converts ShapeStyle to WPF brushes |

### TiledImage.Wpf.Mapping

| Component | Description |
|-----------|-------------|
| `ImageMappingControl` | Perspective transformation and overlay |
| `PerspectiveEditor` | Interactive corner dragging editor |
| `SkiaExtensions` | Matrix3x3/Point2F to SKMatrix/SKPoint |

---

## API Reference

### Namespaces

```csharp
// Core
using TiledImage.Core.IO;              // IImageLoader, IImageExporter
using TiledImage.Core.Memory;          // IPixelBuffer, BufferType
using TiledImage.Core.Tiling;          // TiledImageSource, ITileDataProvider
using TiledImage.Core.Types;           // PixelFormat, ImageFormat

// Transforms
using TiledImage.Transforms;           // TransformPipeline, ITransformStep
using TiledImage.Transforms.Operations;// CropTransform, FlipTransform, etc.
using TiledImage.Transforms.Sampling;  // IPixelSampler, PixelResampler
using TiledImage.Transforms.Types;     // InterpolationMode

// Formats
using TiledImage.Formats.SkiaSharp.IO; // SkiaSharpImageLoader, Exporter

// Geometry
using Geometry.Primitives;             // Point2D, Rect2L, Matrix3x3
using Geometry.Shapes.Shapes;          // IShape, EllipseShape, etc.
using Geometry.Shapes.Types;           // ShapeStyle

// WPF Controls
using TiledImage.Wpf.Viewer;           // ImageViewControl
using TiledImage.Wpf.Drawing;          // DrawingImageControl
using TiledImage.Wpf.Mapping;          // ImageMappingControl
```

### Creating a Custom Provider

```csharp
public class CustomTileProvider : ITileDataProvider
{
    public long ImageWidth { get; }
    public long ImageHeight { get; }
    public long ImageDepth { get; }
    public PixelFormat PixelFormat { get; }
    public int BytesPerPixel => PixelFormat.GetBytesPerPixel();
    public bool IsReady { get; }

    public void ReadTileData(long pixelX, long pixelY, long z,
        int tileWidth, int tileHeight, byte[] buffer) { /* ... */ }

    public void ReadPixels(long x, long y, long z,
        int width, int height, byte[] buffer) { /* ... */ }

    public void Dispose() { /* ... */ }
}

// Usage
var provider = new CustomTileProvider();
var source = new TiledImageSource(provider, tileSize: 512);
```

---

## Requirements

- .NET 8.0 or later
- Windows (WPF)
- SkiaSharp 3.x

## Running the Demo

```bash
dotnet run --project samples/TiledImage.Demo/TiledImage.Demo.csproj
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## Acknowledgments

- [SkiaSharp](https://github.com/mono/SkiaSharp) - Cross-platform 2D graphics library
- [CommunityToolkit.HighPerformance](https://github.com/CommunityToolkit/dotnet) - High-performance helpers
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM toolkit
