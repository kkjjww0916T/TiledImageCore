# PCB 검사 프로파일 설계 문서

## 1. 개요

### 1.1 목적
PCB 검사를 위한 검사 프로파일 시스템으로, 컴포넌트의 핀 단위로 전처리 및 검사 파라메터를 지정하고 판정값을 설정한다.

### 1.2 주요 기능
- 컴포넌트별 핀 기하 정보 관리
- 핀 단위 이미지 전처리(Segmentation) 설정
- 핀 형상별 특징 추출(Feature Extraction) 설정
- 추출된 특징 기반 판정(Judgment) 규칙 설정

---

## 2. 데이터 구조

### 2.1 GERBER 섹션 구조

GERBER 섹션은 **2단계 구조**로 핀 기하 정보를 정의한다.

#### 전체 구조
```
GERBER
├── pads/                          ← 패드 템플릿 정의 (재사용 가능)
│   └── pd[num]
│       └── shape (type, 속성들)
│
├── footprint/                     ← 핀 배치 정의
│   ├── (bodywidth, bodyheight 등 메타정보)
│   └── pin[name, pd, x, y, rot]   ← pd로 템플릿 참조
│
└── pkg/                           ← 패키지 정보 (선택)
```

#### 2.1.1 패드 템플릿 (pads/pd)

재사용 가능한 패드 형상을 정의한다. `num` 속성으로 식별.

```
pd (Pad Template)
├── num: 패드 템플릿 번호 (고유 식별자)
└── shape: 형상 정보
    ├── type: 형상 타입 (rc, cir, ob, poly)
    ├── offx, offy: 형상 내 오프셋 (mm)
    ├── rot: 형상 회전 (도)
    ├── area: 면적 (mm²)
    ├── perimeter: 둘레 (mm)
    └── (타입별 추가 속성)
```

#### 2.1.2 Footprint (컴포넌트 영역)

**Footprint는 필수 요소**로, Pin 배치 영역을 정의한다. Footprint가 정의되지 않으면 Pin을 생성할 수 없다.

```
footprint (Footprint Definition)
├── num: 풋프린트 번호
├── name: 풋프린트 이름
├── bodywidth: 풋프린트 너비 (mm)
├── bodyheight: 풋프린트 높이 (mm)
├── offx: Part 중심으로부터의 X 오프셋 (mm)
├── offy: Part 중심으로부터의 Y 오프셋 (mm)
└── pin[]: 핀 배치 목록
```

**Footprint 좌표계:**
- `offx`, `offy`: Part 공간의 중심(0,0)으로부터의 오프셋
- Footprint 중심 = Part 중심 + (offx, offy)

#### 2.1.3 핀 배치 (footprint/pin)

패드 템플릿을 참조하여 **Footprint 중심 기준 상대 좌표**로 핀 위치를 정의한다.

```
pin (Pin Placement)
├── name: 핀 이름 (고유 식별자, 예: "1", "L1")
├── pd: 참조할 패드 템플릿 번호 (pads/pd의 num)
├── x, y: Footprint 중심 기준 상대 위치 (mm)
└── rot: 핀 회전 각도 (도)
```

**좌표 체계:**
```
Part 공간 (Part Space)
       │
       │  Footprint Offset
       │  (offx, offy)
       ▼
┌──────────────────────────────┐
│        Footprint             │
│    ┌─────────────────┐       │
│    │  Pin Position   │       │
│    │  (x, y)         │       │
│    │  ↙              │       │
│    ◉────────────────►│       │
│    Footprint Center  │       │
│    └─────────────────┘       │
└──────────────────────────────┘
```

**최종 좌표 계산 (Part 공간 기준):**
- X_absolute = `footprint.offx` + `pin.x` + `pd.shape.offx`
- Y_absolute = `footprint.offy` + `pin.y` + `pd.shape.offy`
- Rotation = `pd.shape.rot` + `pin.rot`

> **중요**: Pin의 (x, y)는 **Footprint 중심 기준 상대 좌표**이므로, Footprint가 이동해도 Pin 데이터는 변경되지 않는다.

#### 2.1.4 패드 타입 (Shape Type)

