# 07 — 문맥별 AI 결과 메모리 캐시

## 목표
같은 (문맥, 후보) 조합에 대해 반복 추론을 막아 체감 반응성을 유지한다(기획 10·15번).

## 배경 (레포 기준)
- 5번 통합이 `LayaReranker.RerankAsync`를 호출하는 지점이다. 캐시는 그 **앞**에 끼인다.
- 자동완성은 키 입력과 직결되어 반응성이 정확도보다 우선(기획 15번).

## 작업
1. `AltKey/Services/AiRerank/RerankCache.cs` 작성:
   - key = `(context, candidates)` 해시. value = `RerankResult`.
   - LRU 또는 단순 bounded dictionary(예: 최근 64개). thread-safe(5번이 async로 호출하므로).
   - `TryGet(key)`, `Set(key, result)`.
2. 5번 통합에서 `RerankAsync` 호출 전 `TryGet` → hit이면 추론 생략. miss면 추린 후 `Set`.
3. (선택) 단어 확정 시점(`AcceptSuggestion`/`OnSeparator`)에 **다음 문맥 후보를 미리 계산**하는 prefetch hook을 둔다. 1차에는 단순 LRU만으로도 충분하므로 prefetch는 **향후**로 표기해도 된다.

## 규칙 / 하지 말 것
- **캐시 키 = (context, candidates, topN)**: topN이 다르면 결과가 달라지므로 key에 필수 포함(설정 변경 시 stale 방지).
- 무제한 성장하지 않도록 bound(LRU)를 둔다.

## 검증
- `AltKey.Tests`:
   - 같은 key 두 번 → 두 번째는 LayaReranker **호출 안 됨**(spy).
   - topN 다르면 다른 key(별도 miss).
   - bound 초과 시 가장 오래된 항목 제거(LRU).

## 산출물
- `RerankCache.cs`, 5번 통합에 캐시 끼워 넣기, 테스트.
