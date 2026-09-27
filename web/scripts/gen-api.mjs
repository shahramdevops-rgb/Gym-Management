// Regenerates src/lib/api/schema.d.ts from the API's OpenAPI document (`npm run gen:api`).
//
// The generator needs the API running, and starting it by hand is where this step used to go
// wrong: the wrong launch profile (no connection string), a forgotten API still holding the port.
// So this script does it: an API already answering on :5134 is used as it is; otherwise the API is
// started in the Development profile (which also builds it), the document is awaited, the types
// are generated, and the API is stopped again, whatever happened in between.

import { spawn, spawnSync } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";

const documentUrl = "http://localhost:5134/openapi/v1.json";
const outputFile = "src/lib/api/schema.d.ts";
const startTimeoutMs = 180_000;
const isWindows = process.platform === "win32";

const webDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const apiProject = path.resolve(webDir, "..", "src", "Gym.Api");

async function documentIsServed() {
    try {
        const response = await fetch(documentUrl);
        return response.ok;
    } catch {
        return false;
    }
}

function generate() {
    // shell on Windows, because npx is a .cmd file there.
    const result = spawnSync("npx", ["openapi-typescript", documentUrl, "-o", outputFile], {
        cwd: webDir,
        stdio: "inherit",
        shell: isWindows,
    });

    return result.status ?? 1;
}

/** Stops `dotnet run` and the API process it started: killing only the parent leaves the port held. */
function stop(child) {
    if (child.exitCode !== null) {
        return;
    }

    if (isWindows) {
        spawnSync("taskkill", ["/pid", String(child.pid), "/T", "/F"], { stdio: "ignore" });
    } else {
        process.kill(-child.pid, "SIGTERM");
    }
}

async function startApiAndGenerate() {
    console.log("No API on :5134; starting it (this builds it first)...");

    // No --launch-profile: the only profile, Gym.Api, is the default and sets Development, which is
    // where the connection string, the user secrets and the OpenAPI document live.
    const child = spawn("dotnet", ["run", "--project", apiProject], {
        cwd: webDir,
        detached: !isWindows,
        windowsHide: true,
        stdio: ["ignore", "pipe", "pipe"],
    });

    const output = [];
    const keep = (chunk) => output.push(chunk.toString());
    child.stdout.on("data", keep);
    child.stderr.on("data", keep);

    const cleanUp = () => stop(child);
    process.on("SIGINT", () => {
        cleanUp();
        process.exit(130);
    });

    try {
        const deadline = Date.now() + startTimeoutMs;
        while (!(await documentIsServed())) {
            if (child.exitCode !== null || Date.now() > deadline) {
                console.error(output.join("").split("\n").slice(-30).join("\n"));
                console.error(
                    child.exitCode !== null
                        ? "The API stopped before serving its OpenAPI document (output above). Is `docker compose up -d` running, and is port 5134 free?"
                        : `The API did not serve ${documentUrl} within ${startTimeoutMs / 1000} seconds.`,
                );
                return 1;
            }

            await new Promise((resolve) => setTimeout(resolve, 1000));
        }

        return generate();
    } finally {
        cleanUp();
    }
}

let exitCode;
if (await documentIsServed()) {
    console.log(
        "Using the API already running on :5134. If it was started before your endpoint changes, stop it and run this again.",
    );
    exitCode = generate();
} else {
    exitCode = await startApiAndGenerate();
}

process.exit(exitCode);