| 타입 | 이름 | 설명 | 추가 속성 |
|------|------|------|-----------|
| `rc` | Rectangle | 사각형 | `w`, `h`, `rot` (너비, 높이, 회전각) |
| `cir` | Circle | 원 | `dia` (직경) |
| `ob` | Oblong | 캡슐형 (둥근 사각형) | `w`, `h`, `rot` |
| `poly` | Polygon | 다각형 | `pt[]` (꼭지점 목록) |

#### 타입별 상세

**Rectangle (rc)**
```
shape (type="rc")
├── w: 너비 (mm)
├── h: 높이 (mm)
├── rot: 회전 각도 (도)
├── offx, offy: 중심 오프셋 (mm)
├── area: 면적 (mm²)
└── perimeter: 둘레 (mm)
```

**Circle (cir)**
```
shape (type="cir")
├── dia: 직경 (mm)
├── offx, offy: 중심 오프셋 (mm)
├── area: 면적 (mm²)
└── perimeter: 둘레 (mm)
```

**Oblong (ob)**
```
shape (type="ob")
├── w: 너비 (mm)
├── h: 높이 (mm)
├── rot: 회전 각도 (도)
├── offx, offy: 중심 오프셋 (mm)
├── area: 면적 (mm²)
└── perimeter: 둘레 (mm)
```

**Polygon (poly)**
```
shape (type="poly")
├── offx, offy: 중심 오프셋 (mm)
├── area: 면적 (mm²)
├── perimeter: 둘레 (mm)
└── pt[]: 꼭지점 목록
    └── x, y: 좌표 (mm)
```

#### 2.1.5 XML 예시

**전체 GERBER 구조 예시**
```xml
<GERBER>
    <!-- 패드 템플릿 정의 -->
    <pads>
        <pd num="6">
            <shape type="cir" dia="0.565" offx="0" offy="0" area="0.251" perimeter="1.775" />
        </pd>
        <pd num="9">
            <shape type="rc" w="0.555" h="0.548" offx="0" offy="0" rot="270" area="0.304" />
        </pd>
        <pd num="7">
            <shape type="poly" offx="-0.003" offy="-0.003" rot="0" area="0.241">
                <pt x="-0.27" y="0.097" />
                <pt x="-0.113" y="0.254" />
                <!-- ... 다각형 점들 ... -->
            </shape>
        </pd>
    </pads>

    <!-- Footprint 정의 (필수) + 핀 배치 -->
    <!-- offx, offy: Part 중심으로부터의 오프셋 -->
    <!-- Pin의 x, y: Footprint 중심 기준 상대 좌표 -->
    <footprint num="3" name="B000076617"
               bodywidth="15.499" bodyheight="15.505"
               offx="0.000000" offy="0.000000">
        <pin name="1" pd="13" x="6.736" y="6.728" rot="270" />
        <pin name="2" pd="10" x="6.003" y="-3.589" rot="270" />
        <pin name="3" pd="8" x="5.995" y="3.614" rot="270" />
        <!-- ... 256개 핀 (Footprint 중심 기준 상대 좌표) ... -->
    </footprint>
</GERBER>
```

**패드 템플릿별 예시**

Rectangle (rc)
```xml
<pd num="9">
    <shape type="rc" w="0.555" h="0.548" offx="0" offy="0" rot="270" area="0.304" perimeter="2.206" />
</pd>
```

Circle (cir)
```xml
<pd num="6">
    <shape type="cir" dia="0.565" offx="0" offy="0" area="0.251" perimeter="1.775" />
</pd>
```

Polygon (poly)
```xml
<pd num="7">
    <shape type="poly" offx="-0.003" offy="-0.003" rot="0" area="0.241" perimeter="1.757">
        <pt x="-0.27" y="0.097" />
        <pt x="-0.113" y="0.254" />
        <!-- ... 곡선 근사를 위한 다수의 점들 ... -->
    </shape>
</pd>
```

**핀 배치 예시**
```xml
<!-- pd="6" 템플릿을 (5.995, 2.014) 위치에 270도 회전하여 배치 -->
<pin name="9" pd="6" x="5.995" y="2.014" rot="270" />
```

#### 2.1.6 좌표계 설정

좌표계는 사용자 설정에 따라 변경될 수 있음.

| 설정 | 값 | 설명 |
|------|-----|------|
| 원점 위치 | `LL` (Lower-Left) | 좌하단 기준 (기본값) |
| | `LR` (Lower-Right) | 우하단 기준 |
| | `UL` (Upper-Left) | 좌상단 기준 |
| | `UR` (Upper-Right) | 우상단 기준 |
| 회전 방향 | `CCW` (Counter-Clockwise) | 반시계 방향 (기본값) |
| | `CW` (Clockwise) | 시계 방향 |

