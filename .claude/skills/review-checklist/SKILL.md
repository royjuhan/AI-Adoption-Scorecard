---
name: review-checklist
description: reviewer가 PR을 볼 때
---
# review-checklist

- [ ] src/에 HttpClient, Socket, WebClient, Process.Start, 외부 API 키가 없는가
- [ ] 각 에이전트가 자기 폴더 밖을 고치지 않았는가
- [ ] tests/가 삭제·약화되지 않았는가 (되었다면 PR 본문에 이유가 있는가)
- [ ] 판정·분류 로직이 Core에만 있는가
- [ ] 입력 파일을 쓰는 코드가 없는가
- [ ] 근거 문장에 기본값 표시가 붙는가

결과는 "지적 목록" 또는 "지적 없음"으로 적는다. 지적은 파일·줄·이유·고치는 방법 한 줄.
