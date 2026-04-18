import { expect, test, type Frame, type Page } from '@playwright/test';

function trackNotFoundResponses(page: Page) {
  const notFound = new Set<string>();
  page.on('response', (res) => {
    if (res.status() === 404) notFound.add(res.url());
  });
  return notFound;
}

async function getDesktopDemoFrame(page: Page): Promise<Frame> {
  await page.waitForSelector('iframe[title="ChronosX Desktop Demo"]');
  await page.waitForTimeout(250);
  const frame = page.frames().find((f) => f.url().includes('/demos/desktop/demo.html'));
  if (!frame) throw new Error('Desktop demo iframe did not load');
  return frame;
}

async function getWebPortalFrame(page: Page): Promise<Frame> {
  await page.waitForSelector('iframe[title="ChronosX Web Portal Demo"]');
  await page.waitForTimeout(250);
  const frame = page.frames().find((f) => f.url().includes('/demos/web/demo.html'));
  if (!frame) throw new Error('Web portal iframe did not load');
  return frame;
}

async function expectNoRootScroll(frame: Frame) {
  const { scrollHeight, innerHeight } = await frame.evaluate(() => ({
    scrollHeight: document.documentElement.scrollHeight,
    innerHeight: window.innerHeight,
  }));
  expect(scrollHeight).toBeLessThanOrEqual(innerHeight + 2);
}

test('Individuals: Activity + Reports demos load and fit (no 404)', async ({ page }) => {
  const notFound = trackNotFoundResponses(page);

  await page.setViewportSize({ width: 1280, height: 820 });
  await page.goto('/');
  const demo = page.locator('#demo');

  const desktopFrame = await getDesktopDemoFrame(page);
  await expect(desktopFrame.getByText('My day', { exact: true })).toBeVisible();

  await demo.getByRole('button', { name: /^Activity/i }).click();
  await expect(desktopFrame.getByText('Activities', { exact: true })).toBeVisible();
  await expectNoRootScroll(desktopFrame);

  await demo.getByRole('button', { name: /^Reports/i }).click();
  await expect(desktopFrame.getByRole('heading', { name: 'Reports' })).toBeVisible();
  await expectNoRootScroll(desktopFrame);

  expect(Array.from(notFound)).toEqual([]);
});

test('Teams: Portal + Teams + Reports demos load and fit (no 404)', async ({ page }) => {
  const notFound = trackNotFoundResponses(page);

  await page.setViewportSize({ width: 1280, height: 820 });
  await page.goto('/teams');
  const demo = page.locator('#demo');

  const desktopFrame = await getDesktopDemoFrame(page);

  await demo.getByRole('button', { name: /^Team \(Desktop\)/i }).click();
  await expect(desktopFrame.getByRole('heading', { name: 'Team' })).toBeVisible();
  await expectNoRootScroll(desktopFrame);

  await demo.getByRole('button', { name: /^Reports/i }).click();
  await expect(desktopFrame.getByRole('heading', { name: 'Reports' })).toBeVisible();
  await expectNoRootScroll(desktopFrame);

  await demo.getByRole('button', { name: /^Web portal/i }).click();
  const portalFrame = await getWebPortalFrame(page);
  await expect(portalFrame.getByText('ChronosX Demo Org', { exact: true })).toBeVisible();

  expect(Array.from(notFound)).toEqual([]);
});

test('Responsive: mobile layout still usable', async ({ page }) => {
  const notFound = trackNotFoundResponses(page);

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
  await page.getByRole('button', { name: /open demo menu|abrir menu da demo/i }).click();
  await page.getByRole('button', { name: /^Activity/i }).click();
  await expect(desktopFrame.getByText('Activities', { exact: true })).toBeVisible();

  await page.getByRole('button', { name: /open demo menu|abrir menu da demo/i }).click();
  await page.getByRole('button', { name: /^Reports/i }).click();
  await expect(desktopFrame.getByRole('heading', { name: 'Reports' })).toBeVisible();

  await page.getByRole('button', { name: /open demo menu|abrir menu da demo/i }).click();
  await page.getByRole('button', { name: /^Kanban/i }).click();
  await expect(desktopFrame.getByText('To Do', { exact: true })).toBeVisible();

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

  await page.getByRole('button', { name: /exit demo|sair da demo/i }).click();
  await expect(page).toHaveURL('/teams');

  expect(Array.from(notFound)).toEqual([]);
});