```
LL (Lower-Left) 좌표계          이미지 좌표계 (UL)

     Y+                              0 ────────► X+
     │                               │
     │                               │
     └─────► X+                      │
    원점(0,0)                        ▼ Y+
```

> **참고**: 좌표 변환 시 원점 위치와 회전 방향을 고려해야 함

---

### 2.2 AC_SEG (Segmentation - 전처리)

핀 단위 이미지 전처리 파이프라인을 정의한다.

#### 구조
```
AC_SEG
├── ORDER: 실행 순서 (1, 2, 3, ...)
├── NAME: 알고리즘 이름 ← 외부 시스템 정의
├── VER: 알고리즘 버전 ← 외부 시스템 정의
├── ARGU[]: 파라메터 목록
│   ├── NAME: 파라메터 이름 ← 외부 시스템 정의
│   ├── VALUE: 설정값 ← 프로파일에서 설정
│   ├── MIN: 최소값 ← 외부 시스템 정의
│   ├── MAX: 최대값 ← 외부 시스템 정의
│   ├── DEFAULT: 기본값 ← 외부 시스템 정의
│   ├── UNIT: 단위 ← 외부 시스템 정의
│   └── DESCRIPTION: 설명 ← 외부 시스템 정의
└── PIN[]: 적용 대상 핀 목록 ← 프로파일에서 설정
    └── NAME: 핀 식별자
```

#### 데이터 소스 구분
| 항목 | 소스 | 설명 |
|------|------|------|
| NAME + VER | 외부 시스템 | 알고리즘 고유 식별 |
| ARGU 구조 | 외부 시스템 | 파라메터 정의 (이름, 범위, 단위 등) |
| ARGU VALUE | 프로파일 | 실제 적용할 파라메터 값 |
| ORDER | 프로파일 | 실행 순서 |
| PIN 목록 | 프로파일 | 적용 대상 핀 |

#### 특징
- **실행 순서(ORDER)가 결과에 영향**을 줌
- 동일 알고리즘을 **여러 번 적용 가능** (다른 ORDER, 다른 파라메터)
- 핀별로 다른 전처리 파이프라인 구성 가능

#### XML 예시
```xml
<AC_SEG ORDER="1" NAME="GaussianBlur" VER="1.0.0.0">
  <ARGU NAME="Distance Rate" VALUE="100" MIN="10" MAX="200" DEFAULT="100" UNIT="%"
        DESCRIPTION="A value indicating how much overlap is used with adjacent pads." />
  <ARGU NAME="Spatial Sigma" VALUE="3" MIN="0.001" MAX="1.7976931348623157E+308" DEFAULT="1" UNIT="value"
        DESCRIPTION="Sigma value of gaussian blur" />
  <ARGU NAME="Kernel Width" VALUE="3" MIN="3" MAX="1.7976931348623157E+308" DEFAULT="3" UNIT="count"
        DESCRIPTION="Filter kernel size" />
  <PIN NAME="L1" />
  <PIN NAME="L2" />
  <PIN NAME="L3" />
</AC_SEG>

<AC_SEG ORDER="2" NAME="GlobalAutomatic" VER="1.0.0.0">
  <ARGU NAME="Distance Rate" VALUE="200" MIN="100" MAX="1000" DEFAULT="200" UNIT="%"
        DESCRIPTION="A value indicating how much overlap is used with adjacent pads." />
  <ARGU NAME="Sampling Rate" VALUE="256" MIN="32" MAX="512" DEFAULT="256" UNIT="count"
        DESCRIPTION="Precision setting for binarization" />
  <PIN NAME="L1" />
  <PIN NAME="L2" />
</AC_SEG>
```

---

### 2.3 AC_FE + FE_PIN_GROUP (Feature Extraction)

전처리된 이미지에서 특징을 추출하는 알고리즘과 핀 매핑을 정의한다.

#### 2.3.1 AC_FE (Feature Extraction 인스턴스)

