import { expect, test, type Frame, type Page } from '@playwright/test';

function trackNotFoundResponses(page: Page) {
  const notFound = new Set<string>();
  page.on('response', (res) => {
    if (res.status() === 404) notFound.add(res.url());
  });
  return notFound;
}

async function getDesktopDemoFrame(page: Page): Promise<Frame> {
  const iframe = page.locator('iframe[title="ChronosX Desktop Demo"]');
  await iframe.waitFor();
  for (let i = 0; i < 40; i += 1) {
    const handle = await iframe.elementHandle();
    const frame = await handle?.contentFrame();
    if (frame) return frame;
    await page.waitForTimeout(250);
  }
  throw new Error('Desktop demo iframe did not load');
}

async function getWebPortalFrame(page: Page): Promise<Frame> {
  const iframe = page.locator('iframe[title="ChronosX Web Portal Demo"]');
  await iframe.waitFor();
  for (let i = 0; i < 40; i += 1) {
    const handle = await iframe.elementHandle();
    const frame = await handle?.contentFrame();
    if (frame) return frame;
    await page.waitForTimeout(250);
  }
  throw new Error('Web portal iframe did not load');
}

async function expectNoRootScroll(frame: Frame) {
  const { scrollHeight, innerHeight } = await frame.evaluate(() => ({
    scrollHeight: document.documentElement.scrollHeight,
    innerHeight: window.innerHeight,
  }));
  expect(scrollHeight).toBeLessThanOrEqual(innerHeight + 2);
}

async function expectNoHorizontalOverflowInFrame(frame: Frame) {
  const { scrollWidth, innerWidth } = await frame.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    innerWidth: window.innerWidth,
  }));
  expect(scrollWidth).toBeLessThanOrEqual(innerWidth + 1);
}

async function scrollFrameToBottom(frame: Frame) {
  await frame.evaluate(() => {
    window.scrollTo(0, document.body.scrollHeight);
  });
  await frame.waitForTimeout(250);
}

test('Individuals: Activity + Reports demos load and fit (no 404)', async ({ page }) => {
  const notFound = trackNotFoundResponses(page);

  await page.setViewportSize({ width: 1280, height: 820 });
  await page.goto('/');
  const demo = page.locator('#demo');
  await demo.scrollIntoViewIfNeeded();

  const desktopFrame = await getDesktopDemoFrame(page);
  await expect(desktopFrame.getByText('My day', { exact: true })).toBeVisible();

  await demo.getByRole('button', { name: /^Activity/i }).click();
  await expect(desktopFrame.getByText('Activities', { exact: true })).toBeVisible();
  await expectNoRootScroll(desktopFrame);

  await demo.getByRole('button', { name: /^Reports/i }).click();
  await expect(desktopFrame.getByRole('heading', { name: 'Reports' })).toBeVisible();
  await expectNoRootScroll(desktopFrame);

  await demo.getByRole('button', { name: /^Kanban/i }).click();
  await expect(desktopFrame.getByText('To Do', { exact: true })).toBeVisible();
  await expect(desktopFrame.getByText('In Progress', { exact: true })).toBeVisible();

  expect(Array.from(notFound)).toEqual([]);
});

test('Teams: Portal + Teams + Reports demos load and fit (no 404)', async ({ page }) => {
  const notFound = trackNotFoundResponses(page);

  await page.setViewportSize({ width: 1280, height: 820 });
  await page.goto('/teams');
  const demo = page.locator('#demo');
  await demo.scrollIntoViewIfNeeded();

  const desktopFrame = await getDesktopDemoFrame(page);

  await demo.getByRole('button', { name: /^Team \(Desktop\)/i }).click();
  await expect(desktopFrame.getByRole('heading', { name: 'Team' })).toBeVisible();
  await expectNoRootScroll(desktopFrame);

  await demo.getByRole('button', { name: /^Reports/i }).click();
  await expect(desktopFrame.getByRole('heading', { name: 'Reports' })).toBeVisible();
  await expectNoRootScroll(desktopFrame);

  await demo.getByRole('button', { name: /^Kanban/i }).click();
  await expect(desktopFrame.getByText('To Do', { exact: true })).toBeVisible();

  await demo.getByRole('button', { name: /^Web portal/i }).click();
  const portalFrame = await getWebPortalFrame(page);
  await expect(portalFrame.getByText('ChronosX Demo Org', { exact: true })).toBeVisible();

  expect(Array.from(notFound)).toEqual([]);
});

