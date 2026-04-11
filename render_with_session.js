const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage();
  
  const session = 'agent%3Ajdai-default%3Adiscord%3Achannel%3A1466622912307007690';
  
  // ===== USAGE PAGE =====
  console.log('=== USAGE PAGE (SESSION AUTH) ===');
  await page.goto(`http://127.0.0.1:18789/control/usage?session=${session}`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(2000);
  
  const usageDetails = await page.evaluate(() => {
    const sections = {};
    
    // Page title and subtitle
    sections.pageTitle = document.querySelector('h1')?.textContent?.trim() || 'Usage';
    sections.pageDescription = Array.from(document.querySelectorAll('*'))
      .find(el => el.textContent?.includes('tokens go'))?.textContent?.trim();
    
    // Buttons
    const allButtons = Array.from(document.querySelectorAll('button'))
      .map(b => b.textContent?.trim())
      .filter(t => t && t.length > 0);
    
    sections.filterArea = {
      timeRangeButtons: allButtons.filter(t => ['Today', '7d', '30d'].includes(t)),
      metricToggleButtons: allButtons.filter(t => ['Tokens', 'Cost'].includes(t)),
      mainButtons: allButtons.filter(t => ['Refresh', 'Pin', 'Export'].includes(t) || t.includes('Export')),
      dateInputs: Array.from(document.querySelectorAll('input[type="date"]')).length,
      selects: Array.from(document.querySelectorAll('select')).length
    };
    
    // Data sections visible
    const bodyText = document.body.innerText;
    sections.visibleSections = [
      'Usage Overview',
      'Top Models',
      'Top Providers',
      'Top Tools',
      'Top Agents',
      'Top Channels',
      'Peak Error',
      'Activity by Time',
      'Daily Usage',
      'Tokens By Type',
      'Sessions'
    ].filter(title => bodyText.includes(title));
    
    // Sessions table structure
    const table = document.querySelector('table');
    sections.hasTable = !!table;
    if (table) {
      sections.tableHeaders = Array.from(table.querySelectorAll('th')).map(th => th.textContent?.trim());
      sections.tableRows = table.querySelectorAll('tbody tr').length;
    }
    
    sections.bodyPreview = document.body.innerText.substring(0, 2500);
    
    return sections;
  });
  
  console.log('=== USAGE PAGE DETAILS ===');
  console.log(JSON.stringify(usageDetails, null, 2));
  
  await page.screenshot({ path: '/c/git/JD.AI/usage-session-auth.png', fullPage: true });
  console.log('Screenshot: /c/git/JD.AI/usage-session-auth.png');
  
  // ===== CRON JOBS PAGE =====
  console.log('\n=== CRON JOBS PAGE (SESSION AUTH) ===');
  await page.goto(`http://127.0.0.1:18789/control/cron-jobs?session=${session}`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(2000);
  
  const cronDetails = await page.evaluate(() => {
    const sections = {};
    
    // Page info
    sections.pageTitle = document.querySelector('h1')?.textContent?.trim() || 'Cron Jobs';
    sections.pageDescription = Array.from(document.querySelectorAll('*'))
      .find(el => el.textContent?.includes('recurring'))?.textContent?.trim();
    
    // Top stats
    const bodyText = document.body.innerText;
    sections.topStats = {
      enabled: bodyText.includes('ENABLED'),
      totalJobs: parseInt(bodyText.match(/JOBS[\n\s]*(\d+)/)?.[1] || '0'),
      nextWake: bodyText.includes('NEXT WAKE')
    };
    
    // Buttons
    const allButtons = Array.from(document.querySelectorAll('button'))
      .map(b => b.textContent?.trim())
      .filter(t => t && t.length > 0);
    
    sections.mainButtons = allButtons.filter(t => ['Refresh', 'Reset'].includes(t));
    sections.jobActions = allButtons.filter(t => ['Edit', 'Clone', 'Enable', 'Run', 'Run if due', 'History', 'Remove'].includes(t));
    
    // Filter controls
    sections.filterControls = {
      hasSearch: !!document.querySelector('input[placeholder*="Search"]'),
      selects: Array.from(document.querySelectorAll('select')).length,
      filterOptions: {
        enabled: ['All', 'Enabled', 'Disabled'].filter(opt => bodyText.includes(opt)),
        schedule: ['All', 'At', 'Every', 'Cron'].filter(opt => bodyText.includes(opt)),
        lastRun: ['All', 'OK', 'Error', 'Skipped'].filter(opt => bodyText.includes(opt))
      }
    };
    
    sections.bodyPreview = document.body.innerText.substring(0, 2500);
    
    return sections;
  });
  
  console.log('=== CRON JOBS PAGE DETAILS ===');
  console.log(JSON.stringify(cronDetails, null, 2));
  
  await page.screenshot({ path: '/c/git/JD.AI/cron-session-auth.png', fullPage: true });
  console.log('Screenshot: /c/git/JD.AI/cron-session-auth.png');
  
  await browser.close();
})();