```
AC_FE
├── INDEX: 내부 참조 식별자
├── NAME: 알고리즘 이름 ← 외부 시스템 정의
├── VER: 알고리즘 버전 ← 외부 시스템 정의
├── GLOBAL_FEATURE[]: 그룹 레벨 특징 (선택적)
│   ├── NAME: 특징 이름 ← 외부 시스템 정의
│   ├── UNIT: 단위 ← 외부 시스템 정의
│   └── DESCRIPTION: 설명 ← 외부 시스템 정의
├── OBJECT_FEATURE[]: 핀 레벨 특징 (선택적)
│   ├── NAME: 특징 이름 ← 외부 시스템 정의
│   ├── UNIT: 단위 ← 외부 시스템 정의
│   └── DESCRIPTION: 설명 ← 외부 시스템 정의
└── ARGU[]: 파라메터 목록
    └── (AC_SEG와 동일한 구조)
```

#### 데이터 소스 구분
| 항목 | 소스 | 설명 |
|------|------|------|
| NAME + VER | 외부 시스템 | 알고리즘 고유 식별 |
| GLOBAL_FEATURE | 외부 시스템 | 그룹 레벨 추출 가능 특징 |
| OBJECT_FEATURE | 외부 시스템 | 핀 레벨 추출 가능 특징 |
| ARGU 구조 | 외부 시스템 | 파라메터 정의 |
| INDEX | 프로파일 | 내부 참조용 식별자 |
| ARGU VALUE | 프로파일 | 실제 적용할 파라메터 값 |

#### 특징
- **동일한 NAME + VER = 동일한 ARGU 구조 + FEATURE 목록**
- INDEX가 다른 AC_FE: 동일 알고리즘이지만 **파라메터 값이 다름**
- 하나의 FE는 GLOBAL_FEATURE만, OBJECT_FEATURE만, 또는 둘 다 가질 수 있음

#### FE 분류 기준
| 구분 | 기존 | 변경 예정 |
|------|------|-----------|
| 기준 | 특징별 (Void, MilletShaped 등) | **핀 형상별** (RectPin, PolyPin 등) |
| 특징 | FE 하나 = 특정 특징 추출 | FE 하나 = 해당 형상의 여러 특징 추출 |

#### XML 예시
```xml
<AC_FE INDEX="1" NAME="RectPin" VER="1.0.0.0">
  <GLOBAL_FEATURE NAME="Total Coverage" UNIT="%" DESCRIPTION="그룹 전체 커버리지" />
  <OBJECT_FEATURE NAME="Area" UNIT="mm2" DESCRIPTION="핀 면적" />
  <OBJECT_FEATURE NAME="Rectangularity" UNIT="%" DESCRIPTION="사각형도" />
  <OBJECT_FEATURE NAME="Void Rate" UNIT="%" DESCRIPTION="보이드 비율" />
  <ARGU NAME="Rejection Distance" VALUE="1" MIN="0" MAX="3" DEFAULT="1" UNIT="mm"
        DESCRIPTION="The distance to reject the object" />
</AC_FE>

<AC_FE INDEX="2" NAME="RectPin" VER="1.0.0.0">
  <!-- 동일 알고리즘, 다른 파라메터 값 -->
  <GLOBAL_FEATURE NAME="Total Coverage" UNIT="%" DESCRIPTION="그룹 전체 커버리지" />
  <OBJECT_FEATURE NAME="Area" UNIT="mm2" DESCRIPTION="핀 면적" />
  <OBJECT_FEATURE NAME="Rectangularity" UNIT="%" DESCRIPTION="사각형도" />
  <OBJECT_FEATURE NAME="Void Rate" UNIT="%" DESCRIPTION="보이드 비율" />
  <ARGU NAME="Rejection Distance" VALUE="2" MIN="0" MAX="3" DEFAULT="1" UNIT="mm"
        DESCRIPTION="The distance to reject the object" />
</AC_FE>
```

#### 2.3.2 FE_PIN_GROUP (FE-핀 매핑)

```
FE_PIN_GROUP
├── TYPE: 그룹 타입
│   ├── "PIN": OBJECT_FEATURE 추출용
│   └── "GLOBAL": GLOBAL_FEATURE 추출용
├── REF_FE: 참조할 AC_FE의 INDEX
└── PIN[]: 포함 핀 목록
    └── NAME: 핀 식별자
```

