# 02 — 모델 저장/다운로드 인프라 (manifest, SHA-256)

## 목표
AltKey가 `laya-ko` ONNX 모델을 자체 디렉토리에 관리(다운로드·검증·로드 전 존재 확인)하게 한다. 별도 AI 프로그램 설치 없이 동작하도록 한다.

## 배경 (레포 기준)
- 데이터 저장 루트는 `PathResolver.DataDir`(`AppData/Roaming/AltKey`, 포터블 모드면 exe 옆). 이 아래 `models/<model>/` 하위 구조를 만든다.
- `DownloadService.DownloadAsync(url, destinationPath, progress, ct)`가 이미 존재(진행률 0.0~1.0 보고). 재사용한다.
- 모델 파일은 레포에 **없다**. 이 단계의 핵심 전제는 **`laya-ko` ONNX + 한국어 토크나이저를 실제로 어디에서 가져올지 확정하는 것**.

## ✅ 시작 전 확인 (확인됨)
1. **ONNX 가중치 공개**: `2nugu/laya-ko-decision-onnx`(Apache-2.0). `model.onnx` + `model.onnx.data`(외부 데이터 파일)가 릴리스에 있다. 다운로드 URL을 이 레포의 릴리스/artifact에서 확정해 아래 manifest `sourceUrl`에 기록한다.
2. **CPU 추론**: ONNX Runtime CPU ~30ms/forward pass(레포 벤치마크). 파일 크기는 실제 artifact 확인 후 manifest에 기록.
3. **남은 항목**: 토크나이저는 ONNX에 내장되지 않아 C#에서 처리해야 함(03번). 모델 수입/변환 단위는 불필요하다.

> 다운로드 URL과 각 파일 SHA-256(known-good)을 확정해 manifest에 기록한다. 추측한 값은 적지 않는다.

## 작업
1. `AltKey/Services/AiRerank/ModelStore.cs`(새 파일, 새 서브네임스페이스) 작성:
   - `ModelsDir` = `Path.Combine(PathResolver.DataDir, "models")`.
   - `EnsureModelAsync(modelName, ct)` → 이미 존재하면 no-op, 없으면 `DownloadService`로 다운로드 후 검증.
   - 파일 구성: `model.onnx`, `model.onnx.data`(ONNX 외부 데이터), `tokenizer.json`(또는 토크나이저 데이터), `manifest.json`.
2. `manifest.json` 스키마 정의 + 읽기/쓰기:
   - `name`, `version`, `files[]`(각 파일의 `size`, `sha256`), `sourceUrl`.
   - **known-good SHA-256**(신뢰 소스: `2nugu/laya-ko-decision-onnx` 릴리스의 published hash)을 manifest에 미리 기록하고, 다운로드 파일을 그 해시와 대조해 검증한다. (다운로드 "후"에 자기 해시를 기록하는 방식은 변조를 막지 못하므로 금지.) 로드 전에도 실제 파일 hash를 재대조.
3. 손상/부분 다운로드 방어: 임시 파일(`*.part`)로 받아 검증 성공 후 rename. 검증 실패면 파일을 지우고 다시 내려받는다.

## 하지 말 것
- 모델 바이너리를 레포에 커밋하지 않는다(크기, 라이선스).
- `DownloadService`를 수정하지 않고 그대로 쓴다.

## 검증
- `AltKey.Tests`: manifest 읽기/쓰기, SHA-256 대조 성공/실패, 부분 파일(`.part`) 존재 시 재다운로드 경로. 실제 네트워크는 fake HTTP handler로 대체.

## 산출물
- `ModelStore.cs`, `manifest.json` 스키마, 테스트. **블록커 확인 결과(모델 출처 URL)를 이 파일 상단에 기록.**
