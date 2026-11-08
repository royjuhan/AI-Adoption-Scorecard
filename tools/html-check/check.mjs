// 스코어카드 HTML 가로 스크롤 검사(docs/spec/report.md 6절, ADR 0010 B-12).
// 쓰는 법: node tools/html-check/check.mjs [--shots <폴더>] <scorecard.html> ...
// 파일마다 390·900·1236px 너비로 열어 document.documentElement.scrollWidth <= innerWidth 인지 본다.
// 외부 요청이 하나라도 나가면 실패다(HTML은 오프라인에서 열려야 한다).
// --shots를 주면 첫 파일의 너비별 화면(deviceScaleFactor 1, 전체 페이지)을 그 폴더에 PNG로 저장한다.
// Playwright와 Chromium이 설치된 곳에서 돈다. CI 잡이 아니다(tools/ci 밖).
import { createRequire } from 'node:module';
import { pathToFileURL } from 'node:url';
import path from 'node:path';
import fs from 'node:fs';

const require = createRequire(import.meta.url);
let playwright;
try {
  playwright = require('playwright');
} catch {
  // 전역 설치(예: /opt/node-tools)도 찾는다.
  playwright = createRequire(path.join(process.env.NODE_PATH || '/opt/node-tools/node_modules', 'x.js'))('playwright');
}

const widths = [390, 900, 1236];
const args = process.argv.slice(2);
let shots = null;
const files = [];
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--shots') {
    shots = args[++i];
  } else {
    files.push(args[i]);
  }
}

if (files.length === 0) {
  console.error('HTML 파일을 하나 이상 준다.');
  process.exit(2);
}

const browser = await playwright.chromium.launch();
let failed = 0;
try {
  for (const [index, file] of files.entries()) {
    for (const width of widths) {
      const context = await browser.newContext({ viewport: { width, height: 900 }, deviceScaleFactor: 1 });
      const page = await context.newPage();
      const external = [];
      page.on('request', (request) => {
        if (!request.url().startsWith('file:')) {
          external.push(request.url());
        }
      });
      await page.goto(pathToFileURL(path.resolve(file)).href, { waitUntil: 'load' });
      const { scrollWidth, innerWidth } = await page.evaluate(() => ({
        scrollWidth: document.documentElement.scrollWidth,
        innerWidth: window.innerWidth,
      }));
      const ok = scrollWidth <= innerWidth && external.length === 0;
      if (!ok) {
        failed++;
      }

      console.log(`${ok ? 'OK  ' : 'FAIL'} ${width}px scrollWidth=${scrollWidth} innerWidth=${innerWidth} external=${external.length} ${file}`);
      if (shots && index === 0) {
        fs.mkdirSync(shots, { recursive: true });
        await page.screenshot({ path: path.join(shots, `scorecard-${width}.png`), fullPage: true });
      }

      await context.close();
    }
  }
} finally {
  await browser.close();
}

console.log(failed === 0 ? '모두 통과' : `실패 ${failed}건`);
process.exit(failed === 0 ? 0 : 1);
