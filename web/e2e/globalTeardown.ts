import { stopStack } from "./stack";

export default function globalTeardown() {
  stopStack();
}
