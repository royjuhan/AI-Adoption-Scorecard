# T-023 릴리스 내려받기

- 요청: juhan "오픈소스에서 받는것처럼 하고싶은데"(2026-11-10). 결정은 ADR 0017, 방식은 추천안 채택(ADR 0005 결정 3).
- 바탕: main `6f16269`.
- 폴더 범위: `.github/workflows/release.yml`, `tools/release/`, `docs/`, `README*.md`, `CHANGELOG.md`.

## 수용 기준

1. 릴리스를 발행하면 태그 커밋에서 빌드·테스트 뒤 `scorecard-<태그>-win-x64.zip`과 `.sha256`이 그 릴리스에 붙는다.
2. zip에 `scorecard.exe`, `examples/`(`out/` 없음), `1-예제-돌려보기.bat`, `사용법.txt`가 있다. bat·txt는 CRLF 그대로.
3. PR·main push에서는 이 워크플로가 돌지 않는다.
4. `docs/samples/scorecard-table-compare.html`이 지금 골든과 같다.
5. README 설치 절이 Releases를 가리킨다.

## 머지 뒤 (juhan)

- 웹 화면에서 릴리스 v0.2.0-alpha를 발행한다(태그는 그때 생긴다). 워크플로가 zip을 붙인다.
