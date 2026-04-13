const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage();
  const token = 'a878da53d4ec7e4f045d8a10a4fb9474c7e4bfa6247bcd5c';
  
  // Test 1: Token in URL fragment
  console.log('TEST 1: Token in URL fragment (#token=...)');
  await page.goto(`http://127.0.0.1:18789/control/usage#token=${token}`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(1000);
  let text = await page.textContent('body');
  console.log('Result:', text.includes('Usage Overview') ? 'WORKS' : 'FAILS');
  
  // Test 2: Session parameter only
  console.log('\nTEST 2: Session in query string');
  const session = 'agent%3Ajdai-default%3Adiscord%3Achannel%3A1466622912307007690';
  await page.goto(`http://127.0.0.1:18789/control/usage?session=${session}`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(1000);
  text = await page.textContent('body');
  console.log('Result:', text.includes('Usage Overview') ? 'WORKS' : 'FAILS');
  
  // Test 3: Both token and session
  console.log('\nTEST 3: Token in fragment + session in query');
  await page.goto(`http://127.0.0.1:18789/control/usage?session=${session}#token=${token}`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(1000);
  text = await page.textContent('body');
  console.log('Result:', text.includes('Usage Overview') ? 'WORKS' : 'FAILS');
  
  await browser.close();
})();
