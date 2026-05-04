# 작업 계획: AltKey.Tools 1단계

## 1. AltKey.Tools 프로젝트 생성

- [ ] 1.1 `AltKey.Tools` WPF 프로젝트 생성
  - `net8.0-windows`
  - `UseWPF=true`
  - 편집 도구 전용 실행 파일로 구성

- [ ] 1.2 도구 호스트 창 추가
  - 도구 선택 화면 또는 단순 네비게이션 추가
  - 첫 포커스 위치와 탭 순서 기준 정의

## 2. 공용 데이터 접근 경계 정리

- [ ] 2.1 레이아웃 편집기 의존성 정리
  - `LayoutEditorViewModel`
  - `LayoutService`
  - `ConfigService`
  - 메인 앱 전용 상태 의존성 제거 대상 식별

- [ ] 2.2 사용자 단어 편집기 의존성 정리
  - `UserDictionaryEditorViewModel`
  - `KoreanDictionary`
  - `EnglishDictionary`
  - `WordFrequencyStore`
  - `BigramFrequencyStore`

- [ ] 2.3 저장소 래퍼 또는 얇은 facade 도입 검토
  - `ILayoutRepository`
  - `IUserDictionaryRepository`

## 3. 레이아웃 편집기 분리

- [ ] 3.1 기존 뷰/뷰모델 이관
- [ ] 3.2 저장 경로와 파일 읽기/쓰기 동작 검증
- [ ] 3.3 저장 후 메인 앱 `ReloadLayouts` 연동
- [ ] 3.4 접근성 점검

## 4. 사용자 단어 편집기 분리

- [ ] 4.1 기존 뷰/뷰모델 이관
- [ ] 4.2 한국어/영어 탭, 검색, 추가, 삭제 기능 유지
- [ ] 4.3 bigram 탭 기능 유지
- [ ] 4.4 저장 후 메인 앱 `ReloadUserDictionary`, `ReloadBigramData` 연동
- [ ] 4.5 접근성 점검

## 5. 프로필 매핑 편집 검토

- [ ] 5.1 현재 구조 분석
- [ ] 5.2 별도 도구 적합성 판단
- [ ] 5.3 2단계 후보 여부 결정

## 6. 메인 앱 진입점 수정

- [ ] 6.1 설정창에서 편집기 직접 열기 대신 `AltKey.Tools` 실행으로 전환
- [ ] 6.2 특정 도구 직접 열기 지원 여부 결정
- [ ] 6.3 도구 앱 미존재 시 사용자 안내 방식 정의

## 7. 최소 IPC 또는 재로드 알림

- [ ] 7.1 `ShowTool(toolName)` 지원 여부 결정
- [ ] 7.2 `ReloadLayouts` 구현
- [ ] 7.3 `ReloadUserDictionary` 구현
- [ ] 7.4 `ReloadBigramData` 구현

## 8. 검증

- [ ] 8.1 레이아웃 편집 저장 후 메인 앱 반영 확인
- [ ] 8.2 사용자 단어 저장 후 자동완성 반영 확인
- [ ] 8.3 도구 앱 접근성 수동 검증
- [ ] 8.4 메인 앱 입력 기능 회귀 여부 확인
