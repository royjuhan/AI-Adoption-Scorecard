# 내보내기 확인: form-fill 2단계

- 이 번들은 보안 경계 밖의 제작 환경으로 간다. 실제 자료(품번·이메일·경로·실제 값)가 없어야 한다(지시문 2절 8).
- 사람이 직접 번들 파일을 읽고 맨 아래 확인 줄 끝에 이름이나 역할을 적는다. 스킬·AI는 확인 줄을 적지 않는다.
- 이 파일의 다른 줄을 바꾸거나 번들이 바뀌면(답·단계·결과가 바뀌면) 확인이 무효다. build가 새 초안을 쓴다.

## 번들 파일

| 파일 | SHA-256 |
| --- | --- |
| `spec.md` | `85cceca004511ab945bb20ddff237ec3701234aae1d0c0186a10221af6cdaa83` |
| `io-schema.json` | `ca7f3bd74c238e1ce6e893d4d1c536c21ba6b1d826134a6ac56cc28bcbdd40a3` |
| `acceptance.md` | `9a7fb856eeb5595d0e9a2f44ab3391218e9420262d261d6ce1380760c0aefe1c` |
| `samples/README.md` | `8334b8b1c1cb6a5f89a95f6a3bada952b69b4effdaf16a69ac8c02ca008213a0` |
| `samples/output.csv` | `74a43d310d69e32f7a3640d2d5f88aee7fdc58147c72c4f73c8255a597c4dc0d` |
| `context.json` | `230139beb53a0ef14d5a7e1cfa14899e1a71107068bfe14da5db35fc62f6c653` |

## 문자열 검사

- 검사한 패턴: `email`, `url`, `windows-path`, `unc-path`, `posix-path`, `home-path`, `part-letters-digits`, `part-digit-groups`, `part-long-code`.
- 걸린 것: 0건.

## 사람 확인

위 파일을 모두 읽었고 실제 자료가 없음을 확인했다.

확인한 사람: 검토자