#### 핀 소속 규칙
| TYPE | 핀 중복 허용 | 용도 |
|------|-------------|------|
| PIN | **불가** (배타적) | OBJECT_FEATURE 추출 |
| GLOBAL | **허용** (중복 가능) | GLOBAL_FEATURE 추출 |

> **중요**: 하나의 핀은 반드시 **하나의 TYPE="PIN" 그룹에만** 소속될 수 있다.
> 단, TYPE="GLOBAL" 그룹에는 중복 소속 가능.

#### AC_FE와 FE_PIN_GROUP 관계

```
AC_FE (1) ←───── FE_PIN_GROUP (N)

하나의 AC_FE 인스턴스에 대해:
├── FE_PIN_GROUP(TYPE=PIN)    → OBJECT_FEATURE 추출 대상
└── FE_PIN_GROUP(TYPE=GLOBAL) → GLOBAL_FEATURE 추출 대상
```

#### XML 예시
```xml
<!-- AC_FE INDEX=1에 대한 매핑 -->
<FE_PIN_GROUP TYPE="PIN" REF_FE="1">
  <PIN NAME="L1" />
  <PIN NAME="L2" />
  <PIN NAME="L3" />
</FE_PIN_GROUP>

<FE_PIN_GROUP TYPE="GLOBAL" REF_FE="1">
  <PIN NAME="L1" />
  <PIN NAME="L2" />
  <PIN NAME="L3" />
</FE_PIN_GROUP>

<!-- AC_FE INDEX=2에 대한 매핑 (핀 중복 불가) -->
<FE_PIN_GROUP TYPE="PIN" REF_FE="2">
  <PIN NAME="R1" />  <!-- L1, L2, L3은 이미 INDEX=1에 소속되어 사용 불가 -->
  <PIN NAME="R2" />
  <PIN NAME="R3" />
</FE_PIN_GROUP>

<FE_PIN_GROUP TYPE="GLOBAL" REF_FE="2">
  <PIN NAME="R1" />
  <PIN NAME="R2" />
  <PIN NAME="R3" />
</FE_PIN_GROUP>
```

---

### 2.4 Judgment (판정)

추출된 특징을 기반으로 검사 판정 규칙을 정의한다.

#### 구조
```
PIN_JUDG
├── NAME: 판정 그룹명
├── FEATURE_GROUP[]: 판정에 사용할 특징 목록
│   ├── AC: AC_FE 알고리즘명
│   ├── FE: FE 알고리즘명
│   ├── FEATURE: 특징명
│   └── UNIT: 단위
├── PIN_GROUP[]: 대상 핀 목록
│   └── NAME: 핀 식별자
└── CLASS_RULE[]: 판정 규칙 목록
    ├── NAME: 규칙명
    ├── NG_MSG: 불량 판정 시 메시지
    └── CLASS1 / CLASS2 / CLASS3: 엄격도 레벨별 기준
        ├── AC: AC_FE 알고리즘명
        ├── FE: FE 알고리즘명
        ├── FEATURE: 특징명
        ├── UNIT: 단위
        ├── MIN: 최소 허용값
        ├── MAX: 최대 허용값
        └── IS_USE: 사용 여부 (true/false)
```

#### FE_PIN_GROUP과의 관계
- PIN_JUDG와 FE_PIN_GROUP은 **1:1 매칭**
- PIN_JUDG의 PIN_GROUP 목록 = FE_PIN_GROUP의 PIN 목록

#### CLASS 레벨 (엄격도)
| 레벨 | 설명 | 허용 범위 |
|------|------|-----------|
| CLASS1 | 느슨한 기준 | 넓음 |
| CLASS2 | 중간 기준 | 중간 |
| CLASS3 | 엄격한 기준 | 좁음 |

#### CLASS_RULE (판정 규칙)
- 여러 특징을 **조합**하여 하나의 검사 기준 정의
- 각 특징별로 IS_USE 여부와 MIN/MAX 범위 설정
- 레벨별로 다른 허용 범위 적용 가능

