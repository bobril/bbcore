const runtimeValue = 1;
type User = { name: string };

export { runtimeValue, type User };
export { otherValue as renamedRuntime, type OtherUser as RenamedUser } from "./mod";
