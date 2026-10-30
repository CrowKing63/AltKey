# tools/hangul — 한글 조합 계산 도구

에이전트(Judy)가 한글 유니코드 조합 버그를 디버깅할 때 사용하는 참조 도구 세트.
수동 계산이 없으므로 환각(hallucination) 위험 0.

## 파일 구성

| 파일 | 용도 |
|------|------|
| `UnicodeData_hangul.txt` | UnicodeData.txt에서 추출한 한글 블록 (공신력 있는 원본) |
| `hangul_calc.py` | 조합 계산기 스크립트 |
| `README.md` | 이 파일 |

## 출처

- **원본**: [UnicodeData.txt](https://unicode.org/Public/18.0.0/ucd/UnicodeData.txt) (Unicode Consortium v18.0.0, 2024)
- **공식 참조**: [forensic-proof.com/archives/615](http://forensic-proof.com/archives/615) — 한글 문자 집합 및 인코딩

## 조합 공식

```
code = 0xAC00 + C × 588 + V × 28 + T
```

| 인자 | 범위 | 설명 |
|------|------|------|
| C (초성) | 0–18 | U+1100–U+1112, 19개 |
| V (중성) | 0–20 | U+1161–U+1175, 21개 |
| T (종성) | 0–27 | 0=없음, 1–27=자모 |

총 음절 수: 19 × 21 × 28 = **11,172** (U+AC00 ~ U+D7A3)

## 사용법

### 단어 분해 (decompose)
```bash
python hangul_calc.py "한글조합"
```
각 음절의 코드포인트 + 초성/중성/종성 인덱스 + NFD 자모 목록을 출력.

### 자모 인덱스로 조합 (compose)
```bash
python hangul_calc.py --compose 18 0 4
# → U+D55C (한)
```

### 조합 유효성 검증 (verify)
```bash
python hangul_calc.py --verify "접근성"
# NFC round-trip으로 모든 음절이 유효한지 확인
```

## 구현 방식

Python 내장 `unicodedata.normalize('NFD', ...)` / `normalize('NFC', ...)`를 사용.
CPython이 유니코드 표준을 직접 구현하므로 수동 산술이 필요 없다.
공식은 교차 검증(cross-check) 용도로만 사용한다.

## 에이전트 워크플로우

1. 사용자가 "○○ 단어가 조합 안 된다"고 보고
2. `python hangul_calc.py "○○"` 실행 → ground truth 확보
3. AltKey 코드에서 해당 음절 처리 경로와 대조
4. 불일치 지점을 특정하여 수정
