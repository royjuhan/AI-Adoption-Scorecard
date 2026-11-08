# 내보내기 확인: reply-drafts 1단계

- 이 번들은 보안 경계 밖의 제작 환경으로 간다. 실제 자료(품번·이메일·경로·실제 값)가 없어야 한다(지시문 2절 8).
- 사람이 직접 번들 파일을 읽고 맨 아래 확인 줄 끝에 이름이나 역할을 적는다. 스킬·AI는 확인 줄을 적지 않는다.
- 이 파일의 다른 줄을 바꾸거나 번들이 바뀌면(답·단계·결과가 바뀌면) 확인이 무효다. build가 새 초안을 쓴다.

## 번들 파일

| 파일 | SHA-256 |
| --- | --- |
| `spec.md` | `f1f73a4c455d2972bafc66f2ad5affc8abe8c1f71702e32b84530300f519897e` |
| `io-schema.json` | `b6ace7c0691d94e417c7e2c8e36a3298fc258338a09045203bb1d51c49a9c23c` |
| `acceptance.md` | `4ca6f6dc68740cfac1889a112a190b5be12e7f19cb00ed3839a8cf17e5e51bbd` |
| `samples/README.md` | `8334b8b1c1cb6a5f89a95f6a3bada952b69b4effdaf16a69ac8c02ca008213a0` |
| `samples/output.csv` | `e3b1746619dab3daa8b1db8d866585981a1886aa6fb02179fb9c6a6032f980b3` |
| `context.json` | `13c183c7ee542e521059ce756ffb100cd3631477c8da58883d92394610f40c2c` |

## 문자열 검사

- 검사한 패턴: `email`, `url`, `windows-path`, `unc-path`, `posix-path`, `home-path`, `part-letters-digits`, `part-digit-groups`, `part-long-code`.
- 걸린 것: 0건.

## 사람 확인

위 파일을 모두 읽었고 실제 자료가 없음을 확인했다.

확인한 사람: 검토자
