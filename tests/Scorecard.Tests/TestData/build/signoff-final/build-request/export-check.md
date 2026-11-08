# 내보내기 확인: signoff-final 3단계

- 이 번들은 보안 경계 밖의 제작 환경으로 간다. 실제 자료(품번·이메일·경로·실제 값)가 없어야 한다(지시문 2절 8).
- 사람이 직접 번들 파일을 읽고 맨 아래 확인 줄 끝에 이름이나 역할을 적는다. 스킬·AI는 확인 줄을 적지 않는다.
- 이 파일의 다른 줄을 바꾸거나 번들이 바뀌면(답·단계·결과가 바뀌면) 확인이 무효다. build가 새 초안을 쓴다.

## 번들 파일

| 파일 | SHA-256 |
| --- | --- |
| `spec.md` | `3f972d42b629c00176efc3e523c6bdb95c4cdefa8892ca29d60e1c00a77082f5` |
| `io-schema.json` | `587b2198fcac390e3246b286e9c1f29a328b00f992abb1b661a9a977ae78697b` |
| `acceptance.md` | `50b5ff81c4f63f90f0736484ad96bccc214edc035c4be829bcd21cdad1943f2c` |
| `samples/README.md` | `8334b8b1c1cb6a5f89a95f6a3bada952b69b4effdaf16a69ac8c02ca008213a0` |
| `samples/output.csv` | `25a7f729c90d4352d512bf101d953fb18aaf501543808bb2894dce5f2cbdf7a5` |
| `context.json` | `8fb772ce154cb915e080d8eae0cfb5b0f9158b5d9efbfd2b869c724c9905f540` |

## 문자열 검사

- 검사한 패턴: `email`, `url`, `windows-path`, `unc-path`, `posix-path`, `home-path`, `part-letters-digits`, `part-digit-groups`, `part-long-code`.
- 걸린 것: 0건.

## 사람 확인

위 파일을 모두 읽었고 실제 자료가 없음을 확인했다.

확인한 사람: 검토자
