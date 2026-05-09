using Xunit;

// ConfigService와 PathResolver는 테스트 중에도 전역 설정 경로를 공유합니다.
// 병렬 실행을 허용하면 한 테스트가 OverrideDataDir로 바꾼 경로를 다른 테스트가 함께 사용해
// 설정 파일 충돌이나 잘못된 데이터 디렉터리 참조가 발생할 수 있으므로 테스트 어셈블리 전체를 순차 실행합니다.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
