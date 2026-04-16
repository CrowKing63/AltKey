# Skill: 지능형 앱 릴리즈 (Intelligent App Release)

이 스킬은 외부 스크립트 없이 에이전트가 직접 앱 버전을 관리하고 배포하는 과정을 안내합니다.

## 목표
변경 사항을 분석하여 적절한 버전 타입을 결정하고, 파일을 업데이트한 뒤 Git 릴리즈(커밋, 태그, 푸시)를 완료합니다.

## 실행 단계

### 1. 변경 사항 분석 및 버전 타입 결정
1. `git describe --tags --abbrev=0`으로 마지막 릴리즈 태그를 확인합니다.
2. `git log $latestTag..HEAD --oneline`으로 이전 태그 이후의 커밋 내역을 확인합니다.
3. `git status --short`로 아직 커밋되지 않은 변경 사항을 확인합니다.
4. 수집된 정보를 바탕으로 업그레이드 타입을 결정합니다:
   - **Major**: 하위 호환성이 깨지는 큰 변화(`BREAKING CHANGE`)가 있는 경우.
   - **Minor**: 새로운 기능(`feat:`, `feature:`)이 추가된 경우.
   - **Patch**: 버그 수정(`fix:`), 문서 수정(`docs:`), 리팩토링(`refactor:`) 등 기타 사소한 변경인 경우. (기본값)

### 2. 현재 버전 확인
1. `AltKey/AltKey.csproj` 파일을 읽어 `<Version>`, `<AssemblyVersion>`, `<FileVersion>` 태그 값을 확인합니다.

### 3. 버전 계산 및 파일 업데이트
1. 결정된 타입(Major.Minor.Patch)에 따라 새로운 버전 번호를 계산합니다.
2. `replace_file_content` 또는 `multi_replace_file_content`를 사용하여 `.csproj` 파일 내의 모든 버전 관련 태그를 새 버전으로 업데이트합니다.

### 4. 릴리즈 커밋 생성
1. 다음과 같은 구조의 커밋 메시지를 작성합니다:
   - 제목: `release: vX.X.X`
   - 본문: 분석된 변경 사항 요약 (커밋 로그 및 수정 파일 목록)
2. `git add .`를 실행합니다.
3. `git commit -m "[제목]" -m "[본문]"` 명령으로 커밋을 생성합니다.

### 5. 태그 생성 및 푸시
1. `git tag vX.X.X` 명령으로 새 버전에 대한 태그를 생성합니다.
2. `git push origin main` 및 `git push origin --tags` 명령을 실행하여 원격 저장소에 반영합니다.

---
*참고: 이 스킬은 `scripts/release.ps1` 스크립트를 대체하며, 에이전트의 직접적인 도구 조작을 통해 수행됩니다.*
