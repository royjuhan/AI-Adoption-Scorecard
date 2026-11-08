# 내보내기 확인: table-compare 1단계

- 이 번들은 보안 경계 밖의 제작 환경으로 간다. 실제 자료(품번·이메일·경로·실제 값)가 없어야 한다(지시문 2절 8).
- 사람이 직접 번들 파일을 읽고 맨 아래 확인 줄 끝에 이름이나 역할을 적는다. 스킬·AI는 확인 줄을 적지 않는다.
- 이 파일의 다른 줄을 바꾸거나 번들이 바뀌면(답·단계·결과가 바뀌면) 확인이 무효다. build가 새 초안을 쓴다.

## 번들 파일

| 파일 | SHA-256 |
| --- | --- |
| `spec.md` | `74a033608846e587776b06b8222fe9d9fe5a79b1a77f8e9d6f8f8e122e386b0c` |
| `io-schema.json` | `6188e65fba2ecd7a8f4f461ece6364908327decb24f3628bba3673b05c13e1c9` |
| `acceptance.md` | `ad47b3cecc219aa737058b96855481e5c15ff1346f3b6a1acab8ede57c74a0ac` |
| `samples/README.md` | `8334b8b1c1cb6a5f89a95f6a3bada952b69b4effdaf16a69ac8c02ca008213a0` |
| `samples/output.csv` | `8ab16d2003458aa39a6cf82638797616fc13c85e4f840dd869e8b5534d078ad7` |
| `context.json` | `705736d0b4d227694b9136db84e1dab246384b41ed98fff4dbb62116ca79662a` |

## 문자열 검사

- 검사한 패턴: `email`, `url`, `windows-path`, `unc-path`, `posix-path`, `home-path`, `part-letters-digits`, `part-digit-groups`, `part-long-code`.
- 걸린 것: 0건.

## 사람 확인

위 파일을 모두 읽었고 실제 자료가 없음을 확인했다.

확인한 사람: 검토자
