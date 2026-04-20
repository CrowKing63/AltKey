# 작업 01: Config 모델 — Width/Height → Scale 전환

> 의존성: 없음 (Phase A, 병렬 가능)

---

## 목표

`WindowConfig`에서 픽셀 기반 `Width`/`Height`를 제거하고 정수 `Scale`(퍼센트)로 교체한다.
기존 config.json을 가진 사용자도 자동 마이그레이션으로 무손실 전환.

---

## 변경 파일

### 1. `Models/AppConfig.cs` — WindowConfig

**Before:**
```csharp
public class WindowConfig
{
    public double Left   { get; set; } = -1;
    public double Top    { get; set; } = -1;
    public double Width  { get; set; } = 900;
    public double Height { get; set; } = 320;
}
```

**After:**
```csharp
public class WindowConfig
{
    public double Left   { get; set; } = -1;
    public double Top    { get; set; } = -1;

    /// 창 크기 비율 (퍼센트). 기본 100. 허용 범위 60~200.
    public int Scale { get; set; } = 100;
}
```

- `Width`, `Height` 프로퍼티 삭제
- `Scale` 기본값 100 (기존 기본 900×320은 100%에서 재현됨)

### 2. `Services/ConfigService.cs` — 마이그레이션

config.json을 읽을 때 `Scale` 필드가 없고 `Width`/`Height`가 있으면 자동 변환:

```csharp
// Load 후 마이그레이션 체크
private void MigrateWindowConfig(AppConfig config)
{
    // Scale 필드가 이미 있으면 스킵
    // Width/Height 필드가 있으면 기준 크기(900×320) 대비 비율 계산하여 Scale 설정
    // JSON 역직렬화 시 기본값(100)과 구분하기 위해 원본 JSON에서 필드 존재 여부 확인
}
```

- 900×320 = 100% 기준
- Scale = (int)Math.Round(old.Width / 900.0 * 100)
- 범위 클램프: Math.Clamp(scale, 60, 200)
- 마이그레이션 후 다음 저장 시 새 형식으로 기록 (Width/Height 필드 자연 소멸)

---

## 주의사항

- 이 작업은 모델만 변경. 실제 Scale 적용(창 크기 계산)은 작업 03에서 처리
- 작업 01 적용 직후에는 빌드 에러 발생 가능 (Width/Height 참조가 남아있음)
  → 이 에러들은 작업 05(save/restore)에서 정리
- `ConfigService.Update()` 호출부에서 `c.Window.Width` / `c.Window.Height` 참조는
  모두 작업 05에서 일괄 수정하므로 여기서는 신경 쓰지 않아도 됨
- `Scale`은 `int` 타입. 슬라이더에서 10 단위로만 조절하므로 소수 불필요

---

## 완료 조건

- [ ] `WindowConfig`에서 Width/Height 제거, Scale(int) 추가
- [ ] ConfigService에 마이그레이션 로직 구현
- [ ] 기존 config.json(Width/Height 포함) 로드 시 Scale로 자동 변환
