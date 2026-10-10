import { seedDemo, startStack } from "../stack";

export default function globalSetup() {
  startStack();
  seedDemo();
}
