# 내보내기 확인: mixed-exceptions 1단계

- 이 번들은 보안 경계 밖의 제작 환경으로 간다. 실제 자료(품번·이메일·경로·실제 값)가 없어야 한다(지시문 2절 8).
- 사람이 직접 번들 파일을 읽고 맨 아래 확인 줄 끝에 이름이나 역할을 적는다. 스킬·AI는 확인 줄을 적지 않는다.
- 이 파일의 다른 줄을 바꾸거나 번들이 바뀌면(답·단계·결과가 바뀌면) 확인이 무효다. build가 새 초안을 쓴다.

## 번들 파일

| 파일 | SHA-256 |
| --- | --- |
| `spec.md` | `b294dec51b13e5f6f0d3e95d3a3731ff162f6a6a89f8c8696a5be5032157873d` |
| `io-schema.json` | `410d5148ebc9496b60f32d877848d269eda667f55ac403a0d4d7a9344f3aadc2` |
| `acceptance.md` | `5698a03720d52fdbec4908986e3635c1c876702a9748823dbe918aeba4fe358c` |
| `samples/README.md` | `2d5f4645e814d70b5a713ef49cb167e39d175448067b642b3c46d77b29d81aa4` |
| `context.json` | `e5900cfeae09c21e02de77188e185011f12c30d4d09633eca0f05ddb6d48e7b2` |

## 문자열 검사

- 검사한 패턴: `email`, `url`, `windows-path`, `unc-path`, `posix-path`, `home-path`, `part-letters-digits`, `part-digit-groups`, `part-long-code`.
- 걸린 것: 0건.

## 사람 확인

위 파일을 모두 읽었고 실제 자료가 없음을 확인했다.

확인한 사람: 검토자
