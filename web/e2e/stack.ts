import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

/**
 * The production stack the end-to-end tests run against (task 11.3): the same images, Caddyfile,
 * migration bundle and compose file a release uses, on https://localhost, under a project of its
 * own ("gym-e2e") with its own empty database. It never touches the development database or a
 * local "gym-prod" smoke test, and `down -v` throws it all away afterwards.
 */

const repoRoot = fileURLToPath(new URL("../..", import.meta.url));

export const baseURL = "https://localhost";

/**
 * Settings for the throwaway stack. Not secrets: they guard a database that lives for one test
 * run on this machine, like the signing key in the integration tests' GymApiFactory.
 */
export const owner = { userName: "e2eowner", password: "e2e first owner password 1405" };

const environment: NodeJS.ProcessEnv = {
  ...process.env,
  // The repository's own .env configures the development database; none of it applies here.
  COMPOSE_DISABLE_ENV_FILE: "true",
  DOMAIN: "localhost",
  POSTGRES_DB: "gym",
  POSTGRES_USER: "gym",
  POSTGRES_PASSWORD: "e2e-database-password",
  JWT_SIGNING_KEY: "end-to-end-tests-signing-key-0123456789abcdef",
  SEED_OWNER_USERNAME: owner.userName,
  SEED_OWNER_PASSWORD: owner.password,
  // Its own image tag and network, so a local "gym-prod" smoke test keeps its own.
  TAG: "e2e",
  GYM_SUBNET: "172.29.0.0/24",
};

const compose = [
  "compose",
  "--project-name",
  "gym-e2e",
  "-f",
  "docker-compose.prod.yml",
  "-f",
  "web/e2e/compose.e2e.yml",
];

function run(command: string, args: string[]) {
  execFileSync(command, args, { cwd: repoRoot, env: environment, stdio: "inherit" });
}

/** Builds the images and the migration bundle, migrates an empty database and starts the stack. */
export function startStack() {
  // The same bundle deploy/release.sh builds; artifacts/ is git-ignored.
  run("dotnet", [
    "ef",
    "migrations",
    "bundle",
    "--self-contained",
    "-r",
    "linux-x64",
    "--force",
    "--configuration",
    "Release",
    "--project",
    "src/Gym.Infrastructure",
    "--startup-project",
    "src/Gym.Api",
    "-o",
    "artifacts/efbundle",
  ]);

  // Left over from a run that was stopped before its teardown.
  run("docker", [...compose, "down", "--volumes", "--remove-orphans"]);

  run("docker", [...compose, "build"]);
  run("docker", [...compose, "up", "--detach", "--wait", "postgres"]);
  // As on the server: the API never migrates at startup (deploy/server.sh).
  run("docker", [...compose, "run", "--rm", "migrate"]);
  run("docker", [...compose, "up", "--detach", "--wait", "--wait-timeout", "180"]);
}

/** Stops the stack and deletes its database. */
export function stopStack() {
  run("docker", [...compose, "down", "--volumes", "--remove-orphans"]);
}
