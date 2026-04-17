import fs from "node:fs/promises";
import path from "node:path";

const landingRoot = process.cwd();
const nextRoot = path.join(landingRoot, ".next");
const standaloneRoot = path.join(nextRoot, "standalone");

async function exists(filePath) {
  try {
    await fs.access(filePath);
    return true;
  } catch {
    return false;
  }
}

async function copyDir(sourceDir, destDir) {
  await fs.mkdir(destDir, { recursive: true });
  const entries = await fs.readdir(sourceDir, { withFileTypes: true });

  for (const entry of entries) {
    const sourcePath = path.join(sourceDir, entry.name);
    const destPath = path.join(destDir, entry.name);

    if (entry.isDirectory()) {
      await copyDir(sourcePath, destPath);
      continue;
    }

    if (entry.isSymbolicLink()) {
      const linkTarget = await fs.readlink(sourcePath);
      await fs.symlink(linkTarget, destPath);
      continue;
    }

    await fs.copyFile(sourcePath, destPath);
  }
}

async function main() {
  const hasStandalone = await exists(standaloneRoot);
  if (!hasStandalone) {
    console.log("[prepare-standalone] No .next/standalone found; skipping.");
    return;
  }

  const staticSource = path.join(nextRoot, "static");
  const staticDest = path.join(standaloneRoot, ".next", "static");
  const hasStatic = await exists(staticSource);
  if (hasStatic) {
    await copyDir(staticSource, staticDest);
    console.log("[prepare-standalone] Copied .next/static -> .next/standalone/.next/static");
  } else {
    console.log("[prepare-standalone] No .next/static found; skipping copy.");
  }

  const publicSource = path.join(landingRoot, "public");
  const publicDest = path.join(standaloneRoot, "public");
  const hasPublic = await exists(publicSource);
  if (hasPublic) {
    await copyDir(publicSource, publicDest);
    console.log("[prepare-standalone] Copied public -> .next/standalone/public");
  } else {
    console.log("[prepare-standalone] No public/ found; skipping copy.");
  }
}

main().catch((error) => {
  console.error("[prepare-standalone] Failed:", error);
  process.exitCode = 1;
});

