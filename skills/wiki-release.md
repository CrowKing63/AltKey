# Skill: 위키 게시 (Wiki Publish)

이 스킬은 `C:\Users\UITAEK\AltKey\Wiki\AltKey.wiki` 경로의 위키 Git 저장소에 변경 사항을 커밋하고 원격에 푸시합니다.

## 목표
위키 문서의 변경 사항을 분석하여 커밋 메시지를 작성하고, 원격 저장소(origin/master)에 반영합니다.

## 실행 단계

### 1. 변경 사항 확인
1. `git -C "C:\Users\UITAEK\AltKey\Wiki\AltKey.wiki" status --short`로 수정·추가된 파일 목록을 확인합니다.
2. 변경 사항이 없으면 "게시할 변경 사항이 없습니다."라고 안내하고 종료합니다.

### 2. 커밋 메시지 작성
변경된 파일 목록을 바탕으로 다음 형식의 커밋 메시지를 한국어로 작성합니다.
- 제목: `docs: [변경 내용 한 줄 요약]`
- 본문(선택): 수정된 문서 목록 또는 주요 변경 내용

### 3. 스테이징 및 커밋
1. `git -C "C:\Users\UITAEK\AltKey\Wiki\AltKey.wiki" add .`
2. `git -C "C:\Users\UITAEK\AltKey\Wiki\AltKey.wiki" commit -m "[제목]" -m "[본문]"`

### 4. 원격 푸시
1. `git -C "C:\Users\UITAEK\AltKey\Wiki\AltKey.wiki" push origin master`
2. 푸시 성공 여부를 확인하고 결과를 보고합니다.

## 주의 사항
- 위키 저장소는 메인 프로젝트(`AltKey/`)와 별개의 Git 저장소입니다.
- 모든 git 명령은 `-C` 옵션으로 위키 경로를 명시적으로 지정합니다.
- PowerShell 환경이므로 `&&` 대신 `;` 또는 순차 실행을 사용합니다.
