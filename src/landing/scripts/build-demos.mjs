import { spawn } from "node:child_process";
import fs from "node:fs/promises";
import path from "node:path";

const repoRoot = path.resolve(process.cwd(), "..", "..");
const landingRoot = process.cwd();

const desktopUiRoot = path.join(repoRoot, "src", "ui", "timetrack-ui");
const webUiRoot = path.join(repoRoot, "src", "ui", "timetrack-web");

const outDesktop = path.join(desktopUiRoot, "dist-demo");
const outWeb = path.join(webUiRoot, "dist-demo");

const destDesktop = path.join(landingRoot, "public", "demos", "desktop");
const destWeb = path.join(landingRoot, "public", "demos", "web");

function run(cmd, args, cwd) {
  return new Promise((resolve, reject) => {
    const p = spawn(cmd, args, { cwd, stdio: "inherit", shell: false });
    p.on("exit", (code) => {
      if (code === 0) resolve();
      else reject(new Error(`${cmd} ${args.join(" ")} failed (code ${code})`));
    });
  });
}

async function copyDir(sourceDir, destDir) {
  await fs.rm(destDir, { recursive: true, force: true });
  await fs.mkdir(destDir, { recursive: true });
  // Node 16+ supports fs.cp
  await fs.cp(sourceDir, destDir, { recursive: true });
}

async function exists(filePath) {
  try {
    await fs.access(filePath);
    return true;
  } catch {
    return false;
  }
}

async function main() {
  if (String(process.env.SKIP_DEMOS ?? "").toLowerCase() === "1" || String(process.env.SKIP_DEMOS ?? "").toLowerCase() === "true") {
    console.log("[build-demos] SKIP_DEMOS enabled; skipping demo builds.");
    return;
  }

  console.log("[build-demos] Building desktop demo…");
  await run("npm", ["run", "build:demo"], desktopUiRoot);
  console.log("[build-demos] Building web portal demo…");
  await run("npm", ["run", "build:demo"], webUiRoot);

  if (!(await exists(outDesktop)) || !(await exists(outWeb))) {
    throw new Error("[build-demos] dist-demo output not found after build.");
  }

  console.log("[build-demos] Copying demo assets into landing public/…");
  await copyDir(outDesktop, destDesktop);
  await copyDir(outWeb, destWeb);
  console.log("[build-demos] Done.");
}

main().catch((err) => {
  console.error(err);
  process.exitCode = 1;
});
