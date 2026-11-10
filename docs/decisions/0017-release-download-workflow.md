# 0017. 릴리스 내려받기: GitHub 릴리스에 zip을 붙이는 워크플로

- 날짜: 2026-11-10
- 상태: juhan 요청("오픈소스에서 받는것처럼 하고싶은데", 2026-11-10). 방식은 추천안 채택(리드, ADR 0005 결정 3).
- 관련: ADR 0015(v0.1.0-alpha), ADR 0016(첫 실사용 반영), 카드 `docs/tasks/T-023-release-download.md`.
- 지시문 2절 8·10(실제 자료·회사 자료 금지)은 바꾸지 않는다. 바꾸자는 제안도 하지 않는다.

## 배경

- v0.1.0-alpha는 태그를 올리지 못했다. 세션의 git 프록시가 태그 push를 끊는다. zip은 프로젝트 파일로만 건넸다.
- 그 뒤 T-022(ADR 0016)로 스킬과 스코어카드 화면이 바뀌었다. 옛 zip은 옛 화면을 낸다.
- juhan은 다른 오픈소스처럼 저장소의 Releases 화면에서 받고 싶다.

## 결정

1. **워크플로 `.github/workflows/release.yml`.** 릴리스를 발행하면(`release: published`) 태그 커밋에서 빌드·테스트를 돌리고, 통과하면 win-x64 zip과 SHA-256 파일을 그 릴리스에 붙인다. 이미 있는 릴리스에 다시 붙일 때는 `workflow_dispatch`(태그 입력)를 쓴다.
2. **릴리스 발행은 juhan이 웹 화면에서 한다.** 태그도 그때 GitHub가 만든다. 세션은 태그를 push하지 않는다.
3. **zip 내용.** `scorecard.exe`(self-contained 단일 파일, ADR 0015 명령), `examples/`(공개 합성 예제, `out/` 뺌), `1-예제-돌려보기.bat`, `사용법.txt`. 뒤 둘은 `tools/release/`에 두고 줄바꿈을 지킨다(`* -text`).
4. **다음 판은 v0.2.0-alpha.** v0.1.0-alpha 태그는 만들지 않는다. CHANGELOG의 v0.1.0-alpha 절은 기록으로 둔다. 0.2.0-alpha는 main(T-022, 이 카드 포함)이다.
5. **샘플 HTML 갱신.** `docs/samples/scorecard-table-compare.html`을 지금 골든(`tests/Scorecard.Tests/TestData/report/table-compare/scorecard.html`) 사본으로 바꾼다.

## 남은 위험

- 워크플로는 `contents: write` 권한을 쓴다. 릴리스 발행 때만 돈다. PR에서는 돌지 않는다.
- exe에 서명이 없다. Windows가 "PC 보호" 창을 띄운다(사용법.txt에 적음).
- 워크플로 자체는 첫 발행 때 처음 돈다. 실패하면 로그를 보고 고친 뒤 `workflow_dispatch`로 다시 붙인다.
