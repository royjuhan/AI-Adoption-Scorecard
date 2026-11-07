# 스펙 7.2~7.4의 @@BLOCKS 예제@@ 자리를 생성 파일 바이트로 채운다. 이미 채워졌으면 그 절의 내용을 다시 쓴다.
import hashlib, re, sys
spec, root = sys.argv[1], sys.argv[2]
files = {
  'table-compare': ['case.yaml', 'intake.md', 'reference/logs.csv', 'candidate/logs.csv'],
  'form-fill': ['case.yaml', 'intake.md', 'reference/orders.csv', 'candidate/orders.csv'],
  'mixed-exceptions': ['case.yaml', 'intake.md'],
}
lang = lambda f: 'yaml' if f.endswith('.yaml') else ('csv' if f.endswith('.csv') else 'text')
text = open(spec, encoding='utf-8').read()
for ex, fs in files.items():
    parts = []
    for f in fs:
        b = open(f'{root}/{ex}/{f}', 'rb').read()
        assert b.endswith(b'\n') and b'\r' not in b and '```' not in b.decode('utf-8')
        parts.append(f'`examples/{ex}/{f}` (SHA-256 `{hashlib.sha256(b).hexdigest()}`):\n\n```{lang(f)}\n{b.decode("utf-8")}```')
    body = '\n\n'.join(parts)
    marker = f'@@BLOCKS {ex}@@'
    if marker in text:
        text = text.replace(marker, body)
    else:
        # 다시 채우기: 제목 다음부터 다음 제목(## 또는 ###) 전까지 바꾼다.
        pat = re.compile(r'(### 7\.\d ' + re.escape(ex) + r'\n\n)(.*?)(\n\n#)', re.S)
        text, n = pat.subn(lambda m: m.group(1) + body + m.group(3), text)
        assert n == 1, ex
open(spec, 'w', encoding='utf-8', newline='\n').write(text)
print('ok')