test('Responsive: mobile layout still usable', async ({ page }) => {
  const notFound = trackNotFoundResponses(page);
  const consoleErrors: string[] = [];
  page.on('console', (msg) => {
    if (msg.type() === 'error') consoleErrors.push(msg.text());
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  const demo = page.locator('#demo');
  await demo.scrollIntoViewIfNeeded();

  // No page-level horizontal overflow on the landing page.
  const noOverflow = await page.evaluate(() => {
    return document.documentElement.scrollWidth <= window.innerWidth + 1;
  });
  expect(noOverflow).toBe(true);

  // Mobile demo becomes a button-only CTA (no embedded iframes).
  await expect(page.locator('iframe[title="ChronosX Desktop Demo"]')).toHaveCount(0);
  const tryDemo = demo.getByRole('link', { name: /try the demo|experimentar a demo/i });
  await expect(tryDemo).toBeVisible();
  await tryDemo.click();

  await expect(page).toHaveURL(/\/demo\?/);
  await expect(page.getByRole('button', { name: /exit demo|sair da demo/i })).toBeVisible();

  // Hamburger navigation switches real screens.
  const desktopFrame = await getDesktopDemoFrame(page);
  await expectNoHorizontalOverflowInFrame(desktopFrame);
  await expect(desktopFrame.getByText('My day', { exact: true })).toBeVisible();
  await scrollFrameToBottom(desktopFrame);
  await expect(desktopFrame.locator('[data-demo-scroll-end]')).toBeVisible();

  await page.getByRole('button', { name: /open demo menu|abrir menu da demo/i }).click();
  await page.getByRole('button', { name: /^Focus timer/i }).click();
  await expectNoHorizontalOverflowInFrame(desktopFrame);
  await scrollFrameToBottom(desktopFrame);
  await expect(desktopFrame.locator('[data-demo-scroll-end]')).toBeVisible();

  await page.getByRole('button', { name: /open demo menu|abrir menu da demo/i }).click();
  await page.getByRole('button', { name: /^Activity/i }).click();
  await expect(desktopFrame.getByText('Activities', { exact: true })).toBeVisible();
  await expectNoHorizontalOverflowInFrame(desktopFrame);

  // Activity can scroll to bottom (folders/session sections reachable)
  await scrollFrameToBottom(desktopFrame);
  await expect(desktopFrame.locator('[data-demo-marker="activities-bottom"]')).toBeVisible();
  await expect(desktopFrame.locator('[data-demo-scroll-end]')).toBeVisible();

  await page.getByRole('button', { name: /open demo menu|abrir menu da demo/i }).click();
  await page.getByRole('button', { name: /^Reports/i }).click();
  await expect(desktopFrame.getByRole('heading', { name: 'Reports' })).toBeVisible();
  await expectNoHorizontalOverflowInFrame(desktopFrame);
  await scrollFrameToBottom(desktopFrame);
  await expect(desktopFrame.locator('[data-demo-marker="reports-bottom"]')).toBeVisible();
  await expect(desktopFrame.locator('[data-demo-scroll-end]')).toBeVisible();

  await page.getByRole('button', { name: /open demo menu|abrir menu da demo/i }).click();
  await expect(page.getByRole('button', { name: /^Kanban/i })).toHaveCount(0);
  await page.getByRole('button', { name: /close menu|fechar menu/i }).click().catch(() => {});
  expect(consoleErrors).toEqual([]);

  // Exit returns to the landing page.
  await page.getByRole('button', { name: /exit demo|sair da demo/i }).click();
  await expect(page).toHaveURL('/');

  // Teams parity
  await page.goto('/teams');
  const demoTeams = page.locator('#demo');
  await demoTeams.scrollIntoViewIfNeeded();
  const tryDemoTeams = demoTeams.getByRole('link', { name: /try the demo|experimentar a demo/i });
  await tryDemoTeams.click();
  await expect(page).toHaveURL(/\/demo\?/);

  // Switch to portal
  await page.getByRole('button', { name: /open demo menu|abrir menu da demo/i }).click();
  await page.getByRole('button', { name: /^Web portal/i }).click();
  const portalFrame = await getWebPortalFrame(page);
  await expect(portalFrame.getByText('ChronosX Demo Org', { exact: true })).toBeVisible();
  await expectNoHorizontalOverflowInFrame(portalFrame);

  // Teams (Desktop) can scroll and doesn't freeze.
  const desktopFrameTeams = await getDesktopDemoFrame(page);
  await page.getByRole('button', { name: /open demo menu|abrir menu da demo/i }).click();
  await page.getByRole('button', { name: /^Team \(Desktop\)|^Time \(Desktop\)/i }).click();
  await expect(desktopFrameTeams.getByRole('heading', { name: /team|equipe/i })).toBeVisible();
  await scrollFrameToBottom(desktopFrameTeams);
  await expect(desktopFrameTeams.locator('[data-demo-scroll-end]')).toBeVisible();

  await page.getByRole('button', { name: /exit demo|sair da demo/i }).click();
  await expect(page).toHaveURL('/teams');

  expect(Array.from(notFound)).toEqual([]);
});
