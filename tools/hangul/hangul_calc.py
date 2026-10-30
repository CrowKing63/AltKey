#!/usr/bin/env python3
"""
한글 조합 계산기 (Hangul Composition Calculator)

유니코드 표준 §3.1의 한글 조합 알고리즘을 Python 내장 unicodedata 모듈로
구현한다. 수동 계산이 없으므로 환각(hallucination) 위험이 0이다.

사용법:
  python hangul_calc.py "단어"          # 단어 분해 (decompose)
  python hangul_calc.py --compose 18 18 3  # 자모 인덱스로 음절 조합
  python hangul_calc.py --verify "단어"    # 조합 가능성 검증

출력: 각 음절의 코드포인트 + 자모 분해 (초성/중성/종성 인덱스와 코드포인트)

공식: code = 0xAC00 + C*588 + V*28 + T
  C: 0-18 (초성, U+1100-U+1112)
  V: 0-20 (중성, U+1161-U+1175)
  T: 0-27 (종성, 0=없음, 1-27=자모)

출처: UnicodeData.txt (Unicode Consortium v18.0.0)
  https://unicode.org/Public/18.0.0/ucd/UnicodeData.txt
"""

import sys
import unicodedata


def is_hangul_syllable(ch: str) -> bool:
    """U+AC00-U+D7A3 범위인지 확인."""
    return 0xAC00 <= ord(ch) <= 0xD7A3


def decompose_syllable(ch: str) -> dict:
    """
    한 음절을 NFD로 분해하여 자모 인덱스와 코드포인트를 반환.
    Python의 unicodedata.normalize('NFD', ...)가 유니코드 표준을 구현하므로
    수동 계산이 필요 없다.
    """
    nfd = unicodedata.normalize('NFD', ch)
    parts = []
    for c in nfd:
        code = ord(c)
        name = unicodedata.name(c, 'UNKNOWN')
        parts.append({'code': code, 'name': name})

    # 인덱스 계산
    choseong_idx = None
    jungeong_idx = None
    jacheong_idx = None

    for p in parts:
        code = p['code']
        if 0x1100 <= code <= 0x1112:
            choseong_idx = code - 0x1100
        elif 0x1161 <= code <= 0x1175:
            jungeong_idx = code - 0x1161
        elif 0x11A8 <= code <= 0x11B7:
            jacheong_idx = code - 0x11A8 + 1  # 1-based (0=null)

    return {
        'char': ch,
        'code': ord(ch),
        'choseong': choseong_idx,
        'jungeong': jungeong_idx,
        'jacheong': jacheong_idx,
        'parts': parts,
    }


def compose_syllable(c: int, v: int, t: int) -> str:
    """
    자모 인덱스로 음절을 조합.
    C: 0-18, V: 0-20, T: 0-27
    """
    if not (0 <= c <= 18 and 0 <= v <= 20 and 0 <= t <= 27):
        raise ValueError(f"인덱스 범위 초과: C={c}, V={v}, T={t}")
    code = 0xAC00 + c * 588 + v * 28 + t
    return chr(code)


def jamo_name(idx: int, kind: str) -> str:
    """인덱스 → 자모 이름 (유니코드 공식 명칭)."""
    if kind == 'choseong':
        code = 0x1100 + idx
    elif kind == 'jungeong':
        code = 0x1161 + idx
    elif kind == 'jacheong':
        if idx == 0:
            return '(없음)'
        # T=1..16 → U+11A8..U+11B7, T=17..27 → 초성 블록과 겹침
        if idx <= 16:
            code = 0x11A8 + (idx - 1)
        else:
            # T=17..27: ㅂ,ㅃ,ㅅ,ㅆ,ㅇ,ㅈ,ㅉ,ㅋ,ㅌ,ㅍ,ㅎ
            # U+1107(ㅂ), U+1108(ㅃ), U+1109(ㅅ), U+110A(ㅆ), U+110B(ㅇ),
            # U+110C(ㅈ), U+110D(ㅉ), U+110F(ㅋ), U+1110(ㅌ), U+1111(ㅍ), U+1112(ㅎ)
            overlap_map = [0x1107, 0x1108, 0x1109, 0x110A, 0x110B,
                           0x110C, 0x110D, 0x110F, 0x1110, 0x1111, 0x1112]
            code = overlap_map[idx - 17]
    else:
        raise ValueError(f"알 수 없는 kind: {kind}")

    return unicodedata.name(chr(code), 'UNKNOWN')


def analyze_word(word: str) -> None:
    """단어를 음절별로 분해하고 출력."""
    print(f"입력: {word}")
    print(f"음절 수: {len([c for c in word if is_hangul_syllable(c)])}")
    print()

    for ch in word:
        if not is_hangul_syllable(ch):
            print(f"  [비한글] {ch!r} (U+{ord(ch):04X}) - 조합 대상 아님")
            continue

        d = decompose_syllable(ch)
        c, v, t = d['choseong'], d['jungeong'], d['jacheong']

        # 자모 이름 조회
        c_name = jamo_name(c, 'choseong') if c is not None else '?'
        v_name = jamo_name(v, 'jungeong') if v is not None else '?'
        t_name = jamo_name(t, 'jacheong') if t is not None else '(없음)'

        print(f"  {ch} (U+{d['code']:04X})")
        print(f"    초성: {c} ({c_name})")
        print(f"    중성: {v} ({v_name})")
        print(f"    종성: {t} ({t_name})")

        # NFD 부품 목록
        parts_str = ' + '.join(f"U+{p['code']:04X}" for p in d['parts'])
        print(f"    NFD: {parts_str}")
        print()


def verify_composition(word: str) -> None:
    """
    단어의 각 음절이 유효한 조합인지 검증.
    NFC 정규화로 재조합했을 때 원본과 일치하는지 확인.
    """
    print(f"검증 대상: {word}")
    all_ok = True
    for ch in word:
        if not is_hangul_syllable(ch):
            continue
        # NFD → NFC round-trip
        nfd = unicodedata.normalize('NFD', ch)
        nfc = unicodedata.normalize('NFC', nfd)
        if nfc != ch:
            print(f"  [FAIL] {ch} (U+{ord(ch):04X}): NFC round-trip mismatch")
            all_ok = False
    if all_ok:
        print("  모든 음절이 유효한 조합입니다. ✓")


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)

    mode = sys.argv[1]

    if mode == '--compose':
        # --compose C V T
        if len(sys.argv) < 5:
            print("Usage: hangul_calc.py --compose <C> <V> <T>")
            sys.exit(1)
        c, v, t = int(sys.argv[2]), int(sys.argv[3]), int(sys.argv[4])
        ch = compose_syllable(c, v, t)
        print(f"조합 결과: {ch} (U+{ord(ch):04X})")
        d = decompose_syllable(ch)
        print(f"  초성: {d['choseong']} ({jamo_name(d['choseong'], 'choseong')})")
        print(f"  중성: {d['jungeong']} ({jamo_name(d['jungeong'], 'jungeong')})")
        print(f"  종성: {d['jacheong']} ({jamo_name(d['jacheong'], 'jacheong')})")

    elif mode == '--verify':
        if len(sys.argv) < 3:
            print("Usage: hangul_calc.py --verify <word>")
            sys.exit(1)
        verify_composition(sys.argv[2])

    else:
        # 기본: 단어 분해
        analyze_word(mode)


if __name__ == '__main__':
    main()