#### XML 예시
```xml
<PIN_JUDG NAME="좁쌀 그룹">
  <!-- 사용할 특징 목록 -->
  <FEATURE_GROUP AC="RectPin" FE="RectPin" FEATURE="Area" UNIT="mm2" />
  <FEATURE_GROUP AC="RectPin" FE="RectPin" FEATURE="Rectangularity" UNIT="%" />
  <FEATURE_GROUP AC="RectPin" FE="RectPin" FEATURE="Void Rate" UNIT="%" />

  <!-- 대상 핀 목록 (FE_PIN_GROUP과 동일) -->
  <PIN_GROUP NAME="L1" />
  <PIN_GROUP NAME="L2" />
  <PIN_GROUP NAME="L3" />

  <!-- 판정 규칙 -->
  <CLASS_RULE NAME="면적 검사" NG_MSG="면적 불량">
    <!-- CLASS1: 느슨한 기준 -->
    <CLASS1 AC="RectPin" FE="RectPin" FEATURE="Area" UNIT="mm2" MIN="0.05" MAX="0.20" IS_USE="true" />
    <CLASS1 AC="RectPin" FE="RectPin" FEATURE="Rectangularity" UNIT="%" MIN="0" MAX="100" IS_USE="false" />
    <CLASS1 AC="RectPin" FE="RectPin" FEATURE="Void Rate" UNIT="%" MIN="0" MAX="30" IS_USE="true" />

    <!-- CLASS2: 중간 기준 -->
    <CLASS2 AC="RectPin" FE="RectPin" FEATURE="Area" UNIT="mm2" MIN="0.08" MAX="0.15" IS_USE="true" />
    <CLASS2 AC="RectPin" FE="RectPin" FEATURE="Rectangularity" UNIT="%" MIN="70" MAX="100" IS_USE="true" />
    <CLASS2 AC="RectPin" FE="RectPin" FEATURE="Void Rate" UNIT="%" MIN="0" MAX="20" IS_USE="true" />

    <!-- CLASS3: 엄격한 기준 -->
    <CLASS3 AC="RectPin" FE="RectPin" FEATURE="Area" UNIT="mm2" MIN="0.10" MAX="0.12" IS_USE="true" />
    <CLASS3 AC="RectPin" FE="RectPin" FEATURE="Rectangularity" UNIT="%" MIN="85" MAX="100" IS_USE="true" />
    <CLASS3 AC="RectPin" FE="RectPin" FEATURE="Void Rate" UNIT="%" MIN="0" MAX="10" IS_USE="true" />
  </CLASS_RULE>
</PIN_JUDG>
```

---

## 3. 데이터 관계도

### 3.1 전체 관계

```
┌─────────────────────────────────────────────────────────────┐
│                     외부 시스템 정의                         │
│  ┌────────────────────┐    ┌────────────────────┐          │
│  │ Segmentation 정의   │    │ FE 정의 (형상별)   │          │
│  │ • Name + Ver       │    │ • Name + Ver       │          │
│  │ • Argument 구조    │    │ • Argument 구조    │          │
│  │                    │    │ • Feature 목록     │          │
│  │                    │    │   - GLOBAL_FEATURE │          │
│  │                    │    │   - OBJECT_FEATURE │          │
│  └────────────────────┘    └────────────────────┘          │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   검사 프로파일 (XML)                        │
│                                                             │
│  ① GERBER                                                   │
│     핀 기하 정보 (NAME, SHAPE, 좌표)                         │
│         │                                                   │
│         │ 핀 참조                                            │
│         ▼                                                   │
│  ② AC_SEG[]                                                 │
│     전처리 파이프라인                                        │
│     • ORDER 순서대로 실행                                    │
│     • 핀별 적용 설정                                         │
│         │                                                   │
│         │ 전처리된 이미지                                    │
│         ▼                                                   │
│  ③ AC_FE ←──1:N──→ FE_PIN_GROUP                            │
│     특징 추출 인스턴스      핀-FE 매핑                        │
│         │                     │                             │
│         │                     │ 1:1 매칭                    │
│         ▼                     ▼                             │
│  ④ PIN_JUDG                                                 │
│     판정 그룹                                                │
│         │                                                   │
│         └──→ CLASS_RULE                                     │
│              판정 규칙 (CLASS1/2/3)                          │
└─────────────────────────────────────────────────────────────┘
```

### 3.2 핵심 관계 요약

| 관계 | 설명 |
|------|------|
| GERBER.PIN → AC_SEG.PIN | 전처리 적용 대상 |
| GERBER.PIN → FE_PIN_GROUP.PIN | 특징 추출 대상 |
| AC_FE ← FE_PIN_GROUP (1:N) | FE 인스턴스를 여러 핀 그룹에 적용 |
| FE_PIN_GROUP ↔ PIN_JUDG (1:1) | 판정 그룹과 FE 그룹 매칭 |
| PIN_JUDG → CLASS_RULE (1:N) | 하나의 판정 그룹에 여러 규칙 |

