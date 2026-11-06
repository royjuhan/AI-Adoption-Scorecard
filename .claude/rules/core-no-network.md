---
paths:
  - "src/Scorecard.Core/**"
---
# Core 네트워크 금지

- `HttpClient`, `Socket`, `WebClient`, `Process.Start`, LLM API, 외부 API 키를 쓰지 않는다.
- 난수·현재시각을 판정·분류에 쓰지 않는다.
- 입력 파일은 읽기만 한다. 출력은 새 폴더에 쓴다.
