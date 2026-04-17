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

  const desktopFrame = await getDesktopDemoFrame(page);
  await demo.getByRole('button', { name: /^Reports/i }).click();
  await expect(desktopFrame.getByRole('heading', { name: 'Reports' })).toBeVisible();

  const desktopIframe = page.locator('iframe[title="ChronosX Desktop Demo"]');
  await expect(desktopIframe).toBeVisible();
  const desktopBox = await desktopIframe.boundingBox();
  expect(desktopBox?.width ?? 0).toBeGreaterThanOrEqual(320);
  expect(desktopBox?.height ?? 0).toBeGreaterThanOrEqual(480);

  await page.goto('/teams');
  const demoTeams = page.locator('#demo');
  await demoTeams.scrollIntoViewIfNeeded();
  const webPortalBtn = demoTeams.getByRole('button', { name: /^Web portal/i });
  await webPortalBtn.scrollIntoViewIfNeeded();
  await webPortalBtn.click();

  const portalIframe = page.locator('iframe[title="ChronosX Web Portal Demo"]');
  await expect(portalIframe).toBeVisible();
  const portalBox = await portalIframe.boundingBox();
  expect(portalBox?.width ?? 0).toBeGreaterThanOrEqual(320);
  expect(portalBox?.height ?? 0).toBeGreaterThanOrEqual(480);

  expect(Array.from(notFound)).toEqual([]);
});