---

## 4. 데이터 흐름

```
┌──────────────┐
│   이미지     │
└──────┬───────┘
       │
       ▼
┌──────────────────────────────────────────────────┐
│  ① Segmentation (전처리)                         │
│                                                  │
│  ORDER=1: GaussianBlur → [L1, L2, L3, ...]      │
│      │                                           │
│      ▼                                           │
│  ORDER=2: GlobalAutomatic → [L1, L2, ...]       │
│      │                                           │
│      ▼                                           │
│  ORDER=3: FillVoid → [L1, L2, L3, ...]          │
└──────────────────────────────────────────────────┘
       │
       │ 전처리된 핀별 이미지
       ▼
┌──────────────────────────────────────────────────┐
│  ② Feature Extraction (특징 추출)                │
│                                                  │
│  AC_FE[1] (RectPin, Distance=1)                 │
│      │                                           │
│      ├── FE_PIN_GROUP(PIN) → [L1,L2,L3]         │
│      │   └── OBJECT: Area, Rectangularity, ...  │
│      │                                           │
│      └── FE_PIN_GROUP(GLOBAL) → [L1,L2,L3]      │
│          └── GLOBAL: Total Coverage, ...        │
│                                                  │
│  AC_FE[2] (RectPin, Distance=2)                 │
│      │                                           │
│      ├── FE_PIN_GROUP(PIN) → [R1,R2,R3]         │
│      └── FE_PIN_GROUP(GLOBAL) → [R1,R2,R3]      │
└──────────────────────────────────────────────────┘
       │
       │ 추출된 특징값
       ▼
┌──────────────────────────────────────────────────┐
│  ③ Judgment (판정)                               │
│                                                  │
│  PIN_JUDG "좁쌀 그룹" ← FE_PIN_GROUP[1]         │
│      │                                           │
│      └── CLASS_RULE "면적 검사"                  │
│          ├── CLASS1: Area(0.05~0.20), ...       │
│          ├── CLASS2: Area(0.08~0.15), ...       │
│          └── CLASS3: Area(0.10~0.12), ...       │
│                                                  │
│  PIN_JUDG "톱날 그룹" ← FE_PIN_GROUP[2]         │
│      │                                           │
│      └── CLASS_RULE "형상 검사"                  │
│          ├── CLASS1: ...                        │
│          ├── CLASS2: ...                        │
│          └── CLASS3: ...                        │
└──────────────────────────────────────────────────┘
       │
       │ 판정 결과
       ▼
┌──────────────┐
│  OK / NG     │
│  (레벨별)    │
└──────────────┘
```

---

## 5. 용어 정리

| 용어 | 설명 |
|------|------|
| GERBER | PCB 설계 데이터 형식, 핀 기하 정보 포함 |
| PIN | 검사 기본 단위, 솔더 패드 영역 |
| AC_SEG | Auto Classification Segmentation, 이미지 전처리 |
| AC_FE | Auto Classification Feature Extraction, 특징 추출 |
| FE_PIN_GROUP | FE와 핀의 매핑 그룹 |
| GLOBAL_FEATURE | 핀 그룹 전체에서 추출하는 특징 |
| OBJECT_FEATURE | 개별 핀에서 추출하는 특징 |
| PIN_JUDG | 핀 판정 그룹 |
| CLASS_RULE | 판정 규칙, 특징 조합으로 검사 기준 정의 |
| CLASS1/2/3 | 판정 엄격도 레벨 (1: 느슨, 3: 엄격) |

---

## 6. 변경 이력

| 버전 | 날짜 | 작성자 | 변경 내용 |
|------|------|--------|-----------|
| 1.0 | 2025-01-XX | - | 초안 작성 |
| 1.1 | 2025-12-11 | - | **Footprint 구조 명세 추가** |
|     |            |   | - Footprint 필수 요소화 (Pin 생성 전제조건) |
|     |            |   | - Footprint 속성: bodywidth, bodyheight, offx, offy |
|     |            |   | - Pin 좌표: Footprint 중심 기준 상대 좌표로 변경 |
|     |            |   | - 좌표 체계 다이어그램 추가 |
